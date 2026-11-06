# Paket 01 – Ist-Abgleich Studio 3 (Film, Video, VFX) und Studio 4 (Musik, Audio, Sound)

Stand 06.11.2026. Grundlage: `handoff/paket-01-studios-01-04/studios/S3_*.md`, `S4_*.md` und `qa/TESTFAELLE.csv`.
Status-Wörter wie im Paket: **vorhanden** heißt Code ist da und hat automatische Tests; **TESTED** nur mit Beleg unter `reports/paket01/` (Rohlog, Exitcode, Dateien, SHA-256).
Keine Installation ohne Freigabe. Bereiche 3D/Blender/Unreal/Spiele gehören zu Sitzung B.

## Studio 3 – Film, Video & VFX

| Nr. | Punkt | Was es schon gibt (Datei) | Lücke | Nächster Schritt |
|---|---|---|---|---|
| 01 | AI Film Director | Musikvideo-Drehbuch mit Szenen und Einstellungen (`W2/MusicScript.cs`, `MainWindow.MusikDrehbuch.cs`) | nur für Musikvideos, kein Film-Drehbuch mit Shot-Plan im Videoschnitt | später: Shot-Plan im Videoschnitt aus Szenenliste |
| 02 | Professional Video Editor | Zeitleiste mit Bild-, Overlay- und Musikspur, Übergänge, Undo (`VideoEdit.cs`, `VideoPro.cs`); **EDL/OTIO/FCPXML** (`VideoExport.Edl/Fcpxml`, `FilmDelivery.Otio`) | Multicam, Proxy-Dateien, Keyframes je Clip | Proxy-Dateien als eigener Schritt |
| 03 | Storyboard & Previs | Storyboard im KI-Musikvideo (`MusicVideoRender.cs`, `MainWindow.MusikVideoKi.cs`) | keine Scene-/Shot-IDs im Videoschnitt | mit 01 zusammen |
| 04 | Cinematic Camera Director | Kamerafahrten (Zoom/Schwenk) im Musikvideo-Render | keine virtuellen Objektive/Tiefenschärfe | 3D-Kamera: Sitzung B (Blender/Unreal) |
| 05 | VFX Compositing | zweite Bildspur mit Logo/Bild, Ecke, Größe, Deckkraft (`VideoPro.Overlays`) | Greenscreen, Masken, Tracking, Nodegraph | **Greenscreen (chromakey) auf der Overlay-Spur** |
| 06 | Effects & Simulation | – | Blender/Unreal | Sitzung B |
| 07 | Virtual Production | – | Unreal | Sitzung B |
| 08 | Digital Actors | – | Blender, Einwilligung | Sitzung B |
| 09 | Lip Sync | Prüfung „Lippen“ im KI-Musikvideo (`MainWindow.MusikVideoKi.cs`) | keine Visem-Spur | offen, braucht Modell → Freigabe |
| 10 | Animation Studio | – | Blender | Sitzung B |
| 11 | Color Grading | Looks und Farbregler je Clip (`VideoPro.Looks`, `ColorFilter`), LUT im Musikvideo (`MusicVideo.cs`, lut3d) | LUT je Clip im Videoschnitt, Scopes, OCIO/ACES | **Scopes (Waveform, Vektorskop) + LUT (.cube) je Clip** |
| 12 | Video Enhancement | – | Stabilisierung, Entrauschen, Schärfen, Hochskalieren, Qualitätswerte | **Verbessern mit FFmpeg (deshake/vidstab, hqdn3d, unsharp, lanczos) + PSNR/SSIM-Vergleich** |
| 13 | Generative Video | KI-Musikvideo mit Herkunft (`MainWindow.MusikVideoKi.cs`) | Video aus Text im Videoschnitt | nur mit vorhandenem Modell, sonst Freigabe |
| 14 | Sound Design | Ducking (sidechaincompress) im Export, Lautheit angleichen (`VideoEdit.cs`); **Stems Dialog/Musik** (`FilmDelivery`) | Effekt-Stem, Foley | Effekt-Spur zusammen mit S4-17 |
| 15 | Film Music Integration | Musikspur mit Einsatz und Pegel, Lied ↔ Video verknüpft (`VideoEdit.Song`); **Cue-Liste, Musik in EDL/OTIO** (`FilmDelivery`) | – | **TESTED** (Beleg `reports/paket01/s3-film-auslieferung-lauf.md`) |
| 16 | Dubbing & Multilingual | Untertitel als SRT, Whisper-Transkript (`MainWindow.VideoExportSettings.cs`) | VTT, Sprachfassungen IT/EN | VTT-Ausgabe neben SRT |
| 17 | Motion Graphics & Titles | Titel je Clip mit Lage (`VideoPro.TitleY`) | Abspann, animierte Vorlagen | Abspann-Vorlage |
| 18 | Rendering & Export | Export-Voreinstellungen, Hintergrund-Export mit Abbrechen, GPU-Encoder (`VideoExport.cs`); **Auslieferung mit Codecinfo und SHA-256** | Warteschlange, Bildsequenzen, ProRes, EXR | **ProRes (prores_ks) und PNG-Bildsequenz als Voreinstellung, Warteschlange** |
| 19 | Autonomous Production | Claude im Programm (`MainWindow.ClaudeOperate.cs`) | Ablauf über mehrere Studios | später, mit Sitzung B abstimmen |
| 20 | Quality & Release Director | **Prüfbericht: fehlende Medien, Schwarzbild, Stem-Synchronität, Lautheit, True Peak** (`FilmDelivery.Qc`) | Codec-/Lizenzprüfung | **TESTED** für die genannten Prüfungen (Beleg wie 15) |

## Studio 4 – Musik, Audio & Sound

| Nr. | Punkt | Was es schon gibt (Datei) | Lücke | Nächster Schritt |
|---|---|---|---|---|
| 01 | DAW Studio | Transportleiste, Arrangement (`MainWindow.MusikDaw.cs`), Mehrspur-Mischpult mit Mute/Solo/Pan (`AudioMulti.cs`), Clip-Spuren (`ClipLane.cs`) | Aufnahme in der Mehrspur, Automation | Automation (Lautstärke-Kurve je Spur) |
| 02 | AI Music Producer | Claude plant Songstruktur, Fassungen (`SongProducer.cs`, `MainWindow.ClaudeMusik.cs`, `MainWindow.MusikFassungen.cs`) | – | vorhanden |
| 03 | AI Music Generator | ACE-Step 1.5 lokal (`MainWindow.MusikStudio.cs`) | – | vorhanden (Modell nur nach Nutzer-Ja) |
| 04 | MIDI & Composition | MIDI-Datei aus Melodie und Akkorden (`MusicPro.Midi`, `MainWindow.MusikProWerkzeuge.cs`) | Piano-Roll, Quantisierung | **Piano-Roll mit Quantisieren und MIDI-Export** |
| 05 | Virtual Instruments | Klick-Spur, Loops (`MusicPro.ClickTrack`, `MakeLoop`) | Synth, Sampler, Drumcomputer | mit 13: eingebauter Drum-Synth |
| 06 | Plugin Host | – | VST3/CLAP | **nur Plugin-Scan ohne Laden**; Host erst nach REA-/Sandbox-Bewertung und Freigabe |
| 07 | Vocal Studio | eigene Stimme mit Einwilligung (`MainWindow.MusikEigeneStimme.cs`), Mitsingen (`SingAlong.cs`) | Harmonien, Intonation | später |
| 08 | Stem Separation | Demucs lokal (`W2/KiLibrary.cs`, `MainWindow.MusikSpuren.cs`) | – | vorhanden (Paket nur nach Nutzer-Ja) |
| 09 | Mixing Console | Pegel, Pan, Mute, Solo (`AudioMulti.cs`) | EQ/Kompressor je Spur, Busse, Sends | EQ und Kompressor je Spur |
| 10 | Mastering Director | Mastering mit Stilen und Zielen, LUFS/True Peak, Referenz-EQ (`MusicPro.MasterArgs`, `MatchEq`, `SongMaster`) | – | vorhanden |
| 11 | Spectral Restoration | Spektralansicht und spektrale Bearbeitung, Brumm/Knack (`AudioDsp.SpectralGain`, `AudioEdit`) | – | vorhanden |
| 12 | Time & Pitch | Tempo/Tonhöhe ändern (atempo, `MusicPro.PracticeArgs`, `AudioEdit`) | Intonation, Transienten | später |
| 13 | Electronic Production | Genres mit Arpeggio-Beschreibung (`MusicGenres.cs`) | Step-Sequencer, Arpeggiator | **Step-Sequencer mit eingebautem Drum-Synth → WAV** |
| 14 | Orchestral Scoring | Cue-Liste aus dem Film (`FilmDelivery.CueSheet`) | Tempo-Map, Leitmotive | Tempo-Map-Export (CSV/MIDI) |
| 15 | Adaptive Game Music | – | Event-Map für Godot/Unreal/Unity | mit Sitzung B (Spiele) abstimmen |
| 16 | Spatial Audio | – | Mehrkanal, binaural | **5.1-Export und Kopfhörer-Vorschau mit FFmpeg** |
| 17 | Foley & SFX | – | Geräuschbibliothek | **Geräusch-Werkstatt (Regen, Wind, Schritte) mit Tags, ohne Download** |
| 18 | Lyrics & Multilingual | Silben, Reimhilfe, Liedzeilen-Timing (`LyricTiming.cs`, `MainWindow.MusikTools.cs`) | Übersetzung IT/EN, Karaoke-Export | Karaoke (LRC) und Übersetzung mit Claude; Texte nur vom Nutzer |
| 19 | Audio-to-MIDI | Melodie-Erkennung → MIDI (`MsMidi`), Whisper-Transkript (`MainWindow.MusikVerstehen.cs`) | Unsicherheitsbericht | Unsicherheit je Note ausgeben |
| 20 | Audio QA & Export | Lautheit, Spitze im Audioeditor (`MainWindow.AudioEditor.cs`) | Prüfbericht für Audio: Clipping, Kanalzahl, defekte WAV | **Audio-Prüfbericht wie FilmDelivery.Qc** |

## Reihenfolge der nächsten Schritte (nur eigene Bereiche, ohne Installation)

1. S3-12 Video verbessern + Qualitätswerte
2. S3-05 Greenscreen auf der Overlay-Spur
3. S3-11 Scopes und LUT je Clip
4. S3-18 ProRes, Bildsequenz, Warteschlange
5. S4-20 Audio-Prüfbericht
6. S4-13 Step-Sequencer mit Drum-Synth
7. S4-17 Geräusch-Werkstatt
8. S4-16 5.1-Export und Kopfhörer-Vorschau
9. S4-04 Piano-Roll mit Quantisieren
10. S4-06 Plugin-Scan (ohne Laden)

Jeder Schritt: Positiv- und Negativtest aus `qa/TESTFAELLE.csv`, Beleg mit echtem FFmpeg unter `reports/paket01/`.
Nicht in diesem Paketteil: S3-06/07/08/10, S3-04 (3D-Kamera), S4-15 (Spiele) – Sitzung B; S3-09/S3-13 brauchen Modelle (Freigabe).

## Umgesetzt (Stand 07.11.2026)

| Punkt | Was neu ist | Beleg | Status |
|---|---|---|---|
| S3-02, S3-14, S3-15, S3-18, S3-20 | „Film ausliefern“: Film, Stems Dialog/Musik (24 Bit), EDL (eine Umsetzung mit Musik auf A2), OTIO, Cue-Liste, Prüfbericht mit SHA-256; Bildrate 25 fps wurde vorher als 30 gelesen (behoben) | `reports/paket01/s3-film-auslieferung-lauf.md` | TESTED (positiv + negativ) |
| S3-05 | Greenscreen (Grün/Blau) und Maske auf der zweiten Bildspur; Maske außerhalb des Bildes abgelehnt | `reports/paket01/s3-05-greenscreen-lauf.md` | TESTED; Rotoskopie/Tracking offen |
| S3-11 | LUT (.cube) je Stück, Scopes (Bild · Waveform · Vektorskop), Farbvergleich, bildgenaue LUT-Prüfung (SSIM/PSNR) | `reports/paket01/s3-11-farbe-lut-scopes-lauf.md` | TESTED; OCIO/ACES offen (nicht installiert) |
| S3-12 | Bild verbessern (stabilisieren, entrauschen, schärfen, 2×) mit Messung; Artefakte beim Hochskalieren verworfen | `reports/paket01/s3-12-bild-verbessern-lauf.md` | TESTED |
| S3-18 | ProRes 422 HQ, EXR- und PNG-Sequenz mit WAV; fehlender Encoder → Alternative | `reports/paket01/s3-18-profi-export-lauf.md` | TESTED; Render-Warteschlange offen |
| S4-04 | Piano-Roll (Modus „Noten“), Quantisieren, Transponieren, MIDI speichern/öffnen byte-gleich, kaputte MIDI → Warnung | `reports/paket01/s4-04-piano-roll-midi-lauf.md` | TESTED |
| S4-06 | Plugin-Prüfung ohne Laden (VST3/CLAP, Architektur) | `reports/paket01/s4-06-plugin-pruefung-lauf.md` | Teil; Host BLOCKIERT (Sandbox + REA + Freigabe nötig) |
| S4-13 | Step-Sequencer (Modus „Beat“) mit Drum-Synth, Arpeggiator, Automation; Endlosschleife abgefangen | `reports/paket01/s4-13-step-sequencer-lauf.md` | TESTED |
| S4-16 | Raum-Winkel je Spur, 5.1-Abmischung (Kanalmaske 5.1(side)), Kanalplan, Kopfhörer-Vorschau | `reports/paket01/s4-16-raumklang-lauf.md` | TESTED; Hörtest durch Menschen offen |
| S4-17 | Geräusch-Werkstatt (selbst erzeugt), Bibliothek mit Ereignis-Tags, Paket nur mit gültiger Lizenz | `reports/paket01/s4-17-geraeusch-werkstatt-lauf.md` | TESTED |
| S4-20 | Audio prüfen und freigeben (BS.1770, 4×-True-Peak, Clipping, Format, Phase, Lizenz, Einwilligung) | `reports/paket01/s4-20-audio-pruefung-lauf.md` | TESTED (gegen FFmpeg ebur128 ±0,05) |

Weiter offen in S3/S4 (eigene Bereiche): S3-01/03 Shot-Plan im Videoschnitt, S3-02 Proxy/Multicam, S3-16 VTT und Sprachfassungen, S3-17 Abspann-Vorlage, S3-18 Warteschlange, S4-01 Automation in der Mehrspur, S4-09 EQ/Kompressor je Spur, S4-12 Intonation, S4-14 Tempo-Map, S4-18 Karaoke (LRC), S4-19 Unsicherheitsbericht.
Nicht in diesem Teil: S3-04/06/07/08/10, S4-15 (Sitzung B); S3-09, S3-13 brauchen Modelle (Freigabe).
