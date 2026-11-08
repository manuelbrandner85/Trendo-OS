# Echte Läufe: typst, mediamtx, streamlink (Alle Studios, Runde 1–2) – 08.11.2026

Freigabe des Nutzers zum einmaligen Ausführen: „Ja, alle testen“ (08.11.2026).
Downloads nur von den offiziellen GitHub-Releases, jedes Programm in einem eigenen Ordner unter `C:\Users\<user>\AppData\Local\Temp\trendo-tools-b\`.

## Downloads (SHA-256 = Werte im Abhängigkeitskatalog `src/Trendo.Core/Dependencies.cs`)
| Werkzeug | Datei | Bytes | SHA-256 |
|---|---|---|---|
| typst 0.15.1 | typst-x86_64-pc-windows-msvc.zip | 22463684 | 19ce3551153c2fe7ee9fa2f95208310c8f4d3209fedb699e0333faf8913f6736 |
| mediamtx 1.21.3 | mediamtx_v1.21.3_windows_amd64.zip | 28734316 | b2f5246b07cb72961df03b8d5e8cad43dc657bcf3f49a87bb031bdf22721af74 |
| streamlink 8.6.3-1 | streamlink-8.6.3-1-py315-x86_64.zip | 84611513 | 3b1f1470d4b135080a8107ca2917379ca3ff5acadbcdc215568b79eea1450497 |
| assimp 6.0.5 | windows-x64-v6.0.5.zip | 17953058 | 1ab3ac83e75e64bf3fb2b67e43c3b8436b49e25a212aa0c65813415a098d7c41 |

Versionen (Ausgabe der Programme): `typst 0.15.1 (9dfd3a08)`, `v1.21.3`, `streamlink 8.6.3`.
assimp: Die Windows-Zip enthält nur `assimp-vc143-mt.dll` (kein Kommandozeilenprogramm) – Anbindung später direkt an die Bibliothek. MLT 7.42.0: kein offizielles Windows-Paket im Release – NOT RUN.

## typst
- Direkt: deutsches Dokument (Umlaute, €, Tabelle) → PDF 17750 Bytes in 196 ms, Exit 0; PNG geprüft.
- Über den Trendo-Adapter (`TypstDoc.Letter` + `TypstDoc.Compile`, Test `ToolAdaptersRealTests.Typst_real_letter`): Titel und Text mit `# * $ [ ] " _` werden als Text gesetzt (Bild `typst-rechnung.png`, SHA-256 938885f75bcf2a626a67b7412cd3a4a11cb9f169a35384703acc5c02d170f7f8); eine Datei mit `#read("../../geheim.txt")` scheitert wegen `--root` (Projektgrenze).

## mediamtx
- Konfiguration nur 127.0.0.1, ohne API/Metriken/WebRTC/RTMP/SRT. Log: `[RTSP] started with listeners on 127.0.0.1:18554 (TCP/RTSP)`.
- Teststream (FFmpeg-Testbild, H264) per RTSP hinein, ein Bild wieder heraus: `mediamtx-teststream.jpg` (640×360, SHA-256 83c128c8a751b4c72c62a4020fdbb49313bdc84076d5d62d53fb5a9716ead498). Log: `is reading from path 'trendo', with TCP, 1 track (H264)`.
- Über den Trendo-Adapter (`LiveServer.Prepare`, Test `Mediamtx_real_local_only`): RTSP 18554 und HLS 18888 auf 127.0.0.1 erreichbar; Prozess danach beendet (eigene PID).

## streamlink (nur Öffentlich-Rechtliche, kein Login, keine Aufnahme)
| Seite | Ergebnis |
|---|---|
| daserste.de/live | Plugin ard_mediathek, 270p50 … 1080p50 |
| ardmediathek.de/live/… | Plugin ard_mediathek, 270p50 … 1080p50 |
| arte.tv/de/live/ | Plugin artetv, 180p … 720p; `--stream-url` → HLS-Adresse (m3u8) |
| zdf.de/live-tv, 3sat.de/live, phoenix.de/live, tagesschau.de/…/livestreams, kika.de/live | „No plugin can handle URL“ – daher in Trendo nicht freigegeben |

Über den Trendo-Adapter (`LiveResolve.Info` / `StreamUrl`, Test `Streamlink_real_public_live`): arte → Qualitäten inkl. „best“, Stream-Adresse .m3u8. Schalter `--no-config --no-plugin-sideloading` vom Programm angenommen.

## Tests
- `ToolAdaptersTests` (Kern, nachgebildete Läufe): 14 grün.
- `ToolAdaptersRealTests` (echte Programme, nur mit `TRENDO_WERKZEUGE_TEST`): 3 grün; ohne bereitgestellte Programme 3 übersprungen.
- `Runde2ToolsUi` (Oberfläche: Office „Schreiben“, Live streamen „Studio“, TV „Mediatheken“): 3 grün.

## Nicht geprüft
- Einrichtung über den Trendo-Knopf (Download in `%LOCALAPPDATA%\TrendoWerkzeuge`) – nur die Downloads selbst und die Prüfsummen sind belegt.
- Abspielen des aufgelösten Livestreams im Trendo-Player (Oberflächentest bildet die Adresse nach).
- OBS → mediamtx mit echtem OBS.
