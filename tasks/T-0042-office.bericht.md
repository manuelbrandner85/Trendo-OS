# T-0042 Office

**Ziel:** Tabelle mit bekannter Formel, Ergebnis in der Datei und in der Vorschau.
**Branch:** cursor/v42-office-fa7f
**Dateien:** `SheetPromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 525 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

`Tabelle_rechnet_formel` und `Tabelle_teilt_nicht_durch_null` sind grün. 12,50 + 3,50 = 16. Die alte Datei bleibt bei der zweiten Fassung 16.

## Offene Punkte

- Keine Excel-Funktionen außer Grundrechenarten und SUMME in einer Spalte.
- LibreOffice prüft die Datei hier nicht.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
