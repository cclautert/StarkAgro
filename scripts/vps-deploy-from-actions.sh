#!/usr/bin/env bash
# VPS deploy entrypoint for GitHub Actions (expects cwd = repo root on Hostinger).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$("$SCRIPT_DIR/resolve-vps-repo-root.sh" "${1:-.}")"
cd "$ROOT"

if [[ -d .env ]]; then
  echo "Removing invalid .env directory (Docker bind-mount artifact)"
  rm -rf .env
fi

if [[ -x "$ROOT/scripts/bootstrap-vps-env.sh" ]]; then
  "$ROOT/scripts/bootstrap-vps-env.sh" "$ROOT" || true
fi

if [[ ! -f "$ROOT/.env" ]]; then
  echo "ERROR: .env still missing after bootstrap; ensure production secrets are set" >&2
  exit 1
fi

chmod +x "$ROOT/scripts/deploy-hostinger-remote.sh"
"$ROOT/scripts/deploy-hostinger-remote.sh" "$ROOT"
