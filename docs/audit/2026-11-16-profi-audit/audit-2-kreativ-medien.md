# Audit Gruppe „Kreativ & Medien“

Stand der Arbeitskopie: `studios-aufraeumen` (ba52927a8). Nur gelesen, nichts gebaut, keine Tests gestartet. Pfade relativ zum Repo. `App/` = `src/Trendo.App/`, `Core/` = `src/Trendo.Core/`.

Urteile: VOLLWERTIG / TEILWEISE / FEHLT / BLOCKIERT / NICHT GETESTET. „n. z.“ = Kriterium passt nicht zur Programmart (kein Dokument-Editor).

## Gemeinsame Belege (gelten für alle Programme mit Programm-Hülle)

Programme mit Hülle (`Shell: True` in `docs/programme/katalog.json`): schnitt, ton, bild, mode-programm, kuenstler-programm, animation-programm, buch-programm, comic-programm, film-programm, logo-programm, podcast-programm, fotos-verwalten-programm. Ohne Hülle: kreativ-medien, assets, sendung, musik-studio, tv.

| Kürzel | Was | Beleg |
|---|---|---|
| H-Neu | Startseite mit Vorlagen, Neu, Zuletzt, Wiederherstellen | `App/MainWindow.ProgramShell.cs:15-17`, `:334` |
| H-Öffnen | `.tdoc` laden, Zuletzt-Liste | `Core/Programs/ProgramDocument.cs:202`, `:239-244`; Test `ProgramShellUiTests.cs:15` |
| H-Undo | Rückgängig/Wiederholen, 100 Schritte, Schnappschuss des ganzen JSON je Schritt | `Core/Programs/ProgramDocument.cs:38`, `:75-98`; Strg+Z/Y `App/MainWindow.ProgramShell.cs:350-351` |
| H-Save | Autosave alle 30 s, 1,5 s nach Änderung, beim Schließen; atomar (tmp + Move); bis 20 Versionen | `App/MainWindow.ProgramShell.cs:85-88`, `:228`; `Core/Programs/ProgramDocument.cs:139-148`, `:151` |
| H-Crash | Absturz-Sicherung `.autosave` bzw. Sicherungsordner, Karte „Wiederherstellen“ | `Core/Programs/ProgramDocument.cs:215-236`; `App/MainWindow.ProgramShell.cs:520-526`; Test `ProgramShellUiTests.cs:56` |
| H-Bedien | Strg+S/Z/Y/N/W; F11 Fokusmodus; Bedienstufen Einfach/Erweitert/Profi; Rechtsklick nur „Claude fragen“; Dateien aus dem Explorer landen nur in Pfad-Textfeldern, nicht auf Arbeitsflächen | `App/MainWindow.ProgramShell.cs:343-354`, `:19`, `:50-52`; `App/MainWindow.ProgramFrame.cs:217`; `App/MainWindow.ClaudeOperate.cs:206`; `App/MainWindow.Files.cs:34-39`, `:155-170` |
| K-Claude | Claude Code (Abo) arbeitet im Projektordner, Trendo spielt Änderungen live ins Dokument, „Behalten/Verwerfen“; Claude bedient Felder und Knöpfe der Seite | `App/MainWindow.ClaudeProgramDoc.cs:7`, `App/MainWindow.ClaudeLive.cs:9`, `App/MainWindow.ClaudeOperate.cs:14`, `App/MainWindow.ClaudePrograms.cs:99-129` |
| K-Ersatz | In allen Tests ist Claude ersetzt → Claude-Weg Ende-zu-Ende NICHT GETESTET | `App/MainWindow.Claude.cs:46`, `:110` (`ClaudeRunOverride`); unter `reports/claude/` liegt nur ein Migrationsaudit |
| K-Plan | Zweiter KI-Weg „Prompt-Aktion“ (Trendo plant selbst): je Studio eine Aktion, alle Stufe Partial oder Blocked | `Core/PromptAction/CapabilityCatalog.cs:36-49` |

Hinweis FFmpeg: Echtlauf-Tests brechen ohne FFmpeg mit `return` ab und gelten dann als **bestanden**, nicht als übersprungen (z. B. `tests/Trendo.Core.Tests/VideoEditTests.cs:56`). Auf diesem PC liegt FFmpeg im PATH, hier laufen sie also wirklich. Auf einem PC ohne FFmpeg wäre der grüne Test wertlos.

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| CORE-08 | Videos & Fotos bearbeiten | Videoschnitt (`schnitt`) | TEILWEISE (stark) | Vorschau zeigt Effekte, Übergänge, Einblendungen, Keyframes nicht – nur der Export | groß |
| CORE-08 | | Bildbearbeitung (`bild`) | TEILWEISE (stark) | Export nur PNG/JPG/SVG, kein PSD/TIFF/WebP; keine Werkzeug-Tastenkürzel | mittel |
| CORE-08 | | Audio bearbeiten (`ton`) | TEILWEISE (stark) | Mehrspur nur eine Datei je Spur, Aufnahme fest 10 s, Effekte mit festen Werten | mittel |
| CORE-08 | | Medien bearbeiten (`kreativ-medien`) | TEILWEISE | Umwandler mit 7 festen Aufgaben, kein Editor | klein |
| X-07 (in CORE-08) | Freie Bilder & Klänge | `assets` | TEILWEISE | KI-Aktion darf nichts laden (Blocked) | klein |
| SPEC-08 | Kleidung & Produkte entwerfen | `mode-programm` | TEILWEISE | Schnitt nur aus Vorlagen berechnet, kein freies Schnittzeichnen | groß |
| SPEC-15 | Live streamen | `sendung` | TEILWEISE | echtes Senden an eine Plattform nie geprüft | mittel |
| STUDIO-ANIM-01 | Animation & Motion | `animation-programm` | TEILWEISE (schwach) | Schlüsselbilder über Textfelder, nur Rechteck/Kreis/Text, keine Bühne zum Anfassen | groß |
| STUDIO-ART-01 | Künstlerstudio | `kuenstler-programm` | TEILWEISE (stark) | keine Auswahl-/Transformier-/Textwerkzeuge; PSD nur am Dateikopf geprüft | mittel |
| STUDIO-BOOK-01 | Bücher schreiben | `buch-programm` | TEILWEISE | ein Markdown-Textfeld; kein Satz, kein PDF/DOCX, kein Import | mittel |
| STUDIO-COMIC-01 | Comics & Storyboards | `comic-programm` | TEILWEISE (schwach) | Formular: feste Aufteilungen, Textfelder, Bild einsetzen – kein Zeichnen, keine frei gesetzten Sprechblasen | groß |
| STUDIO-FILM-01 | Filme machen | `film-programm` | FEHLT (als Filmprogramm) | Es entsteht kein Film – nur Drehbuch, Figurenliste, Shotliste | sehr groß |
| STUDIO-LOGO-01 | Logos & Marken | `logo-programm` | TEILWEISE (schwach) | Generator-Formular mit 5 Formen, kein Vektor-Editor | groß |
| STUDIO-MUSIK-01 | Trendo Musik | `musik-studio` | TEILWEISE (stark) | Lied-Erzeugung in den Tests ersetzt; Echtlauf nur auf Anforderung | mittel |
| STUDIO-PHOTOS-01 | Fotos verwalten | `fotos-verwalten-programm` | TEILWEISE | Hilfsprogramm mit Ordnerwahl, keine Bildansicht im Programm | klein |
| STUDIO-PODCAST-01 | Podcast aufnehmen | `podcast-programm` | TEILWEISE (schwach) | Nimmt nicht auf – Knopf springt ins Live-Studio; kein Schnitt | groß |
| STUDIO-TV-01 | TV & Live | `tv` | TEILWEISE | Abspielen und Aufnehmen in allen UI-Tests ersetzt | klein |

Zahl je Gesamturteil (17 Programme): VOLLWERTIG 0 · TEILWEISE 16 (davon stark 5, schwach 4) · FEHLT 1 · BLOCKIERT 0.

---

## CORE-08 Videoschnitt (`schnitt`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Eigene Zeitleiste mit Lineal, Bild-/Musikspur, Ziehen, Kanten kürzen: `App/EditTimeline.cs:11-14`, `:107-137`; eingebunden `App/MainWindow.VideoSchnitt.cs:218` |
| 2 | Werkzeuge | TEILWEISE | Teilen, Löschen, Slip, Rolling, Einrasten, Ripple, Spuren: `App/MainWindow.VideoSchnitt.cs:43-69`. Hauptbildspur ist lückenlos („magnetisch“), Videoclips lassen sich dort nicht frei platzieren: `Core/Media/VideoEdit.cs:6-7`. Multicam über Texteingabe „Sekunde:Kamera“: `App/MainWindow.VideoMulticam.cs:10` |
| 3 | Neues Projekt | VOLLWERTIG | 3 Vorlagen (HD, Full HD, Hochformat): `App/MainWindow.VideoSchnitt.cs:34-39` |
| 4 | Dateien importieren | VOLLWERTIG | MP4/MKV/MOV/AVI/WEBM, Ton, Bilder; Länge und Tonspur über ffprobe: `App/MainWindow.VideoSchnitt.cs:82-100`, `Core/MediaStudio.cs:21` |
| 5 | Projekte bearbeiten | VOLLWERTIG | H-Öffnen; fehlende Dateien neu verknüpfen: `Core/Media/VideoEdit.cs:228-244` |
| 6 | Rückgängig | VOLLWERTIG | H-Undo; alle Schritte über `d.Edit`: `App/MainWindow.VideoSchnitt.cs:43`, `:98` |
| 7 | Vorschau | TEILWEISE | Flüssig 640×360 mit Ton über mpv: `App/MainWindow.VideoPreview.cs:13-17`, `:69`. Zeigt nur das rohe Stück; Farbe, Übergänge, Einblendungen, Keyframes, Effekte fehlen in der Vorschau (`App/MainWindow.VideoPreview.cs:69` nimmt die Quelldatei direkt) – so auch im eigenen Abnahmedokument: `docs/STUDIO_REBUILD_4_0/pilot/VIDEOSCHNITT_ABNAHME_4.0.md` Abschnitt „Nicht geprüft“. Anderes Tempo: 6 Bilder/s, 320 px: `App/MainWindow.VideoSchnitt.cs:449`, `Core/Media/VideoEdit.cs:292-301` |
| 8 | Speichern/Autosave | VOLLWERTIG | H-Save |
| 9 | Absturz | VOLLWERTIG | H-Crash |
| 10 | Export | VOLLWERTIG | H.264, HEVC, AV1, ProRes 422 HQ, PNG/EXR-Sequenz, nur Ton; NVENC; Bereich, Lautheit, Ducking: `Core/Media/VideoExport.cs:19-33`, `:77-80`; „Film ausliefern“ mit Stems, EDL, OTIO: `App/MainWindow.FilmDelivery.cs:8`, `:18-36` |
| 11 | Rundreise | TEILWEISE | Exportiertes MP4 wird mit ffprobe geprüft (Länge, Ton): `tests/Trendo.Core.Tests/VideoEditTests.cs:53-74`. Schnittlisten (EDL/OTIO/FCPXML) werden nur geschrieben – ein Import wurde im Kern nicht gefunden (Suche nach `FromEdl/ParseEdl/FromOtio` in `Core/Media/` ohne Treffer). MLT-Export existiert nur im Kern, in der App nicht angeschlossen (kein `MltProject.` in `src/Trendo.App`), siehe `reports/alle-studios/mlt-e2e.md:23-24` |
| 12 | Fachfunktion real | VOLLWERTIG | Echter FFmpeg-Graph mit xfade, acrossfade, drawtext, amix, Sidechain: `Core/Media/VideoEdit.cs:148-225`; Lauf `:247-256`, Hintergrund mit Fortschritt und Abbruch `:259-289` |
| 13 | Claude/KI | TEILWEISE | K-Claude (Dokument `.tdoc` live). Prompt-Aktion nur „Datei umwandeln“ (`medien.umwandeln`, Partial) und „Hochladen“ Blocked: `Core/PromptAction/MediaPromptAdapter.cs:7-10`, `:33-36`. Eine KI-Aktion, die schneidet, gibt es nicht. K-Ersatz |
| 14 | Tests | TEILWEISE | Kern echt mit FFmpeg: `VideoEditTests.cs:53`, `VideoAnimTests.cs:11`, `VideoExportTests.cs:63`, `ProExportTests.cs:47`, `PreviewStreamTests.cs:11`. UI-Tests ersetzen Export, Vorschau, Ton, Encoder: `App/MainWindow.VideoSchnitt.cs:134`, `:447`, `:542`, `:545`; `App/MainWindow.VideoPreview.cs:19-23`. Klick-bis-Datei über die Oberfläche: NICHT GETESTET |
| 15 | Bedienung | TEILWEISE | S, Entf, Leertaste, Pfeile, J/K/L, I/O, M, Pos1/Ende: `App/MainWindow.VideoSchnitt.cs:242-253`, `:474-501`; Rechtsklick-Menü auf Stücken `:277`; H-Bedien. Dateien direkt auf die Zeitleiste ziehen: nicht gefunden (H-Bedien, nur Pfadfelder) |

**Gesamturteil:** TEILWEISE (stark) – der Export ist echt und breit, aber man sieht vor dem Rendern nicht, was man gebaut hat.

**Was fehlt (priorisiert):**
1. Vorschau, die das Ergebnis zeigt: Farbe, Übergänge, Einblendungen, Keyframes, Titel in echter Lage.
2. Freie Platzierung auf der Hauptbildspur (Lücken, Überlagern) statt nur Reihenfolge.
3. Dateien aus dem Explorer auf die Zeitleiste ziehen.
4. Schnittlisten wieder einlesen (EDL/OTIO) und MLT-Export in der Oberfläche anschließen.
5. Ein UI-Test mit echtem FFmpeg vom Klick bis zur geprüften Datei.
6. Multicam mit Bildansicht statt Texteingabe.

**Abnahmekriterien:**
- Ein Stück mit Schwarzweiß-Look, Überblendung und Logo sieht in der Vorschau an drei Zeitpunkten so aus wie im exportierten Bild (Bildvergleich mit Toleranz).
- Ein Videoclip lässt sich mit der Maus an eine freie Stelle der Bildspur legen; die Lücke erscheint im Export als Schwarzbild.
- Eine aus dem Explorer gezogene MP4 liegt danach als Stück an der Abwurfstelle.
- UI-Test ohne `ExportAsyncOverride`: Projekt mit zwei Stücken und Musik → MP4, ffprobe bestätigt Länge ±0,2 s, Bild- und Tonspur.
- Ein von Trendo geschriebenes OTIO lässt sich wieder öffnen und ergibt dieselben Schnittpunkte.

**Aufwand:** groß. **Risiko:** mittel – die Vorschau muss denselben Filtergraphen wie der Export nutzen, sonst laufen beide auseinander; Leistung bei 4K.

**Dateien:** `App/MainWindow.VideoSchnitt.cs`, `App/MainWindow.VideoPreview.cs`, `App/EditTimeline.cs`, `App/MainWindow.VideoSchnittPro.cs`, `App/MainWindow.VideoKeys.cs`, `App/MainWindow.VideoMixer.cs`, `App/MainWindow.VideoExportSettings.cs`, `Core/Media/VideoEdit.cs`, `VideoExport.cs`, `VideoPro.cs`, `VideoAnim.cs`, `VideoLanes.cs`, `MltProject.cs`, `FilmDelivery.cs`.

---

## CORE-08 Bildbearbeitung (`bild`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Leinwand mit Ebenen, 12 Werkzeuge links: `App/MainWindow.Bildbearbeitung.cs:25-36`, `:100`; `App/ImageCanvas.cs` |
| 2 | Werkzeuge | TEILWEISE | Verschieben, Pinsel, Radierer, Text, Formen, Zuschneiden, Vektorpinsel, Auswahl (Rechteck/Ellipse/Lasso/Zauberstab), Verlauf, Füllen, Reparieren, Klonen: ebd.; Tests `BildbearbeitungUiTests.cs:23`, `:112`, `:169`. Offen laut eigener Liste: Auswahl hinzufügen/abziehen, generative Füllung: `docs/claude/vorbilder/bild-gimp-krita-photoshop.md:27`, `:33-34` |
| 3 | Neues Projekt | VOLLWERTIG | 4 Vorlagen: `App/MainWindow.Bildbearbeitung.cs:76-79` |
| 4 | Import | TEILWEISE | Bild als Ebene, SVG als bearbeitbare Ebenen: `App/MainWindow.Bildbearbeitung.cs:115`, `:127`. PSD/TIFF/RAW öffnen: nicht gefunden |
| 5 | Projekte bearbeiten | VOLLWERTIG | H-Öffnen |
| 6 | Rückgängig | VOLLWERTIG | H-Undo; Test `BildbearbeitungUiTests.cs:23` |
| 7 | Vorschau | VOLLWERTIG | Leinwand ist das Ergebnis (gleicher Renderer für Export: `App/ImageRender.cs`, `App/MainWindow.Bildbearbeitung.cs:96`) |
| 8 | Speichern | VOLLWERTIG | H-Save |
| 9 | Absturz | VOLLWERTIG | H-Crash |
| 10 | Export | TEILWEISE | Nur PNG, JPG, SVG: `App/MainWindow.Bildbearbeitung.cs:94-98`. Kein PSD, TIFF, WebP, PDF, kein Farbprofil |
| 11 | Rundreise | TEILWEISE | SVG speichern und wieder öffnen getestet: `BildbearbeitungUiTests.cs:84`. PNG/JPG kommen nur als flache Ebene zurück |
| 12 | Fachfunktion real | VOLLWERTIG | Eigene Bildrechnung (Filter, Tonwerte, Kurven, Masken, inhaltsbasiertes Füllen): `Core/Media/ImageOps.cs`, `ImageDoc.cs`, `ImageVector.cs`; Tests `ImageOpsTests.cs`, `ImageVectorTests.cs` |
| 13 | Claude/KI | TEILWEISE | K-Claude. Eigene Prompt-Aktion für die Bildbearbeitung gibt es nicht (CORE-08 hat nur Umwandeln: `Core/PromptAction/MediaPromptAdapter.cs:7`). K-Ersatz |
| 14 | Tests | VOLLWERTIG | Kein Fremdwerkzeug nötig; UI-Tests malen Pixel und prüfen sie: `BildbearbeitungUiTests.cs:23-169` |
| 15 | Bedienung | TEILWEISE | H-Bedien. Werkzeug-Tastenkürzel (B, E, V …) und eigenes Rechtsklick-Menü in `MainWindow.Bildbearbeitung.cs` und `ImageCanvas.cs` nicht gefunden |

**Gesamturteil:** TEILWEISE (stark) – echtes Bildprogramm mit Ebenen, aber ohne Profi-Austauschformate.

**Was fehlt:** 1. PSD/TIFF/WebP lesen und schreiben (PSD-Schreiber gibt es schon im Künstlerstudio: `App/MainWindow.Kuenstlerstudio.cs:96`). 2. Werkzeug-Tastenkürzel und Rechtsklick-Menü. 3. Auswahl hinzufügen/abziehen. 4. Explorer-Drop auf die Leinwand. 5. Generative Füllung mit lokaler Bild-KI.

**Abnahmekriterien:** Ein Dokument mit drei Ebenen wird als PSD gespeichert, in GIMP oder Krita geöffnet und zeigt drei Ebenen mit Namen. · Taste B wählt den Pinsel, E den Radierer. · Ein PNG aus dem Explorer auf die Leinwand gezogen wird eine neue Ebene.

**Aufwand:** mittel. **Risiko:** gering. **Dateien:** `App/MainWindow.Bildbearbeitung.cs`, `App/ImageCanvas.cs`, `App/ImageRender.cs`, `App/ImageTools.cs`, `Core/Media/ImageDoc.cs`, `ImageOps.cs`, `ImageVector.cs`.

---

## CORE-08 Audio bearbeiten (`ton`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Wellenform mit Maus-Auswahl, Spektrogramm: `App/WaveView.cs`, `App/MainWindow.AudioEditor.cs:14-16`, `:90`; Test `AudioEditorUiTests.cs:33` |
| 2 | Werkzeuge | TEILWEISE | Schneiden, Blenden, Normalisieren, Rauschprofil, Brummen, Kompressor, spektral absenken: `App/MainWindow.AudioEditor.cs:49-98`. Menüband-Befehle haben feste Werte (±3 dB, Echo 0,3, Tempo ±10 %): `:60-72`; Regler dafür in `MainWindow.AudioEditor.cs` nicht gefunden (kein `Slider`) |
| 3 | Neues Projekt | VOLLWERTIG | Datei, leere Spur, Mehrspur: `App/MainWindow.AudioEditor.cs:42-44` |
| 4 | Import | VOLLWERTIG | WAV/MP3 direkt, übrige über FFmpeg: `App/MainWindow.AudioEditor.cs:155` |
| 5 | Projekte bearbeiten | VOLLWERTIG | H-Öffnen; nicht-destruktive Schrittliste: `Core/Media/AudioEdit.cs` |
| 6 | Rückgängig | VOLLWERTIG | H-Undo; Test `AudioEditorUiTests.cs:33` |
| 7 | Vorschau | VOLLWERTIG | Abspielen von Auswahl oder ab Marke: `App/MainWindow.AudioEditor.cs:217-220`; echter mpv-Test `tests/Trendo.App.Tests/MpvWindowTests.cs:10` |
| 8 | Speichern | VOLLWERTIG | H-Save |
| 9 | Absturz | VOLLWERTIG | H-Crash |
| 10 | Export | VOLLWERTIG | WAV 16/24 Bit, MP3, FLAC, OGG: `App/MainWindow.AudioEditor.cs:103-107`; Freigabe mit Prüfbericht `:95-96` |
| 11 | Rundreise | TEILWEISE | WAV-Export im UI-Test, Lautheit gegen FFmpeg ebur128 abgeglichen: `AudioDspTests.cs:62`, `AudioQaTests.cs:68`. Wieder-Öffnen des Exports als eigener Test: nicht gefunden |
| 12 | Fachfunktion real | VOLLWERTIG | Eigene DSP in C#: `Core/Media/AudioDsp.cs`, `AudioEdit.cs`, `AudioQa.cs`, `TimePitch.cs` |
| 13 | Claude/KI | TEILWEISE | Wie Bildbearbeitung: K-Claude, keine eigene Prompt-Aktion; K-Ersatz |
| 14 | Tests | TEILWEISE | Kern echt (`AudioEditTests.cs`, `AudioDspTests.cs`, `AudioMultiTests.cs`). Plugin-Host im Test ersetzt: `App/MainWindow.AudioPluginHost.cs:17`, `:55-62` → echter Plugin-Lauf NICHT GETESTET. Mikrofon-Aufnahme real: nicht geprüft |
| 15 | Bedienung | TEILWEISE | H-Bedien. Eigene Tastenkürzel (Leertaste, Strg+C/X/V auf der Welle) und Rechtsklick-Menü in `MainWindow.AudioEditor.cs` und `WaveView.cs` nicht gefunden |

**Mischen (Mehrspur):** je Spur genau eine Datei mit Startzeit, Pegel, Panorama, Stumm, Solo: `Core/Media/AudioMulti.cs:10`; Busse, Sends, EQ, Kompressor: `App/MainWindow.AudioBuses.cs:9`. Mehrere Clips je Spur und Blenden zwischen Clips stehen selbst als offen in `docs/claude/vorbilder/audio-audacity-audition-rx.md` („Nächste Schritte“). Aufnahme fest 10 Sekunden: `App/MainWindow.AudioEditor.cs:91`.

**Gesamturteil:** TEILWEISE (stark) – echter Wellenform-Editor mit Reparatur und Messung; das Mehrspur-Mischen ist noch schmal.

**Was fehlt:** 1. Mehrere Clips je Spur, Schneiden und Blenden in der Mehrspur. 2. Aufnahme mit freier Länge und Pegelanzeige. 3. Effekte mit Reglern und Vorhören statt fester Werte. 4. Tastenkürzel und Rechtsklick auf der Welle. 5. Echter Plugin-Lauf als Beleg.

**Abnahmekriterien:** Zwei Clips auf einer Spur lassen sich überblenden, die Abmischung enthält die Blende (Pegelmessung). · Aufnahme läuft, bis „Stopp“ gedrückt wird. · Leertaste startet und stoppt die Wiedergabe. · Exportierte FLAC wird wieder geöffnet und ist auf ±1 Sample gleich lang.

**Aufwand:** mittel. **Risiko:** gering bis mittel (Mikrofon je Gerät verschieden). **Dateien:** `App/MainWindow.AudioEditor.cs`, `AudioMulti.cs`, `AudioBuses.cs`, `AudioQa.cs`, `AudioPluginHost.cs`, `App/WaveView.cs`, `Core/Media/Audio*.cs`, `MixRouting.cs`.

---

## CORE-08 Medien bearbeiten (`kreativ-medien`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Werkbank aus Pfadfeld, Aufgabenliste, Ergebnis – kein Editor: `App/MainWindow.W4Media.cs:25`, Aufbau ab `:34` der Ansicht |
| 2 | Werkzeuge | VOLLWERTIG (für den Zweck) | 7 Aufgaben (MP4, MP3, kleiner, Vorschaubild, GIF, Ausschnitt, Bild verkleinern), Ausschnitt, Aneinanderhängen: `Core/MediaStudio.cs:10-19`, `:84`, `:99` |
| 3 | Neues Projekt | n. z. | kein Dokument |
| 4 | Import | VOLLWERTIG | `Core/MediaStudio.cs:21` |
| 5 | Projekte bearbeiten | n. z. | |
| 6 | Rückgängig | FEHLT (unkritisch) | Original bleibt, jede Aufgabe schreibt eine neue Datei: Test `W4MediaTests.cs:15` |
| 7 | Vorschau | TEILWEISE | Vorschaubilder der Zeitleiste: `Core/MediaStudio.cs:64-69`; Ergebnis öffnet das Systemprogramm |
| 8/9 | Speichern/Absturz | n. z. | |
| 10 | Export | TEILWEISE | feste Zielformate MP4/MP3/JPG/GIF, keine Einstellungen |
| 11 | Rundreise | VOLLWERTIG | Test prüft echte Ausgabedateien: `W4MediaTests.cs:15`, `W4MediaUiTests.cs:10` |
| 12 | Fachfunktion real | VOLLWERTIG | FFmpeg ohne Shell: `Core/MediaStudio.cs:31-52` |
| 13 | Claude/KI | TEILWEISE | `medien.umwandeln` wandelt wirklich um (Partial), Hochladen Blocked: `Core/PromptAction/MediaPromptAdapter.cs:7-10`, `:33-36` |
| 14 | Tests | VOLLWERTIG | echt mit FFmpeg: `W4MediaTests.cs:15` (`NeedsFact("ffmpeg")`) |
| 15 | Bedienung | TEILWEISE | Datei aufs Pfadfeld ziehen geht (H-Bedien); sonst Knöpfe |

**Gesamturteil:** TEILWEISE – ehrlicher Umwandler, kein Fachprogramm. **Was fehlt:** Stapelverarbeitung mehrerer Dateien, wählbare Qualität/Zielgröße, Fortschritt mit Abbruch. **Abnahme:** Zehn Dateien in einem Lauf umwandeln, Fortschritt je Datei, Abbruch löscht die halbe Datei. **Aufwand:** klein. **Risiko:** gering. **Dateien:** `App/MainWindow.W4Media.cs`, `Core/MediaStudio.cs`.

---

## X-07 Freie Bilder & Klänge (`assets`, in CORE-08 eingegliedert: `App/StudioCatalog.cs` FoldedInto)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG (als Medienbrowser) | Raster, Vorschau mit Lizenz und Urheber, Merklisten: Test `MediaBrowserUiTests.cs:38`, `:98` |
| 2 | Werkzeuge | VOLLWERTIG | Suche, Filter, Laden mit Herkunftsdatei, Namensnennung kopieren, „in der Bildbearbeitung öffnen“: ebd. |
| 3–9 | Projekt, Undo, Autosave, Absturz | n. z. | Browser, kein Dokument |
| 4 | Import | n. z. | |
| 7 | Vorschau | VOLLWERTIG | Bildvorschau, Klänge vorhören: `MediaBrowserUiTests.cs:38` |
| 10 | Export | VOLLWERTIG | Download in den Medienordner mit Lizenzdatei und Prüfsumme: `App/MainWindow.W4Assets.cs:15`, `:39`; `W4AssetsTests.cs:21` |
| 11 | Rundreise | TEILWEISE | Übergabe an die Bildbearbeitung getestet (`MediaBrowserUiTests.cs:98`) |
| 12 | Fachfunktion real | TEILWEISE | Echter Download-Test nur mit Internet, sonst übersprungen: `W4AssetsUiTests.cs:26`; übrige Tests mit simuliertem Netz `:10` |
| 13 | Claude/KI | TEILWEISE | Claude als Rechercheur mit Lizenzprüfung: `App/MainWindow.ClaudeMedia.cs:10`. Prompt-Aktion: „Lizenz merken“ schreibt nur einen Hinweistext, „Quelle holen“ ist Blocked: `Core/PromptAction/CapabilityCatalog.cs:49`, `Core/PromptAction/StudyPromptAdapter.cs:302-345` |
| 14 | Tests | TEILWEISE | siehe 12 |
| 15 | Bedienung | TEILWEISE | nicht im Einzelnen geprüft |

**Gesamturteil:** TEILWEISE – als Browser brauchbar; die KI-Zeile holt nichts. **Was fehlt:** KI-Aktion, die nach Zustimmung wirklich lädt; Beleg eines echten Laufs je Quelle unter `reports/`. **Aufwand:** klein. **Risiko:** gering (fremde Schnittstellen ändern sich). **Dateien:** `App/MainWindow.W4Assets.cs`, `App/MainWindow.ClaudeMedia.cs`, `Core/Assets.cs`.

---

## SPEC-08 Kleidung & Produkte entwerfen (`mode-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Figur mit Maßlinien und Schnittlage, Teile auf der Stoffbahn ziehbar: `App/MainWindow.ProgMode.cs:13`, `App/MarkerCanvas.cs`. Schnittteile selbst entstehen aus Maßen, Punkte und Kurven sind nicht frei zeichenbar (Vorlagen `:35-38`, Teile als Breite × Höhe `:47`) |
| 2 | Werkzeuge | TEILWEISE | Varianten, Größe aus Maßen, Teile, Gradierung, Stoffe, Muster: `App/MainWindow.ProgMode.cs:43-48`, `:223-226`; `App/MainWindow.ProgModeGarment.cs:16`, `:148` |
| 3 | Neues Projekt | VOLLWERTIG | 4 Vorlagen: `App/MainWindow.ProgMode.cs:35-38` |
| 4 | Import | TEILWEISE | Eigenes Bild als Stoffdruck: `App/MainWindow.ProgModeGarment.cs:197`. Fremde Schnitte (DXF-AAMA, Seamly `.val`): nicht gefunden |
| 5/6/8/9 | Bearbeiten, Undo, Speichern, Absturz | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | TEILWEISE | 3D-Anprobe als drei gerenderte Fotos über Blender, keine drehbare Ansicht: `App/MainWindow.ProgModeGarment.cs:239-259`; `reports/alle-studios/kleidung-profi-e2e.md` |
| 10 | Export | VOLLWERTIG | Datenblatt, Schnittlage SVG 1:1, Varianten-CSV: `App/MainWindow.ProgMode.cs:53-55`; Schnitt-PDF A4 mit Kontrollquadrat, DXF für Plotter: `App/MainWindow.ProgModeGarment.cs:206`, Bericht ebd. |
| 11 | Rundreise | NICHT GETESTET | DXF in einem Fremdprogramm geöffnet: kein Beleg gefunden |
| 12 | Fachfunktion real | TEILWEISE | Echte Blender-Läufe für T-Shirt und Rock belegt, mit Bildern: `reports/alle-studios/kleidung-profi-e2e.md` („Echte Läufe“). Übrige vier Kleidungsstücke: Bilder vorhanden (`kleidung-anprobe-*.png`), Lauf nicht einzeln beschrieben |
| 13 | Claude/KI | TEILWEISE | Prompt-Aktion `groesse.pruefen` liefert nur einen Größentext: `Core/PromptAction/StudyPromptAdapter.cs:71-90`, `CapabilityCatalog.cs:48`. K-Claude, K-Ersatz |
| 14 | Tests | TEILWEISE | Blender im UI-Test ersetzt: `App/MainWindow.ProgModeGarment.cs:19-20`, `:259`; Kern `SewingTests.cs:70-82` echt nur mit FreeSewing-Ordner |
| 15 | Bedienung | TEILWEISE | H-Bedien; Teile ziehen (`MarkerCanvas.cs`) |

**Gesamturteil:** TEILWEISE – echte Rechnung und echte Anprobe-Bilder, aber kein freies Schnittprogramm wie Seamly2D.

**Was fehlt:** 1. Schnittteile mit Punkten und Kurven zeichnen und ändern. 2. Fremde Schnitte importieren. 3. Drehbare 3D-Ansicht statt drei Fotos. 4. Beleg, dass DXF/PDF in einem Fremdprogramm stimmen. 5. KI-Aktion, die ein Produktdokument anlegt statt eines Textes.

**Abnahmekriterien:** Ein Punkt am Armloch lässt sich ziehen, Nahtzugabe und Stoffbedarf rechnen neu. · Das exportierte DXF öffnet in einem freien CAD-Betrachter mit Maß 1:1 (Kontrollquadrat 10 cm). · UI-Test mit echtem Blender liefert drei Bilder ungleich leer.

**Aufwand:** groß. **Risiko:** mittel (Stoffphysik ist empfindlich, siehe Fehlerliste im Bericht). **Dateien:** `App/MainWindow.ProgMode.cs`, `App/MainWindow.ProgModeGarment.cs`, `App/MarkerCanvas.cs`, Kern `Fashion`, `FashionPlus`, `Marker`, `Sewing`.

---

## SPEC-15 Live streamen (`sendung`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Ablauftabelle mit On-Air-Uhr, Szenen, Tonmischer: `App/MainWindow.Spec15.cs:11`, `App/MainWindow.LiveStudio.cs`; Test `LiveStudioUiTests.cs:21`. Kein Szenen-Editor mit Quellen zum Anfassen (Bauchbinde, Logo, zweite Kamera als feste Einblendungen: `LiveShowTests.cs:15`) |
| 2 | Werkzeuge | TEILWEISE | Vorschau, Aufnahme, Stopp, Fenster/Kamera/Mikrofon wählen, Probelauf mit Ampel, Ton-Wache: `LiveStreamUiTests.cs:65`, `:98`; `LiveShowTests.cs:98`, `:111` |
| 3–6, 8, 9 | Projekt, Undo, Autosave, Absturz | TEILWEISE | Keine Programm-Hülle (`Shell: False`). Szenen speichern und laden: `LiveShowTests.cs:15`. Rückgängig und Absturz-Sicherung: nicht gefunden |
| 4 | Import | TEILWEISE | Logo und Musikdatei für Szenen: `LiveShowTests.cs:41` |
| 7 | Vorschau | TEILWEISE | Vorschau über FFmpeg ohne Mikrofon: `LiveStreamTests.cs:58`, `:85`; am echten Bildschirm nicht belegt |
| 10 | Export | TEILWEISE | Aufnahme als Datei, OBS-Szenensammlung, CSV/Kapitel: `App/MainWindow.Spec15.cs:192`, `BroadcastUiTests.cs:48` |
| 11 | Rundreise | NICHT GETESTET | OBS-Szenensammlung in OBS geöffnet: kein Beleg |
| 12 | Fachfunktion real | TEILWEISE | Aufnahme mit Szene, Logo, Bauchbinde, Ton echt mit FFmpeg: `LiveShowTests.cs:67`. Senden an eine Plattform: nur Befehlsbau geprüft (`LiveStreamTests.cs:108`, `:129`); lokaler RTSP-Teststream über mediamtx belegt: `reports/alle-studios/runde2-werkzeuge-e2e.md:21-24` |
| 13 | Claude/KI | TEILWEISE | Claude als Regie baut Szenen und Sendeplan: `App/MainWindow.ClaudeLiveRegie.cs:8`. Prompt-Aktion `sendung.planen` schreibt nur einen Plan, `sendung.senden` ist Blocked: `Core/PromptAction/StudyPromptAdapter.cs:256-296`, `CapabilityCatalog.cs:47` |
| 14 | Tests | TEILWEISE | Kern teils echt (s. o.), Live-Sendung zu YouTube/Twitch NICHT GETESTET |
| 15 | Bedienung | TEILWEISE | nicht im Einzelnen geprüft |

**Gesamturteil:** TEILWEISE – Aufnahme und Szenen sind echt; das Versprechen „streamen“ ist bis zur Plattform nie nachgewiesen.

**Was fehlt:** 1. Nachweis einer echten Sendung (privater Teststream) mit Protokoll unter `reports/`. 2. Szenen-Editor mit frei platzierbaren Quellen. 3. Rückgängig und Wiederherstellung für Szenen und Ablauf. 4. Wiederverbinden bei Netzabbruch (nicht gefunden, nicht geprüft).

**Abnahmekriterien:** 60 s Stream an einen lokalen RTMP-Server kommen dort mit Bild und Ton an (ffprobe am Empfänger). · Eine Quelle lässt sich in der Szene mit der Maus verschieben und skalieren. · Nach einem erzwungenen Programmende sind Szenen und Ablauf wieder da.

**Aufwand:** mittel. **Risiko:** mittel (Geräte, Netz, Plattformregeln; Senden braucht Freigabe). **Dateien:** `App/MainWindow.Spec15.cs`, `App/MainWindow.LiveStudio.cs`, `App/MainWindow.Live.cs`, `App/MainWindow.ClaudeLiveRegie.cs`, Kern `BroadcastDesk`, `BroadcastPlus`, `ObsScenes`.

---

## STUDIO-ANIM-01 Animation & Motion (`animation-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Bühne nur zum Ansehen (`Viewbox`), Zeitregler, Schlüsselleiste: `App/MainWindow.KreativStudios2.cs:240-256` (Namen `anim-stage`, `anim-slider`, `anim-keys`). Kein Zeiger-Ereignis auf der Bühne gefunden – Ebenen lassen sich nicht anfassen |
| 2 | Werkzeuge | TEILWEISE | Nur Rechteck, Kreis, Text; fertige Bewegungen; Schlüssel über Zahlenfeld + Knopf: `:183-187`, `:306-311` (`anim-val-*`, `anim-key-*`) |
| 3 | Neues Projekt | VOLLWERTIG | 3 Vorlagen: `:176-178` |
| 4 | Import | FEHLT | Bilder, SVG, Ton einsetzen: nicht gefunden |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | VOLLWERTIG | Abspielen im Programm: `:245` (`anim-play`) |
| 10 | Export | TEILWEISE | Animiertes SVG, MP4 fest 1280×720/30 fps: `:189`, `:195`. Kein GIF, Lottie, durchsichtiges Video |
| 11 | Rundreise | TEILWEISE | „SVG gültig“ im Kern-Test (`KreativKits2Tests.cs:56`); Abspielen in einem Browser nicht belegt |
| 12 | Fachfunktion real | TEILWEISE | Rechnung echt (`Core/Kreativ/KreativKits2.cs`, AnimKit); MP4 über FFmpeg |
| 13 | Claude/KI | TEILWEISE | `animation.titel` schreibt eine echte Datei (Titel-Animation): `Core/PromptAction/KreativPromptAdapters2.cs:38-76`, `CapabilityCatalog.cs:43`; K-Claude, K-Ersatz |
| 14 | Tests | TEILWEISE | Video im UI-Test ersetzt: `App/MainWindow.KreativStudios2.cs:322-323`, `KreativStudios2UiTests.cs:81`. Echter FFmpeg-Lauf für die Animation: nicht gefunden → NICHT GETESTET |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Gesamturteil:** TEILWEISE (schwach) – ein Schlüsselbild-Formular für drei Formen, kein Motion-Programm.

**Was fehlt:** 1. Ebenen auf der Bühne ziehen, skalieren, drehen. 2. Bilder, SVG und Ton importieren. 3. Zeitleiste mit ziehbaren Schlüsselbildern je Eigenschaft. 4. Wählbare Größe/Bildrate, GIF und durchsichtiges Video. 5. Echter Render-Test.

**Abnahmekriterien:** Eine Ebene wird mit der Maus verschoben, bei eingeschalteter Aufnahme entsteht ein Schlüsselbild. · Ein PNG wird als Ebene eingefügt und animiert. · Export 1080×1920 als MP4, ffprobe bestätigt Größe und Dauer.

**Aufwand:** groß. **Risiko:** mittel. **Dateien:** `App/MainWindow.KreativStudios2.cs:169-345`, `Core/Kreativ/KreativKits2.cs`.

---

## STUDIO-ART-01 Künstlerstudio (`kuenstler-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Malfläche mit Stiftdruck, Zwischenpunkten, Schwenken, Zoom: `App/ArtControls.cs:41`, `:111-156`; Test `KuenstlerstudioUiTests.cs:47` |
| 2 | Werkzeuge | TEILWEISE | Pinselsatz, Radierer, Ebenen mit Mischarten, Spiegeln, Perspektive, Glätten, Farbrad, Posenpuppe, Skizze säubern: `KuenstlerTests.cs:13-224`. Auswahl, Transformieren, Text: in `Core/Kreativ/Kuenstler.cs` nicht gefunden |
| 3 | Neues Projekt | VOLLWERTIG | 6 Vorlagen: `App/MainWindow.Kuenstlerstudio.cs:54-59` |
| 4 | Import | TEILWEISE | ORA, PNG, JPG: `:73`, `:260-264`. PSD öffnen: nicht gefunden |
| 5 | Projekte bearbeiten | VOLLWERTIG | H-Öffnen; Ebenen als PNG (Base64) im Dokument: `:146`, `:170` |
| 6 | Rückgängig | TEILWEISE | H-Undo, getestet `KuenstlerTests.cs:80`. Jeder Strich legt das ganze Dokument samt aller Ebenen-PNGs als Text ab (`Core/Programs/ProgramDocument.cs:75-82`), bis 100-mal – Speicherbedarf bei großen Bildern nicht gemessen |
| 7 | Vorschau | VOLLWERTIG | Leinwand ist das Ergebnis |
| 8/9 | Speichern/Absturz | VOLLWERTIG | H-Save, H-Crash (gleiche Einschränkung wie 6: große Dateien alle 1,5 s) |
| 10 | Export | VOLLWERTIG | PNG, PNG ohne Hintergrund, ORA, PSD mit Ebenen, Druckdateien 300 dpi, Palette GPL: `App/MainWindow.Kuenstlerstudio.cs:94-101` |
| 11 | Rundreise | TEILWEISE | ORA hin und zurück mit Ebenen und Pixeln: `KuenstlerTests.cs:93`. PSD nur am Dateikopf und an der Länge geprüft (`KuenstlerTests.cs:120-126`), nicht in einem Fremdprogramm geöffnet |
| 12 | Fachfunktion real | VOLLWERTIG | Eigene Strich-Engine und Compositing: `Core/Kreativ/Kuenstler.cs`, `Kuenstler2.cs` |
| 13 | Claude/KI | TEILWEISE | `KunstPromptAdapter` legt eine echte Malvorlage (ORA mit Ebenen, Palette) an: `Core/PromptAction/KunstPromptAdapter.cs:11`, `CapabilityCatalog.cs:44`, Test `KuenstlerTests.cs:279`. Ideenskizze über lokale Bild-KI im Test ersetzt: `App/MainWindow.Kuenstlerstudio.cs:324-336` |
| 14 | Tests | TEILWEISE | Malen echt im Headless-Test; Zeitraffer-Video ersetzt: `:32`, `:388-394`, `KuenstlerstudioUiTests.cs:171` |
| 15 | Bedienung | TEILWEISE | Leertaste schwenkt: `App/ArtControls.cs:163-164`; H-Bedien. Weitere Werkzeug-Kürzel nicht gefunden |

**Gesamturteil:** TEILWEISE (stark) – echtes Malprogramm; es fehlen Auswahl/Transformieren und ein Fremdprogramm-Nachweis für PSD.

**Was fehlt:** 1. Auswahl, Verschieben/Skalieren/Drehen von Ebeneninhalt, Text. 2. PSD in Krita/GIMP öffnen und belegen; PSD einlesen. 3. Rückgängig ohne Vollkopie (nur geänderte Kacheln) und Messung bei 4000 × 4000 px. 4. Kürzel für Pinsel, Radierer, Größe.

**Abnahmekriterien:** PSD mit drei Ebenen öffnet in Krita mit Namen, Deckkraft und Mischart. · 200 Striche auf 4000 × 4000 px mit 5 Ebenen: Arbeitsspeicher bleibt unter einem festgelegten Wert, Rückgängig unter 200 ms. · Ein ausgewählter Bereich lässt sich drehen und ist danach rückgängig machbar.

**Aufwand:** mittel. **Risiko:** mittel (Speicher/Leistung ist ungemessen). **Dateien:** `App/MainWindow.Kuenstlerstudio.cs`, `App/ArtControls.cs`, `Core/Kreativ/Kuenstler.cs`, `Kuenstler2.cs`, `Core/PromptAction/KunstPromptAdapter.cs`.

---

## STUDIO-BOOK-01 Bücher schreiben (`buch-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Ein Markdown-Textfeld plus Kapitelübersicht und Wortziel: `App/MainWindow.KreativStudios2.cs:95`, `:70` (Übersicht), `:91` (Ziel). Keine formatierte Seite, kein Kapitel-Baum zum Umordnen |
| 2 | Werkzeuge | TEILWEISE | Neues Kapitel, Weiterschreiben und Lektorat über Claude: `:47-51` |
| 3 | Neues Projekt | VOLLWERTIG | `:41-42` |
| 4 | Import | FEHLT | DOCX/ODT/MD einlesen: nicht gefunden |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | TEILWEISE | Übersicht und Statistik; keine Seiten- oder E-Book-Vorschau gefunden |
| 10 | Export | TEILWEISE | EPUB, Markdown, Text: `:56-58`. Kein PDF für den Druck, kein DOCX für Verlage |
| 11 | Rundreise | TEILWEISE | „EPUB-Export lässt sich öffnen“ (eigener Leser): `KreativStudios2UiTests.cs:42`, `KreativKits2Tests.cs:13`. Prüfung mit epubcheck: nicht gefunden |
| 12 | Fachfunktion real | TEILWEISE | EPUB 3 wird wirklich gebaut (`Core/Kreativ/KreativKits2.cs`, BookKit) |
| 13 | Claude/KI | TEILWEISE | `buch.gliederung` schreibt ein Gerüst als Datei, „ohne Claude nur Gerüst und Statistik, kein Text“: `Core/PromptAction/KreativPromptAdapters2.cs:8-34`, `CapabilityCatalog.cs:42`. Claude-Knöpfe schicken nur einen Auftrag in den Chat (`:50-51`); K-Ersatz |
| 14 | Tests | TEILWEISE | ohne Fremdwerkzeug, s. o. |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Gesamturteil:** TEILWEISE – ein Textfeld mit EPUB-Knopf, kein Autorenprogramm.

**Was fehlt:** 1. Kapitel als eigene Einheiten mit Umordnen, Notizen, Figuren. 2. Import DOCX/ODT/MD. 3. Druck-PDF mit Satzspiegel und DOCX-Export. 4. E-Book-Vorschau. 5. EPUB-Prüfung mit einem unabhängigen Prüfer.

**Abnahmekriterien:** Ein DOCX mit fünf Überschriften wird zu fünf Kapiteln. · Kapitel 3 lässt sich vor Kapitel 1 ziehen, das EPUB folgt der neuen Reihenfolge. · Das EPUB besteht epubcheck ohne Fehler. · Druck-PDF im Format 12,5 × 19 cm mit Seitenzahlen entsteht.

**Aufwand:** mittel. **Risiko:** gering. **Dateien:** `App/MainWindow.KreativStudios2.cs:35-168`, `Core/Kreativ/KreativKits2.cs`.

---

## STUDIO-COMIC-01 Comics & Storyboards (`comic-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Seitenansicht aus festen Aufteilungen; bearbeitet wird in Textfeldern je Bild (Sprechblase, Unterschrift) und mit „Bild wählen“: `App/MainWindow.KreativStudios.cs:383`, `:439-449`. Kein Zeichnen, keine frei gesetzten oder geformten Sprechblasen |
| 2 | Werkzeuge | TEILWEISE | Aufteilung wählen, Bild einsetzen, Geschichte über Claude: `:383-385`, `:445-449` |
| 3 | Neues Projekt | VOLLWERTIG | `:377-378` |
| 4 | Import | TEILWEISE | PNG/JPG je Bildfeld: `:448` |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | VOLLWERTIG | Seite mit eingesetzten Bildern: `:409-410` |
| 10 | Export | TEILWEISE | SVG-Seite, Storyboard-Liste als Markdown: `:390-391`. Kein PDF, PNG, CBZ, keine Mehrseiten-Ausgabe |
| 11 | Rundreise | TEILWEISE | „SVG-Export mit eingebettetem Bild“ getestet: `KreativStudiosUiTests.cs:105` |
| 12 | Fachfunktion real | TEILWEISE | Layout und SVG echt (`Core/Kreativ/KreativKits.cs`, ComicKit) |
| 13 | Claude/KI | TEILWEISE | `comic.seite` legt eine SVG-Seite an; „Bilder entstehen nicht hier“: `Core/PromptAction/KreativPromptAdapters.cs:110-137`, `CapabilityCatalog.cs:41` |
| 14 | Tests | TEILWEISE | `KreativStudiosUiTests.cs:105`, `KreativKitsTests.cs:74` |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Gesamturteil:** TEILWEISE (schwach) – Formular mit Seitenvorschau; ein Comic entsteht nur, wenn die Bilder woanders gemacht wurden.

**Was fehlt:** 1. Bildfelder frei ziehen und teilen. 2. Sprechblasen auf der Seite setzen, formen, mit Dorn. 3. Zeichnen im Bildfeld oder Übergabe an das Künstlerstudio und zurück. 4. Mehrere Seiten, Export PDF/PNG/CBZ. 5. Bilder je Feld über die lokale Bild-KI.

**Abnahmekriterien:** Eine Sprechblase wird mit der Maus in ein Bildfeld gezogen und ihr Dorn auf eine Figur gerichtet. · Ein Bildfeld öffnet sich im Künstlerstudio, nach dem Speichern steht die Zeichnung im Comic. · Drei Seiten werden als ein PDF exportiert.

**Aufwand:** groß. **Risiko:** mittel. **Dateien:** `App/MainWindow.KreativStudios.cs:360-455`, `Core/Kreativ/KreativKits.cs`.

---

## STUDIO-FILM-01 Filme machen (`film-programm`)

Versprechen im Katalog: „Kurzfilme und Trailer: vom Drehbuch über Figuren und Einstellungen **zum fertigen Film**“ (`App/StudioCatalog.cs:41`).

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Drei Schritte: Drehbuch-Textfeld mit Seitenansicht, Figuren & Orte (Textfelder), Einstellungen (Liste): `App/MainWindow.FilmStudio.cs:108-120`, Textfeld `film-script` ab `:143` |
| 2 | Werkzeuge | TEILWEISE | „Einstellungen vorschlagen“, „Figuren & Orte übernehmen“: `:62-63` – beides Textauswertung des Drehbuchs |
| 3 | Neues Projekt | VOLLWERTIG | `:55-58` |
| 4 | Import | FEHLT | Fountain/Final Draft einlesen, Videos, Bilder, Ton: nicht gefunden |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | TEILWEISE | Drehbuch als gesetzte Seite; keine Bild- oder Filmvorschau |
| 10 | Export | TEILWEISE | Fountain, Drehbuch-PDF (braucht typst), Shotliste CSV, Drehbuchauszug MD: `:66-71`, `:79-88`. Kein Video |
| 11 | Rundreise | NICHT GETESTET | Fountain wieder einlesen: nicht gefunden |
| 12 | Fachfunktion real | **FEHLT** | Es entsteht kein Bild und kein Film. In `MainWindow.FilmStudio.cs` gibt es keinen Aufruf des Videoschnitts, der Video-KI oder von Claude (Suche nach `VK`, `Video`, `Claude`, `Intel.` ohne Treffer außer einem Kommentar `:15`). Der Katalog sagt es selbst: „Bilder und Clips sind hier noch nicht angeschlossen“ (`Core/PromptAction/CapabilityCatalog.cs:38`); Kopfkommentar „Teil 1“ (`App/MainWindow.FilmStudio.cs:14`) |
| 13 | Claude/KI | TEILWEISE | `film.drehbuch.geruest` schreibt ein festes Drei-Akt-Gerüst mit Platzhaltern („Zeig, wer die Hauptfigur ist …“) – ohne KI, unabhängig von der Idee: `Core/PromptAction/StudyPromptAdapter.cs:155-180` |
| 14 | Tests | TEILWEISE | Zwei UI-Tests: `FilmStudioUiTests.cs:14`, `:57`; PDF über typst im Test ersetzbar (`ToolRunOverride`, `App/MainWindow.FilmStudio.cs:85`) |
| 15 | Bedienung | TEILWEISE | H-Bedien |

Angrenzend vorhanden, aber nicht aus dem Film-Programm erreichbar: Storyboard-Tafeln aus einer Shotliste in die Zeitleiste (`App/MainWindow.VideoShotPlan.cs:10`, `:30`), „Film ausliefern“ (`App/MainWindow.FilmDelivery.cs`), KI-Filmszenen im Musik-Studio (`App/MainWindow.MusikVideoKi.cs:10`, `:140`, `:230`).

**Gesamturteil:** FEHLT (als Filmprogramm) – ein Drehbuch-Editor unter dem Namen „Filme machen“; der Weg zum Film ist nicht gebaut.

**Was fehlt:** 1. Übergabe der Shotliste an den Videoschnitt als Projekt mit Tafeln und Marken (Bausteine existieren). 2. Bilder je Einstellung (Storyboard) aus Figuren- und Ortsbeschreibung über die lokale Bild-KI. 3. Clips je Einstellung über die Video-KI mit Prüfung (Bausteine in `MusikVideoKi`). 4. Animatic mit Ton und Rohschnitt als MP4. 5. Import von Fountain/FDX. 6. Ein belegter Lauf „Drehbuch → MP4“.

**Abnahmekriterien:** Aus dem Beispiel-Kurzfilm entsteht mit einem Klick ein Videoschnitt-Projekt mit einer Tafel je Einstellung in Plan-Länge. · Für jede Einstellung liegt ein Bild vor, Figuren sehen über alle Bilder gleich aus (Sichtprüfung dokumentiert). · Export als MP4, ffprobe bestätigt Länge = Summe der Einstellungen ±0,5 s. · Ein Beleg unter `reports/` zeigt einen echten Lauf mit Bildern.

**Aufwand:** sehr groß. **Risiko:** hoch (Bild- und Video-KI lokal: Rechenzeit, Speicher, gleichbleibende Figuren; ohne KI bleibt nur ein Animatic).

**Dateien:** `App/MainWindow.FilmStudio.cs`, `Core/Film/Fountain.cs`, `Core/Media/ShotPlan.cs`, `App/MainWindow.VideoShotPlan.cs`, `App/MainWindow.MusikVideoKi.cs`, `Core/PromptAction/StudyPromptAdapter.cs:155-180`.

---

## STUDIO-LOGO-01 Logos & Marken (`logo-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Vorschau hell/dunkel, gezeichnet aus Feldern (Name, Zusatz, zwei Farben, Form, Schrift): `App/MainWindow.KreativStudios.cs:139-146`, `:160`. Nichts auf der Fläche lässt sich anfassen |
| 2 | Werkzeuge | TEILWEISE | Fünf feste Formen (Kreis, Schild, Quadrat, Wortmarke, Mit Linie) und eine Schriftliste: `:145-146`; Kontrastprüfung |
| 3 | Neues Projekt | VOLLWERTIG | `:139-141` |
| 4 | Import | FEHLT | eigenes Zeichen/SVG/Bild einsetzen: nicht gefunden |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | VOLLWERTIG | gleiche Maße wie das SVG: `:160` |
| 10 | Export | TEILWEISE | SVG, Markenpaket als Markdown: `:151-152`. Kein PNG in Größen, kein PDF, kein Favicon-Satz |
| 11 | Rundreise | TEILWEISE | „SVG-Export ist gültig“: `KreativStudiosUiTests.cs:57` |
| 12 | Fachfunktion real | TEILWEISE | SVG wird echt erzeugt (`Core/Kreativ/KreativKits.cs`, LogoKit) |
| 13 | Claude/KI | TEILWEISE | `logo.entwerfen` schreibt ein SVG aus denselben Feldern; „Bilder-KI ist hier nicht angeschlossen“: `Core/PromptAction/KreativPromptAdapters.cs:34-68`, `CapabilityCatalog.cs:39` |
| 14 | Tests | TEILWEISE | `KreativStudiosUiTests.cs:57`, `KreativKitsTests.cs:30` |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Gesamturteil:** TEILWEISE (schwach) – ein Logo-Generator mit fünf Schablonen, kein Gestaltungsprogramm.

**Was fehlt:** 1. Freies Zeichen: Formen und Pfade auf der Fläche bearbeiten (Vektor-Kern gibt es in `Core/Media/ImageVector.cs`). 2. Eigenes SVG/Bild als Bildmarke. 3. Abstände, Größen, Anordnung per Maus. 4. Export-Paket: PNG in Größen, einfarbig/negativ, PDF, Favicon. 5. Varianten nebeneinander.

**Abnahmekriterien:** Die Bildmarke lässt sich auf der Fläche verschieben und skalieren. · Ein eigenes SVG wird als Zeichen eingesetzt und umgefärbt. · Ein Klick erzeugt einen Ordner mit SVG, PNG 512/1024/2048, einfarbiger und negativer Fassung.

**Aufwand:** groß. **Risiko:** gering. **Dateien:** `App/MainWindow.KreativStudios.cs:120-226`, `Core/Kreativ/KreativKits.cs`.

---

## STUDIO-MUSIK-01 Trendo Musik (`musik-studio`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Seiten Erstellen/Studio/Bibliothek/Stimmen, Arrangement-Ansicht, Step-Sequencer, Piano-Roll: `App/MainWindow.MusikSuno.cs:10-83`, `App/MainWindow.MusikDaw.cs:89`, `:143`, `App/MainWindow.MusikBeat.cs:10`, `App/MainWindow.MusikNoten.cs:10`. Das Arrangement eines KI-Lieds ist nur Anzeige mit Klick auf die Position (`MusikDaw.cs:143`); zum Bearbeiten geht es ins Audio-Programm (`:122`) |
| 2 | Werkzeuge | TEILWEISE | Erzeugen, Verlängern, Remix/Zuschneiden, Mashup, Spuren trennen, Stimme lernen, Mastern, Mitsingen, Cover: jeweils eigene Datei `App/MainWindow.Musik*.cs`. Viele hängen an eingerichteten KI-Laufzeiten |
| 3 | Neues Projekt | VOLLWERTIG (als Lied) | `App/MainWindow.MusikStudio.cs:52` |
| 4 | Import | VOLLWERTIG | eigene Aufnahmen, alle gängigen Formate, Rest über FFmpeg: `App/MainWindow.MusikTools.cs:85-90`; MIDI öffnen `App/MainWindow.MusikNoten.cs:35` |
| 5 | Projekte bearbeiten | TEILWEISE | Bibliothek `musik/lieder.json`: `Core/W2/SongLibrary.cs:41`; Fassungen `App/MainWindow.MusikFassungen.cs` |
| 6 | Rückgängig | TEILWEISE | Kein Dokument-Rückgängig (keine Hülle). Löschen geht in den Windows-Papierkorb: `App/MainWindow.MusikStudio.cs:245`, `Core/W2/SongLibrary.cs:77-78` |
| 7 | Vorschau | VOLLWERTIG | Player mit Wellenform: `App/MainWindow.MusikDaw.cs:34`, `:76`; echter mpv-Test `MpvWindowTests.cs:10` |
| 8/9 | Speichern/Absturz | TEILWEISE | Bibliothek speichert sofort; Wiederherstellung eines laufenden Auftrags nach Absturz: nicht gefunden |
| 10 | Export | VOLLWERTIG | MP3 mit Cover, WAV, FLAC: `App/MainWindow.MusikStudio.cs:547-548`; Spuren-Ordner, Noten/MIDI |
| 11 | Rundreise | TEILWEISE | Import-Test mit echtem FFmpeg: `MusikImportUiTests.cs:17` |
| 12 | Fachfunktion real | TEILWEISE | Erzeugung über ACE-Step 1.5 lokal: `App/MainWindow.MusikStudio.cs:10`, `:52`. Echte Läufe belegt für Spurentrennung und Stimm-Lernen, dort auch ein „echtes KI-Lied (60 s, mit Trendo Musik/ACE-Step erzeugt)“: `reports/musik-echt/BERICHT.md`. Ein eigener Beleg „Klick auf Erstellen → Lied“ aus der App: nicht gefunden; der Bericht nennt „Einrichtung über den Knopf in Trendo“ als nicht geprüft |
| 13 | Claude/KI | TEILWEISE | Claude als Produzent setzt Text/Stil ins Profi-Formular und stößt Mastern an: `App/MainWindow.ClaudeMusik.cs:9`, `:78`. Prompt-Aktion `musik.lied.planen` schreibt nur einen Liedplan mit festem Aufbau als Textdatei – kein Lied: `Core/PromptAction/StudyPromptAdapter.cs:126-152`; der Katalog verspricht für dieselbe Aktion „Lied mit Gesang erzeugen … MP3“ (`Core/PromptAction/CapabilityCatalog.cs:37`) |
| 14 | Tests | TEILWEISE | In UI-Tests ersetzt: ACE-Step (`App/MainWindow.Musik.cs:16-17`), Trennung (`MusikSpuren.cs:12`), Stimme (`MusikEigeneStimme.cs:34`), Mastern (`MusikProfi.cs:11`), Mashup (`MusikMashup.cs:14`), Export (`MusikStudio.cs:566`). Echter Lied-Lauf nur auf Anforderung mit Umgebungsvariablen: `tests/Trendo.App.Tests/MusikLiveTests.cs:13-15` → im normalen Lauf übersprungen |
| 15 | Bedienung | TEILWEISE | Rechtsklick-Menü je Lied: `App/MainWindow.MusikStudio.cs:435`; Vollbild-Texteditor `App/MainWindow.MusikTexte.cs:18`, `:87`. Einfach/Profi vorhanden (`MusikStudio.cs:134`, `:153`) |

**Gesamturteil:** TEILWEISE (stark) – breitestes Programm der Gruppe mit echten KI-Läufen im Bericht; die Kernfunktion ist im Standard-Testlauf nie echt.

**Was fehlt:** 1. Echtlauf-Beleg aus der App („Erstellen“ → MP3 mit Gesang, Dauer, Lautheit) fest im Volltest oder als Bericht. 2. Lied im Studio selbst bearbeiten (Abschnitte umstellen, schneiden) statt Wechsel ins Audio-Programm. 3. KI-Aktion, die wirklich ein Lied erzeugt, oder ehrliche Benennung „Lied planen“ im Katalog. 4. Auftrag nach Absturz fortsetzen oder sauber melden. 5. Mehrere Clips/Takes je Spur (hängt an der Audio-Mehrspur).

**Abnahmekriterien:** Mit eingerichtetem ACE-Step erzeugt „Erstellen“ in der App ein 60-s-Lied; ffprobe bestätigt Dauer ±2 s, Lautheit −14 LUFS ±1. · Die Prompt-Aktion liefert eine MP3 oder heißt überall „Lied planen“. · Zwei Abschnitte im Arrangement lassen sich tauschen, das Ergebnis ist hörbar und exportierbar. · Wird Trendo während der Erzeugung beendet, zeigt der nächste Start den abgebrochenen Auftrag.

**Aufwand:** mittel. **Risiko:** mittel (GPU-Speicher, Laufzeit der KI, Installation landet laut Bericht im App-Container).

**Dateien:** `App/MainWindow.Musik*.cs` (30 Dateien), `App/MainWindow.ClaudeMusik.cs`, `Core/W2/SongLibrary.cs`, `MusicSuno.cs`, `SongMaster.cs`, `SongTools.cs`, `Core/Media/StepSequencer.cs`, `PianoRoll.cs`, `tests/Trendo.App.Tests/MusikLiveTests.cs`.

---

## STUDIO-PHOTOS-01 Fotos verwalten (`fotos-verwalten-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Quelle und Ziel wählen, Ordnung wählen, Probelauf als Liste: `App/MainWindow.KreativStudios2.cs:379-392`. Bilder sieht man erst im getrennten Fotoalbum (`:389`, Sprung nach `medien`) |
| 2 | Werkzeuge | VOLLWERTIG (für den Zweck) | Ordnung nach Jahr/Monat/Tag, Umbenennen, Doppelte überspringen, Probelauf, Einsortieren erst nach zweitem Klick: Tests `KreativStudios2UiTests.cs:95`, `KreativKits2Tests.cs:80` |
| 3–5 | Projekt | TEILWEISE | eine Vorlage `:384`; H-Öffnen |
| 6 | Rückgängig | VOLLWERTIG | Eigenes Rückgängig der Dateibewegungen über ein Protokoll: `:453`, `:485` |
| 7 | Vorschau | TEILWEISE | Probelauf-Liste, keine Vorschaubilder im Programm |
| 8/9 | | VOLLWERTIG | H-Save, H-Crash |
| 10 | Export | TEILWEISE | Probelauf als Text: `:392` |
| 11 | Rundreise | n. z. | |
| 12 | Fachfunktion real | VOLLWERTIG | Echte Kopien/Verschiebungen ohne Überschreiben: `KreativKits2Tests.cs:80` |
| 13 | Claude/KI | TEILWEISE | `fotos.plan` nur Probelauf, Einsortieren im Studio: `Core/PromptAction/KreativPromptAdapters2.cs:80-115`, `CapabilityCatalog.cs:45` |
| 14 | Tests | VOLLWERTIG | echte Dateien im Temp-Ordner, Ordnerwahl ersetzt (`:406`) |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Gesamturteil:** TEILWEISE – zuverlässiges Hilfsprogramm, aber keine Fotoverwaltung mit Bildern, Bewertung, Stichwörtern in einem Fenster. **Was fehlt:** Raster mit Vorschaubildern im Programm, Bewertung/Stichwörter/EXIF-Ansicht, Doppelte nebeneinander vergleichen, RAW-Vorschau. **Abnahme:** 500 Fotos erscheinen als Raster in unter 3 s; ein Doppelten-Paar wird nebeneinander gezeigt und eines in den Papierkorb gelegt; Rückgängig stellt es wieder her. **Aufwand:** klein bis mittel. **Risiko:** gering. **Dateien:** `App/MainWindow.KreativStudios2.cs:346-553`, `App/MainWindow.Fotoalbum.cs`, `Core/Kreativ/KreativKits2.cs`.

---

## STUDIO-PODCAST-01 Podcast aufnehmen (`podcast-programm`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Textfelder Titel, Beschreibung, Kapitel („00:00 Begrüßung“); Kapitel wie im Player: `App/MainWindow.KreativStudios.cs:234-235`, Test `KreativStudiosUiTests.cs:79`. Keine Wellenform, kein Schnitt |
| 2 | Werkzeuge | TEILWEISE | Aufnahme laden, Lautheit angleichen: `:240-241`. **„Aufnehmen“ nimmt nicht auf**, sondern öffnet die Seite `sendung`: `:242` |
| 3 | Neues Projekt | VOLLWERTIG | `:234-235` |
| 4 | Import | VOLLWERTIG | MP3, WAV, M4A, OGG: `:240` |
| 5/6/8/9 | | VOLLWERTIG | H-Öffnen, H-Undo, H-Save, H-Crash |
| 7 | Vorschau | TEILWEISE | Kapitel-Ansicht; Anhören im Programm: nicht gefunden |
| 10 | Export | TEILWEISE | Shownotes MD, Kapiteldatei, RSS-Feed, MP3 mit Kapiteln über „Lautheit angleichen“: `:247-249`, `:273-299` |
| 11 | Rundreise | NICHT GETESTET | MP3-Kapitel in einem Player, Feed in einem Prüfer: kein Beleg |
| 12 | Fachfunktion real | TEILWEISE | Lautheit −16 LUFS über FFmpeg als echter Prozess: `:283-296`; ein Test mit echtem FFmpeg dafür wurde nicht gefunden (`KreativKitsTests.cs:49` prüft nur die Argumente) → NICHT GETESTET |
| 13 | Claude/KI | TEILWEISE | `podcast.shownotes` schreibt Shownotes und Kapitel als Datei: `Core/PromptAction/KreativPromptAdapters.cs:72-106`, `CapabilityCatalog.cs:40` |
| 14 | Tests | TEILWEISE | s. 12 |
| 15 | Bedienung | TEILWEISE | H-Bedien |

**Fehler:** Fehlt FFmpeg, bleibt `_podcBusy` für immer gesetzt – `App/MainWindow.KreativStudios.cs:278` setzt das Kennzeichen, `:279` verlässt die Methode ohne Zurücksetzen (das `finally` in `:298` greift erst im späteren `try`). Danach meldet jeder weitere Klick „Die Lautheit wird schon angeglichen“, auch nachdem FFmpeg eingerichtet wurde, bis zum Neustart.

**Gesamturteil:** TEILWEISE (schwach) – Shownotes- und Kapitelwerkzeug; das namensgebende Aufnehmen und das Schneiden fehlen im Programm.

**Was fehlt:** 1. Aufnahme im Programm mit Pegel, mehreren Spuren (Gast), Pause. 2. Schnitt an der Wellenform (Editor-Kern liegt in „Audio bearbeiten“). 3. Anhören mit Sprung zu Kapiteln. 4. Fehler `_podcBusy` beheben. 5. Echter Test für Lautheit und Kapitel in der MP3. 6. Intro/Outro und Musikbett.

**Abnahmekriterien:** „Aufnehmen“ startet im Programm eine Aufnahme, „Stopp“ legt eine WAV ins Projekt. · Ein Versprecher wird an der Welle markiert und entfernt, das Kapitel danach verschiebt sich mit. · Die fertige MP3 hat −16 LUFS ±1 und Kapitelmarken, die ffprobe ausgibt. · Ohne FFmpeg erscheint der Hinweis; nach dem Einrichten funktioniert der Knopf ohne Neustart.

**Aufwand:** groß (mit Wiederverwendung des Audio-Editors mittel). **Risiko:** mittel (Mikrofon-Geräte). **Dateien:** `App/MainWindow.KreativStudios.cs:227-359`, `Core/Kreativ/KreativKits.cs`.

---

## STUDIO-TV-01 TV & Live (`tv`)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG (als Abspielprogramm) | Startseite, Senderliste mit „Jetzt läuft“, Programmführer-Raster, Filme/Serien, Mediatheken, Video-Plattformen: `App/MainWindow.Tv.cs:119`, `App/MainWindow.TvGuide.cs:10`, `TvVod.cs:13`, `TvLegal.cs:11`, `TvVideos.cs:11` |
| 2 | Werkzeuge | TEILWEISE | Abspielen über eingebettetes mpv, Aufnahme über FFmpeg, Assistent in Alltagssprache: `App/MainWindow.Tv.cs:21`, `:424`, `App/MainWindow.TvAssistant.cs:12`. Offen laut eigener Liste: Senderlogos überall, Zeitversetzt: `docs/claude/vorbilder/tv-kodi-tivimate.md:15-16` |
| 3, 5, 6, 8, 9 | Projekt, Undo, Autosave, Absturz | n. z. | kein Dokument-Editor |
| 4 | Import | VOLLWERTIG | Senderliste M3U/Xtream, EPG: `Core/PromptAction/CapabilityCatalog.cs:46`, Tests `IpTvTests.cs` (6 Fälle) |
| 7 | Vorschau | TEILWEISE | mpv im Fenster (`App/MainWindow.Tv.cs:21`), Vollbild mit Esc (`App/MainWindow.TvFull.cs:10`), Bild-in-Bild (`TvPlus.cs:194`). Echtes Bild nicht automatisiert belegt |
| 10 | Export | TEILWEISE | Aufnahme als TS-Datei |
| 11 | Rundreise | NICHT GETESTET | |
| 12 | Fachfunktion real | NICHT GETESTET | Wiedergabe und Aufnahme sind in allen UI-Tests ersetzt: `App/MainWindow.Tv.cs:140-144`, `:341`, `:424`. Echter Abruf einer Stream-Adresse belegt nur für arte über streamlink: `reports/alle-studios/runde2-werkzeuge-e2e.md:26-34` |
| 13 | Claude/KI | TEILWEISE | `tv.liste.pruefen` ordnet eine eingefügte Senderliste als Text: `Core/PromptAction/StudyPromptAdapter.cs:93-123` |
| 14 | Tests | TEILWEISE | 11 UI-, 22 Kern-Testfälle (`Tv*Tests.cs`, `IpTvTests.cs`), Netz und Abspieler ersetzt |
| 15 | Bedienung | TEILWEISE | Vollbild vorhanden; Fernbedienungs-/Pfeiltasten-Navigation nicht geprüft |

**Gesamturteil:** TEILWEISE – als Abspielprogramm weit ausgebaut; ob wirklich ein Sender läuft und aufgenommen wird, belegt kein automatischer Lauf.

**Was fehlt:** 1. Echtlauf-Beleg: frei verfügbarer Teststream → Bild im mpv-Fenster und 10-s-Aufnahme mit ffprobe. 2. Senderlogos, Zeitversetzt. 3. Bedienung nur mit Pfeiltasten geprüft. **Abnahme:** Ein lokaler HLS-Teststream (mediamtx liegt vor) wird abgespielt, ein Bildschirmfoto zeigt das Testbild; die Aufnahme hat Bild und Ton. **Aufwand:** klein. **Risiko:** gering bis mittel (fremde Quellen wechseln; Trendo bringt bewusst keine Sender mit). **Dateien:** `App/MainWindow.Tv*.cs`, Kern `Tv/` (`M3u`, `Xmltv`, `TvLibrary`).

---

## Auffälligkeiten der Gruppe

**Fassaden und Versprechen, die der Code nicht hält**
1. „Filme machen“ erzeugt keinen Film. Katalogsatz „zum fertigen Film“ (`App/StudioCatalog.cs:41`) gegen Exporte Fountain/PDF/CSV/MD (`App/MainWindow.FilmStudio.cs:66-71`) und das eigene Eingeständnis im Fähigkeiten-Katalog (`Core/PromptAction/CapabilityCatalog.cs:38`).
2. „Podcast aufnehmen“ nimmt nicht auf: der Knopf wechselt nur die Seite (`App/MainWindow.KreativStudios.cs:242`).
3. Prompt-Aktionen, die nur Text liefern, wo der Katalog ein Produkt nennt: Musik – Katalog „Lied mit Gesang erzeugen … MP3“ (`CapabilityCatalog.cs:37`), Aktion schreibt `liedplan.txt` mit festem Aufbau (`StudyPromptAdapter.cs:140-149`). Film – festes Gerüst mit Platzhaltersätzen (`StudyPromptAdapter.cs:171-176`). Kleidung – Größentext (`:71-90`). Live – Plan (`:279-296`). Freie Medien – Lizenznotiz (`:325-345`).
4. Logo, Comic, Animation, Podcast sind Formulare mit Vorschau: bearbeitet wird in Textfeldern und Knöpfen, auf der Fläche lässt sich nichts anfassen (`App/MainWindow.KreativStudios.cs:139-146`, `:439-449`; `App/MainWindow.KreativStudios2.cs:240-311`).

**Vorschau ≠ Ergebnis**
5. Videoschnitt: Farbe, Übergänge, Einblendungen, Keyframes und Effekte sieht man nur nach dem Export (`App/MainWindow.VideoPreview.cs:69`; bestätigt in `docs/STUDIO_REBUILD_4_0/pilot/VIDEOSCHNITT_ABNAHME_4.0.md`).

**Fehler**
6. `App/MainWindow.KreativStudios.cs:278-279`: `_podcBusy` bleibt gesetzt, wenn FFmpeg fehlt – der Knopf „Lautheit angleichen“ ist danach bis zum Neustart tot.

**Doku ≠ Code**
7. `docs/programme/katalog.json` führt Animation, Buch, Comic, Logo, Podcast, Fotos mit `Tests: []` und „Test NOT_RUN“, obwohl UI- und Kern-Tests existieren (`KreativStudiosUiTests.cs:57-105`, `KreativStudios2UiTests.cs:42-95`). Dieselben Einträge listen Formate und Abhängigkeiten der ganzen Quelldatei (z. B. `.docx`, `.ods`, `.mp4`, FFmpeg beim Buch-Programm), nicht des Programms.
8. Katalog-Zeile `musik-studio`: `Engine: []`, `Formats: []`, `SourceFiles: []`, Tests beginnen mit einem fremden Test („A2/A4: Texte & Tabellen …“); ebenso bei `schnitt`. Die Zuordnung Programm → Test ist dort nicht verlässlich.
9. `docs/claude/vorbilder/video-kdenlive-resolve.md`, `audio-…`, `bild-…` zeigen fast nur ✓; die offenen Punkte stehen nur unter „Nächste Schritte“ bzw. im Pilot-Dokument. Wer nur die ✓-Liste liest, bekommt ein zu gutes Bild.
10. MLT-Export: im Kern fertig und echt gerendert (`Core/Media/MltProject.cs`, `reports/alle-studios/mlt-e2e.md:13-20`), in der App aber nicht angeschlossen (kein Aufruf in `src/Trendo.App`), im Bericht als „NOT RUN“ vermerkt (`:23-24`).

**Testlage**
11. Echtlauf-Tests mit `if (ff is null) return;` zählen ohne FFmpeg als bestanden statt übersprungen (z. B. `VideoEditTests.cs:56`, `VideoExportTests.cs:63`, `AudioQaTests.cs:68`, `FilmDeliveryTests.cs:71`). Sauber gelöst nur dort, wo ein eigenes Attribut überspringt (`PreviewStreamTests.cs:11`, `VideoAnimTests.cs:11`, `W4MediaTests.cs:15`).
12. Über die Oberfläche läuft in dieser Gruppe kein Test vom Klick bis zur echten Mediendatei: Videoschnitt-Export, Vorschau, Musik-KI, Spurentrennung, Stimme, TV-Wiedergabe, Animation-Video, Zeitraffer, Anprobe, Plugin-Host sind jeweils durch `*Override` ersetzt (Belege in den Tabellen). Echt sind die Kern-Tests und die Berichte unter `reports/`.
13. Claude in den Programmen ist in allen Tests ersetzt (`App/MainWindow.Claude.cs:46`); ein Beleg, dass Claude ein Dokument dieser Gruppe wirklich ändert, liegt nicht vor.

**Gemeinsame Bedien-Lücken**
14. Dateien aus dem Explorer lassen sich nur auf Pfad-Textfelder ziehen, nicht auf Zeitleiste, Leinwand oder Wellenform (`App/MainWindow.Files.cs:155-170`).
15. Werkzeug-Tastenkürzel und eigene Rechtsklick-Menüs gibt es nur im Videoschnitt (`App/MainWindow.VideoSchnitt.cs:242-277`) und in der Liederliste (`App/MainWindow.MusikStudio.cs:435`); in Bild, Audio, Künstlerstudio (außer Leertaste) wurden keine gefunden.
16. Rückgängig speichert je Schritt das ganze Dokument als Text (`Core/Programs/ProgramDocument.cs:75-82`). Für Dokumente mit eingebetteten Bildern (Künstlerstudio, Ebenen als Base64-PNG: `App/MainWindow.Kuenstlerstudio.cs:170`) ist der Speicherbedarf nicht gemessen.

**Nicht geprüft**
- Kein Programm wurde gestartet; alle Aussagen stammen aus Code, Tests und Berichten.
- Eigenschaften-Spalten der Programme (ob dort Regler für Effekte liegen) nur stichprobenhaft.
- TV-Bedienung mit Tastatur, Fotoalbum (`App/MainWindow.Fotoalbum.cs`), Musik-Unterseiten im Einzelnen (30 Dateien nur über Kopfkommentare und Ersatzstellen gesichtet).
- Ob die Echtlauf-Tests im gerade laufenden Volltest grün sind.
