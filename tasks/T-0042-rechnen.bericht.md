# T-0042 Rechnen

**Ziel:** Die lokalen Rechen- und Helfer-Studios schreiben eine echte Datei. Die Fräse startet nicht.
**Branch:** cursor/v42-rechnen-fa7f
**Dateien:** `FieldKit.cs`, `MeasurePromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 553 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Musik abspielen, Dateien verschieben und eine Maschinenfahrt sind nicht angeschlossen.
- Der Grundriss liegt im Projekt. Die Raumliste der Seite selbst bleibt unverändert, weil der Auftrag den gespeicherten Wohnungsplan nicht erweitert.
- SPEC-10 zeigt nur eingetippte Werte. Es wird kein Gerät verbunden.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Keine echte Fräse, keine Kartenkachel, kein Messgerät.
