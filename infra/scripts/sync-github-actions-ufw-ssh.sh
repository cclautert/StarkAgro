#!/usr/bin/env bash
# Sync UFW allow rules for GitHub Actions runner IP ranges on SSH (port 22).
# Idempotent: safe to re-run (cron weekly or after Hostinger firewall changes).
#
# Usage (on VPS as root or sudo):
#   sudo ./infra/scripts/sync-github-actions-ufw-ssh.sh
#
# Rollback:
#   sudo ufw status numbered | grep 'GitHub Actions'
#   sudo ufw delete <rule-number>   # repeat for each GH Actions rule

set -euo pipefail

PORT="${SSH_PORT:-22}"
META_URL="${GITHUB_META_URL:-https://api.github.com/meta}"
COMMENT="GitHub Actions SSH deploy"

if ! command -v ufw >/dev/null 2>&1; then
  echo "ufw not installed — configure Hostinger hPanel firewall manually:" >&2
  echo "  Allow TCP ${PORT} from https://api.github.com/meta → actions[] CIDRs" >&2
  exit 1
fi

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  echo "Run as root: sudo $0" >&2
  exit 1
fi

echo "Fetching GitHub Actions IP ranges from ${META_URL}..."
mapfile -t CIDRS < <(curl -fsSL "$META_URL" | python3 -c "
import json, sys
data = json.load(sys.stdin)
for cidr in data.get('actions', []):
    print(cidr)
")

if [[ ${#CIDRS[@]} -eq 0 ]]; then
  echo "No actions CIDRs returned from GitHub meta API" >&2
  exit 1
fi

echo "Found ${#CIDRS[@]} GitHub Actions CIDR blocks."

added=0
for cidr in "${CIDRS[@]}"; do
  if ufw status | grep -Fq "${cidr}.*${PORT}.*${COMMENT}"; then
    continue
  fi
  ufw allow from "$cidr" to any port "$PORT" proto tcp comment "$COMMENT"
  added=$((added + 1))
done

ufw reload || true
echo "Done. Added ${added} new rule(s). Current SSH rules:"
ufw status | grep -E "${PORT}|${COMMENT}" || ufw status numbered | head -20
