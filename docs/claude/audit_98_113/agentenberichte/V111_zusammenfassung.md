# V111 – Cross-Studio Fusion & Platform Completion: Bestandsabgleich

Read-only-Audit, Repo `C:\Users\<user>\Desktop\Trend Os`, HEAD `7e1b8634378a424a2b69cc9e4d70c0dbe5bfb322` (entspricht dem in der Spec genannten SHA). Nichts gebaut, nichts getestet, kein Netz.
Quellen: `VERBESSERUNG_111_GODMODE.md` (762 Zeilen, vollständig gelesen), `anhang/V111_FUNKTIONSREGISTER.csv` (50 Zeilen V111-01 bis V111-50, alle `APPROVED_SPEC / NOT_RUN`). Die IDs in Spec und Register stimmen überein.
Detailtabelle: `V111.csv`.

Testangaben: Der volle dotnet-Testlauf war am 30.10.2026 auf Commit c062bb3 grün (Api 15/15, Core 845, App 432). Das ist **nicht PC_VERIFIED**, und dieser Audit hat ihn nicht wiederholt. Der Python-Test `tests/claude_handoff/test_package.py` gehört nicht zu diesem Lauf. Sein Status ist unbekannt.

## 1. Zahlen je Status (50 Funktionen)

| Status | Anzahl | Funktionen |
|---|---|---|
| IMPLEMENTIERT_MIT_TEST | 0 | – |
| IMPLEMENTIERT_OHNE_TEST | 0 | – |
| TEILWEISE | 34 | 01–04, 06–09, 15, 17, 18, 22, 25–28, 31–41, 43, 44, 46–50 |
| NUR_SPEZIFIZIERT | 16 | 05, 10, 11, 12, 13, 14, 16, 19, 20, 21, 23, 24, 29, 30, 42, 45 |

„TEILWEISE“ bedeutet hier: Ein vorhandener Baustein deckt einen Teil der Funktion ab und ist meist auch getestet. Die eigentliche V111-Funktion (z. B. ein Datenvertrag, ein Studio, eine Ansicht oder ein Negativtest aus der Abnahme) fehlt aber. Keine der 50 Funktionen erfüllt ihren V111-Abnahmetest vollständig.

## 2. Wichtigste Lücken

1. **Kein Nachweis-/Statusmodell je Funktion (V111-22, -26, -27, -29).** `StatusRules` (src/Trendo.Core/StudioRegistry.cs) liest nur die YAML-Felder `implementation_status`/`test_status` je Modul. `Tested()` akzeptiert `PASS`/`TESTED_BOX`, ohne einen Beleg zu prüfen. Es gibt keinen Datensatz, der eine Anforderung mit Commit, Test-ID, Log und SHA-256 verbindet. `RunState.Evidence` (RunStore.cs) gilt nur für Prompt-Läufe.
2. **Das Kanonregister endet bei IMP-97 (V111-21).** In `requirements.yaml` und `docs/spec/decisions.yaml` fehlen IMP-98 bis IMP-111. V111 selbst ist also nicht als Entscheidung im Repo eingetragen.
3. **Der gesamte Asset Hub fehlt (V111-11 bis -20).** Es gibt kein Asset-Manifest, keine Inhaltserkennung per Magic-Byte, keinen transaktionalen Import, keine Revisionen oder Verknüpfungen und keine Konfliktlogik. `Assets.cs` ist nur ein Katalog freier Assets (Poly Haven usw.).
4. **Kein globaler Projektkontext (V111-02, -05, -09, -10).** `ProjectStore` isoliert sauber und ist getestet (QA-003). In der App gibt es aber kein aktives Projekt. `ProgramDocument` und `DeskWindow` haben kein ProjectId-Feld. `GlobalSearch` indiziert den ganzen Projekte-Ordner sowie Eigene Dokumente und Bilder ohne Projekttrennung.
5. **Die Job-Governance hat Lücken (V111-25, -42).** `tools/trendo_ops.py` hat Statusübergänge, Worktrees und ein Opt-in über `--execute`. Es fehlen aber das Limit von höchstens 1–3 RUNNING-Jobs, erlaubte Pfade je Job (BLOCKED ohne Pfad) und ein Jobtyp für `dotnet build/test` mit Rohlog.

## 3. Konflikte und Hinweise

- **Keine zweiten Kerne.** Die Spec fordert das selbst. Dafür kommen nur diese vorhandenen Bausteine infrage: `StatusRules`/`Progress` (Status), `RunStore`/`AuditLog` (Evidenz), `ApprovalGate` (Freigabe), `trendo_ops.py` (Jobs), `GlobalSearch` (Suche), `StudioCatalog` (Navigation) und `CapabilityCatalog` (Reife).
- **Zwei Agentensysteme.** `AgentCenter` (src/Trendo.Core/W2/Agents.cs, 12 Trendo-interne Rollen) und die Jobs in `trendo_ops.py` (Claude Code) sind getrennt. V111-25 darf sie nicht vermischen.
- **Überschneidung mit IMP-17** „Universal Studio Fusion & Project Workspace“ (requirements.yaml, APPROVED, IN_PROGRESS, 12 Funktionen). V111-01 bis -10 decken dasselbe Feld ab. Vor dem Bau muss klar sein, welche ID führt, damit nichts doppelt entsteht.
- **Duplikat innerhalb der Spec:** V111-28 und V111-41 beschreiben beide die Plattformmatrix. Es darf nur eine Implementierung geben (Basis `TripleMode.Capabilities`).
- **Gesperrte Entscheidungen:** IMP-86 ist REJECTED, IMP-97 ist UNDECIDED, SPEC-07 und LANG-2.0 sind REJECTED. Das ist in `decisions.yaml` bestätigt und durch `DecisionRegisterTests` sowie den PromptAction-Test „23, 25, 34, Language Studio 2.0 und 35 bleiben unverändert“ abgesichert. V111-37 (Hilfen) darf Language Studio 2.0 nicht wiederbeleben. Ab 114 ist pausiert. V112/V113 liegen zwar im Übergabepaket, gehören aber nicht zu diesem Auftrag.
- **5 neue ProgramSpec-IDs** (`one-workspace-fusion`, `universal-data-asset-hub`, `production-completion-center`, `premium-experience-studio`, `platform-release-center`). Sie erweitern den StudioCatalog. Sie müssen dort eingetragen werden und dürfen keine zweite Navigation bilden.
- **Statusregel weicht ab:** `StatusRules.Tested` (PASS/TESTED_BOX ohne Beleg) widerspricht der V111-Definition von TESTED (Commit, Rohlog, Exitcode, Hash). Die Regel lässt sich nur mit Entscheidung 02 ändern. Sonst wird sie „rot“ für bestehende Module.

## 4. Fehlende oder falsche Quellen

- In `requirements.yaml` und `docs/spec/decisions.yaml` fehlen Einträge für IMP-98 bis IMP-111.
- Die Spec nennt Pfade, die es nicht gibt: `src/Trendo.Core/KiLibraryStore.cs` (tatsächlich `src/Trendo.Core/W2/KiLibraryStore.cs`), `deps/` und `LICENSES`.
- Die V98–V110-MDs liegen nur im Übergabepaket (`spezifikationen/VERBESSERUNG_098…110_GODMODE.md`), nicht im Repo. Sie wurden für diese Analyse nicht gelesen.
- Die Spec gibt keine Priorität je Funktion an, nur die Phasen P2 bis P6. Die Spalte `prioritaet_spec` enthält deshalb die Phase.

## 5. Vorschläge für die ersten Arbeitspakete (klein und gut testbar)

### AP-1: Funktionsregister mit Statusanzeige aus Beweisen (deckt Teile von V111-21, -22, -26, -27; kein zweiter Kern)
- **Daten:** Unter `improvement_functions` in `requirements.yaml` bleibt die Struktur erhalten (es gibt bereits Einträge wie `IMP-42-01`). Je Funktion kommt ein optionales Feld `evidence` hinzu: `commit`, `test_display_name`, `log_path`, `log_sha256`. V111-01 bis -50 werden **erst nach ausdrücklicher Nutzerfreigabe** eingetragen (Agent 02).
- **Logik:** `src/Trendo.Core/StudioRegistry.cs` (`StatusRules`) bekommt eine neue, reine Funktion, etwa `EvidenceState(impl, test, evidence, headCommit, fileExists, sha)`. Sie liefert APPROVED_SPEC, IMPLEMENTED, TESTED, STALE oder INVALID. TESTED gibt es nur bei passendem Commit und übereinstimmendem Log-SHA. Die bestehenden `Built`/`Tested`/`Done` bleiben unverändert, damit keine Regression entsteht.
- **Zählung:** `src/Trendo.Core/Progress.cs` erhält getrennte Brüche (approved/implemented/tested) und gibt bei Nenner 0 „keine Daten“ aus.
- **Anzeige:** die vorhandene Studios-/Bauplan-Ansicht (`src/Trendo.App/MainWindow.StudiosView.cs`) über `Progress`. Es entsteht keine neue Seite und kein neues Studio.
- **Tests (Core, rein und ohne UI):** Eine testlose Funktion bleibt IMPLEMENTED. Ohne Log-SHA wird sie nicht TESTED. Ein verändertes Log ergibt INVALID. Ein anderer Commit ergibt STALE. 5 von 50 TESTED ergibt 10 %. Nenner 0 ergibt „keine Daten“. IMP-86 bleibt REJECTED.
- **Bausteine mit Pfaden:** `src/Trendo.Core/StudioRegistry.cs` (StatusRules, StudioRegistry.Load), `src/Trendo.Core/Progress.cs` (Progress.Load, GermanStatus), `src/Trendo.Core/AuditLog.cs` (hash-verkettet, Verify), `src/Trendo.Core/PromptAction/RunStore.cs` (RunState.Evidence/Qa), `tests/Trendo.Core.Tests/DecisionRegisterTests.cs`, `tests/Trendo.Core.Tests/Tests.cs` (QA-015, Bauplan), `docs/buildplan.json`.
- **Agenten:** Builder `06-data-memory`, Review `02-requirements-decisions` und `01-architecture-lead` (gegen Duplikate), QA `19-qa-verification`.

### AP-2: Job-Limit und erlaubte Pfade in trendo_ops (V111-25, Teil)
- `tools/trendo_ops.py`: In `run()` wird geprüft, ob schon 3 oder mehr Jobs RUNNING sind. Dann wird abgelehnt und kein Start ausgelöst. Dazu kommt eine neue Spalte `allowed_paths` (Migration per `ALTER TABLE … ADD COLUMN`, falls sie fehlt). Fehlt sie, geht der Job auf BLOCKED. Ein Wiederanlauf behält dieselbe Job-ID, wie es schon heute der Fall ist.
- **Tests:** in `tests/claude_handoff/test_package.py` nach dem Muster von `test_transactions_survive_new_connection` (Temp-DB, kein Claude-Start).
- **Agenten:** Builder `08-agent-workflow`, Review `03-security-permissions`, QA `19-qa-verification`.
