# Aufträge für Grok Build

Ablauf (Grok Bot = Dispatcher/Reviewer, Grok Build = Entwickler auf dem PC):
1. Grok Bot legt `tasks/T-<nr>-<kurz>.md` nach `tasks/VORLAGE.md` an (auf `main` per PR oder direkt im PC-Klon).
2. Start auf dem PC im Repo-Ordner:
   `& ".\scripts\grok-trendo.ps1" -Task "tasks\T-<nr>-<kurz>.md"`
   Optional `-TimeoutMin` (Minuten, Standard 60): danach beendet das Skript den Lauf und endet mit Exit-Code 2.
   (Protokoll landet in `tasks/logs/` – nicht im Repo, siehe .gitignore.)
3. Grok Build arbeitet auf `grok/T-<nr>-…`, testet, pusht den Branch und schreibt `tasks/T-<nr>-….bericht.md`.
4. Grok Bot öffnet/prüft den PR (Diff, Tests auf Box und PC), merged nur bei Grün und meldet Manuel das Ergebnis.
5. Folgefragen in derselben Sitzung: `grok -c` im Repo-Ordner.
