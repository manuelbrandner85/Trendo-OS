# Bericht T-0746: Live streamen und TV – echte Läufe mit lokalem Teststream

**Branch:** `claude/T-0746-stream-tv` (eigene Kopie von `main` in `%TEMP%\trendo-auftrag-T-0746`, nach dem Push gelöscht)
**Status:** beide Echtläufe auf diesem PC bestanden, Belege unter `reports/T-0746/`. Vier echte Fehler im Sendeweg und zwei kleine im TV-Weg behoben. Kein Volltest (macht die Hauptsitzung).

## Vorhanden auf diesem PC (geprüft, nichts installiert)
- FFmpeg/ffprobe 9.0.2 (PATH, winget-Paket im Benutzerordner) und `%LOCALAPPDATA%\TrendoWerkzeuge\ffmpeg\bin`.
- mpv v0.41 in `%LOCALAPPDATA%\TrendoWerkzeuge\mpv`.
- Alles nur auf 127.0.0.1. Kein externer Dienst, kein Konto, Probe-Schlüssel `trendo-probe-0746` ohne Bedeutung.

## SPEC-15 „Live streamen“
**Was war:** Echtes Senden nie geprüft (audit-2); alle UI-Tests mit ersetztem Prozess (`ICaptureProcessFactory`).

**Echtlauf gefunden und behoben:**
| # | Fehler (Beleg) | Jetzt | Test |
|---|---|---|---|
| 1 | Mit „Kopie auf diesem PC speichern“ startete FFmpeg gar nicht: `Output with label 'vout' does not exist in any defined filter graph, or was already used elsewhere` – derselbe Filterausgang war Stream und Kopie zugeordnet (einfacher Weg und Szene/Tonmischer) | eigener Ausgang je Ziel (`split=3[vout][vcopy][p]`, beim Tonmischer `asplit=2[alive][acopy]`) | Core `Copy_uses_own_outputs` (3 Fälle); Echtlauf mit Kopie |
| 2 | Brach der Empfänger ab, blieb die Anzeige auf „Live“ – die Fehlerzeile `Error muxing a packet … -10054` wurde von den Fortschrittszeilen (2×/s) und den libx264-Statistikzeilen am Ende aus dem 600-Zeichen-Ende verdrängt; bei weiteren Ausgängen lief FFmpeg sogar weiter | Fortschritt wird nur als letzte Zeile gehalten, Verbindungsfehler bleiben vorn im Ende stehen; die Sitzung erkennt den Abbruch auch, solange FFmpeg noch läuft, und beendet diesen Lauf | Core `Dropped_while_process_keeps_running`; Echtlauf |
| 3 | Kein Wiederverbinden | `LiveSession.Tick()`: lief die Sendung ≥ 5 s und brach die Verbindung ab, verbindet sie bis zu 3-mal neu (nach 2, 4, 8 s) – nur zum freigegebenen Ziel derselben Sendung; eigene Kopie je Verbindung (`…-teil2.mp4`), damit `-y` nichts überschreibt; Stopp beendet auch das Warten. Ein Ziel, das von Anfang an nicht antwortet, wird nicht wiederholt | Core `Reconnects_then_gives_up`, `Refused_at_start_no_retry`, `Stop_cancels_retry`; Echtlauf |
| 4 | Status zeigte rohe Zeilen („FFmpeg hat aufgehört. … Error number -10054 occurred“) und nie, wie lange gesendet wird | „Live auf den Server 127.0.0.1. Gesendet: 0:08. Mit Stopp beenden.“, „Verbindung zum Sendeserver abgebrochen. Neuer Versuch in 2 s (1 von 3).“, „Der Sendeserver ist nicht erreichbar …, dann erneut „Live anfragen““; die Rohzeilen stehen in `LiveSession.LastLog`. „Die Kopie … ist gespeichert“ nur, wenn es die Datei wirklich gibt | Core `Stats_from_ffmpeg`, `Status_shows_progress` |

**Beleg (echt):** `reports/T-0746/live-lauf.log`. Ablauf über die Oberfläche: „Eigenes RTMP“, Adresse `rtmp://127.0.0.1:<port>/live`, Schlüssel, Häkchen Kopie → „Live anfragen …“ → „Ja, jetzt senden“ (Sendung läuft nach 0,7 s) → Empfänger 1 beendet nach 8 s → App: „Neuer Versuch in 2 s (1 von 3)“ → nach 2,1 s „Wieder verbunden“ an Empfänger 2 → „Stopp“. Quelle: Bildschirm (gdigrab), 720p30, 2750 kbit/s eingestellt.
- ffprobe Empfang 1: h264 1280×720 30 fps + AAC, 8,02 s, 3 045 kbit/s; Empfang 2: 8,86 s, 2 912 kbit/s.
- ffprobe Kopien der App: `aufnahme-….mp4` 9,23 s, 2 568 kbit/s; `aufnahme-…-teil2.mp4` 8,90 s, 2 611 kbit/s.
- Schlüssel steht in keiner Datei der App, nicht in der Anzeige. Bildschirminhalte wurden nicht abgelegt (nur ffprobe-Werte).
- Laufzeit Klick bis Stopp 21,1 s.

## STUDIO-TV-01 „TV & Live“
**Was war:** Abspielen und Aufnehmen in UI-Tests ersetzt (`TvPlayOverride`, `TvRecordOverride`).

**Echtlauf:** FFmpeg erzeugt einen HLS-Teststream (Testbild 640×360, 440-Hz-Ton) in einen Temp-Ordner; ein kleiner HTTP-Server des Tests (TcpListener, nur 127.0.0.1) liefert ihn und eine `liste.m3u` aus. Über die Oberfläche: „Quellen“ → Name und Adresse → „Hinzufügen & laden“ (✓ 1 Sender) → Klick auf den Sender → „Aufnehmen“ → nach ~10 s „Beenden“.
- Abspielen: der Player der App spielt die Adresse; weil das Testfenster kein echtes Fenster für mpv hat (`--wid`), startet der Test das echte mpv mit genau den Einstellungen der App (`MpvView.Args`, neu herausgelöst) ohne Bildausgabe: Exit 0, `VO: [null] 640x360`, `video=playing`, 5 s abgespielt.
- Aufnahme: Zustand „fertig“; ffprobe: mpegts, h264 640×360 25 fps + AAC 48 kHz, 14,03 s, 963 kbit/s. Datei liegt bei: `reports/T-0746/tv-aufnahme-teststream.ts` (nur Testbild und Sinuston, SHA-256 `63c19017…8a21`).

**Behoben:**
- „Beenden“ einer TV-Aufnahme beendete FFmpeg hart (`Kill`); jetzt „q“ über die Eingabe, damit die Datei sauber abschließt, nach 5 s erst hart. (Kein eigener Rot-Test; im Echtlauf endet die Aufnahme „fertig“ und ffprobe liest sie fehlerfrei.)
- Ohne Programmführer hieß die Aufnahme „Trendo Teststream – Trendo Teststream.ts“; jetzt nur einmal der Sendername. Test `Tv_recording_name_without_guide`.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: **VOLLWERTIG** für diesen Weg – aus leerem Zustand über die Oberfläche eine echte Sendung (Empfänger zeichnet auf) und eine echte TV-Aufnahme.
2. Fremddatei importieren/exportieren, öffnet sich im Fremdprogramm: **TEILWEISE** – Fremdquelle (HLS/M3U von FFmpeg) importiert, Ergebnis (.ts, .flv, .mp4) von ffprobe gelesen; eine Bearbeitung gibt es in diesen Studios nicht.
3. Prozessabbruch ≤ 30 s Verlust: **TEILWEISE** – Verbindungsabbruch: Kopie je Verbindung bleibt erhalten (fragmentiertes MP4), Neuverbinden nach 2 s; Absturz der App selbst nicht geprüft.
4. Claude/KI-Fachaufgabe: **NICHT GETESTET** (nicht Teil dieses Pakets).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `T0746_StreamTv_Echt_Ui` (beide Läufe, Klick → Datei → ffprobe).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **NICHT GETESTET** (keine Bedienelemente geändert oder neu).

## Tests (Zahlen aus der Ausgabe)
| Lauf | Ergebnis |
|---|---|
| Core `T0746_LiveReconnect` + `SPEC15_LiveStream` | 18/18 bestanden |
| Core-Filter `T0746_LiveReconnect|SPEC15_LiveStream|IpTv|LiveShow|Tv` | 59/59 bestanden |
| App `T0746_StreamTv_Echt_Ui` mit `TRENDO_ECHT_STREAM=reports/T-0746` | 2/2 bestanden (76,4 s) |
| App `T0746_StreamTv_Echt_Ui` ohne Variable | 2 übersprungen („Übersprungen – Echtlauf nur mit TRENDO_ECHT_STREAM=<Belegordner>.“) |
| App: AppsAndSetupResumeTests 8, ClaudeLiveRegie_Ui 1, DesignPaket3_Ui 14, FreigabenKernUi 5, LiveStudio_Ui 1, Runde2ToolsUi 3, SPEC15_LiveUi 6, SPEC15_Ui 2, T0203_Programme_Ui 12, T0715_HuelleE3_Ui 5, T0726GitHubTor_Ui 1, T0726Offen_Ui 4, T0728_Bedienung_Ui 7, TvGuide_Ui 1, TvLegal_Ui 1, TvPlus_Ui 1, TvStudioUiTests 5, TvVideos_Ui 1, UpdatesUiTests 4 | alle 0 Fehler |

Rot-Grün: die neuen Core-Tests kompilieren auf dem alten `LiveStream.cs`/`IpTv.cs` nicht (`LiveSession` ohne `Tick`, `CopyFiles`, `LastLog`, `Retrying` …) – rot. Fehler 1 und 2 sind zusätzlich im Echtlauf vor der Behebung belegt (siehe oben, Status-Ausschnitt). UebersichtEinfach_Ui, DeadButtonTests, EveryButton_Alltag nicht gelaufen: keine Bedienelemente dazugekommen.

## Was ersetzt blieb / nicht geprüft
- Im Testfenster läuft kein Zeitgeber: der Test ruft den Takt der App (`LiveTick()`, wie `BcTick`/`TvRecTick`) selbst auf.
- Das Testfenster erkennt nur Programme im eigenen Datenordner; der Test verweist dafür per Junction (ohne Adminrechte) auf die echten FFmpeg/mpv-Ordner. Die Programme selbst sind echt.
- mpv im Fenster der App (`--wid`) nicht geprüft – im Testfenster gibt es kein Fenster-Handle; geprüft ist mpv mit den Einstellungen der App ohne Bildausgabe.
- Senden an echte Dienste (YouTube/Twitch, rtmps) bewusst nicht geprüft. Kamera, Mikrofon, Fenster-Quelle nicht im Echtlauf (nur Bildschirm).
- Zeitangabe „Gesendet“: FFmpeg meldet anfangs `time=N/A`; dann steht nur „Live auf …“. Bitrate zeigt FFmpeg bei mehreren Ausgängen nicht (`bitrate=N/A`), daher ohne kbit/s – die Bitrate steht im Beleg aus ffprobe.
- Geplante TV-Aufnahmen über den 20-s-Zeitgeber, Catch-up und Mehrfachansicht nicht echt geprüft.

## Offene Punkte
- Nach dem Lauf zeigt `netstat` für den HTTP-Port einen Eintrag „ABHÖREN“ mit PID 0 (Rest eines beendeten Prozesses im Netzstapel); eine Verbindung dorthin wird verweigert. Kein eigener Prozess läuft mehr.

## Berührte Dateien
`.gitattributes` · `src/Trendo.Core/W6/LiveStream.cs` · `src/Trendo.Core/Tv/IpTv.cs` · `src/Trendo.App/MainWindow.Live.cs` · `src/Trendo.App/MainWindow.Tv.cs` · `tests/Trendo.Core.Tests/LiveReconnectT0746Tests.cs` · `tests/Trendo.App.Tests/T0746StreamTvEchtUiTests.cs` · `reports/T-0746/*` · `tasks/T-0746-stream-tv*.md`
