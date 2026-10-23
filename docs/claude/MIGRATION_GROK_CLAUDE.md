# Migration Grok Build → Claude Code

**Stand:** 2026-10-23 · **Grundlage:** `reports/claude/AUDIT_CLAUDE_MIGRATION_2026-10-23.md`
**Freigabe:** Manuel hat am 2026-10-23 die Schritte M1 (Regelbrücke) und M3 (Schutz härten) freigegeben. M2 (Rollen) und M4 (Entscheidungsregister) sind **offen**.

`AGENTS.md` bleibt unverändert und wird über `@AGENTS.md` in `CLAUDE.md` geladen. Diese Datei legt fest, welche Teile davon für Claude Code weiter gelten und welche ersetzt sind.

## Gilt unverändert weiter

| Regel aus `AGENTS.md` | Inhalt |
|---|---|
| Sprache | Antworten und UI-Texte auf Deutsch |
| Nie auf `main` | Arbeit nur auf eigenem Branch, kein `--force`, kein Rebase gepushter Commits |
| Tests vor Abgabe | Alle drei Testprojekte grün (`dotnet test Trendo.sln -c Release` deckt sie ab) |
| Kleine Änderungen | Nur die im Auftrag genannten Dateien/Bereiche anfassen |
| Bericht | `tasks/<task-id>.bericht.md` mit Auftrag, Branch, Dateien, Testzahlen je Projekt, offene Punkte, nicht Geprüftes |
| Verboten | Admin-Rechte, systemweite Installationen, Registry, Dienste, Treiber, Datenträger; Blender/Unreal/Visual Studio ändern; Nachrichten, Käufe, Formulare, Releases |
| Datenschutz im Repo | Keine Secrets, keine Benutzer-/Rechnernamen, keine Pfade mit Benutzernamen (`C:\Users\<user>`, `<pc>`) |
| Status ehrlich | `APPROVED_SPEC` ≠ `IMPLEMENTED`; `TESTED`/`PC_VERIFIED` nur mit Rohlog + SHA-256 unter `reports/` |
| Gesperrt | QA-017, QA-019 und Codesignatur nie als bestanden markieren; SPEC-07 abgelehnt; Logo nicht freigegeben |
| KI-Kosten | Kostenpflichtige KI-APIs nur optional, nie vorausgesetzt |
| UI | Höchstens 2 Navigationsebenen; Studio-Namen nur aus `src/Trendo.App/StudioCatalog.cs` |
| Issues | W1-Issues (#14, #15, #17–#22) und W7-Issues (#64–#66) bleiben offen, bis die Module fertig sind |
| Forensik | 120-h-Regel exakt wie definiert (im Code: `ForensicsLab.MaxLife`) |

## Ersetzt für Claude Code

| Grok-Regel | Ersatz | Status |
|---|---|---|
| Grok Bot = Dispatcher, Reviewer, Merger | Manuel entscheidet über Review und Merge; Claude liefert Diff, Testlog und Bericht | **offen (M2)** |
| Grok Build pusht `grok/*` selbst | Claude pusht **nicht**: Der Hook blockiert jeden `git push`, die Claude-Berechtigungen fragen bei jedem Push nach. Manuel pusht | vorläufig bis M2 |
| Branch `grok/<task-id>-<kurz>` | Branch `claude/<task-id>-<kurz>` | vorläufig bis M2 |
| Release nur durch Grok Bot über `installer/release.sh` | Release nur durch Manuel; Claude ist für `installer/release.sh` gesperrt | gilt |
| Nur PowerShell, keine Pipes/`;`/`&&`, keine Variablen | Entfällt: Diese Regel kam aus dem Grok-`dontAsk`-Abbruch. Claude prüft zusammengesetzte Befehle Teil für Teil | gilt |
| Start über `scripts/grok-trendo.ps1`, Rechte aus `.grok/config.toml` | Start mit `claude` im Repo; Rechte in `.claude/settings.json` (`permissions` + Hook) | gilt |
| Protokolle in `tasks/logs/` | Claude-Jobs in `.trendo-local/`; vorhandene Grok-Protokolle bleiben in `tasks/logs/` | gilt |

## Übertragene Schutzregeln (`.grok/config.toml` → `.claude/settings.json`)

| Grok-Deny | Claude |
|---|---|
| `git push*main*`, `--force`, `-f` | `permissions.deny` + Hook |
| `git reset --hard`, `git rebase` | `permissions.deny` + Hook (reset) |
| `gh release*`, `installer/release.sh` | `permissions.deny` |
| `rm -rf`, `Remove-Item -Recurse` | `permissions.deny` + Hook |
| `-Verb RunAs`, `winget`, `msiexec`, `choco` | `permissions.deny` (+ Hook für RunAs) |
| `reg add/delete`, `Set-ItemProperty HK*`, `Set-ExecutionPolicy` | `permissions.deny` (+ Hook für reg/ExecutionPolicy) |
| `diskpart`, `bcdedit`, `Format-Volume`, `shutdown`, `Stop-/Restart-Computer`, `sc.exe`, `netsh` | `permissions.deny` (+ Hook teilweise) |

Zusätzlich sperren `permissions.deny` und der Hook Schreibzugriffe auf `AGENTS.md`, `requirements.yaml`, `docs/spec/decisions.yaml`, `.claude/settings.json`, `.mcp.json` und den Hook selbst. Änderungen daran macht Manuel oder ein ausdrücklich freigegebener Migrationsschritt.

**Grenze:** `permissions.deny` greift auf die Befehlsform, die Claude schreibt. Umgehungen wie `git -C . push` erkennt die Regel nicht. Die Regeln sind Leitplanken und keine Sandbox. Der Hook-Fehlermodus `onFailure: block` wirkt erst ab Claude Code 2.1.295; installiert war beim Audit 2.1.282. Das Update ist noch offen.

## Grok-Artefakte

`.grok/`, `scripts/grok-trendo.ps1`, `docs/spec/grok/` und `docs/spec/qa/GROK_QA_MATRIX.csv` bleiben bis zum Abschluss von M2 unverändert. Danach werden sie als historisch markiert, aber nicht gelöscht.
