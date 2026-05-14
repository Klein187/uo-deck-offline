#!/usr/bin/env bash
# launch-gui.sh — wrapper around start-uo.sh for desktop / Steam shortcuts.
#
# Usage:
#   launch-gui.sh                 # auto-pick the only expansion, or prompt
#                                   if both are installed (zenity dialog)
#   launch-gui.sh pretoa          # launch Pre-T2A directly
#   launch-gui.sh t2a             # launch T2A directly
#
# All stdout/stderr is redirected to ~/uo-offline/logs/launch.log so the
# shortcut works with Terminal=false (no Konsole required).
set -uo pipefail   # NOT -e: we want to keep going and report errors via GUI

INSTALL_ROOT="${HOME}/uo-offline"
LOG_DIR="${INSTALL_ROOT}/logs"
LAUNCH_LOG="${LOG_DIR}/launch.log"
START_SCRIPT="${INSTALL_ROOT}/start-uo.sh"

mkdir -p "${LOG_DIR}"

# Send everything to a log file from this point on.
exec >>"${LAUNCH_LOG}" 2>&1
echo "==================================================================="
echo "$(date)  launch-gui.sh starting with args: $*"

# Helper to surface fatal errors visually. zenity is available on KDE Plasma
# (SteamOS Desktop), kdialog is the Plasma-native alternative, notify-send
# is a last fallback. If none are available, we just bail to the log.
gui_error() {
  local msg="$1"
  if command -v kdialog >/dev/null 2>&1; then
    kdialog --error "$msg\n\nSee ${LAUNCH_LOG}"
  elif command -v zenity >/dev/null 2>&1; then
    zenity --error --no-wrap --text="$msg\n\nSee ${LAUNCH_LOG}"
  elif command -v notify-send >/dev/null 2>&1; then
    notify-send -u critical "UO Offline" "$msg"
  fi
  exit 1
}

# Decide which expansion to launch.
EXPANSION=""

if [[ $# -ge 1 ]]; then
  EXPANSION="$1"
else
  # No argument — see what's installed and decide.
  PRETOA_INSTALLED=0
  T2A_INSTALLED=0
  [[ -f "${INSTALL_ROOT}/servers/pretoa/ServUO.exe" ]] && PRETOA_INSTALLED=1
  [[ -f "${INSTALL_ROOT}/servers/t2a/ServUO.exe" ]]    && T2A_INSTALLED=1

  if (( PRETOA_INSTALLED == 0 && T2A_INSTALLED == 0 )); then
    gui_error "No UO Offline server is installed yet. Run install.sh first."
  elif (( PRETOA_INSTALLED == 1 && T2A_INSTALLED == 0 )); then
    EXPANSION="pretoa"
  elif (( PRETOA_INSTALLED == 0 && T2A_INSTALLED == 1 )); then
    EXPANSION="t2a"
  else
    # Both installed — show a chooser. Prefer kdialog on Plasma since that's
    # what SteamOS desktop is. Fall back to zenity.
    if command -v kdialog >/dev/null 2>&1; then
      EXPANSION=$(kdialog --radiolist "Which version of UO do you want to play?" \
        pretoa "Pre-T2A (Sept 1997 launch)" on \
        t2a    "The Second Age (June 1998)" off 2>/dev/null)
    elif command -v zenity >/dev/null 2>&1; then
      EXPANSION=$(zenity --list \
        --title="UO Offline" \
        --text="Which version of UO do you want to play?" \
        --radiolist \
        --column="Pick" --column="Key" --column="Description" \
        TRUE  pretoa "Pre-T2A (Sept 1997 launch)" \
        FALSE t2a    "The Second Age (June 1998)" 2>/dev/null)
    else
      # No dialog tool — pick pretoa silently.
      EXPANSION="pretoa"
    fi
  fi
fi

if [[ -z "${EXPANSION}" ]]; then
  echo "User cancelled expansion picker."
  exit 0
fi

case "${EXPANSION}" in
  pretoa|t2a) ;;
  *) gui_error "Unknown expansion: '${EXPANSION}'. Expected 'pretoa' or 't2a'." ;;
esac

if [[ ! -x "${START_SCRIPT}" ]]; then
  gui_error "Launcher script missing or not executable:\n${START_SCRIPT}"
fi

echo "Launching expansion: ${EXPANSION}"
# Hand off to the real launcher. Its exec will replace this shell.
exec "${START_SCRIPT}" --expansion="${EXPANSION}"
