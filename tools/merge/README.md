# Merge-Kette

    tools/merge/chain.sh 243 244 245            # Dry-Run (Standard)
    tools/merge/chain.sh --execute 243 244 245  # echter Lauf
    --retarget-main                             # Basis eines PR bei Bedarf auf main umstellen

- Log: `/tmp/chain.log` (per `tee`, auch im Terminal), anders mit `LOG=...`.
- Bricht beim ersten Fehler ab (Konflikt -> `merge --abort`, Build/Test-Fehler, State != MERGED).
- #104 und #236 sind blockiert.
- Vor jedem Merge wird `backup/pre-<pr>` auf die aktuelle main-SHA gesetzt.

## Rückweg

Kein force-push, kein reset auf main. Stattdessen:

    git fetch origin
    git switch -c revert/pr-<pr> origin/main
    git revert -m 1 <merge-sha>        # merge-sha steht im Log
    git push origin revert/pr-<pr>
    gh pr create --base main --title "Revert #<pr>"

Mehrere PRs in umgekehrter Reihenfolge revertieren. `backup/pre-<pr>` dient nur als Referenz/Vergleich.
