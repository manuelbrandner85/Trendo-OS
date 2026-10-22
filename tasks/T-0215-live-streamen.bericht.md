# T-0215: Live streamen

**Auftrag:** Das Studio soll wirklich streamen: Quellen wählen, Vorschau, lokale Aufnahme, Sendung über OBS oder FFmpeg nur nach Freigabe.
**Branch:** cursor/live-streamen-db3a
**Geänderte Dateien:**
- `src/Trendo.Core/W6/LiveStream.cs`
- `src/Trendo.App/MainWindow.Live.cs`
- `src/Trendo.App/MainWindow.Spec15.cs`
- `src/Trendo.App/MainWindow.axaml.cs` (Freigaben-Seite öffnet nur noch Ordner und https-Seiten, nicht jede Aktion)
- `tests/Trendo.Core.Tests/LiveStreamTests.cs`
- `tests/Trendo.App.Tests/LiveUiTests.cs`
- `docs/build-log/2026-10-22-live-streamen.md`

**Testergebnis** (Release, dieser Rechner, 22.10.2026):
- Trendo.Core.Tests: 448 bestanden, 10 übersprungen, 0 Fehler
- Trendo.App.Tests: 187 bestanden, 0 Fehler
- Trendo.Api.Tests: 14 bestanden, 0 Fehler

**Offene Punkte / Risiken:**
- Auf diesem Rechner keine echte Kamera, kein OBS und kein Lauf ins Netz. Der FFmpeg-Befehl und der OBS-Start sind mit einem Ersatz geprüft.
- Unter Linux misst „Quellen suchen“ Fenster nur, wenn `wmctrl` und `xwininfo` da sind. Ein frei eingetippter Titel reicht dort nicht.
- Die Freigabe-Seite führt Ordner und https-Seiten weiter selbst aus. Sendung und spätere Gerätebefehle starten erst der Knopf im Studio.

**Nicht geprüft:** echter Stream zu YouTube oder Twitch, echtes OBS mit Profil auf einem Windows-PC, Bildschirmgrab auf einem laufenden Desktop.
