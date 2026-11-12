#!/usr/bin/env bash
# Merged PRs streng nacheinander. Standard: Dry-Run.
# Nutzung: chain.sh [--execute] [--retarget-main] <pr> [<pr> ...]
set -euo pipefail

LOG="${LOG:-/tmp/chain.log}"
exec > >(tee -a "$LOG") 2>&1

ARGS=()
PRS=()
for a in "$@"; do
  case "$a" in
    --execute|--retarget-main) ARGS+=("$a") ;;
    *) p="${a#\#}"
       case "$p" in 104|236) echo "FEHLER: PR #$p ist blockiert"; exit 3 ;; esac
       PRS+=("$p") ;;
  esac
done
[[ ${#PRS[@]} -gt 0 ]] || { echo "FEHLER: keine PRs angegeben"; exit 2; }

cd "$(git rev-parse --show-toplevel)"
[[ -z "$(git status --porcelain)" ]] || { echo "FEHLER: Arbeitsbaum nicht sauber"; exit 4; }

ORIG="$(git rev-parse --abbrev-ref HEAD)"
restore() { git checkout -q "$ORIG" || echo "WARNUNG: Rueckwechsel auf $ORIG fehlgeschlagen"; }
trap restore EXIT

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "=== chain start $(date '+%F %T') prs=${PRS[*]} args=${ARGS[*]:-dry-run} orig=$ORIG"
for pr in "${PRS[@]}"; do
  echo "--- PR #$pr"
  "$HERE/mergepr.sh" "${ARGS[@]}" "$pr"
done
echo "=== chain ok $(date '+%F %T')"
