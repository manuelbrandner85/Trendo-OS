# T-0042: Verbesserung 42, Phase A+B

**Ziel:** Gemeinsamer Prompt-zu-Auftrag-Kern und ehrliche Abdeckung aller 54 Studios, ohne Fachadapter vorzutäuschen.
**Branch:** cursor/v42-kern-fa7f
**Dateien/Bereich:** `src/Trendo.Core/PromptAction/`, `src/Trendo.App/MainWindow.PromptAction.cs`, eine Zeile in `MainWindow.Sys13.cs`, die 3D-Leiste in `MainWindow.W4Studio3D.cs`, Tests, `docs/improvement-42/`.

## Testergebnis

- `dotnet test tests/Trendo.Api.Tests -c Release`: 15 bestanden, 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release`: 511 bestanden, 10 übersprungen, 0 Fehler.
- `dotnet test tests/Trendo.App.Tests -c Release`: 207 bestanden, 0 Fehler.

## Offene Punkte

- Kein Brief-, Office-, Mail-, Geschäfts-, Software-, 3D-, Spiele- oder Medienadapter. Alle Katalogzeilen sind `not_implemented`.
- PDF, Blender und Unreal sind auf diesem Image nicht ausführbar.
- Verbesserung 42 steht im Auftrag, nicht in `decisions.yaml`. Die Datei bleibt unverändert.
- Desktop, Startmenü, Fenster und Briefstudio-Layout (PR #237) sind nicht angefasst. Die 3D-Leiste sitzt im Rollbereich, weil die Vorschau bei 1366×768 nur 7 px Luft hat.

## Nicht geprüft

- Kein Fenster auf einem echten Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
- Kein Versand, keine Zahlung, kein Git-Push aus einem Auftrag.
- Phase C bis E sind nicht begonnen.
