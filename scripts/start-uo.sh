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
  if [[ -f "Distribution/ServUO.dll" ]]; then
    nohup dotnet Distribution/ServUO.dll >"${SERVER_LOG}" 2>&1 &
  elif [[ -f "ServUO.exe" ]]; then
    nohup dotnet ServUO.exe >"${SERVER_LOG}" 2>&1 &
  else
    echo "ERROR: cannot locate built ServUO binary in ${SERVUO_DIR}" >&2
    exit 1
  fi
  echo $! > "${PIDFILE}"
  popd >/dev/null

  echo "Waiting for server on 127.0.0.1:${PORT}..."
  for i in $(seq 1 60); do
    if (echo > "/dev/tcp/127.0.0.1/${PORT}") 2>/dev/null; then
      echo "Server up after ${i}s."
      break
    fi
    sleep 1
    if (( i == 60 )); then
      echo "Server did not start within 60s. See ${SERVER_LOG}" >&2
      tail -n 40 "${SERVER_LOG}" >&2
      exit 1
    fi
  done
fi

# --- Write ClassicUO settings.json for the chosen port ---
cat > "${CLIENT_DIR}/settings.json" <<EOF
{
  "username": "",
  "password": "",
  "ip": "127.0.0.1",
  "port": ${PORT},
  "ultimaonlinedirectory": "${INSTALL_ROOT}/uodata",
  "clientversion": "3.0.8j",
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
