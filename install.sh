#!/usr/bin/env bash
# uo-deck-offline installer
# Sets up an offline pre-T2A and/or T2A Ultima Online server (ServUO Pub 57)
# with PlayerBots and ClassicUO on a Steam Deck.
#
# Usage: ./install.sh
#
# Requires: a working internet connection, an existing UO client install
# from which we can copy the .mul / .uop data files. The user supplies
# their own client files — we do not redistribute them.

set -euo pipefail

# ---------- Constants ----------
readonly INSTALL_ROOT="${HOME}/uo-offline"
readonly SERVERS_ROOT="${INSTALL_ROOT}/servers"
readonly CLIENT_DIR="${INSTALL_ROOT}/ClassicUO"
readonly UODATA_DIR="${INSTALL_ROOT}/uodata"
readonly DOTNET_DIR="${INSTALL_ROOT}/dotnet"
readonly LOG_FILE="${INSTALL_ROOT}/install.log"

# Pinned ServUO commit — Publish 57.4.1 (last release with T2A / None support).
readonly SERVUO_REPO="https://github.com/ServUO/ServUO.git"
readonly SERVUO_COMMIT="aa4c139"

readonly CLASSICUO_RELEASE_URL="https://github.com/ClassicUO/ClassicUO/releases/latest/download/ClassicUO-dev-release-linux-x64.zip"

readonly DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"
readonly DOTNET_CHANNEL="8.0"

# Expansion definitions.
declare -rA EXPANSION_NAME=(
  [pretoa]="Pre-T2A (Sept 1997 launch)"
  [t2a]="The Second Age (June 1998)"
)
declare -rA EXPANSION_CFG=(
  [pretoa]="None"
  [t2a]="T2A"
)
declare -rA EXPANSION_PORT=(
  [pretoa]="2593"
  [t2a]="2594"
)

# Each entry is a group of equivalent filenames. The user's UO install needs
# at least ONE file from each group. Modern UO clients use .uop archives
# instead of the old .mul files; ClassicUO reads either.
readonly DATA_FILE_GROUPS=(
  "anim.mul|AnimationFrame1.uop|AnimationSequence.uop"
  "anim.idx"
  "map0.mul|map0LegacyMUL.uop"
  "staidx0.mul"
  "statics0.mul"
  "tiledata.mul"
  "radarcol.mul"
  "hues.mul"
  "art.mul|artLegacyMUL.uop"
  "artidx.mul|artLegacyMUL.uop"
  "gumpart.mul|gumpartLegacyMUL.uop"
  "gumpidx.mul|gumpartLegacyMUL.uop"
  "skills.mul"
  "skills.idx"
  "sound.mul|soundLegacyMUL.uop"
  "soundidx.mul|soundLegacyMUL.uop"
  "speech.mul"
  "multi.mul|MultiCollection.uop"
  "multi.idx|MultiCollection.uop"
)

SELECTED=()
PK_TARGET_MODE="both"

# ---------- TUI helpers ----------
c_reset=$'\e[0m'; c_bold=$'\e[1m'; c_dim=$'\e[2m'
c_red=$'\e[31m'; c_grn=$'\e[32m'; c_ylw=$'\e[33m'; c_blu=$'\e[34m'; c_cya=$'\e[36m'

banner() {
  clear
  cat <<EOF
${c_cya}${c_bold}
  ┌─────────────────────────────────────────────────────────┐
  │   Ultima Online: Offline for Steam Deck                 │
  │   Pre-T2A / T2A · ServUO + ClassicUO + PlayerBots       │
  └─────────────────────────────────────────────────────────┘
${c_reset}
EOF
}

info() { printf "${c_blu}[info]${c_reset} %s\n" "$*"; }
ok()   { printf "${c_grn}[ ok ]${c_reset} %s\n" "$*"; }
warn() { printf "${c_ylw}[warn]${c_reset} %s\n" "$*"; }
err()  { printf "${c_red}[err ]${c_reset} %s\n" "$*" >&2; }
die()  { err "$*"; exit 1; }

confirm() {
  local prompt="${1:-Continue?}" ans
  while true; do
    read -r -p "${prompt} [y/N] " ans
    case "${ans,,}" in
      y|yes) return 0 ;;
      n|no|"") return 1 ;;
    esac
  done
}

prompt() {
  local prompt="$1" default="${2:-}" var
  if [[ -n "$default" ]]; then
    read -r -p "${prompt} [${default}]: " var
    printf "%s" "${var:-$default}"
  else
    read -r -p "${prompt}: " var
    printf "%s" "$var"
  fi
}

prompt_password() {
  local prompt="$1" pw pw2
  while true; do
    read -r -s -p "${prompt}: " pw; echo
    [[ -z "$pw" ]] && { warn "Password cannot be empty."; continue; }
    [[ ${#pw} -lt 4 ]] && { warn "Password too short (min 4 chars)."; continue; }
    read -r -s -p "Confirm password: " pw2; echo
    [[ "$pw" == "$pw2" ]] && { printf "%s" "$pw"; return; }
    warn "Passwords did not match. Try again."
  done
}

# ---------- Expansion selection ----------
choose_pk_mode() {
  banner
  cat <<EOF
${c_bold}Step 1b: Configure player killers (PKs)${c_reset}

The world includes red-named PK bots that lurk at dungeon entrances and
wilderness areas. By default they're ~5% of total population in gangs of
1-3. You can change this later in-game with [pkdensity and [pkmode, but
let's pick an initial PK targeting policy:

  1) Players only      — PKs only attack you. Bots are safe. Easy mode.

  2) Bots only         — PKs only kill other bots. You can watch the
                          chaos from a distance. Civilian respawn keeps
                          the world populated.

  3) Both (default)    — PKs attack everything. Authentic, chaotic.
                          Civilian respawn keeps the world from emptying.

  4) No PKs            — Disable PK bots entirely.

EOF
  local choice
  while true; do
    choice=$(prompt "Select 1, 2, 3, or 4" "3")
    case "$choice" in
      1) PK_TARGET_MODE="PlayersOnly"; break ;;
      2) PK_TARGET_MODE="BotsOnly";    break ;;
      3) PK_TARGET_MODE="Both";        break ;;
      4) PK_TARGET_MODE="none";        break ;;
      *) warn "Please enter 1-4." ;;
    esac
  done
  info "PK mode: ${PK_TARGET_MODE}"
}

choose_expansions() {
  banner
  cat <<EOF
${c_bold}Step 1: Choose which version(s) to install${c_reset}

  1) Pre-T2A (Sept 1997)   — Britannia only, no Lost Lands, no T2A monsters
                              Closest to launch-day UO.

  2) T2A (June 1998)       — Adds the Lost Lands, ophidians, terathans,
                              T2A dungeon bosses.

  3) Both                  — Install both side-by-side. Pick at launch time.
                              ~2× disk usage. Independent saves and characters.

EOF
  local choice
  while true; do
    choice=$(prompt "Select 1, 2, or 3" "2")
    case "$choice" in
      1) SELECTED=(pretoa); break ;;
      2) SELECTED=(t2a); break ;;
      3) SELECTED=(pretoa t2a); break ;;
      *) warn "Please enter 1, 2, or 3." ;;
    esac
  done

  echo
  info "Will install: ${SELECTED[*]}"
  for exp in "${SELECTED[@]}"; do
    info "  - ${EXPANSION_NAME[$exp]}  (port ${EXPANSION_PORT[$exp]})"
  done
  echo
  confirm "Proceed?" || die "Aborted."
}

# ---------- Sanity checks ----------
check_prereqs() {
  info "Checking prerequisites..."
  local missing=()
  for cmd in curl git unzip rsync; do
    command -v "$cmd" >/dev/null 2>&1 || missing+=("$cmd")
  done
  (( ${#missing[@]} )) && die "Missing required tools: ${missing[*]}"
  ok "All required tools present."
}

check_disk_space() {
  local need_gb=4
  (( ${#SELECTED[@]} > 1 )) && need_gb=6
  local avail_kb avail_gb
  avail_kb=$(df -P "$HOME" | awk 'NR==2 {print $4}')
  avail_gb=$(( avail_kb / 1024 / 1024 ))
  if (( avail_gb < need_gb )); then
    warn "Only ${avail_gb} GiB free in \$HOME. Need at least ${need_gb} GiB."
    confirm "Continue anyway?" || die "Aborted."
  else
    ok "Disk space: ${avail_gb} GiB free (need ~${need_gb})."
  fi
}

# ---------- .NET ----------
install_dotnet() {
  if command -v dotnet >/dev/null 2>&1 && \
     dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.NETCore.App ${DOTNET_CHANNEL}"; then
    ok "System .NET ${DOTNET_CHANNEL} already available."
    return
  fi
  if [[ -x "${DOTNET_DIR}/dotnet" ]]; then
    ok "User-local .NET already installed at ${DOTNET_DIR}."
    return
  fi
  info "Installing .NET ${DOTNET_CHANNEL} to ${DOTNET_DIR} (user-local, no sudo)..."
  mkdir -p "${DOTNET_DIR}"
  local installer="${INSTALL_ROOT}/dotnet-install.sh"
  curl -fsSL "${DOTNET_INSTALL_URL}" -o "${installer}"
  chmod +x "${installer}"
  "${installer}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_DIR}" --no-path
  rm -f "${installer}"
  ok ".NET installed."
}

dotnet_bin() {
  if [[ -x "${DOTNET_DIR}/dotnet" ]]; then
    printf "%s" "${DOTNET_DIR}/dotnet"
  else
    command -v dotnet
  fi
}

# ---------- UO client data (shared across expansions) ----------
find_uo_data() {
  banner
  cat <<EOF
${c_bold}Step 2: Locate your Ultima Online client files${c_reset}

You need an existing UO client install. We will copy the data files
(.mul / .uop) into ${UODATA_DIR}. These are shared across all expansion
installs — only need to be supplied once.

We do NOT redistribute UO files. You must provide them yourself.

EOF
  if [[ -d "${UODATA_DIR}" ]] && [[ -f "${UODATA_DIR}/map0.mul" ]]; then
    ok "UO data already present at ${UODATA_DIR}"
    confirm "Re-copy from a new source?" || return
  fi

  local src=""
  while true; do
    src=$(prompt "Path to UO client folder")
    src="${src/#\~/$HOME}"
    [[ ! -d "$src" ]] && { warn "Not a directory: $src"; continue; }

    local missing=()
    for group in "${DATA_FILE_GROUPS[@]}"; do
      local found_any=0
      # Each group is pipe-separated alternatives.
      IFS='|' read -ra alternatives <<< "$group"
      for alt in "${alternatives[@]}"; do
        if find "$src" -maxdepth 1 -iname "$alt" -print -quit | grep -q .; then
          found_any=1
          break
        fi
      done
      if (( ! found_any )); then
        # Show the first alternative as the canonical name in error messages.
        missing+=("${alternatives[0]}")
      fi
    done

    if (( ${#missing[@]} )); then
      warn "Missing required files in $src:"
      printf '       %s\n' "${missing[@]}"
      echo "       (Modern UO clients use .uop archives instead of some .mul"
      echo "        files. The installer accepts either, but at least one"
      echo "        equivalent must exist.)"
      confirm "Try a different path?" || die "Cannot continue without UO data files."
      continue
    fi
    ok "All required UO data files (or modern equivalents) found in $src"
    break
  done

  info "Copying UO data files to ${UODATA_DIR}..."
  mkdir -p "${UODATA_DIR}"
  # Copy every matching alternative found so we get both .mul and .uop
  # variants if both are present.
  for group in "${DATA_FILE_GROUPS[@]}"; do
    IFS='|' read -ra alternatives <<< "$group"
    for alt in "${alternatives[@]}"; do
      while IFS= read -r found; do
        [[ -n "$found" ]] && rsync -a "$found" "${UODATA_DIR}/"
      done < <(find "$src" -maxdepth 1 -iname "$alt")
    done
  done
  # Also pull anything else useful (cliloc, additional .uop, .mul, .idx)
  find "$src" -maxdepth 1 \( -iname "*.mul" -o -iname "*.uop" -o -iname "*.idx" -o -iname "cliloc.*" \) \
    -exec rsync -a {} "${UODATA_DIR}/" \;
  # Normalize to lowercase for ServUO's Linux-side reads
  ( cd "${UODATA_DIR}" && for f in *; do
      lc="${f,,}"
      [[ "$f" != "$lc" ]] && mv -n "$f" "$lc"
    done )
  ok "UO data files copied."
}

# ---------- ServUO (per-expansion) ----------
fetch_servuo_for() {
  local exp="$1"
  local dir="${SERVERS_ROOT}/${exp}"
  info "[${exp}] Fetching ServUO into ${dir}..."

  if [[ -d "${dir}/.git" ]]; then
    info "[${exp}] Already cloned — fetching updates..."
    git -C "${dir}" fetch --tags --quiet
  else
    mkdir -p "${SERVERS_ROOT}"
    git clone --quiet "${SERVUO_REPO}" "${dir}"
  fi
  git -C "${dir}" checkout --quiet "${SERVUO_COMMIT}"
  ok "[${exp}] ServUO checked out at ${SERVUO_COMMIT}."
}

configure_servuo_for() {
  local exp="$1"
  local dir="${SERVERS_ROOT}/${exp}"
  local cfg_value="${EXPANSION_CFG[$exp]}"
  local port="${EXPANSION_PORT[$exp]}"

  info "[${exp}] Applying ${cfg_value} configuration (port ${port})..."

  local exp_cfg="${dir}/Config/Expansion.cfg"
  if [[ -f "$exp_cfg" ]]; then
    sed -i "s/^CurrentExpansion=.*/CurrentExpansion=${cfg_value}/" "$exp_cfg"
  fi

  local srv_cfg="${dir}/Config/Server.cfg"
  if [[ -f "$srv_cfg" ]]; then
    sed -i 's/^Address=.*/Address=127.0.0.1/' "$srv_cfg" || true
    sed -i "s/^Port=.*/Port=${port}/" "$srv_cfg" || true
    sed -i 's|^Website=.*|Website=|' "$srv_cfg" || true
    sed -i 's|^PatchServer=.*|PatchServer=|' "$srv_cfg" || true
  fi

  local dp="${dir}/Scripts/Misc/DataPath.cs"
  if [[ -f "$dp" ]]; then
    sed -i "s|return \"\";|return \"${UODATA_DIR}\";|" "$dp"
  fi

  info "[${exp}] Installing PlayerBot scripts..."
  mkdir -p "${dir}/Scripts/Custom/PlayerBots"
  cp -r "${REPO_DIR}/assets/playerbots/"*.cs "${dir}/Scripts/Custom/PlayerBots/"

  # Marker file the bot system reads at runtime to pick the right spawn table.
  echo "${exp}" > "${dir}/Scripts/Custom/PlayerBots/Expansion.txt"

  # Initial settings file — players can change at runtime via admin commands.
  # If PK_TARGET_MODE is "none", we set PKDensity=0 instead of writing an
  # invalid enum value.
  local pk_density="0.05"
  local pk_mode="${PK_TARGET_MODE}"
  if [[ "${PK_TARGET_MODE}" == "none" ]]; then
    pk_density="0.00"
    pk_mode="Both"
  fi
  cat > "${dir}/Scripts/Custom/PlayerBots/Settings.txt" <<SETTINGS
# PlayerBot system settings — edit and use [reseedbots to apply
TargetBotCount=300
CivilianRatio=0.30
RoleAppropriateChance=0.75

# PK settings
PKDensity=${pk_density}
PKTargetMode=${pk_mode}
PKGangMin=1
PKGangMax=3
# Probability a given dungeon entrance gets a PK gang on a reseed
DungeonOccupancyChance=0.55
# Fraction of PKs that wander wilderness vs camp dungeons
PKWanderRatio=0.50

# Macroer settings — appear as players leveling skills, at banks only
MacroerRatio=0.20
MacroerBankBias=1.00

# Civilian respawn (replace bots killed by PKs)
CivilianRespawnEnabled=True
CivilianRespawnSeconds=600
SETTINGS

  ok "[${exp}] Configured."
}

build_servuo_for() {
  local exp="$1"
  local dir="${SERVERS_ROOT}/${exp}"
  info "[${exp}] Building ServUO (first build can take 2–3 minutes)..."
  local dn; dn=$(dotnet_bin)

  export NUGET_PACKAGES="${INSTALL_ROOT}/nuget-cache"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_NOLOGO=1
  mkdir -p "${NUGET_PACKAGES}"

  pushd "${dir}" >/dev/null
  info "[${exp}] Restoring NuGet packages..."
  "$dn" restore >>"${LOG_FILE}" 2>&1 || die "[${exp}] NuGet restore failed."
  "$dn" build -c Release --no-restore >>"${LOG_FILE}" 2>&1 || die "[${exp}] Build failed."
  popd >/dev/null

  ok "[${exp}] Built successfully."
}

configure_admin_for() {
  local exp="$1"
  local dir="${SERVERS_ROOT}/${exp}"

  banner
  cat <<EOF
${c_bold}Admin account for ${EXPANSION_NAME[$exp]}${c_reset}

EOF
  local username password
  username=$(prompt "Admin username for ${exp}" "admin")
  password=$(prompt_password "Admin password for ${exp}")

  mkdir -p "${dir}/Saves/Accounts"
  cat > "${dir}/Saves/Accounts/accounts.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<accounts>
  <count>1</count>
  <account index="0">
    <username>${username}</username>
    <password>${password}</password>
    <accessLevel>Administrator</accessLevel>
    <banned>false</banned>
    <created>$(date -u +"%Y-%m-%dT%H:%M:%S.0000000Z")</created>
    <lastLogin>$(date -u +"%Y-%m-%dT%H:%M:%S.0000000Z")</lastLogin>
    <totalGameTime>00:00:00</totalGameTime>
    <chars count="6" />
    <accessCheck />
    <addressList count="0" />
    <emailHistory />
  </account>
</accounts>
EOF
  chmod 600 "${dir}/Saves/Accounts/accounts.xml"
  ok "[${exp}] Admin account '${username}' created."
}

# ---------- ClassicUO (shared) ----------
install_classicuo() {
  banner
  echo "${c_bold}Installing ClassicUO (shared by all expansions)${c_reset}"

  if [[ -x "${CLIENT_DIR}/classicuo" ]]; then
    ok "ClassicUO already installed."
    return
  fi

  mkdir -p "${CLIENT_DIR}"
  local zip="${INSTALL_ROOT}/classicuo.zip"
  info "Downloading ClassicUO..."
  curl -fL --progress-bar "${CLASSICUO_RELEASE_URL}" -o "${zip}"
  info "Extracting..."
  unzip -q -o "${zip}" -d "${CLIENT_DIR}"
  rm -f "${zip}"

  local bin
  bin=$(find "${CLIENT_DIR}" -maxdepth 3 -name "ClassicUO" -type f -executable -print -quit)
  [[ -z "$bin" ]] && bin=$(find "${CLIENT_DIR}" -maxdepth 3 -name "ClassicUO.bin.x86_64" -print -quit)
  [[ -z "$bin" ]] && die "Could not locate ClassicUO binary after extraction."
  chmod +x "$bin"
  ln -sf "$bin" "${CLIENT_DIR}/classicuo"

  ok "ClassicUO installed. (Settings written per-launch by start-uo.sh.)"
}

# ---------- Launcher install ----------
install_launcher() {
  banner
  echo "${c_bold}Installing launcher${c_reset}"

  cat > "${INSTALL_ROOT}/.env" <<EOF
export NUGET_PACKAGES="${INSTALL_ROOT}/nuget-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_ROOT="${DOTNET_DIR}"
export PATH="${DOTNET_DIR}:\${PATH}"
EOF

  # Manifest of installed expansions, read by the launcher.
  : > "${INSTALL_ROOT}/installed.list"
  for exp in "${SELECTED[@]}"; do
    printf "%s\t%s\t%s\n" "${exp}" "${EXPANSION_NAME[$exp]}" "${EXPANSION_PORT[$exp]}" \
      >> "${INSTALL_ROOT}/installed.list"
  done

  install -m 755 "${REPO_DIR}/scripts/start-uo.sh"       "${INSTALL_ROOT}/start-uo.sh"
  install -m 755 "${REPO_DIR}/scripts/stop-uo.sh"        "${INSTALL_ROOT}/stop-uo.sh"
  install -m 755 "${REPO_DIR}/scripts/verify-offline.sh" "${INSTALL_ROOT}/verify-offline.sh"

  local desktop_src="${REPO_DIR}/launchers/UO-Offline.desktop"
  local desktop_dst="${HOME}/.local/share/applications/UO-Offline.desktop"
  mkdir -p "$(dirname "$desktop_dst")"
  sed "s|@INSTALL_ROOT@|${INSTALL_ROOT}|g" "$desktop_src" > "$desktop_dst"
  chmod 644 "$desktop_dst"
  update-desktop-database "${HOME}/.local/share/applications" 2>/dev/null || true

  ok "Launcher installed."
}

# ---------- Main ----------
main() {
  banner
  cat <<EOF
This installer will set up offline Ultima Online on this Steam Deck,
with your choice of pre-T2A, T2A, or both.

EOF
  confirm "${c_bold}Begin install?${c_reset}" || die "Aborted."

  mkdir -p "${INSTALL_ROOT}"
  : > "${LOG_FILE}"

  choose_expansions
  choose_pk_mode
  check_prereqs
  check_disk_space
  install_dotnet
  find_uo_data

  for exp in "${SELECTED[@]}"; do
    fetch_servuo_for "$exp"
    configure_servuo_for "$exp"
    build_servuo_for "$exp"
    configure_admin_for "$exp"
  done

  install_classicuo
  install_launcher

  banner
  cat <<EOF
${c_grn}${c_bold}Install complete!${c_reset}

Installed:
EOF
  for exp in "${SELECTED[@]}"; do
    echo "  - ${EXPANSION_NAME[$exp]}"
    echo "      server: ${SERVERS_ROOT}/${exp}"
    echo "      port:   ${EXPANSION_PORT[$exp]}"
  done
  cat <<EOF

  Shared client:  ${CLIENT_DIR}
  Shared data:    ${UODATA_DIR}
  Launcher:       ${INSTALL_ROOT}/start-uo.sh

To play:
  ${INSTALL_ROOT}/start-uo.sh
EOF
  if (( ${#SELECTED[@]} > 1 )); then
    echo "  (you'll be prompted which version to launch)"
  fi
  echo
  echo "Verify offline-readiness:"
  echo "  ${INSTALL_ROOT}/verify-offline.sh"
  echo
}

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_DIR

main "$@"
