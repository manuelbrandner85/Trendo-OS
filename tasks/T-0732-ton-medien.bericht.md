# Bericht T-0732: CORE-08 Audio bearbeiten + Medien bearbeiten

**Auftrag:** Audit-Restliste (`docs/audit/2026-11-16-profi-audit/d7-restliste.md`) Rang 2 (`ton`) und Rang 7 (`kreativ-medien`).
**Branch:** `claude/T-0732-ton-medien` (eigene Kopie von `main`, Stand 0.9.103)

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Aufnahme | fest 10 s, keine Anzeige (`MainWindow.AudioEditor.cs:95` alt: `AuRecord(d, 10)`) | „Aufnehmen“ läuft bis „Stopp“; Leiste über der Welle mit Zeit, Pegelbalken (gelb/orange/rot) und Übersteuerungs-Lampe; Aufnahme wird in „Medien\Aufnahmen“ abgelegt und an der Marke eingefügt (Mehrspur: neue Spur) | `MicRecorder` (ein FFmpeg-Lauf: WAV + 8-kHz-Pegelstrom, Stopp über „q“ → sauberer WAV-Kopf). Test `TonMedienT0732Tests.Record_until_stop_with_level` und UI-Test `TonT0732_Ui.Record_until_stop` mit **echtem FFmpeg**; ersetzt ist nur das Mikrofon durch eine FFmpeg-Tonquelle in Echtzeit (`-re -f lavfi sine`) |
| Leertaste | nicht vorhanden (Audit: „Tastenkürzel … nicht gefunden“) | Leertaste spielt ab (Auswahl oder ab Marke) bzw. hält an; Menüleiste „Wiedergabe → Abspielen / Anhalten (Leertaste)“; die Welle nimmt nach jedem Neuaufbau den Fokus; in Feldern gehört die Taste dem Feld | `TonT0732_Ui.Space_plays_and_stops` (echte Tastendrücke; Abspieler ersetzt – mpv-Lauf belegt `MpvWindowTests`) |
| Effekt-Werte | fest (±3 dB, Echo 0,3, Tempo ±10 %, 80 Hz, 8 kHz …) | rechte Spalte „Effekt-Werte“: Lauter/Leiser, Normalisieren-Ziel, Blendenlänge, Echo-Abstand, Tiefen/Höhen weg, Tempo-Schritt, Rauschschwelle, Kompressor-Schwelle, Stille-Länge; die Menüband-Knöpfe nutzen diese Werte | `TonT0732_Ui.Effects_with_values` (6 dB → Spitze +6,0 dB, Rückgängig, 2,5 s Stille, 200 Hz/0,5 s im Schritt) |
| Stapel | 7 feste Aufgaben, nur eine Datei | Karte „Mehrere Dateien auf einmal“: Dateien wählen (Mehrfachauswahl) oder aus dem Explorer ablegen (Karte oder ganze Werkbank – eine Datei wird gewählt, mehrere kommen in den Stapel), Aufgabe wählen, Ausgabeordner wählen, Start; je Datei Balken und Zustand; „Abbrechen“; Unpassendes wird mit Grund übersprungen | `MediaBatch` (FFmpeg `-progress`, `Cli.RunStreamAsync` mit Abbruch, Zwischendatei + Umbenennen). Tests `Batch_ten_files`, `Batch_cancel` (Core) und `MedienStapelT0732_Ui.Batch_drop_and_run`, `Batch_cancel` (UI, echtes Ablegen-Ereignis, echtes FFmpeg) |

## Echtbeleg `reports/T-0732/`
`beleg.sh` → `rohlog.txt`, `beleg.log`, `aufnahme.wav`, `sha256.txt`. FFmpeg 9.0.2 (gyan.dev). Zehn Windows-Systemklänge (`C:\Windows\Media\Alarm01–10.wav`, echte Fremddateien) in einem Lauf nach MP3 (1,8 s, 30 Fortschrittsmeldungen), jede MP3 mit ffprobe geöffnet; Abbruch bei 9 % → 0 Dateien im Ausgabeordner; Ton-Kette Alarm01 → −6/+6 dB, Echo 0,5 s → FLAC → mit ffprobe geöffnet → wieder eingelesen, 0 Samples Abweichung; Aufnahme bis Stopp nach 4,3 s, Pegel −18,1 dB, WAV von ffprobe gelesen. MP3/FLAC aus den Windows-Klängen liegen nicht im Repo (fremde Klänge) – Prüfsumme und ffprobe-Ausgabe stehen im Log.

## Kommandos und Testergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~TonMedienT0732Tests"` → Bestanden 6/6 (im Beleg); dazu `W4_Core08_Media|MediaRunIntoTests|T0715_HuelleE3Tests|T0726OffenRot|AudioEditTests|AudioMultiTests|FilmCutTests|TonMedienT0732Tests` → Bestanden 29/29
- App (`Trendo.App.Tests.exe -class …`, je Klasse Failed 0): `TonT0732_Ui` 3, `MedienStapelT0732_Ui` 2, `W4_Core08_Ui` 1, `AudioEditor_Ui` 2, `AudioBuses_Ui` 1, `AudioFoley_Ui` 1, `AudioMidi_Ui` 1, `AudioMultiRecord_Ui` 1, `AudioPluginHost_Ui` 1, `AudioPlugins_Ui` 1, `AudioPro_Ui` 2, `AudioQa_Ui` 1, `AudioSfx_Ui` 1, `DesignPaket1_Ui` 31, `Einfachheit_Ui` 2, `EveryButton_Alltag` 1, `MediaEditTests` 4, `MusikDaw_Ui` 1, `OfficeNetzFeedbackTests` 10, `ProgramFrameTests` 4, `ProgramHeadDesign_Ui` 6, `ProgramShellUiTests` 3, `StudiosKreativ3D_Ui` 30, `T0697ComicPodcast_Ui` 8, `T0710_MenuBar_Ui` 8, `T0715_HuelleE3_Ui` 5, `T0728_Bedienung_Ui` 7
- `EveryButton_Alltag` fand beim ersten Lauf einen echten Fehler: „Ordner öffnen“ stürzte ab, wenn als Ausgabeordner eine Datei gewählt wurde – behoben (Ordnerwahl lehnt Dateien ab, Start und Öffnen melden es verständlich), danach grün.
- Rot-Grün: die neuen Tests lassen sich auf dem alten Stand nicht einmal übersetzen (`MicRecorder`, `MediaBatch`, `AuRecordStart`, `AuFx`, `MediaBatchAdd` fehlen dort).
- Bildprüfung: `tools/Trendo.Render` für `ton` (Profi, leere Spur) und `kreativ-medien` angesehen – Menüband „Aufnehmen“, Stapel-Karte oben in der Werkbank, Dunkel-Gold.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: **VOLLWERTIG** (Aufnahme in leere Spur bzw. Stapel aus leerer Werkbank → echte Dateien, UI-Tests)
2. Fremddatei importieren/bearbeiten/exportieren, Fremdprogramm öffnet: **TEILWEISE** – Windows-Klänge → MP3/FLAC, geöffnet mit ffprobe (fremdes Programm); in einem Abspieler mit Oberfläche nicht geöffnet
3. Abbruch ≤ 30 s Verlust: **TEILWEISE** – Stapel: nie halbe Dateien (Zwischendatei), fertige Ergebnisse bleiben; die Stapel-Liste selbst wird nicht in der Notsicherung geführt. Aufnahme: bei Absturz von Trendo während der Aufnahme bleibt die WAV in `%TEMP%\trendo-audio-<pid>` ohne abgeschlossenen Kopf – nicht automatisch wiederhergestellt
4. Claude/KI-Fachaufgabe: **NICHT GETESTET** (in diesem Paket nicht angefasst; K-Claude wie bisher)
5. Klick bis echte Datei: **VOLLWERTIG** für den Stapel (Ablegen → Ordnerwahl → Start → MP3s, echtes FFmpeg) und die Aufnahme (Start → Stopp-Klick → WAV, echtes FFmpeg, Mikrofon ersetzt)
6. Einfach/Profi, Kürzel, Rechtsklick, Ablegen: **TEILWEISE** – Leertaste und Ablegen auf der Arbeitsfläche neu; Rechtsklick-Menü auf der Welle weiter nicht vorhanden

## Nicht geprüft
- Echtes Mikrofon: **BLOCKIERT** – auf diesem PC meldet FFmpeg/DirectShow kein Aufnahmegerät; Voraussetzung: angeschlossenes Mikrofon. Der DirectShow-Weg (`MicRecorder.DshowInput`) ist nur als Argumentliste geprüft.
- Wiedergabe über mpv im Leertasten-Test (Abspieler ersetzt).
- Großer Stapel mit Videos über Stunden, Speicherplatzmangel während des Stapels.

## Offene Punkte / Risiken
- Rechtsklick-Menü und Strg+C/X/V auf der Welle (Audit Punkt 4) offen.
- Effekt-Werte gelten eine Sitzung lang (nicht im Dokument gespeichert); jeder angewandte Schritt speichert seinen Wert im Dokument.
- Stapel: Qualität/Zielgröße weiter fest je Aufgabe.

## Geänderte Dateien
`src/Trendo.App/MainWindow.AudioEditor.cs`, `src/Trendo.App/MainWindow.AudioRecord.cs` (neu), `src/Trendo.App/WaveView.cs`, `src/Trendo.App/MainWindow.W4Media.cs`, `src/Trendo.App/MainWindow.MediaBatch.cs` (neu), `src/Trendo.Core/MediaStudio.cs`, `src/Trendo.Core/MediaBatch.cs` (neu), `src/Trendo.Core/Media/MicRecorder.cs` (neu), `tests/Trendo.Core.Tests/TonMedienT0732Tests.cs` (neu), `tests/Trendo.App.Tests/TonMedienT0732UiTests.cs` (neu), `reports/T-0732/*`, `tasks/T-0732-ton-medien*.md`. Keine gemeinsamen Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer/neuerungen) geändert.

**Status:** umgesetzt und gezielt getestet; Volltest steht aus (Hauptsitzung).
