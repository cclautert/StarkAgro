#!/usr/bin/env bash
# VPS deploy entrypoint for GitHub Actions (expects cwd = repo root on Hostinger).
set -euo pipefail

ROOT="${1:-.}"
cd "$ROOT"

if [[ ! -d AgripeWebUI && -d /opt/agripeweb/AgripeWebUI ]]; then
  echo "Detected repo at /opt/agripeweb"
  if [[ -f .env && ! -f /opt/agripeweb/.env ]]; then
    echo "Moving .env into /opt/agripeweb"
    mv .env /opt/agripeweb/.env
  fi
  cd /opt/agripeweb
fi

if [[ -d .env ]]; then
  echo "Removing invalid .env directory (Docker bind-mount artifact)"
  rm -rf .env
fi

if [[ -x scripts/bootstrap-vps-env.sh ]]; then
  scripts/bootstrap-vps-env.sh . || true
fi

if [[ ! -f .env ]]; then
  echo "ERROR: .env still missing after bootstrap; ensure production secrets are set" >&2
  exit 1
fi

chmod +x scripts/deploy-hostinger-remote.sh
scripts/deploy-hostinger-remote.sh .
