#!/usr/bin/env bash
# Resolve AgripeWeb repo root on Hostinger VPS (prints absolute path to stdout).
set -euo pipefail

REQUIRED_DIRS=(AgripeWebUI AgripeWebAPI AgripeWebWorker docker)

has_all_app_dirs() {
  local base="$1"
  [[ -f "$base/AgripeWebUI/Dockerfile" ]] || return 1
  [[ -f "$base/AgripeWebAPI/Dockerfile" ]] || return 1
  [[ -f "$base/AgripeWebWorker/Dockerfile" ]] || return 1
  [[ -f "$base/docker/docker-compose.yml" ]] || return 1
  return 0
}

restore_app_dirs() {
  local root="$1"
  cd "$root"
  echo "Restoring tracked application directories under $root"
  git sparse-checkout disable >/dev/null 2>&1 || true
  git checkout -f HEAD -- AgripeWebUI AgripeWebAPI AgripeWebWorker docker scripts
  for d in "${REQUIRED_DIRS[@]}"; do
    if [[ ! -d "$d" ]]; then
      echo "WARN: missing $d — restoring from git"
      git checkout -f HEAD -- "$d"
    fi
  done
}

collect_candidates() {
  local start="${1:-.}"
  local -a raw=()

  raw+=("/opt/agripeweb")

  if [[ "$start" != "." && -d "$start" ]]; then
    raw+=("$(cd "$start" && pwd)")
  elif [[ -d "." ]]; then
    raw+=("$(pwd)")
  fi

  if [[ -n "${VPS_DEPLOY_PATH:-}" && -d "$VPS_DEPLOY_PATH" ]]; then
    raw+=("$(cd "$VPS_DEPLOY_PATH" && pwd)")
  fi

  raw+=("/opt")

  local found_ui
  found_ui=$(find /opt -maxdepth 5 -type f -path '*/AgripeWebUI/Dockerfile' 2>/dev/null | head -1 || true)
  if [[ -n "$found_ui" ]]; then
    raw+=("$(dirname "$(dirname "$found_ui")")")
  fi

  local -A seen=()
  for c in "${raw[@]}"; do
    [[ -z "$c" || -n "${seen[$c]+x}" || ! -d "$c" ]] && continue
    seen[$c]=1
    printf '%s\n' "$c"
  done
}

pick_repo_root() {
  local start="${1:-.}"
  local candidate

  while IFS= read -r candidate; do
    if [[ -d "$candidate/.git" ]] && has_all_app_dirs "$candidate"; then
      echo "Using git repo root: $candidate" >&2
      echo "$candidate"
      return 0
    fi
  done < <(collect_candidates "$start")

  while IFS= read -r candidate; do
    if [[ -d "$candidate/.git" ]]; then
      restore_app_dirs "$candidate"
      if has_all_app_dirs "$candidate"; then
        echo "Restored git repo root: $candidate" >&2
        echo "$candidate"
        return 0
      fi
    fi
  done < <(collect_candidates "$start")

  echo "ERROR: Could not resolve AgripeWeb git repo root with required app directories" >&2
  collect_candidates "$start" | while IFS= read -r candidate; do
    echo "Candidate $candidate (git=$([[ -d "$candidate/.git" ]] && echo yes || echo no)):" >&2
    ls -la "$candidate" >&2 || true
  done
  exit 1
}

pick_repo_root "$@"
