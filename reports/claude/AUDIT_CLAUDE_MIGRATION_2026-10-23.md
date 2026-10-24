# Trendo OS – Read-only-Audit und Migrationsplan früherer Agent → Claude Code

**Datum:** 2026-10-23 · **Branch:** `claude/godmode-2` (Basis `agent/T-0004-tote-tasten` @ `5c366da`, Paket-Commit `ff6e920`)
**Art:** Read-only. Am Code, an `AGENTS.md`, `requirements.yaml` und `docs/spec/decisions.yaml` wurde nichts geändert. Einzige neue Datei: dieser Bericht (nicht committet).
**Umgebung:** Windows 11, .NET SDK 10.0.402, Python 3.13, Claude Code 2.1.282, Git für Windows. Blender nicht im PATH, Unreal-Variablen nicht gesetzt.

---

## 1. Build und Tests (real ausgeführt)

| Schritt | Befehl | Ergebnis |
|---|---|---|
| Build | `dotnet build Trendo.sln -c Release` | **Exit 0**, 0 Fehler, **18 Warnungen** (9 verschiedene, doppelt gezählt) |
| API-Tests | `dotnet test Trendo.sln -c Release --no-build` | **14/14 bestanden** |
| Core-Tests | (derselbe Lauf) | **427/427 bestanden** |
| UI-Tests | (derselbe Lauf) | **188/188 bestanden** (Dauer 5 min 43 s); Gesamtlauf **Exit 0**, 629 Tests |
| Paket-Tests | `python -m unittest discover -s tests/claude_handoff` | 6/6 bestanden |

Rohlogs liegen lokal unter `.trendo-local/audit-build.log` und `.trendo-local/audit-test.log` (nicht im Repo, enthalten lokale Pfade).

**Abweichung zur README:** Die README nennt „baut ohne Warnungen, 60 Tests (39 Core, 14 API, 7 UI)“. Tatsächlich: 18 Warnungen (CS8669, CS0618 veraltete API, CS9113 ungenutzter Parameter `root`, CS8602 möglicher Nullverweis, CS0108) und deutlich mehr Tests. Die README ist veraltet.

## 2. Alte Agenten-Mechanismen (Inventar)

| Mechanismus | Datei | Funktion |
|---|---|---|
| Agentenregeln | `AGENTS.md` | früherer Agent schreibt Code, frühere Cloud-Leitung dispatcht/reviewt/merged; Branches `agent/<task>`; Bericht `tasks/<id>.bericht.md` |
| Rechte | die frühere Rechtekonfiguration | Allow-/Deny-Liste, `dontAsk`-Modus; erlaubt `git push origin agent/*`, verbietet Push auf `main`, `--force`, Rebase, Installationen, Registry |
| Starter | das frühere Startskript | Startet früherer Agent mit Auftrag, Timeout, Log nach `tasks/logs/`; schaltet Claude-Kompatibilität von früherer Agent ab |
| Aufträge | `tasks/T-*.md`, `tasks/VORLAGE.md`, `tasks/README.md` | Auftrag → Branch → Push → PR → Review durch frühere Cloud-Leitung |
| Prompts/Setup | die früheren Agenten-Prompts (7 Dateien) | Rollen, Setup, MCP-Architektur, frühere Migration Claude → früherer Agent (21.10.2026) |
| QA-Matrix | die frühere Agenten-QA-Matrix | QA-Fälle mit Agenten-Bezug |
| Build-Log | `docs/build-log/` (93 Dateien) | Ein Eintrag pro Modul/Datum |
| Release | `installer/release.sh` | Laut AGENTS.md nur frühere Cloud-Leitung aus grünem `main` |

## 3. Konfliktmatrix Agenten-Regeln ↔ Claude-Paket

| # | Thema | früherer Agent (`AGENTS.md`/die frühere Rechtekonfiguration) | Claude (`CLAUDE.md`/Hook) | Bewertung |
|---|---|---|---|---|
| K1 | **Regeln laden** | früherer Agent lädt `AGENTS.md` | Claude Code lädt nur `CLAUDE.md`; `AGENTS.md` wird **nicht automatisch** gelesen | **Hoch.** Fachregeln wie keine Benutzernamen in Berichten, QA-017/019 nie bestanden, SPEC-07 abgelehnt, Studio-Namen nur aus `StudioCatalog.cs`, Testausgabe pro Projekt gehen sonst verloren |
| K2 | Push | früherer Agent pusht `agent/*`-Branches selbst | Hook blockiert **jeden** `git push` | Entscheidung nötig: Wer pusht Claude-Branches für PRs? |
| K3 | Review/Merge | frühere Cloud-Leitung reviewt und merged | Release-Agent nur Empfehlung; Merge nur Owner | frühere Cloud-Leitung fällt weg → Review-Rolle neu zuordnen (Manuel oder Claude-QA-Agent + Manuel) |
| K4 | Branchnamen | `agent/<task-id>-<kurz>` | `claude/...` | Konvention festlegen, z. B. `claude/T-<nr>-<kurz>` |
| K5 | Bericht | `tasks/<id>.bericht.md` + `docs/build-log/` | Abschlussbericht im Chat; Jobs in `.trendo-local/` | Vereinheitlichen: Berichtsdatei beibehalten |
| K6 | Tests | `dotnet test` je Testprojekt | `dotnet test Trendo.sln` | Beides gleichwertig; sln-Lauf genügt |
| K7 | Release | nur frühere Cloud-Leitung über `installer/release.sh` | nie autonom | Release-Rolle neu zuordnen (Manuel) |
| K8 | Web-Zugriff | die frühere Rechtekonfiguration erlaubt webfetch/websearch | nicht geregelt | ok |
| K9 | Shell-Stil | keine Pipes/`;`/`&&` (Agenten-dontAsk-Abbruch) | keine Einschränkung | Agenten-spezifisch, entfällt |
| K10 | Status `PC_VERIFIED` | nur mit Rohlog + SHA-256 unter `reports/` | „keine erfundenen Ergebnisse“ | Agenten-Regel ist strenger → übernehmen |

## 4. Entscheidungen: kanonisches Register ↔ Übergabe (Decision-Reconciliation)

`docs/spec/decisions.yaml` (MP-1.0, 22.10.2026): 78 APPROVED, 5 REJECTED (`SPEC-07`, `IMP-23`, `IMP-25`, `IMP-34`, `LANG-2.0`), 1 UNDECIDED (`IMP-35`). Verbesserungen sind dort nur bis **IMP-41** erfasst.

| Punkt | `decisions.yaml` | Übergabe 23.10. | Abweichung |
|---|---|---|---|
| IMP-23/25/34 | REJECTED | NEIN | keine |
| IMP-35 | UNDECIDED | OFFEN | keine |
| Language Studio 2.0 | REJECTED | NEIN | keine |
| SPEC-07 | REJECTED | nicht erwähnt | keine (bleibt abgelehnt) |
| IMP-42 … IMP-85 | **fehlt** | JA | **neu einzutragen** |
| IMP-86 (CRM/Service Desk) | **fehlt** | NEIN | **neu einzutragen** |
| IMP-87 … IMP-94 | **fehlt** | JA | **neu einzutragen** |
| IMP-95, IMP-96 | **fehlt** | JA (Architecture/BIM, Lighting/Acoustics) | **neu, aber Funktionsdetails fehlen** im Handoff (nur bis 94) |
| IMP-97 | **fehlt** | OFFEN | **neu (UNDECIDED)** |
| 120-h-Forensikregel | AGENTS.md Regel 5 | unverändert | keine; im Code umgesetzt (`ForensicsLab.MaxLife = 120 h`, Warnung 24 h) |

Auffälligkeiten zur Klärung:
- **IMP-95 (Architecture/BIM)** überschneidet sich fachlich mit **IMP-55 (Architecture & Interior Design)** und dem vorhandenen Code `W6/Architecture*.cs` – Abgrenzung nötig, damit keine Doppel-Engine entsteht.
- Der Handoff verweist bei IMP-50/93 auf „AION/GENESIS“ – das sind getrennte Spielprojekte und keine Trendo-Module. Die Trennung muss erhalten bleiben.
- `decisions.yaml` und `requirements.yaml` werden laut Projektregeln nur von Manuel geändert, und `DecisionRegisterTests` sperren REJECTED/UNDECIDED. Nachtrag IMP-42…97 deshalb nur nach deiner Freigabe.

## 5. Spezifikation ↔ Implementierung

`requirements.yaml` (109 Module, von `StudioRegistry` gelesen):

| Feld | Werte |
|---|---|
| approval_status | 108 APPROVED_SPEC, 1 REJECTED_UPGRADE_BASE_PRESERVED |
| implementation_status | 101 IMPLEMENTED, 7 IN_PROGRESS, 1 NOT_STARTED |
| test_status | 53 PC_VERIFIED, 30 TESTED_BOX, 25 TESTED, 1 NOT_RUN |
| Phasen | P0 ACCEPTED, P1 ACCEPTED (offen: QA-017, QA-019, Code-Signatur), P2 IN_PROGRESS, P3 APPROVED_SPEC |

- `StatusRules.Done()` zeigt ein Studio nur dann als bereit, wenn es gebaut **und** belegt getestet ist. Das ist die ehrliche Statuslogik, auf der aufgebaut werden soll.
- Die Verbesserungen 42–96 sind **APPROVED_SPEC**, nicht IMPLEMENTED. Ein Teil davon setzt auf bestehenden Modulen auf (z. B. 56 → `W6/Engineering*`, 57 → `W6/Electronics*`, 60 → `W6/PointCloud*`, 61 → `W6/Cnc*`, 63 → `W3/Forensics`, 64 → `W6/Geo*`, 67 → `W6/Robotics*`, 75 → `W6/DigitalTwin*`, 84 → `W7/Fusion`). Diese Zuordnung ist ein Vorschlag und muss je Auftrag geprüft werden.

## 6. Vorhandene Kernmodule (wiederverwenden, nicht neu bauen)

| Zweck | Vorhanden | Ort |
|---|---|---|
| KI-Kern | `IntelligenceCore`, `Intel.AskAsync(KiRequest)` (5 Aufrufstellen in der App) | `src/Trendo.Core/W2/IntelligenceCore.cs` |
| Prompts | `PromptLibrary` | `W2/Prompts.cs` |
| Agenten | `AgentRole`, `AgentTask`, `AgentCenter` | `W2/Agents.cs` |
| Workflows | `Workflow`, `WorkflowEngine` | `W2/Workflows.cs` |
| Freigaben | `ApprovalGate` (Plan → einmalige, ablaufende Freigabe → Ausführung) | `ApprovalGate.cs` |
| Rechte | `Rbac` (Guest/User/Owner, 6 Permissions), Audit bei Ablehnung | `Rbac.cs` |
| Audit | `AuditLog` | `AuditLog.cs` |
| Projekte | `ProjectStore` | `ProjectStore.cs` |
| Studio-Status | `StudioRegistry` (aus `requirements.yaml`) | `StudioRegistry.cs` |
| Studio-Anzeige | `StudioCatalog` (54 Einträge, 9 Bereiche, deutsche Namen) | `src/Trendo.App/StudioCatalog.cs` |

`StudioRegistry` (Status) und `StudioCatalog` (Anzeige) ergänzen sich und sind keine Doppelung.
Die `MainWindow` ist auf 67 `partial`-Dateien verteilt. UI-Änderungen haben dadurch eine große Kontaktfläche, und Dateizuständigkeiten müssen pro Auftrag eng gefasst werden.

## 7. Rechte- und Netzgrenzen (Code-Stand)

| Komponente | Grenze | Befund |
|---|---|---|
| Lokale API | Kestrel nur `IPAddress.Loopback`; prüft zusätzlich Remote-IP (403) und Host-Header (421); Bearer-Token je Rolle; `Rbac.Demand` auf jeder Route | solide |
| Companion | `HttpListener`, wahlweise `http://+:port`; Zugriff nur Loopback/LAN (`AllowedRemote`), Kopplung per Token | LAN-Freigabe bewusst; bei `+`-Bindung braucht Windows eine URL-ACL (Admin) – nicht geprüft |
| Ordnerfreigabe | `ShareServer` nur privates LAN/Loopback, Start nur über `ApprovalGate.Execute` | solide |
| Forensik | 120 h Höchstlebensdauer, 24 h Warnung, im Code erzwungen | entspricht Regel |

## 8. Risiken und Funde im Claude-Paket

| # | Fund | Risiko | Vorschlag |
|---|---|---|---|
| R1 | `onFailure: "block"` im Hook braucht laut Claude-Code-Doku **v2.1.295+**, installiert ist **2.1.282** | Fällt der Hook aus (Python fehlt, Timeout), läuft der Befehl trotzdem weiter | Claude Code aktualisieren; zusätzlich harte `deny`-Regeln in den Claude-Berechtigungen statt nur im Hook |
| R2 | `AGENTS.md` wird von Claude nicht geladen (K1) | Agenten-Fachregeln wirken nicht | In `CLAUDE.md` eine Zeile `@AGENTS.md` ergänzen oder die Fachregeln übernehmen |
| R3 | Hook blockiert jeden `git push` (K2) | Kein PR-Ablauf durch Claude möglich | Entscheidung: Manuel pusht selbst, oder Push nur für `claude/*` erlauben |
| R4 | `.claude/worktrees/` (Claude-Worktrees) und `.trendo-local/` stehen nicht in `.gitignore` (`.trendo-local` nur lokal in `.git/info/exclude`) | Versehentliches Committen in einem anderen Klon | `.gitignore` um beide Pfade ergänzen |
| R5 | `trendo_ops.py job run --execute` ruft `claude --print` ohne Berechtigungsmodus auf | Jobs scheitern still an Rechteabfragen, statt zu arbeiten | Bei Bedarf eine `--permission-mode`-/`--allowedTools`-Politik festlegen |
| R6 | README veraltet (Warnungen, Testzahlen, Rollen frühere Cloud-Leitung/früherer Agent) | Falsches Bild vom Stand | Mit Migration aktualisieren |
| R7 | Basis von `claude/godmode-2` ist `agent/T-0004`, das auf GitHub 1 Commit weiter ist und noch nicht in `main` liegt | Paket hängt an ungemergtem Agenten-Branch | Paket-Commit auf aktuelles `main` umsetzen, nachdem T-0004 entschieden ist |

## 9. Migrationsplan – kleinste sichere Schritte

Jeder Schritt bekommt einen eigenen Branch, eine eigene Freigabe und eigene Tests. Code wird erst ab M4 angefasst.

| Schritt | Inhalt | Dateien | Prüfung | Freigabe nötig |
|---|---|---|---|---|
| **M0** | T-0004 klären (mergen oder verwerfen), danach `claude/godmode-2` auf `main` setzen | Git | Paket-Tests 6/6, `dotnet test` grün | Merge durch Manuel |
| **M1** | Regelbrücke: `CLAUDE.md` importiert `@AGENTS.md`; Agenten-spezifische Teile (Shell-Stil, Cloud-Leitungs-Rollen, Push-Erlaubnis) in einer Migrationsnotiz als „ersetzt“ markieren; `AGENTS.md` selbst bleibt unverändert | `CLAUDE.md`, `docs/claude/MIGRATION_ZU_CLAUDE.md` (neu) | Claude-Sitzung zeigt beide Regelwerke | ja (Regelwerk) |
| **M2** | Rollen und Ablauf festlegen: Push, Review, Merge und Release (K2, K3, K7); Branchschema `claude/T-<nr>-<kurz>`; Bericht weiter als `tasks/<id>.bericht.md` | `CLAUDE.md`, `tasks/README.md`, `tasks/VORLAGE.md` | Probeauftrag läuft durch | ja (Rollen) |
| **M3** | Schutz härten: Claude Code ≥ 2.1.295, `.claude/settings.json` um `permissions.deny` (Push auf `main`, `--force`, Installer, Registry) ergänzen, `.gitignore` um `.trendo-local/`, `.claude/worktrees/` und `.claude/settings.local.json` | `.claude/settings.json`, `.gitignore` | Paket-Hooktests + manueller Deny-Test | ja (Sicherheitsdateien sind geschützt) |
| **M4** | Decision-Delta: IMP-42…97 als Vorschlag in einem separaten Branch in `decisions.yaml` nachtragen (86 REJECTED, 97 UNDECIDED, 95/96 mit Hinweis „Details fehlen“) | `docs/spec/decisions.yaml` | `DecisionRegisterTests` grün | **ja, nur Manuel ändert decisions** |
| **M5** | Hausputz ohne Funktionsänderung: 18 Build-Warnungen beheben, README auf Ist-Stand | betroffene `.cs`-Dateien, `README.md` | Build ohne Warnungen, alle Tests grün | ja |
| **M6** | Erster echter Vertical Slice für Verbesserung 42 (Prompt → Auftrag → `ApprovalGate` → Fachadapter → echtes Artefakt) an **einem** Studio, z. B. Briefe (`LetterCase`/`Letters`), über `IntelligenceCore` und `PromptLibrary` | eng begrenzt, per `/trendo-plan` | xUnit + PC-Nachweis mit Rohlog | ja |

Agenten-Artefakte (die frühere Rechtekonfiguration, das frühere Startskript, die früheren Agenten-Dokumente) bleiben bis zum Abschluss von M2 unverändert. Danach können sie als „historisch“ markiert werden. Gelöscht wird nichts ohne Freigabe.

## 10. Was dieses Audit nicht geprüft hat

- Keine Laufzeit der Claude-Hooks und Projekt-Skills in einer echten `claude`-Sitzung in diesem Ordner (Hooks/Skills: NOT_RUN).
- Kein Start der App, keine UI-Sichtprüfung, kein Installer, keine Linux-/Pi-Edition.
- Inhalt der 93 Build-Log- und der `reports/`-Nachweise nicht einzeln gegen `requirements.yaml` abgeglichen. Die Status-Zahlen oben stammen aus der Datei, nicht aus einer Nachprüfung.
- Blender/Unreal-Hilfen nicht ausgeführt (Blender nicht im PATH, Unreal-Variablen fehlen).

**Status:** AUDIT_DONE (read-only). Keine Code-Änderung. Nächster Schritt erst nach Freigabe der Punkte M0–M3.
