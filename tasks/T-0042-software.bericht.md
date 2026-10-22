# T-0042 Software

**Ziel:** Projektvorlage als echte Dateien, Test und lokales Git nur auf Wunsch, kein Push.
**Branch:** cursor/v42-software-fa7f
**Dateien:** `SoftwarePromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 535 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Kein Remote, kein Merge, kein Deployment. Eine Freigabe im Text kann nichts hochladen.
- C# und Node werden angelegt, ihr Build läuft in diesem Auftrag nicht automatisch mit.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Kein Unreal und kein Blender.
