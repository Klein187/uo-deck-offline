#!/usr/bin/env bash
# install-launchers.sh — install desktop icons / shortcuts so UO Offline
# can be launched without a terminal. Run this once after install.sh.
#
# Installs:
#   1. ~/Desktop/UO Offline.desktop and per-expansion variants (Desktop Mode)
#   2. ~/.local/share/applications/ entries (KDE app menu)
#   3. Copies launch-gui.sh + start-uo.sh into the install root
#
# Steam non-Steam game entry — see instructions printed at the end.
set -euo pipefail

INSTALL_ROOT="${HOME}/uo-offline"
REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/.."
DESKTOP_DIR="${HOME}/Desktop"
APP_DIR="${HOME}/.local/share/applications"

c_grn=$'\e[32m'; c_ylw=$'\e[33m'; c_blu=$'\e[34m'; c_reset=$'\e[0m'
say()  { printf "${c_blu}[info]${c_reset} %s\n" "$*"; }
ok()   { printf "${c_grn}[ ok ]${c_reset} %s\n" "$*"; }
warn() { printf "${c_ylw}[warn]${c_reset} %s\n" "$*"; }
die()  { printf "[err ] %s\n" "$*" >&2; exit 1; }

if [[ ! -d "${INSTALL_ROOT}" ]]; then
  die "Install root not found at ${INSTALL_ROOT}. Run install.sh first."
fi

# 1. Make sure launch-gui.sh is in the install root.
say "Installing launch-gui.sh to ${INSTALL_ROOT}/launch-gui.sh"
cp "${REPO_ROOT}/scripts/launch-gui.sh" "${INSTALL_ROOT}/launch-gui.sh"
chmod +x "${INSTALL_ROOT}/launch-gui.sh"
ok "launch-gui.sh installed"

# 2. Write desktop entries with the right Exec path. We render each template,
#    substituting @INSTALL_ROOT@ with the real install dir.
mkdir -p "${DESKTOP_DIR}" "${APP_DIR}"

write_desktop_entry() {
  local template="$1"
  local out_name="$2"
  local rendered
  rendered=$(sed "s|@INSTALL_ROOT@|${INSTALL_ROOT}|g" "${REPO_ROOT}/launchers/${template}")
  # Write to ~/Desktop and ~/.local/share/applications
  echo "${rendered}" > "${DESKTOP_DIR}/${out_name}"
  echo "${rendered}" > "${APP_DIR}/${out_name}"
  chmod +x "${DESKTOP_DIR}/${out_name}"
  ok "${out_name}"
}

say "Writing desktop launchers..."
write_desktop_entry "UO-Offline.desktop"       "UO-Offline.desktop"
[[ -f "${INSTALL_ROOT}/servers/pretoa/ServUO.exe" ]] && \
  write_desktop_entry "UO-Offline-PreT2A.desktop" "UO-Offline-PreT2A.desktop"
[[ -f "${INSTALL_ROOT}/servers/t2a/ServUO.exe" ]] && \
  write_desktop_entry "UO-Offline-T2A.desktop"    "UO-Offline-T2A.desktop"

# 3. Refresh KDE's icon / desktop database so the new entries show up.
if command -v kbuildsycoca5 >/dev/null 2>&1; then
  kbuildsycoca5 --noincremental >/dev/null 2>&1 || true
elif command -v kbuildsycoca6 >/dev/null 2>&1; then
  kbuildsycoca6 --noincremental >/dev/null 2>&1 || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "${APP_DIR}" >/dev/null 2>&1 || true
fi

echo
ok "Desktop icons installed!"
echo
echo "${c_grn}From Desktop Mode:${c_reset}"
echo "  - Double-click 'UO Offline' on the desktop"
echo "  - Or find it in the application menu under Games"
echo
echo "${c_grn}From Steam (Gaming Mode + Desktop Mode):${c_reset}"
echo "  Open Steam (Desktop Mode), then:"
echo "  1. Games  →  'Add a Non-Steam Game to My Library'"
echo "  2. Click 'Browse' and select:"
echo "       ${INSTALL_ROOT}/launch-gui.sh"
echo "  3. Confirm 'Add Selected Programs'"
echo "  4. (Optional) Rename it from 'launch-gui.sh' to 'UO Offline'"
echo "  5. (Optional) Right-click → Properties → set a launch option"
echo "     to skip the picker:  pretoa   or   t2a"
echo
echo "  After this, UO Offline appears in your Steam library and"
echo "  is launchable from Gaming Mode like any other game."
echo
