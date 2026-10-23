# Aufträge für Claude Code

Ablauf (Regeln in `CLAUDE.md`, Abschnitt „Rollen und Ablauf“):
1. Auftrag `tasks/T-<nr>-<kurz>.md` nach `tasks/VORLAGE.md` anlegen (Claude Code per `/trendo-plan` oder Manuel).
2. Claude Code im Repo-Ordner starten (`claude`) und den Auftrag nennen, z. B. „Bearbeite tasks/T-<nr>-<kurz>.md“.
   Lange Aufträge optional über das lokale Job-System: `python tools/trendo_ops.py job create …` (Status in `.trendo-local/`, nicht im Repo).
3. Claude Code arbeitet auf `claude/T-<nr>-<kurz>`, testet (`dotnet test Trendo.sln -c Release` + Paket-Tests), schreibt `tasks/T-<nr>-<kurz>.bericht.md` und pusht den Branch nur bei grünen Tests.
4. Claude-QA (Agent `19-qa-verification`, bei Sicherheitsthemen zusätzlich `03-security-permissions`) prüft den PR unabhängig und schreibt das Ergebnis in den PR.
5. Merge nach `main` und Releases erst nach ausdrücklichem OK von Manuel im Chat.
