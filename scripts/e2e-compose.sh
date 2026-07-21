#!/usr/bin/env bash
# End-to-end smoke test of the reference Compose stack.
# Prerequisite: Docker (with the compose plugin) available on PATH.
set -euo pipefail

cd "$(dirname "$0")/.."

docker compose up -d --build
cleanup() { docker compose down -v; }
trap cleanup EXIT

for _ in $(seq 1 30); do
  if curl -fsS -H "Host: whoami.local" http://localhost/ >/dev/null 2>&1; then
    echo "OK: sample service reachable through DockYarp."
    exit 0
  fi
  sleep 2
done

echo "FAIL: sample service not reachable through DockYarp." >&2
exit 1
