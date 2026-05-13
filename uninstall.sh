#!/usr/bin/env bash
# Remove everything this installer placed.
set -euo pipefail

INSTALL_ROOT="${HOME}/uo-offline"
DESKTOP_FILE="${HOME}/.local/share/applications/UO-Offline.desktop"

echo "This will delete:"
echo "  ${INSTALL_ROOT}"
echo "  ${DESKTOP_FILE}"
echo

if [[ -f "${INSTALL_ROOT}/installed.list" ]]; then
  echo "Installed expansions that will be removed:"
  while IFS=$'\t' read -r exp name port; do
    [[ -n "$exp" ]] && echo "  - ${name}"
  done < "${INSTALL_ROOT}/installed.list"
  echo
fi

read -r -p "Type 'yes' to confirm: " ans
[[ "$ans" == "yes" ]] || { echo "Cancelled."; exit 0; }

# Stop any running servers
if [[ -x "${INSTALL_ROOT}/stop-uo.sh" ]]; then
  "${INSTALL_ROOT}/stop-uo.sh" 2>/dev/null || true
fi

rm -rf "${INSTALL_ROOT}"
rm -f "${DESKTOP_FILE}"
update-desktop-database "${HOME}/.local/share/applications" 2>/dev/null || true

echo "Uninstalled."
