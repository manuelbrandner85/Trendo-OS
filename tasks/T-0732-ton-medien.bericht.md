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
`beleg.sh` → `rohlog.txt`, `beleg.log`, `aufnahme.wav`, `sha256.txt`. FFmpeg 9.0.2 (gyan.dev). Zehn Windows-Systemklänge (`C:\Windows\Media\Alarm01–10.wav`, echte Fremddateien) in einem Lauf nach MP3 (3,1 s laut `beleg.log`/`rohlog.txt`, 30 Fortschrittsmeldungen; die frühere Angabe „1,8 s“ war falsch), jede MP3 mit ffprobe geöffnet; Abbruch bei 9 % → 0 Dateien im Ausgabeordner; Ton-Kette Alarm01 → −6/+6 dB, Echo 0,5 s → FLAC → mit ffprobe geöffnet → wieder eingelesen, 0 Samples Abweichung (das zeigt nur, dass die FLAC-Ausgabe verlustfrei ist und sich in einem fremden Programm öffnen lässt – **nicht**, dass die Effekt-Werte wirken; das belegt erst der UI-Test `TonT0732_Ui.Effects_with_values`); Aufnahme bis Stopp nach 4,3 s, Pegel −18,1 dB, WAV von ffprobe gelesen. MP3/FLAC aus den Windows-Klängen liegen nicht im Repo (fremde Klänge) – Prüfsumme und ffprobe-Ausgabe stehen im Log.

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
3. Abbruch ≤ 30 s Verlust: **TEILWEISE** – Stapel: nie halbe Dateien (Zwischendatei), fertige Ergebnisse bleiben; die Stapel-Liste selbst wird nicht in der Notsicherung geführt. Aufnahme: normales Schließen während der Aufnahme verliert nichts mehr (siehe „Gegenprüfung behoben“); bei einem **Absturz** von Trendo während der Aufnahme bleibt die WAV weiter in `%TEMP%\trendo-audio-<pid>` ohne abgeschlossenen Kopf – nicht automatisch wiederhergestellt
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
`src/Trendo.App/MainWindow.AudioEditor.cs`, `src/Trendo.App/MainWindow.AudioRecord.cs` (neu), `src/Trendo.App/WaveView.cs`, `src/Trendo.App/MainWindow.W4Media.cs`, `src/Trendo.App/MainWindow.MediaBatch.cs` (neu), `src/Trendo.Core/MediaStudio.cs`, `src/Trendo.Core/MediaBatch.cs` (neu), `src/Trendo.Core/Media/MicRecorder.cs` (neu), `tests/Trendo.Core.Tests/TonMedienT0732Tests.cs` (neu), `tests/Trendo.App.Tests/TonMedienT0732UiTests.cs` (neu), `reports/T-0732/*`, `tasks/T-0732-ton-medien*.md`. Keine gemeinsamen Dateien (MenuBar, StudioCatalog, requirements.yaml, installer/neuerungen) geändert. Mit der Gegenprüfung zusätzlich geändert: `src/Trendo.App/MainWindow.ProgramShell.cs` (Closing-Hook, wenige Zeilen) und `.gitattributes` (eine Zeile).

## Gegenprüfung behoben (17.11.2026)

Die unabhängige Gegenprüfung (keine Freigabe: 1 × mittel, 4 × niedrig) ist abgearbeitet. Neue Tests mit **echtem FFmpeg**, ersetzt ist nur das Mikrofon durch eine FFmpeg-Tonquelle. Alle drei neuen UI-Tests liefen auf dem alten Stand **rot** (Quellcode auf `364a5a08b` zurückgesetzt, Tests unverändert) und auf dem neuen **grün**.

| Befund | Änderung | Nachweis |
|---|---|---|
| **mittel**: Aufnahme geht beim Schließen des Fensters verloren | `AuRecordStopOnClose()` (`MainWindow.AudioRecord.cs`): FFmpeg bekommt wie bei „Stopp“ ein „q“, die WAV wird sauber abgeschlossen, nach „Medien\Aufnahmen“ gelegt und ins Dokument übernommen. Der Closing-Hook der Hülle (`MainWindow.ProgramShell.cs`) ruft das **vor** `ProgramAutosaveAll()` auf. Dadurch sichert die Hülle das Dokument mit der Aufnahme, und beim nächsten Start bietet sie es zum Wiederherstellen an. Nicht, wenn das Schließen abgelehnt wird (Einrichtung läuft, `MayClose`). | `TonT0732_Ui.Close_during_recording_keeps_it`: schließt mitten in der Aufnahme. Danach wird geprüft: RIFF- und data-Größe im WAV-Kopf stimmen, `AudioFile.Read` und FFmpeg lesen 2–5 s, das Dokument hat „Datei einfügen“ als Rückgängig-Schritt, die Notsicherung `programme\sicherung\*.tdoc` enthält die Aufnahme, Schließen dauert < 5 s. Alt: rot („Assert.False“, Aufnahme läuft weiter, nichts gesichert). |
| niedrig: laufender Stapel wird beim Schließen nicht abgebrochen | `MediaBatchStopOnClose()` (`MainWindow.MediaBatch.cs`): bricht `_mbCts` ab (FFmpeg wird beendet) und wartet bis zu 15 s, bis `MediaBatch` die Zwischendatei gelöscht hat. Hängt etwas, löscht Trendo die eigenen `*.~xxxxxxxx.teil.*` im Ausgabeordner selbst. Gleicher Hook wie oben. | `MedienStapelT0732_Ui.Close_cancels_batch`: zwei 40-s-Videos, Schließen mitten in der ersten Datei, während die Zwischendatei nachweislich existiert. Danach läuft der Stapel nicht mehr, alle Dateien sind „abgebrochen“ und der Ausgabeordner ist leer. Alt: rot (Stapel lief nach dem Schließen weiter). |
| niedrig: Aufnahme landet im gerade offenen statt im gemerkten Dokument | `AuRecordInsert()`: Das gemerkte `_auRecDoc` wird kurz aktiv (Marke, Mehrspur und Rückgängig gelten dort), danach kommt das vordere Dokument wieder nach vorn. Die Meldung nennt das Ziel („eingefügt in „…““). Wurde das Dokument inzwischen geschlossen, entsteht ein neues. | `TonT0732_Ui.Recording_goes_to_started_document`: Aufnahme in A starten, B öffnen und nach vorn holen, Stopp. Danach bleibt B vorn und unverändert, A hat „Datei einfügen“, die richtige Länge und die richtige Rückgängig-Stelle. Alt: rot (Einfügeschritte in B). |
| niedrig: Bericht „1,8 s“ statt „3,1 s“, FLAC-Aussage zu weit | Zahl auf 3,1 s korrigiert (so in `beleg.log`/`rohlog.txt`). Klargestellt, dass die FLAC-Rundreise nur Verlustfreiheit und Öffnen im Fremdprogramm zeigt, nicht die Wirkung der Effekt-Werte; die belegt `Effects_with_values`. | dieser Bericht, Abschnitt „Echtbeleg“ |
| niedrig: `sha256sum -c` scheitert mit `core.autocrlf=true` | `.gitattributes`: `reports/T-0732/** -text` (wie T-0724/T-0727). Die Blobs hatten schon die Original-Bytes (LF), daher sind keine Belegdateien geändert. | frischer Klon mit `core.autocrlf=true`: `sha256sum -c sha256.txt` → 4 × OK |

**Tests nach der Behebung:** `dotnet build Trendo.sln -c Release` → 0 Fehler, 0 Warnungen. App-Lauf 1 (24 Klassen wie in der Gegenprüfung, darunter `TonT0732_Ui` mit jetzt 5 und `MedienStapelT0732_Ui` mit jetzt 3 Tests) → **72/72** grün. App-Lauf 2: `StudiosKreativ3D_Ui`, `DesignPaket1_Ui`, `T0697ComicPodcast_Ui` sowie alle Klassen, die Notsicherung und Schließen der Hülle prüfen (`BueroAusbau_Ui`, `EinfachBedienen_Ui`, `OfficeRecoveryUiTests`, `ProgramCrashUiTests`, `Studio3DMeshEditUiTests`, `T0708_HuelleEtappe1_Ui`, `T0711_HuelleEtappe1b_Ui`, `T0714_HuelleE2_Ui`, `T0716_HuelleE4_Ui`, `T0717_HuelleE5_Ui`, `T0730_OfficeRecover_Ui`) → **151/151** grün. Core `TonMedienT0732Tests` → 6/6 (Core-Code unverändert).

**Weiter offen:** Ein **Absturz** von Trendo während der Aufnahme (Prozess hart beendet) hinterlässt die WAV ohne abgeschlossenen Kopf in `%TEMP%\trendo-audio-<pid>` und wird nicht automatisch wiederhergestellt. Kriterium 3 bleibt dafür TEILWEISE. Das echte Mikrofon ist weiter BLOCKIERT.

**Status:** umgesetzt und gezielt getestet; Volltest steht aus (Hauptsitzung).
