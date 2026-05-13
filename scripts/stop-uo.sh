#!/usr/bin/env bash
# Stop any running ServUO instance(s) cleanly.
set -euo pipefail

INSTALL_ROOT="${HOME}/uo-offline"

shopt -s nullglob
pidfiles=("${INSTALL_ROOT}"/.servuo-*.pid)

if (( ${#pidfiles[@]} == 0 )); then
  echo "No running ServUO instances found."
  exit 0
fi

for pidfile in "${pidfiles[@]}"; do
  exp=$(basename "$pidfile" .pid)
  exp="${exp#.servuo-}"
  pid=$(cat "$pidfile" 2>/dev/null || true)
  [[ -z "$pid" ]] && { rm -f "$pidfile"; continue; }

  if ! kill -0 "$pid" 2>/dev/null; then
    echo "[$exp] stale PID file, removing."
    rm -f "$pidfile"
    continue
  fi

  echo "[$exp] stopping (pid $pid)..."
  kill -TERM "$pid"
  for _ in $(seq 1 30); do
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  if kill -0 "$pid" 2>/dev/null; then
    echo "[$exp] forcing kill."
    kill -KILL "$pid" 2>/dev/null || true
  fi
  rm -f "$pidfile"
  echo "[$exp] stopped."
done
