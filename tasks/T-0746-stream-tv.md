# T-0746: Live streamen und TV – echte Läufe mit lokalem Teststream

**Ziel:** SPEC-15 „Live streamen“ und STUDIO-TV-01 „TV & Live“ einmal echt laufen lassen – nur auf 127.0.0.1 – und gefundene Fehler im Sendeweg beheben (Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 20 und 21).
**Branch:** claude/T-0746-stream-tv
**Dateien/Bereich:** `src/Trendo.Core/W6/LiveStream.cs`, `src/Trendo.Core/Tv/IpTv.cs`, `src/Trendo.App/MainWindow.Live.cs`, `src/Trendo.App/MainWindow.Tv.cs`, neue Tests `tests/Trendo.Core.Tests/LiveReconnectT0746Tests.cs`, `tests/Trendo.App.Tests/T0746StreamTvEchtUiTests.cs`, Belege `reports/T-0746/`, `.gitattributes` (eine Zeile).
**Fertig, wenn:**
- [x] Die App sendet über „Live anfragen“ → „Ja, jetzt senden“ an einen lokalen RTMP-Empfänger (FFmpeg `-listen 1` auf 127.0.0.1), der aufzeichnet; ffprobe liest die Aufzeichnung; Protokoll mit Bitrate und Dauer.
- [x] Fehler im Sendeweg (Abbruch, Wiederverbinden, Statusanzeige) behoben, mit Test.
- [x] Lokaler HLS-Teststream (FFmpeg → Temp-Ordner, HTTP-Server des Tests auf 127.0.0.1): App lädt die Senderliste, spielt ab, nimmt auf; ffprobe liest die Aufnahme.
- [x] Echt-Tests ohne `TRENDO_ECHT_STREAM` mit `Assert.Skip("Übersprungen – …")`.
- [x] Alle eigenen Prozesse beendet, kein eigener Port offen.
- [x] Betroffene Testklassen grün (kein Volltest).
**Nicht tun:** nie an YouTube, Twitch o. ä. senden, kein Konto, kein echter Schlüssel, keine echten Senderstreams aufnehmen, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0746-stream-tv.bericht.md`.
