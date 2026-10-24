# T-0042 Medien

**Ziel:** PNG mit ffmpeg zu JPEG umwandeln und die Datei prüfen. Hochladen bleibt aus.
**Branch:** cursor/v42-medien-fa7f
**Dateien:** `MediaPromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 542 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Ohne ffmpeg wird der Umwandlungstest übersprungen und der Auftrag endet mit „nicht unterstützt“. Hochladen ist nicht angeschlossen.
- Video- und Audio-Aufgaben nutzen denselben ffmpeg-Weg. Der belegte Lauf hier ist ein Bild.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Kein Upload-Dienst.
