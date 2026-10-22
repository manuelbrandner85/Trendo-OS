# T-0042 Spiele

**Ziel:** Browser-Spiel als echte Dateien, Unreal-Projektdatei ohne Editor, Starten bleibt aus.
**Branch:** cursor/v42-spiele-fa7f
**Dateien:** `GamePromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 540 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Kein Playtest im Unreal-Editor. „Starten: ja“ öffnet weder Browser noch Editor.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Keine installierte Unreal-Engine.
