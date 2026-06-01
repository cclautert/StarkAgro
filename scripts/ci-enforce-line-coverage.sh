#!/usr/bin/env bash
# Enforce minimum line coverage from ReportGenerator TextSummary (Summary.txt).
# Usage: ci-enforce-line-coverage.sh <summary.txt> <min-percent>
set -euo pipefail

summary="${1:?Summary.txt path required}"
min="${2:?Minimum line coverage percent required}"

if [[ ! -f "$summary" ]]; then
  echo "Coverage summary not found: $summary"
  exit 2
fi

line="$(grep -E '^  Line coverage:' "$summary" | head -1 || true)"
if [[ -z "$line" ]]; then
  echo "Could not parse line coverage from $summary"
  exit 2
fi

pct="$(echo "$line" | grep -oE '[0-9]+(\.[0-9]+)?' | head -1)"
awk -v pct="$pct" -v min="$min" 'BEGIN {
  if (pct + 0 < min + 0) {
    printf "FAIL: line coverage %.2f%% < required %.2f%%\n", pct, min;
    exit 1;
  }
  printf "PASS: line coverage %.2f%% >= required %.2f%%\n", pct, min;
}'
