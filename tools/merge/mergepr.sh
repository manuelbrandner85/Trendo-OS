#!/usr/bin/env bash
# Merged genau einen PR. Standard: DRY_RUN=1. Echter Lauf nur mit --execute.
# Nutzung: mergepr.sh [--execute] [--retarget-main] <pr-nummer>
set -euo pipefail

DRY_RUN="${DRY_RUN:-1}"
RETARGET=0
PR=""
BLOCKED=(104 236)

while [[ $# -gt 0 ]]; do
  case "$1" in
    --execute) DRY_RUN=0 ;;
    --retarget-main) RETARGET=1 ;;
    -h|--help) sed -n '2,3p' "$0"; exit 0 ;;
    *) PR="${1#\#}" ;;
  esac
  shift
done

[[ "$PR" =~ ^[0-9]+$ ]] || { echo "FEHLER: ungueltige PR-Nummer '$PR'" >&2; exit 2; }
for b in "${BLOCKED[@]}"; do
  [[ "$PR" == "$b" ]] && { echo "FEHLER: PR #$PR ist blockiert" >&2; exit 3; }
done

log() { echo "[$(date '+%F %T')] [#$PR] $*"; }
run() {
  if [[ "$DRY_RUN" == "1" ]]; then log "DRY-RUN: $*"; else log "RUN: $*"; "$@"; fi
}

[[ -z "$(git status --porcelain)" ]] || { log "FEHLER: Arbeitsbaum nicht sauber"; exit 4; }

git fetch origin --prune
HEAD_REF="$(gh pr view "$PR" --json headRefName -q .headRefName)"
BASE_REF="$(gh pr view "$PR" --json baseRefName -q .baseRefName)"
STATE="$(gh pr view "$PR" --json state -q .state)"
log "head=$HEAD_REF base=$BASE_REF state=$STATE"
[[ "$STATE" == "OPEN" ]] || { log "FEHLER: PR ist nicht OPEN"; exit 5; }

if [[ "$BASE_REF" != "main" ]]; then
  if [[ "$RETARGET" == "1" ]]; then
    run gh pr edit "$PR" --base main
  else
    log "FEHLER: Basis ist '$BASE_REF', nicht main (Option --retarget-main noetig)"; exit 6
  fi
fi

git checkout -B "$HEAD_REF" "origin/$HEAD_REF"
[[ "$(git rev-parse --abbrev-ref HEAD)" == "$HEAD_REF" ]] || { log "FEHLER: falscher Branch"; exit 7; }

if ! git merge --no-edit origin/main; then
  log "FEHLER: Konflikt beim Einmergen von origin/main"
  git merge --abort || true
  exit 8
fi

log "dotnet build"
dotnet build Trendo.sln -c Release
log "dotnet test"
dotnet test Trendo.sln -c Release --no-build

MAIN_SHA="$(git rev-parse origin/main)"
run git tag "backup/pre-$PR" "$MAIN_SHA"
run git push origin "refs/tags/backup/pre-$PR"
run git push origin "HEAD:refs/heads/$HEAD_REF"
run gh pr ready "$PR"
run gh pr merge "$PR" --merge

if [[ "$DRY_RUN" == "0" ]]; then
  FINAL="$(gh pr view "$PR" --json state -q .state)"
  [[ "$FINAL" == "MERGED" ]] || { log "FEHLER: State nach Merge ist '$FINAL'"; exit 9; }
  MERGE_SHA="$(gh pr view "$PR" --json mergeCommit -q .mergeCommit.oid)"
  log "MERGED merge-sha=$MERGE_SHA backup=backup/pre-$PR ($MAIN_SHA)"
else
  log "DRY-RUN fertig (main=$MAIN_SHA)"
fi
