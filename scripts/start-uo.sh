#!/usr/bin/env bash
# start-uo.sh
# If one expansion is installed, launch it.
# If multiple, prompt the user. Pass --expansion=NAME to skip the prompt.
# Boot the matching ServUO, wait for its port, launch ClassicUO, clean up.

set -euo pipefail

INSTALL_ROOT="${HOME}/uo-offline"
SERVERS_ROOT="${INSTALL_ROOT}/servers"
CLIENT_DIR="${INSTALL_ROOT}/ClassicUO"
DOTNET_DIR="${INSTALL_ROOT}/dotnet"
LOG_DIR="${INSTALL_ROOT}/logs"
MANIFEST="${INSTALL_ROOT}/installed.list"
mkdir -p "${LOG_DIR}"

# Offline env (NUGET_PACKAGES, DOTNET_ROOT, PATH, telemetry off)
if [[ -f "${INSTALL_ROOT}/.env" ]]; then
  # shellcheck disable=SC1091
  source "${INSTALL_ROOT}/.env"
fi
if [[ -x "${DOTNET_DIR}/dotnet" ]]; then
  export PATH="${DOTNET_DIR}:${PATH}"
  export DOTNET_ROOT="${DOTNET_DIR}"
fi

# Preflight: confirm Flatpak Mono runtime is available. ServUO Pub 57 is a
# .NET Framework 4.8 build; we run it through the Mono binary that ships
# inside the Freedesktop SDK Flatpak extension. We invoke the binary
# *directly* (not via `flatpak run`) because Flatpak's network namespacing
# would otherwise isolate the listening port from the host.
if ! flatpak info org.freedesktop.Sdk.Extension.mono6//24.08 >/dev/null 2>&1; then
  cat <<'EOF' >&2
ERROR: Flatpak Mono runtime not found.

ServUO needs Mono to run on Linux. Install it once:

  flatpak install --user -y flathub org.freedesktop.Sdk//24.08
  flatpak install --user -y flathub org.freedesktop.Sdk.Extension.mono6//24.08

Then re-run this launcher.
EOF
  exit 1
fi

# Resolve the path to mono inside the Flatpak runtime. The hash component
# changes when the extension is updated, so we ask flatpak for it.
MONO_BASE=$(flatpak info --show-location org.freedesktop.Sdk.Extension.mono6//24.08 2>/dev/null)/files
MONO_BIN="${MONO_BASE}/bin/mono"
if [[ ! -x "${MONO_BIN}" ]]; then
  echo "ERROR: Mono binary not found at ${MONO_BIN}" >&2
  exit 1
fi
export LD_LIBRARY_PATH="${MONO_BASE}/lib:${LD_LIBRARY_PATH:-}"

# --- Parse args ---
FORCED_EXPANSION=""
for arg in "$@"; do
  case "$arg" in
    --expansion=*) FORCED_EXPANSION="${arg#*=}" ;;
    --help|-h)
      echo "Usage: $0 [--expansion=pretoa|t2a]"
      exit 0 ;;
  esac
done

# --- Load manifest ---
if [[ ! -f "${MANIFEST}" ]]; then
  echo "No installed.list found at ${MANIFEST}. Did install.sh complete?" >&2
  exit 1
fi

mapfile -t lines < <(grep -v '^[[:space:]]*$' "${MANIFEST}")
if (( ${#lines[@]} == 0 )); then
  echo "No expansions installed." >&2
  exit 1
fi

# --- Pick expansion ---
EXPANSION=""
PORT=""
NAME=""

pick_by_key() {
  local key="$1"
  for line in "${lines[@]}"; do
    IFS=$'\t' read -r k n p <<<"$line"
    if [[ "$k" == "$key" ]]; then
      EXPANSION="$k"; NAME="$n"; PORT="$p"
      return 0
    fi
  done
  return 1
}

if [[ -n "$FORCED_EXPANSION" ]]; then
  pick_by_key "$FORCED_EXPANSION" || { echo "Expansion '$FORCED_EXPANSION' not installed." >&2; exit 1; }
elif (( ${#lines[@]} == 1 )); then
  IFS=$'\t' read -r EXPANSION NAME PORT <<<"${lines[0]}"
else
  echo
  echo "Multiple Ultima Online versions are installed. Choose one:"
  echo
  for i in "${!lines[@]}"; do
    IFS=$'\t' read -r k n p <<<"${lines[$i]}"
    printf "  %d) %s\n" $((i+1)) "$n"
  done
  echo
  while true; do
    read -r -p "Selection [1-${#lines[@]}]: " sel
    if [[ "$sel" =~ ^[0-9]+$ ]] && (( sel >= 1 && sel <= ${#lines[@]} )); then
      IFS=$'\t' read -r EXPANSION NAME PORT <<<"${lines[$((sel-1))]}"
      break
    fi
    echo "Invalid selection."
  done
fi

SERVUO_DIR="${SERVERS_ROOT}/${EXPANSION}"
SERVER_LOG="${LOG_DIR}/${EXPANSION}-$(date +%Y%m%d-%H%M%S).log"
PIDFILE="${INSTALL_ROOT}/.servuo-${EXPANSION}.pid"

echo
echo "==> Launching: ${NAME}"
echo "    Server dir: ${SERVUO_DIR}"
echo "    Port:       ${PORT}"
echo "    Log:        ${SERVER_LOG}"
echo

cleanup() {
  if [[ -f "${PIDFILE}" ]]; then
    local pid; pid=$(cat "${PIDFILE}")
    if kill -0 "$pid" 2>/dev/null; then
      echo "Stopping ServUO (pid $pid) — letting it save..."
      kill -TERM "$pid" 2>/dev/null || true
      for _ in $(seq 1 30); do
        kill -0 "$pid" 2>/dev/null || break
        sleep 1
      done
      kill -KILL "$pid" 2>/dev/null || true
    fi
    rm -f "${PIDFILE}"
  fi
}
trap cleanup EXIT INT TERM

# --- Start server (if not already running for this expansion) ---
if [[ -f "${PIDFILE}" ]] && kill -0 "$(cat "${PIDFILE}")" 2>/dev/null; then
  echo "ServUO already running (pid $(cat "${PIDFILE}")). Skipping server boot."
else
  echo "Starting ServUO..."
  pushd "${SERVUO_DIR}" >/dev/null

  # ServUO Pub 57 is a .NET Framework 4.8 app — ServUO.exe must be run via
  # Mono. We invoke the Mono binary directly (resolved above from the
  # Flatpak runtime) rather than through `flatpak run`, because Flatpak's
  # network sandbox would isolate the listening port from the host shell
  # and ClassicUO would not be able to connect.
  if [[ -f "ServUO.exe" ]]; then
    nohup "${MONO_BIN}" ServUO.exe >"${SERVER_LOG}" 2>&1 &
  elif [[ -f "Distribution/ServUO.dll" ]]; then
    # Fallback: newer ServUO build that produced a .NET Core DLL
    nohup dotnet Distribution/ServUO.dll >"${SERVER_LOG}" 2>&1 &
  else
    echo "ERROR: cannot locate built ServUO binary in ${SERVUO_DIR}" >&2
    exit 1
  fi
  echo $! > "${PIDFILE}"
  popd >/dev/null

  echo "Waiting for server on 127.0.0.1:${PORT} (up to 120s for first boot)..."
  server_up=0
  for i in $(seq 1 120); do
    # Try multiple ways to detect the port being listened on, in order of
    # preference. ss is the modern tool; netstat is the classic; /dev/tcp
    # is the bash builtin fallback.
    if command -v ss >/dev/null 2>&1; then
      if ss -ltn 2>/dev/null | grep -qE "[:.]${PORT}\\b"; then
        server_up=1
      fi
    elif command -v netstat >/dev/null 2>&1; then
      if netstat -ltn 2>/dev/null | grep -qE "[:.]${PORT}\\b"; then
        server_up=1
      fi
    else
      # bash /dev/tcp fallback
      if exec 3<>"/dev/tcp/127.0.0.1/${PORT}" 2>/dev/null; then
        server_up=1
        exec 3<&- 3>&- 2>/dev/null || true
      fi
    fi

    if (( server_up )); then
      echo "Server up after ${i}s."
      break
    fi

    # Sanity check: did the server crash during boot?
    if ! kill -0 "$(cat "${PIDFILE}" 2>/dev/null)" 2>/dev/null; then
      echo "Server process died during boot. Last 60 lines of log:" >&2
      tail -n 60 "${SERVER_LOG}" >&2
      exit 1
    fi

    sleep 1
  done

  if (( ! server_up )); then
    echo "Server did not start within 120s. See ${SERVER_LOG}" >&2
    tail -n 40 "${SERVER_LOG}" >&2
    exit 1
  fi
fi

# --- Write ClassicUO settings.json for the chosen port ---
cat > "${CLIENT_DIR}/settings.json" <<EOF
{
  "username": "",
  "password": "",
  "ip": "127.0.0.1",
  "port": ${PORT},
  "ultimaonlinedirectory": "${INSTALL_ROOT}/uodata",
  "clientversion": "7.0.24.0",
  "lastservernum": 1,
  "lastserver": "UO Deck Offline — ${NAME}",
  "fps": 60,
  "debug": false,
  "auto_login": false,
  "reconnect": false,
  "login_music": true,
  "login_music_volume": 70,
  "shard_type": 0,
  "fixed_time_step": true,
  "run_mouse_in_separate_thread": true,
  "force_driver": 0,
  "check_updates": false,
  "use_verdata": true
}
EOF

# --- Launch ClassicUO (foreground; cleanup runs on exit) ---
echo "Launching ClassicUO..."
cd "${CLIENT_DIR}"
./classicuo
