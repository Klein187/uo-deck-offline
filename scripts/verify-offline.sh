#!/usr/bin/env bash
# verify-offline.sh
# Confirms the install can run without network access. Walks the manifest
# and checks each installed expansion.

set -euo pipefail

INSTALL_ROOT="${HOME}/uo-offline"
MANIFEST="${INSTALL_ROOT}/installed.list"

c_grn=$'\e[32m'; c_red=$'\e[31m'; c_ylw=$'\e[33m'; c_reset=$'\e[0m'
ok()   { printf "${c_grn}[ ok ]${c_reset} %s\n" "$*"; }
fail() { printf "${c_red}[fail]${c_reset} %s\n" "$*"; FAILED=1; }
warn() { printf "${c_ylw}[warn]${c_reset} %s\n" "$*"; }

FAILED=0

echo "Checking shared install components..."
declare -A required=(
  ["${INSTALL_ROOT}/dotnet/dotnet"]=".NET runtime"
  ["${INSTALL_ROOT}/ClassicUO/classicuo"]="ClassicUO binary"
  ["${INSTALL_ROOT}/uodata"]="UO data files directory"
  ["${INSTALL_ROOT}/start-uo.sh"]="Launcher script"
  ["${INSTALL_ROOT}/nuget-cache"]="NuGet cache (for offline rebuilds)"
  ["${INSTALL_ROOT}/.env"]="Environment config"
  ["${MANIFEST}"]="Installed manifest"
)
for path in "${!required[@]}"; do
  if [[ -e "$path" ]]; then
    ok "${required[$path]}: $path"
  else
    fail "Missing — ${required[$path]}: $path"
  fi
done

echo
echo "Checking UO data files..."
for f in map0.mul tiledata.mul hues.mul anim.mul; do
  if [[ -f "${INSTALL_ROOT}/uodata/${f}" ]]; then
    ok "uodata/${f}"
  else
    fail "uodata/${f} missing"
  fi
done

if [[ ! -f "${MANIFEST}" ]]; then
  echo
  echo "${c_red}No manifest — cannot check per-expansion state.${c_reset}"
  exit 1
fi

while IFS=$'\t' read -r exp name port; do
  [[ -z "$exp" ]] && continue
  echo
  echo "Checking expansion: ${name} (${exp}, port ${port})"
  srv_dir="${INSTALL_ROOT}/servers/${exp}"

  if [[ ! -d "$srv_dir" ]]; then
    fail "Server directory missing: $srv_dir"
    continue
  fi

  if find "$srv_dir" -name "ServUO.dll" -o -name "ServUO.exe" 2>/dev/null | grep -q .; then
    ok "[${exp}] ServUO build output present"
  else
    fail "[${exp}] No ServUO.dll or ServUO.exe found"
  fi

  srv_cfg="${srv_dir}/Config/Server.cfg"
  if [[ -f "$srv_cfg" ]]; then
    if grep -qE '^(Website|PatchServer)=https?://' "$srv_cfg"; then
      warn "[${exp}] Server.cfg has phone-home URLs (harmless but not strictly offline)"
    else
      ok "[${exp}] No phone-home URLs"
    fi
    if grep -qE '^Address=127\.0\.0\.1' "$srv_cfg"; then
      ok "[${exp}] Bound to 127.0.0.1"
    else
      warn "[${exp}] Server may listen on non-loopback"
    fi
    if grep -qE "^Port=${port}\$" "$srv_cfg"; then
      ok "[${exp}] Port ${port} configured"
    else
      warn "[${exp}] Port mismatch — Server.cfg vs manifest"
    fi
  fi

  exp_cfg="${srv_dir}/Config/Expansion.cfg"
  if [[ -f "$exp_cfg" ]]; then
    exp_val=$(grep -E '^CurrentExpansion=' "$exp_cfg" | cut -d= -f2)
    ok "[${exp}] Expansion = ${exp_val}"
  fi
done < "${MANIFEST}"

echo
if (( FAILED )); then
  echo "${c_red}One or more checks failed.${c_reset}"
  exit 1
else
  echo "${c_grn}All checks passed.${c_reset} You can disconnect and play."
  echo
  echo "To prove it: turn off Wi-Fi, then run ${INSTALL_ROOT}/start-uo.sh"
fi
