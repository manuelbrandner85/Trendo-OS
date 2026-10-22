# T-0042 Wissen

**Ziel:** Wissen, Karte, Anweisung, Größe, Sendeablauf, Lizenz und Bildzeit schreiben eine echte Datei. Stream, Download und Scan bleiben aus.
**Branch:** cursor/v42-wissen-fa7f
**Dateien:** `StudyPromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 563 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Die Kartenliste auf der Seite bleibt beim Öffnen stehen. Die Karte liegt in der CSV und im Kartenstapel.
- Stream, Quelle holen und der 3D-Scan sind nicht angeschlossen. Es entsteht keine Punktwolke.
- Die KI wird aus der Anweisung nicht gefragt.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Kein COLMAP, kein Meshroom, kein Livestream, kein Headset.
