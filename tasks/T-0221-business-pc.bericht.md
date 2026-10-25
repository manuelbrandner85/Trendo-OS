# Bericht T-0221: BUS-001 bis BUS-030 am Windows-PC (C2)

**Auftrag:** „Mach mit C2 weiter: BUS-001 bis BUS-030 am Windows-PC prüfen, mit Rohlog und SHA-256.“ (Manuel, 2026-10-25)
**Branch/Worktree:** `claude/T-02xx`, Worktree `integration`, Basis `8fbcbab`

## Ergebnis
- Alle 30 BUS-Nummern haben mindestens einen benannten Test, der am Windows-PC (Release) besteht: Core 15/15, Oberfläche 5/5.
- Rohprotokolle `reports/c2-business/bus-core.trx` und `bus-app.trx` (Rechner- und Benutzername geschwärzt, gültiges XML), Übersicht je Nummer `UEBERSICHT.md`, Prüfsummen `SHA256SUMS`.
- `requirements.yaml`: 30 × `test_status: TESTED_BOX` → `PC_VERIFIED`, `evidence: reports/c2-business/UEBERSICHT.md`. `implementation_status` unverändert (`IMPLEMENTED`).

## Geänderte Dateien
- `tests/Trendo.Core.Tests/W5BusinessTests.cs`: neuer Test für BUS-001 (Idee speichern, ändern, löschen – bisher gab es keinen Test mit dieser Nummer).
- `tests/Trendo.App.Tests/W5BusinessUiTests.cs`: Testname nennt jetzt auch BUS-001.
- `tests/Trendo.Core.Tests/PromptActionTests.cs`: „Unreal-Projektdatei ohne Editor“ vergleicht die UnrealEditor-Prozesse vor und nach dem Lauf. Vorher schlug der Test fehl, sobald Manuel selbst einen Editor geöffnet hatte (in der letzten Minute gestartet) – Fehlalarm des Tests, nicht von Trendo.
- `reports/c2-business/*`, `.gitattributes` (`-text` für die Rohbelege), `requirements.yaml`, `docs/claude/FERTIGSTELLUNGSPLAN.md` (C2 erledigt, Zähler 30/30), `tasks/T-0221-business-pc.md`.

## Kommandos
- `dotnet test tests/Trendo.Core.Tests -c Release --filter "DisplayName~BUS-" --logger trx` und dasselbe für `tests/Trendo.App.Tests`
- `dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler
- `dotnet test Trendo.sln -c Release --no-build --blame-hang --blame-hang-timeout 15m --blame-hang-dump-type none` → Exitcode 0
- `python -m unittest discover -s tests/claude_handoff` → OK

## Testergebnis je Projekt
| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 673 | 1 | 0 | 674 |
| Trendo.App.Tests | 329 | 2 | 0 | 331 (29 min 22 s) |
| Pakettests | 8 | – | 0 | 8 |

## Was NICHT geprüft wurde
- Keine echten externen Dienste: keine Zahlung, keine Domain-Registrierung, kein Veröffentlichen, kein Versand. Die Tests prüfen die Rechnung, die erzeugten Dateien und die Sperren davor.
- 9 Nummern haben nur Core-Tests ohne Oberflächen-Test: BUS-002, 006, 007, 008, 012, 014, 024, 025, 027.
- Kein Lauf auf frischem Windows oder einem zweiten PC.

## Status
Erledigt. Alle Tests grün.
