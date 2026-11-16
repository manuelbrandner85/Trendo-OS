# Audit 6 – Querschnitt: Architektur, KI-Anbindung, Datensicherheit, Speicher- und Produktionssystem

Stand: Arbeitskopie `studios-aufraeumen`, Commit `ba52927a8` (Version 0.9.103, `src/Trendo.App/Trendo.App.csproj:10`). Nur gelesen, nichts gebaut, kein Test gestartet.
Pfade relativ zum Repo. Urteile: VOLLWERTIG / TEILWEISE / FEHLT / BLOCKIERT / NICHT GETESTET. Was nicht belegt ist, steht als „nicht geprüft“.

## Übersicht

| Nr. | Bereich | Urteil | Schwerster Befund |
|---|---|---|---|
| 1 | Programm-Hülle, Speichern, Wiederherstellung | TEILWEISE | Notsicherung nicht atomar, `.autosave`-Nebendatei wird nie angeboten, keine Sperre gegen zweites Fenster, Rückgängig kopiert das ganze Dokument |
| 2 | Claude als Fachassistent | TEILWEISE | Von 110 Aktionen liefern 67 nur Text/Markdown, 25 sind gesperrt, 18 erzeugen ein Fachformat; keine Stufe „Ready“ außer Mail-Entwurf |
| 3 | Externe Fachadapter | TEILWEISE | Fehlen wird meist sauber gemeldet; echte Läufe sind einmalige Belege, in der Testsuite fast überall ersetzt |
| 4 | Tests und Nachweise | TEILWEISE | 243 Ersatz-Haken, 839 Zuweisungen in Tests; `requirements.yaml` passt nicht zum Code |
| 5 | Sicherheit und Rechte | TEILWEISE | Rechteprüfung in der Desktop-App faktisch wirkungslos (immer Besitzer); Claude-Zustimmung hängt an Beschriftungs-Mustern |
| 6 | Architektur | TEILWEISE | `MainWindow` = 338 Teildateien, 76 446 Zeilen; mindestens 15 getrennte Rückgängig-Systeme; Doku nennt 58 Studios statt 83 |
| 7 | Installation, Update, Deinstallation | NICHT GETESTET (aktueller Stand) | Letzter echter Setup-Lauf bei 0.9.7/0.9.10, heute 0.9.103; keine Codesignatur; kein CI-Testlauf |

---

## 1. Programm-Hülle (Speichern, Autosave, Wiederherstellung, Versionen, Undo)

**Reichweite:** Nur 40 Aufrufe `RegisterProgram(` gegenüber 125 Programmen im Katalog (`docs/programme/katalog.json`, 125 Einträge). Alle anderen Programme (Text, Tabelle, Präsentation, CAD, 3D, Haushaltsbuch, PDF, Kalender …) haben eigene Speicher- und Rückgängig-Wege (siehe Punkt 6).

| Kriterium | Befund | Beleg | Urteil |
|---|---|---|---|
| Speichern atomar | `Save` schreibt erst `.tmp`, dann `SafeFile.Move` mit bis zu 20 Versuchen | `src/Trendo.Core/Programs/ProgramDocument.cs:145`, `src/Trendo.Core/SafeFile.cs:10-16` | VOLLWERTIG |
| Autosave-Takt | 1,5 s nach der letzten Änderung (entprellt), zusätzlich alle 30 s und beim Schließen | `src/Trendo.App/MainWindow.ProgramShell.cs:228`, `:85`, `:88` | VOLLWERTIG |
| Autosave blockiert die Oberfläche | Läuft im `DispatcherTimer` auf dem UI-Faden, serialisiert das ganze Dokument eingerückt und schreibt synchron | `MainWindow.ProgramShell.cs:228`, `ProgramDocument.cs:127-136` | TEILWEISE |
| Notsicherung atomar | **Nein.** `Autosave()` schreibt direkt mit `File.WriteAllText` in die Zieldatei. Absturz mitten im Schreiben ergibt eine halbe Sicherung | `ProgramDocument.cs:223` | FEHLT |
| Kaputte Sicherung auf der Startseite | Die Startseite ruft für jede Sicherung `ProgramDocument.Load(file).Title` ohne Abfangen an dieser Stelle auf; `Load` wirft bei unlesbarer Datei `InvalidDataException`. Ob ein äußerer Rahmen das fängt: nicht geprüft | `MainWindow.ProgramShell.cs:525`, `ProgramDocument.cs:205-206` | TEILWEISE (Risiko) |
| Wiederherstellen-Angebot | Vorhanden für **nie gespeicherte** Dokumente aus `programme/sicherung` (Karte „Wiederherstellen“ mit Wiederherstellen/Verwerfen) | `MainWindow.ProgramShell.cs:517-527`, `ProgramDocument.cs:227-228` | TEILWEISE |
| Nebendatei `<datei>.tdoc.autosave` | Wird geschrieben, wenn automatisches Speichern aus ist oder scheitert, aber **nirgends zum Wiederherstellen angeboten**: `AutosaveFile` wird nur beim Schreiben und Löschen benutzt, `ProgramOpen` prüft sie nicht | `ProgramDocument.cs:147`, `:215`, `:221`; `MainWindow.ProgramShell.cs:110-119` | FEHLT |
| Wiederherstellung getestet | Ein Test, im selben Prozess, ohne echten Abbruch: `ProgramShellUiTests.Autosave_restore` (`tests/Trendo.App.Tests/ProgramShellUiTests.cs:56-68`). Kein Test mit beendetem Prozess oder halb geschriebener Datei | – | NICHT GETESTET (echter Absturz) |
| Wiederherstellen löscht sofort | Beim Klick wird die Sicherung gelöscht, bevor das Dokument wieder gespeichert ist | `MainWindow.ProgramShell.cs:526` | TEILWEISE |
| Office (Text, Tabelle, Präsentation) | Eigenes zweites System: je Art **eine** Datei unter `wiederherstellen/` (ein Platz je Art, nicht je Dokument), Banner mit Wiederherstellen/Verwerfen | `src/Trendo.App/MainWindow.OfficeRecovery.cs:15-16`, `:19-25`, `:50-57`; Test `OfficeRecoveryUiTests.cs` | TEILWEISE |
| Versionen | Höchstens 20 je Datei unter `.versionen/<Name>`, beim Autosave höchstens alle 10 Minuten; Schreiben nicht atomar, Fehler werden verschluckt | `ProgramDocument.cs:151`, `:157-174`, `:170`; `MainWindow.ProgramShell.cs:73` | TEILWEISE |
| Rückgängig-Grenze | 100 Schritte, **jeder Schritt = vollständiges Dokument als JSON-Text**; `Edit` serialisiert zweimal je Änderung | `ProgramDocument.cs:38`, `:51`, `:77-80` | TEILWEISE |
| Speicherbedarf große Dokumente | Künstlerstudio legt jede Ebene als Base64-PNG ins Dokument; jeder Pinselstrich schiebt das ganze Dokument auf den Stapel. Bei z. B. 20 MB Dokument sind 100 Schritte rund 2 GB Text im Speicher (Rechnung, nicht gemessen). Dazu 20 Versionen in voller Größe auf der Platte und Vollschreiben alle 1,5 s | `src/Trendo.App/MainWindow.Kuenstlerstudio.cs:164-176`, `:217-221` | FEHLT (kein Limit nach Größe, keine Deltas) |
| Rückgängig nach Neustart | Stapel nur im Speicher, nach Schließen weg (Ersatz: Versionen) | `ProgramDocument.cs:51` | TEILWEISE |
| Dateiformat-Version | `"version": 1` wird geschrieben, beim Laden **nicht gelesen**; keine Migration, keine Ablehnung neuerer Fassungen | `ProgramDocument.cs:131`, `:202-212` | FEHLT |
| Schema-Prüfung der Daten | Keine. `dokument_setzen` von Claude schreibt beliebiges JSON; ein bekannter Absturz daraus ist im Code vermerkt | `MainWindow.ClaudeBridge.cs:194-199`, `ProgramDocument.cs:9` | FEHLT |
| Zwei Fenster / zwei Prozesse | Im selben Prozess wird ein schon offenes Dokument wiederverwendet. Eine Dateisperre, Sperrdatei oder Einzelinstanz-Prüfung wurde **nicht gefunden** (Suche nach `Mutex`, `FileShare.None`, `.lock`, Einzelinstanz in `src/Trendo.App`). Zwei Trendo-Prozesse überschreiben sich still (letzter gewinnt) | `MainWindow.ProgramShell.cs:113-114` | FEHLT |
| „Zuletzt geöffnet“ | `zuletzt.json` wird direkt überschrieben (nicht atomar); bei kaputter Datei leere Liste | `ProgramDocument.cs:242`, `:248` | TEILWEISE |

**Urteil Punkt 1: TEILWEISE.** Normales Speichern ist solide, der Notfallweg nicht.

**Maßnahmen**
1. Notsicherung, Versionen und `zuletzt.json` über denselben Weg wie `Save` schreiben (Zwischendatei, dann verschieben). Aufwand klein, Risiko gering.
2. `.autosave`-Nebendatei beim Öffnen erkennen und anbieten; kaputte Sicherungen überspringen statt werfen; Sicherung erst nach erfolgreichem Speichern löschen. Aufwand klein, Risiko gering.
3. Test mit echtem Prozessabbruch (Programm starten, ändern, Prozess beenden, neu starten, Wiederherstellen prüfen) und Test mit halb geschriebener Datei. Aufwand klein bis mittel.
4. Formatversion beim Laden auswerten, Migrationskette, neuere Fassungen ablehnen statt beschädigen. Aufwand mittel, Risiko mittel.
5. Große Binärdaten (Ebenen, Bilder) aus dem JSON in Nebendateien oder einen Container auslagern; Rückgängig als Delta oder mit Speicherobergrenze. Aufwand groß, Risiko hoch (Dateiformat aller Zeichenprogramme).
6. Sperrdatei je geöffnetem Dokument plus Einzelinstanz-Prüfung. Aufwand klein bis mittel.
7. Speichern vom UI-Faden nehmen. Aufwand mittel, Risiko mittel (Wettläufe).

---

## 2. Claude als Fachassistent

### 2.1 Es gibt vier getrennte Wege, nicht eine Kette

| Weg | Was er tut | Beleg |
|---|---|---|
| A. `StudioWorkflow` + 110 Adapter | Auftrag → Felder → Plan → ggf. Freigabe → Datei im Projektordner (`ProjectStore`) | `src/Trendo.Core/PromptAction/StudioWorkflow.cs:52`, `:403`, `:557`; Liste `IntelligencePromptModel.cs:28-51`, `SystemPromptAdapter.cs:251-297` |
| B. Programm-Assistent (`AssistPlanner`) | Regelbasierter Planer ohne KI, baut aus den Menüband-Befehlen des offenen Programms einen Plan | `src/Trendo.Core/Programs/Assist.cs:9-12`, `src/Trendo.App/MainWindow.Assist.cs:39-49` |
| C. Claude-Brücke (MCP) | Claude Code bedient das laufende Programm: lesen, klicken, Befehl, `dokument_lesen`/`dokument_setzen` | `src/Trendo.App/MainWindow.ClaudeBridge.cs:60-200` |
| D. Plan-Zeilen `[[trendo:…]]` | Ersatz-KI/Plan mit Knopf | `MainWindow.ClaudeBridge.cs:261-267`, `MainWindow.ClaudeChat.cs:545` |

Weg A schreibt in Projektordner, Weg B/C arbeiten im offenen `.tdoc`. Die Ergebnisse von A landen nicht im offenen Dokument (Ausnahme: `wohnung.flaeche` gibt zusätzlich ein `tdoc` aus, `MeasurePromptAdapter.cs:60`). Die in `CLAUDE.md` beschriebene eine Kette „Prompt → … → echter Fachadapter → echte Dateien → QA → Vorschau → Export“ gibt es so nicht durchgängig.

### 2.2 Zählung der 110 Aktionen (aus `OutputKinds`, `DeclaredLevel`, `Produce`)

Der Test `PromptActionTests.cs:1702` hält 110 fest. 96 Klassen, davon `GateAdapter` 15-mal instanziiert.

| Gruppe | Anzahl | Bedeutung |
|---|---|---|
| Nur Text (`["txt"]`) | 54 | Textdatei mit Rechenergebnis, Liste, Hinweis oder Plan |
| Nur Markdown (`["md"]`) | 13 | Plan, Gliederung, Fragebogen, Entwurf |
| **Summe Text/Plan** | **67** | |
| Gesperrt, keine Datei (`[]`) | 25 | 15 `GateAdapter` + 10 eigene Sperrklassen |
| Gesperrt mit Format | 1 (in den 25 nicht enthalten) | `mail.senden` nennt `eml`, ist aber `Blocked` (`MailPromptAdapter.cs:33`) – zählt unten zu „Fachformat“ nicht mit |
| **Fachformat** | **18** | siehe Liste |

Rechnung: 67 Text/Plan + 25 gesperrt + 18 Fachformat = 110 (mit `mail.senden` als gesperrt statt Fachformat: 67 + 26 + 17).

**Stufen:** 84 `Partial`, 1 `Ready`, 25 `Blocked` (`grep DeclaredLevel`: 84 / 1 / 11 Klassen, davon `GateAdapter` 15-fach). Die einzige `Ready`-Aktion ist `mail.entwurf` (`MailPromptAdapter.cs:10`).

**Fachformat-Aktionen (18):**

| Studio | Aktion | Ausgabe | Fundstelle |
|---|---|---|---|
| STUDIO-LETTER-01 | brief.schreiben | docx | `LetterPromptAdapter.cs:6` |
| SYS-10 | tabelle.rechnen | xlsx | `SheetPromptAdapter.cs:8` |
| STUDIO-DECKDESIGN-01 | praesentation.gestalten | pptx, txt | `DeckScanPromptAdapters.cs:8` |
| SYS-09 | mail.entwurf | eml (Ready) | `MailPromptAdapter.cs:4` |
| SYS-09 | mail.senden | eml (Blocked) | `MailPromptAdapter.cs:33` |
| SPEC-11 | rechnung.schreiben | html | `BusinessPromptAdapter.cs:8` |
| CORE-07 | software.anlegen | html, py, cs, js | `SoftwarePromptAdapter.cs:4` |
| X-03 | seite.schreiben | html, json | `SystemPromptAdapter.cs:46` |
| CORE-06 | spiel.browser | html, js, md | `GamePromptAdapter.cs:6` |
| CORE-06 | spiel.unreal | uproject (nur Projektdatei) | `GamePromptAdapter.cs:33` |
| CORE-05 | modell.bauen | obj, mtl, png, json | `ModelPromptAdapter.cs:6` |
| CORE-08 | medien.umwandeln | jpg, mp4, mp3, gif (FFmpeg) | `MediaPromptAdapter.cs:4` |
| SPEC-01 | wohnung.flaeche | svg, tdoc | `MeasurePromptAdapter.cs:60` |
| STUDIO-LOGO-01 | logo.entwerfen | svg | `KreativPromptAdapters.cs:34` |
| STUDIO-COMIC-01 | comic.seite | svg | `KreativPromptAdapters.cs:110` |
| STUDIO-ANIM-01 | animation.titel | svg | `KreativPromptAdapters2.cs:38` |
| STUDIO-FILM-01 | film.drehbuch.geruest | fountain (nur Struktur) | `StudyPromptAdapter.cs:156` |
| SPEC-06 | karte.anlegen | csv | `StudyPromptAdapter.cs:25` |
| STUDIO-TRAIN-01 | ki.trainieren | json | `KreativPromptAdapters3.cs:109` |

(19 Zeilen, weil `mail.senden` mit aufgeführt ist.)

**Beispiele für „nur Plan“ trotz Fachversprechen** (streng nach Leitsatz als Scheinfunktion zu werten):
- `musik.lied.planen`: fester Textbaustein „Liedplan“ mit `[Intro] … [Outro]`, Verweis „im Studio erzeugen“ (`StudyPromptAdapter.cs:127-151`).
- `pdf.auftrag`: prüft Seitenangaben und schreibt die Bedienschritte als Text, „Die KI bearbeitet keine Dateien selbst“ (`BueroPdfPromptAdapters.cs:7-8`, `:19`).
- `bild.auftrag`, `video-ki.szenenplan`, `fotos.plan`, `konsole.plan`, `code.projekt`, `agent.entwurf`, `buch.gliederung`, `scan.auftrag`, `formular.zuordnen`, `kunst.vorlage`, `grusskarte.anlegen`, `lebenslauf.pruefen`: jeweils `txt` oder `md` (Fundstellen in `KreativPromptAdapters*.cs`, `BueroPromptAdapters.cs`, `DeckScanPromptAdapters.cs:58`, `KunstPromptAdapter.cs:9`).
- Hinweis: Unter den 54 Text-Aktionen sind auch Aktionen mit echter Wirkung nach Freigabe (z. B. `datei.laden`, `programm.installieren`, `programm.starten`, `datei.teilen`, `ConnectedAdapters.cs:349`, `:430`, `:675`, `:538`) und Fachrechnungen (Balken, Schaltung, Route). Sie liefern trotzdem nur ein Textprotokoll.

**Abdeckung:** 80 verschiedene Studio-Kennungen haben mindestens eine Aktion (Auswertung der Adapterliste), bei 83 Studios.

### 2.3 Was heißt „Partial“, gibt es „Full“?

- Die Aufzählung kennt nur `Ready, Partial, Blocked, NotImplemented` – **eine Stufe „Full“ gibt es nicht** (`Model.cs:23-26`).
- „Partial“ ist eine reine Selbstauskunft je Adapter (`DeclaredLevel`), kein gemessener Zustand. Ein Studio ist `Ready` nur, wenn **alle** seine Aktionen `Ready` sind (`CapabilityCatalog.cs:119`). Da fast jedes Studio eine gesperrte Zwillingsaktion hat, kann praktisch kein Studio `Ready` werden; selbst SYS-09 nicht, weil `mail.senden` gesperrt ist.
- Daneben existiert eine zweite, unabhängige Reife-Aufzählung `Capability { Ready, NotConnected, Blocked, SpecOnly, Rejected, InProgress }` aus `requirements.yaml` (`StudioRegistry.cs:6`). Zwei Reifesysteme nebeneinander.

### 2.4 Kann Claude das offene Dokument wirklich ändern?

- **Ja, über Weg C** für alle Programme der Programm-Hülle: `dokument_setzen` ruft `doc.Edit("Claude: …")`, als Rückgängig-Schritt, mit Sperre für Pfade mit „consent/einwillig/freigabe/approved“ (`MainWindow.ClaudeBridge.cs:186-200`). `befehl` führt jeden Menüband-Befehl aus (`:158-169`). Für die 3D-Seite gibt es einen Sonderweg (`:186-193`).
- Voraussetzungen: Claude Code installiert und angemeldet, Schalter „Claude arbeitet selbständig ohne Klicks“ an (`MainWindow.ClaudeBridge.cs:22`, `:43`). Im Testmodus ist er aus.
- **Programme mit Befehlen/Dokument (40 Anmeldungen, 39 verschiedene Schlüssel):** Audio (AUK), Bild (BK), Video (VK), Lebenslauf, Konsole, Präsentations-Design, Scan, Film, Formulare, Karten, Zusammenfassung, Logo, Podcast, Comic, Buch, Animation, Fotos, Bild-KI, Video-KI, Agenten, Modelle, Training, Code-KI, Bilder/Vision, Quellen, Tagesplaner, Kunst, PDF-Werkstatt, POD, Spiele (GK), Mode, Wohnung (AK), Schaltung (EK), Statik (TK), Messung (SK), Scan 3D (PK), Karte (MK), Fräse (CK), Verträge (Suche `RegisterProgram(` in `src/Trendo.App`).
- **Ohne Dokumentzugriff:** die übrigen rund 85 Programme. Dort kann Claude nur die Oberfläche bedienen (`seite_lesen`, `feld_setzen`, `klicken`). `RegisterAssistAdapter` für Programme außerhalb der Hülle ist definiert, wird aber **nirgends aufgerufen** (`MainWindow.Assist.cs:323`, einziger Treffer). Text, Tabelle, Präsentation, CAD, E-Mail haben also keine `AssistAction`s.
- Weg B (`AssistAction`) wird automatisch aus Menüband-Befehlen abgeleitet, **ohne Parameter** (`MainWindow.Assist.cs:48-49`). „Setze Titel auf X“ geht darüber nicht, nur „drücke Befehl“.
- Prüfung nach der Änderung: keine Schema-Prüfung des Dokuments (siehe 1). QA gibt es nur in Weg A, dort meist „Datei geschrieben und wieder gelesen“ (`ConnectedAdapters.cs:40-49`).

**Urteil Punkt 2: TEILWEISE.** Die Brücke ist echt und reicht für 39 Programme bis ins Dokument. Die 110 „KI-Aktionen“ sind überwiegend Text- und Planausgaben.

**Maßnahmen**
1. Text/Plan-Aktionen, die ein Produkt versprechen, an die echte Fachfunktion des Programms binden (PDF wirklich teilen, Lied wirklich erzeugen, Scan wirklich entzerren) oder ehrlich als „Hilfe/Anleitung“ umbenennen und aus der Zahl 110 herausnehmen. Aufwand sehr groß (67 Aktionen), Risiko mittel.
2. Wege A–D zusammenführen: Adapter-Ergebnis landet im offenen Dokument; ein Plan-/Freigabe-/Prüfweg. Aufwand groß, Risiko hoch.
3. JSON-Schema je Programm und Prüfung in `dokument_setzen`. Aufwand mittel bis groß.
4. Reifestufe messen statt erklären (Stufe aus bestandenem Ende-zu-Ende-Test), zweite Aufzählung entfernen. Aufwand mittel.
5. `RegisterAssistAdapter` für Text, Tabelle, Präsentation, CAD, 3D nutzen oder entfernen. Aufwand mittel je Programm.

---

## 3. Externe Fachadapter

**Erkennung (GRUNDSATZ-01):** zentral über `DependencyCatalog` (Katalog mit Lieferart, Quelle, bekannten Pfaden, `src/Trendo.Core/Dependencies.cs:33`, `:83-84`, `:131`) und `DependencyManager` (bekannte Pfade, gespeicherter Pfad, PATH, Registry „App Paths“, winget-Ablage, `Dependencies.cs:184`, `:196`, `:252`); Funktionsprüfung über `StudioTools.Verify` mit Exe und Prüfargument (`src/Trendo.Core/ToolCheck.cs:22-31`, `:36-37`).
**Lücke:** `StudioTools.Required` ordnet nur **7 Studios** Werkzeuge zu (`ToolCheck.cs:15-19`), bei 83 Studios. Godot, Unity, sd-cli, ACE-Step, Whisper, Node stehen dort nicht.

| Werkzeug | Angebunden in | Fehlt es → | Echter Lauf belegt | In Tests ersetzt durch | Urteil |
|---|---|---|---|---|---|
| Blender | `Dependencies.cs:83`, `ExternalApps.cs`, `MixamoBlender.cs`, Kreatur | KI-Aktion `modell.blender` gesperrt (`ModelPromptAdapter.cs:32`); Tests mit `BlenderFact` überspringen (`KreaturUndRegelnTests.cs:38`) | `reports/paket01/s2-17-blender-rundlauf.md`; Test „Kreatur-Baukasten: echter Blender …“ | `KreaturRunOverride`, `MixamoPrepOverride`, `Render3DOverride`, `EngineExportOverride` | TEILWEISE |
| Unreal | `UnrealGame.cs:187`, `MainWindow.ProgGameUnreal.cs` | Meldung „ohne Unreal klare Meldung“ (Test `GameUnrealUiTests.cs:16`); nie automatisch installiert (`FachprogrammTests.cs:270`) | Kein Beleg eines Unreal-Laufs unter `reports/paket01` gefunden (nur ein ausgewertetes Protokoll in `GameStage3Tests.cs:250`) – nicht geprüft | `UnrealRunOverride` (7 Zuweisungen), `UnrealEngineOverride` | NICHT GETESTET (Ende-zu-Ende) |
| Godot | `EngineTargets.cs`, `GameEngineSetup.cs`, `MainWindow.ProgGameEngines.cs` | „Blockiert: … nicht installiert“ (`MainWindow.ProgGameEngines.cs:75`) | `reports/paket01/s1-01-godot-lauf.md` (Godot 4.7.2, Import, Levelprüfung, Gegenprobe) | `EngineProbeOverride` (10), `EngineRunOverride`, `LibGodotOverride` | TEILWEISE |
| Unity | `EngineTargets.cs`, `GameEngineSetup.cs` | „Blockiert: Unity ist nicht installiert. … installierst du selbst“ (`MainWindow.ProgGameEngines.cs:75`) | `reports/paket01/s1-01-unity-lauf.md` (EditMode 3/3, PlayMode 1/1, Windows-Build 95,9 MB) | wie Godot | TEILWEISE |
| FFmpeg | 102 Dateien, u. a. `MediaPromptAdapter.cs:3` („Ohne ffmpeg keine Datei“) | Kein Ergebnis; Tests überspringen sichtbar (`W4MediaUiTests.cs:13`, `MediaEditTests.cs:58`) **oder kehren still zurück und gelten als grün** (`MusikSunoUiTests.cs:294`, `:349`) | Viele „echt mit FFmpeg“-Tests und Belege (`reports/paket01/s3-*`) | `FfmpegRunOverride` (11), `ExportOverride`, `ExportAsyncOverride`, `VideoAiFfmpegOverride`, `MsMashupRunOverride` … | TEILWEISE (bester Stand) |
| LibreOffice | `Dependencies.cs`, `ToolCheck.cs:24`, `Letters.cs` | Prüfung nur Dateiversion (kein Start, `ToolCheck.cs:24`); Test „Metadaten aus einem echten LibreOffice-PDF“ **übersprungen** (`reports/volltest-0.9.93/dotnet.log:6`) | Kein Lauf im jüngsten Volltest | – | NICHT GETESTET |
| Browser (Chrome/Edge) | `WebsitePreview.FindBrowser`, PDF-Druck | Tests überspringen (`WebsitePreviewTests.cs:29`, `Sys03UiTests.cs:90-125`) | „in echtem Chrome“-Tests (WebDesign) | `BrowsersOverride`, `WebAppLaunchOverride` | TEILWEISE |
| sd-cli (Bild/Video-KI) | `W2/KiLibrary.cs:58` (Paket mit SHA-256), `Media/GenVideo.cs:49` | nicht geprüft | Test „S3-13 echt: Wan 2.2 erzeugt einen Clip“, Beleg `reports/paket01/s3-13-ki-video-lauf.md` | `KiRunOverride`, `ImageAiOverride` (6), `VideoKiOverride`, `EditGenModelOverride` | TEILWEISE |
| ACE-Step | `MainWindow.Musik.cs:16`, `W2/*` | nicht geprüft | Einzelbeleg `reports/musik-echt/BERICHT.md`; Live-Test **übersprungen** („Musik-KI live …“, `MusikLiveTests.cs:14`) | `AceClientOverride` (22 Zuweisungen) | NICHT GETESTET (im Volltest) |
| Tesseract | `Dependencies.cs:131`, `Office/DocScan.cs` | Sauber: „◐ Tesseract ist nicht eingerichtet … Ohne entsteht eine PDF aus Bildern“ (`MainWindow.DocScan.cs:457`) | Test „Scan → durchsuchbares PDF mit Tesseract“ **übersprungen** (`reports/volltest-0.9.93/dotnet.log:7`) | `ScanTesseractOverride`, `VisionTesseractOverride`, `TesseractOverride` | NICHT GETESTET |
| Ollama | `Dependencies.cs:227`, `:343`, `OllamaClient.cs` | nicht geprüft | Nur mit `TRENDO_PC_LAUF=1` (`WebDesignStufe2UiTests.cs:260`) | `KiBackendsOverride` (18), `KiChatOverride` | NICHT GETESTET (im Volltest) |

**Übersprungene Tests im belegten Volltest 0.9.93** (`reports/volltest-0.9.93/dotnet.log:5-14`), Core 5 + App 4:
1. X-08: BlueZ sucht ohne zu koppeln …
2. SYS-10: Metadaten aus einem echten LibreOffice-PDF entfernt
3. SYS-10-P08: Scan → durchsuchbares PDF mit Tesseract
4. T-0223 T8: Rechtschreibung Deutsch
5. Briefe F3: Rechtschreibung im Brieftext
6. T-0306: mpv (echt)
7. SYS-03-P14: Seite als PDF (Chrome/Edge)
8. Musik-KI live: 30 s Lied erzeugen
9. T-0202: Klick-Audit über alle Seiten

Im jüngsten Lauf (T-0670, `reports/volltest-T-0670/volltest-0ff66ff50.txt`) sind es 5 Core + 6 App; die Namen der zwei zusätzlichen App-Tests stehen nicht im Beleg (nicht geprüft).

**Stille Grün-Meldungen:** Mindestens 15 Tests kehren ohne Werkzeug einfach zurück und zählen als bestanden statt als übersprungen, z. B. `GameCombatTests.cs:79`, `GameInventoryTests.cs:61`, `GameMenusTests.cs:31/41/52/61/79`, `GameplayTests.cs:79`, `GameVehicleTests.cs:64`, `Sys10Tests.cs:77`, `W4DevTests.cs:23`, `W4OssTests.cs:23`, `MusikSunoUiTests.cs:294/349`. Insgesamt 41 Stellen mit `… is null) return;` in `tests/`.

**Urteil Punkt 3: TEILWEISE.** Fehlende Werkzeuge werden überwiegend ehrlich gemeldet, nicht simuliert. Der durchgehende Weg ist aber nur für FFmpeg, Node und Chrome regelmäßig echt geprüft.

**Maßnahmen**
1. `StudioTools.Required` für alle 83 Studios pflegen und daraus die Anzeige „BLOCKIERT: X fehlt“ erzeugen. Aufwand mittel, Risiko gering.
2. Stille `return;` in Tests durch `Assert.Skip` ersetzen. Aufwand klein.
3. Einen festen „Echt-Lauf“ je Werkzeug als eigene Testkategorie mit Pflichtbeleg unter `reports/` je Release (Blender, Unreal, Godot, Unity, LibreOffice, Tesseract, ACE-Step, sd-cli, Ollama). Aufwand mittel bis groß, braucht den PC des Nutzers.
4. Unreal-Ende-zu-Ende (Projekt anlegen, kompilieren, paketieren) einmal echt belegen. Aufwand groß (Laufzeit), Risiko mittel.

---

## 4. Tests und Nachweise

**Umfang (jüngster Beleg `reports/volltest-T-0670/volltest-0ff66ff50.txt`):** Api 16, Core 2597 (5 übersprungen), App 1341 (6 übersprungen), Dauer App **42 min 26 s**, Core rund 1,5 min. Der Lauf selbst endete mit **2 Fehlern** in Core („Vorbild-Analysen …“, „QA-015 … echte Liste hat 71 Module“), Nachlauf 1 mit 1 Fehler, Nachläufe 2 und 3 grün (`core-nachlauf-1-wackler.txt`, `core-nachlauf-2-3.txt`).

**Ersatz-Haken:** 243 öffentliche `*Override`-Eigenschaften (242 in `src/Trendo.App`, 1 in `src/Trendo.Core/DesktopPlatform.cs:102`), davon 83 `static`. In `tests/` gibt es 839 Zuweisungen auf 231 verschiedene Haken; 195 von 373 App-Testdateien nutzen mindestens einen.

**Vollständige Liste nach Gruppen** (Datei in `src/Trendo.App/`, ohne Präfix `MainWindow.`):

| Gruppe | Haken (Datei:Zeile) | Ersetzt |
|---|---|---|
| Claude / KI | `ClaudeRunOverride`, `ClaudeStatusOverride`, `ClaudeOpenOverride` (Claude.cs:46/45/20); `ClaudeConsentOverride` (ClaudeBridge.cs:24); `ClaudeFallbackOverride`, `KiToolOverride`, `DictateOverride` (ClaudeFallback.cs:18/20/22); `ReviewOverride`, `ProbeOverride`, `SoundOverride`, `SpeakOverride` (ClaudeExtras.cs:323/387/103/438); `StudioMcpOverride` (ClaudeLos.cs:12); `ChatTrashOverride` (ClaudeChat.cs:50); `AgentAskOverride` (AgentTab.cs:24); `KiChatOverride`, `KeyValidatorOverride` (axaml.cs:983/1074); `KiBackendsOverride` (W2.cs:15); `OpenRouter*Override` ×3 (OpenRouter.cs:20-24); `KieWebOverride`, `KieDelayOverride` (Kie.cs:21/22); `KiSource*Override` ×3 (KiSourceSetup.cs:15-19); `KiDownloadOverride`, `KiRunOverride` (KiLibrary.cs:23/25); `KiStepOverride`, `NvidiaOverride` (KiPy.cs:15/17); `CvAiOverride`, `DeckAiOverride`, `WeekSummaryOverride`, `GamePromptOverride`, `GameJudgeOverride`, `WebDesignKiOverride`, `WebDesignVisionOverride` | Den Claude-Prozess (90 Zuweisungen allein für `ClaudeRunOverride`), Anmeldestatus, Zustimmung, lokale KI, Cloud-KI, Diktat, Sprachausgabe |
| Spiele / Engines | `EngineProbeOverride`, `EngineRunOverride` (ProgGameEngines.cs:16/18); `UnrealRunOverride`, `UnrealEngineOverride`, `NatureFetchOverride` (ProgGameUnreal.cs:16-20); `MixamoPrepOverride`, `MixamoRiggerOverride` (ProgGameMixamo.cs:20/22); `UploadOverride` (ProgGameRelease.cs:18); `GameRefFetchOverride`, `GameServiceReadyOverride` (ProgGameStage3.cs:18/20); `Lib*Override` ×4 (GameLibrary.cs:21-27); `ConsoleSendOverride`, `ConsoleGitOverride` (ConsolePort.cs:18/20) | Godot/Unity/Unreal-Aufrufe, Blender für Mixamo, Hochladen, Netzabrufe |
| 3D / CAD / Technik | `Render3DOverride` (Studio3DPro.cs:29); `EngineExportOverride` (Studio3DEngines.cs:13); `AssimpRunOverride`, `AssimpDllOverride`; `F3dRunOverride`, `F3dExeOverride`; `GltfRunOverride`, `GltfValidatorOverride`, `GltfTransformOverride`; `Open3dRunOverride`; `ScanToolOverride`, `ScanFfmpegOverride`; `IfcRunOverride`, `IfcViewDirOverride`, `IfcViewScriptOverride`; `XrFixOverride`; `DiscoverOverride`, `SliceOverride` (Print3DPro.cs:21/23); `CncPhotoOverride`; `KreaturRunOverride`; `GarmentRunOverride`; `SewingDirOverride`, `SewingRunOverride`; `ObjectsRunOverride`; `MapHttpOverride`, `KartenDirOverride`, `VectorScriptOverride` | Blender-Render, assimp, F3D, glTF-Prüfer, Open3D, Photogrammetrie, IfcOpenShell, Slicer, Drucker-Suche |
| Video / Film | `ExportOverride`, `ExportAsyncOverride`, `EncodersOverride`, `PlayFramesOverride` (VideoSchnitt.cs:134/542/545/447); `PreviewSourceOverride`, `PreviewAudioOverride`, `PreviewMixOverride` (VideoPreview.cs:19-23); `FfmpegRunOverride` (FilmDelivery.cs:12); `SubsOverride`; `EditGenModelOverride`; `EditLipWordsOverride`, `EditLipMouthOverride`; `CreditsFontOverride`; `ExportRecycleOverride`; `AnimVideoOverride`, `ArtVideoOverride`, `VideoAiFfmpegOverride`, `ImageAiOverride` | **Das Rendern/Exportieren des Videoschnitts selbst**, Vorschau, FFmpeg, Untertitel, Bild-/Video-KI |
| Musik / Audio | `AceClientOverride` (Musik.cs:16); `AceBaseReadyOverride`; `MusicLyricsOverride`, `SongRecycleOverride`, `GpuMemoryOverride`, `SongExportOverride` (MusikStudio.cs:39/40/132/566); `CoverImageOverride`, `CoverTranslateOverride`, `EraSoundOverride`, `ReplaceAudioOverride`; `OwnVoiceRunOverride`, `OwnVoiceMixOverride`; `MsMashupRunOverride`, `MsCropRunOverride`, `MsSpeechTtsOverride`, `MsSpeechMixOverride`; `StemSplitOverride`, `NotenRunOverride`; `MicRecordOverride`, `MicStreamOverride`, `MelodyOverride`, `VoiceAnalyseOverride`; `MasterRunOverride`; `SongAnalyseOverride`, `SongFactsFetchOverride`; `SingersOverride`, `WordsOverride`, `ThemeOverride`; `SectionsOverride`, `PexelsFetchOverride`, `StockDownloadOverride`; `VideoKiOverride`, `VideoCheckOverride`, `VisionCheckOverride`; `AudioOverride`, `MusicFoldersOverride`; `AuPluginRunOverride`, `PluginRootsOverride`; `TodayOverride` | ACE-Step (22 Zuweisungen), Stimmtrennung, Mikrofon, Mastering, Tonausgabe, Plugin-Host |
| Web / Netz | `WebDesign*Override` ×10 (WebDesign*.cs); `FigmaHandlerOverride`; `AxeDirOverride`, `AxeRunOverride`; `NetlifyHandlerOverride`, `GhExeOverride`, `ConnRunOverride`, `ClaudeHomeOverride` (Connections.cs:15-21); `GitHub*Override` ×4 (Programme.cs:19/129/131/268); `OssSearchOverride`, `OssReadmeOverride`; `ExtFetchOverride`; `ReaderFetchOverride`; `MediaFetchOverride`, `IconFetchOverride`, `WebIconFetchOverride`; `BibHttpOverride`; `PciFetchOverride`; `VollpaketOnlineOverride`; `DownloadRunOverride`; `PodWebOverride`, `PodDelayOverride`, `PodPngOverride`; `KittlCliOverride`, `KittlConsoleOverride`; `AutomationInstallOverride`, `AutomationHttpOverride`; `TvFetchOverride`, `TvLegalSearchOverride`, `TvVideoSearchOverride`, `TvVideoResolveOverride`; `ImapStreamOverride`, `SendOverride` (Pim.cs:35/36) | Jeden Netzzugriff: GitHub, Netlify, Figma, Printful, n8n, IMAP/SMTP, Downloads |
| System / Installation | `InstallOverride`, `AppPathOverride`, `ObsDirOverride` (Programs.cs:141/13/16); `AppBackendOverride`; `SourceRunOverride`, `SourceWebStartOverride`; `ToolRunOverride`, `ToolExeOverride`, `LiveServerStartOverride` (Runde2Tools.cs:12/13/77); `ResticDirOverride`, `ResticRunOverride`; `YaraDirOverride`, `YaraRunOverride`; `ImageWriteOverride`, `UninstallPcOverride` (SetupAbbild.cs:18/26); `DrivesOverride`, `ImageReadyOverride`; `DeviceRunnerOverride`, `DeviceSysRootOverride`; `AdbOverride`, `HealthOverride`; `ScrcpyOverride`, `BluetoothOverride`; `Phone*Override` ×2, `Tailscale*Override` ×2; `ShareAddressOverride`; `WindowsConfirmOverride`, `SwitchPersonOverride`; `TaskMeasureOverride`, `TaskKillOverride`; `FreeGpuOverride`; `ProgramStartsOverride`, `ProgramFindOverride`; `StudioLaunchOverride`; `WebAppLaunchOverride`, `BrowsersOverride`, `TelegramInstallerOverride`; `XdgOverride` (Core) | **Installation von Programmen, Abbild schreiben, Deinstallation**, Sicherung, Geräte, Handy |
| Dateien / Uhr / Sonstiges | `PickOverride` (Files.cs:19); `FilesOpenOverride`; `TrashOverride`; `FolderPickOverride`; `PhotoFoldersOverride`; `DownloadsOverride`; `LetterRecycleOverride`; `ScanTesseractOverride`, `VisionTesseractOverride`, `TesseractOverride`; `DocsRunOverride`; `CodeAiRunOverride`; `TvPlayOverride`, `TvRecordOverride`, `TvLegalOpenOverride`; `ProbeAnalyzeOverride`; `SystemLightOverride`; `CvNowOverride`, `DayNowOverride`, `ConNowOverride`, `TaskClockOverride`, `BcClockOverride`, `WeekTodayOverride`; `WfDueOverride` | Dateidialoge, Papierkorb, OCR, Uhrzeit |

Die genaue Zeilenliste aller 243 Haken liegt als Rohdatei bei: Arbeitsordner der Prüfung (nicht im Repo).

**Verhältnis echt zu ersetzt:** 104 Tests tragen „echt/live“ im Namen (viele davon verneinend, z. B. „startet nie ein echtes Claude“). Wirklich mit Fremdwerkzeug laufen vor allem FFmpeg-, Node-, Chrome-, typst-, assimp-, glTF-, IFC- und ein Blender-Test – jeweils nur, wenn das Werkzeug da ist. Für Claude selbst gibt es **keinen** Test mit echtem Claude (90 Ersetzungen von `ClaudeRunOverride`; Test „im Testmodus startet nie ein echtes Claude“). Die gesamte Kette „Claude ändert ein Dokument“ ist damit nur gegen eine Nachbildung geprüft: NICHT GETESTET (Ende-zu-Ende).

**Wackelnde Tests**
- „Pilot Text Leistung: … Seite aufbauen unter 3 s, Formatwechsel unter 1,5 s“ (`tests/Trendo.App.Tests/TextLookUiTests.cs:56`) hängt an Zeitgrenzen und damit an der Last des Rechners (parallele Sitzungen).
- „Job-Objekt: beim Schließen enden der Prozess und seine Unterprozesse“ (`tests/Trendo.App.Tests/ProcessJobTests.cs:10`) hängt an echten Prozessen und Zeitpunkten.
- Belegt ist ein Wackler in Core im Lauf T-0670 (1 Fehler, danach zweimal grün, `reports/volltest-T-0670/core-nachlauf-1-wackler.txt`); welcher Test es war, steht nicht im Beleg (nicht geprüft).
- Ursache der Anfälligkeit: 83 statische Haken sind prozessweiter Zustand; ein Test, der seinen Haken nicht zurücksetzt, beeinflusst andere.

**`requirements.yaml` gegen die Wirklichkeit** (359 Einträge mit Status):

| implementation_status | test_status | Anzahl |
|---|---|---|
| IMPLEMENTED | PC_VERIFIED | 76 |
| IMPLEMENTED | TESTED | 25 |
| IN_PROGRESS | PC_VERIFIED | 7 |
| IN_PROGRESS | NOT_RUN | 59 |
| NOT_STARTED | NOT_RUN | 192 |

Summen: IMPLEMENTED 101, IN_PROGRESS 66, NOT_STARTED 192; PC_VERIFIED 83, TESTED 25, NOT_RUN 251 (plus 44 QA-Matrixzeilen `status: NOT_RUN`).

Abweichungen:
- **Zu hoch:** `CORE-06` (Spiele) steht auf IMPLEMENTED/PC_VERIFIED mit Beleg aus Version 0.9.2 (`requirements.yaml:184-191`, `reports/pc-0.9.2/tests-pc-4e6b81d.txt`), heute 0.9.103. Der Beleg deckt den heutigen Code nicht. Das sagt auch das frühere Audit (`docs/STUDIO_REBUILD_4_0/TRENDO_REA_AUDIT_4.0.md:36`).
- **Zu niedrig:** `IMP-42` (Prompt-to-Action) steht auf IN_PROGRESS/NOT_RUN (`requirements.yaml:1511-1518`), obwohl 110 Adapter mit Tests existieren. 187 `IMP-…`-Einträge stehen auf NOT_STARTED (z. B. `IMP-44`, `IMP-45` Office), obwohl dafür Code vorhanden ist.
- Die Datei nennt sich „automatisch erzeugt“ (`requirements.yaml:1`), enthält aber von Hand ergänzte Einträge mit **kaputten Umlauten** („Formulare ausfÃ¼llen“, „BÃ¼ro“, `requirements.yaml:538`, `:540`, `:545`).
- Alle 108 getesteten Einträge haben einen Belegverweis (Auswertung per Skript, 0 ohne).

**CLAUDE.md-Regeln:** QA-017 und QA-019 stehen als offen/NOT_RUN (`requirements.yaml:33-34`, `:2835-2843`), Codesignatur offen (`requirements.yaml:350`, Release-Titel „(unsigniert)“ in `installer/release.sh`). Regel eingehalten; fachlich heißt das: Barrierefreiheit (Narrator, Kontrast, Mehrschirm) und echter Offline-Lauf sind **NICHT GETESTET**.

**Urteil Punkt 4: TEILWEISE.** Viel Testmasse, aber der Großteil prüft Oberfläche gegen Nachbildungen.

**Maßnahmen**
1. Testkategorien einführen: „ersetzt“, „echt-lokal“, „echt-PC-Lauf“; Bericht je Release mit Zahl je Kategorie. Aufwand klein bis mittel.
2. Statische Haken in eine je Fenster/Test eingespritzte Dienstschicht überführen (siehe Punkt 6). Aufwand groß.
3. `requirements.yaml` neu aus Code und Belegen erzeugen (Status aus bestandenem Test und Belegdatum, veraltete Belege verfallen), Umlaute reparieren. Aufwand mittel; Änderung braucht laut CLAUDE.md die Freigabe des Nutzers.
4. Zeitgrenzen-Tests in eigene Leistungsmessung auslagern. Aufwand klein.
5. Ein echter Claude-Rauchtest (Anmeldung des Nutzers, ein Dokument ändern) als PC-Lauf mit Beleg. Aufwand klein, braucht den Nutzer.

---

## 5. Sicherheit und Rechte

| Thema | Befund | Beleg | Urteil |
|---|---|---|---|
| ApprovalGate (Kern) | Einmalige, ablaufende Freigabe (10 min), an Aktion und Ziel gebunden, Not-Stopp verwirft alles, jede Stufe im Protokoll | `src/Trendo.Core/ApprovalGate.cs:71-83`, `:105-124`, `:38-53` | VOLLWERTIG (als Baustein) |
| Freigaben dauerhaft | Nur im Arbeitsspeicher (`Dictionary`), nach Neustart weg | `ApprovalGate.cs:23` | TEILWEISE |
| Durchsetzung im Workflow (Weg A) | Wirkung nach außen läuft nur über `Approvals.Execute` | `StudioWorkflow.cs:403`, `:557-564` | VOLLWERTIG |
| Durchsetzung über die lokale API | Loopback, Host-Prüfung, Token → Rolle, `Rbac.Demand` je Endpunkt | `src/Trendo.Api/LocalApi.cs:33-46`, `:59`, `:64`, `:69` | VOLLWERTIG |
| Durchsetzung in der Desktop-App | 728 Stellen arbeiten fest mit `Principal.LocalOwner` (Rolle Besitzer, alle Rechte). `Rbac` kann dort nie ablehnen. `Approvals.Request/Execute` wird in nur 18 Dateien benutzt (32 Aufrufe). Die meisten Aktionen der Oberfläche (Speichern, Löschen, Exportieren, Prozesse starten) laufen ohne Freigabe-Tor | `src/Trendo.Core/Rbac.cs:9-12`, Suche `Principal.LocalOwner` | TEILWEISE – Schutz liegt in der Oberfläche, nicht im Kern |
| Mehrere Personen | Es gibt Personen und Profilsperren (`MainWindow.Lock.cs`), aber keine Rolle je Person im Kern | `MainWindow.Lock.cs:84`, `:240` | TEILWEISE |
| Claude-Brücke: Zustimmung | Zustimmung wird verlangt, wenn **Beschriftung oder Name** eines Knopfs auf ein Muster passt (`ConsentRx`), oder eine Freigabe offen ist. Ein gefährlicher Knopf mit harmloser Beschriftung rutscht durch; `dokument_setzen` prüft nur den Pfadnamen | `src/Trendo.Core/W2/TrendoMcp.cs:181-187`, `MainWindow.ClaudeBridge.cs:253-258`, `:196` | TEILWEISE |
| Claude-Prozess selbst | Stufe „Sicher“: `--restricted --strict-mcp-config`, Umsetzen mit `acceptEdits`. Stufe „Voll“: `--permission-mode auto`, ohne Einschränkung – Befehle, Web, eigene Erweiterungen. Die Tests zeigen: Der Chat startet in der Vorgabe **ohne** `--restricted` mit `auto` | `src/Trendo.Core/W2/ClaudeCode.cs:100`, `:105`; `tests/Trendo.App.Tests/ClaudeChatUiTests.cs:59`, `ClaudeStudioUiTests.cs:127` | TEILWEISE – Projektgrenze gilt dann nur per Arbeitsanweisung im Prompt (`ClaudeCode.cs:88` „arbeite nur im Projektordner“) |
| Schalter `--restricted` | Ob die installierte Claude-Code-Fassung diesen Schalter kennt und so auslegt, ist im Repo nicht belegt | `ClaudeCode.cs:10` | nicht geprüft |
| Projektgrenzen | `ResolveInProject`: weist leere, absolute und Laufwerks-Pfade ab, prüft Präfix nach Normalisierung, lehnt Verknüpfungen/Junctions entlang des Pfads ab, protokolliert Verstöße | `src/Trendo.Core/ProjectStore.cs:49-65`, `:87-91` | VOLLWERTIG |
| Reichweite der Projektgrenze | Gilt nur für `ProjectStore`. Die Programm-Hülle speichert an beliebige Pfade (`ProgramSave(path)`), ohne diese Prüfung | `MainWindow.ProgramShell.cs:122-131` | TEILWEISE (gewollt für Nutzer, aber auch für Claude über Befehle erreichbar) |
| AuditLog | Nur-Anhängen mit Hash-Kette, prozessübergreifend exklusiv geöffnet, `Verify`/`FirstBroken` | `src/Trendo.Core/AuditLog.cs:24-41`, `:69-90` | TEILWEISE |
| AuditLog – Schwächen | (a) Hash ohne Schlüssel: wer die Datei ändern kann, rechnet die Kette neu – nur Schutz gegen Versehen. (b) **Jedes Anhängen liest und parst die ganze Datei** (`Parse(ReadText(fs))`), Aufwand wächst mit der Länge; Autosave schreibt bei jeder Speicherung einen Eintrag. Keine Rotation gefunden | `AuditLog.cs:30`, `:93-94`; `MainWindow.ProgramShell.cs:178` | FEHLT (Skalierung, Fälschungsschutz) |
| Secret-Speicher | Windows: Anmeldeinformationsverwaltung über advapi32, je Person eigener Namensraum. **Nicht-Windows: nur Arbeitsspeicher**, nach Neustart weg | `src/Trendo.Core/Secrets.cs:27-31`, `:34-36`, `:45-48`, `:18-24` | Windows VOLLWERTIG, Linux/Pi FEHLT |
| Geheimnisfelder für Claude | Brücke füllt keine Passwortfelder | `MainWindow.ClaudeBridge.cs:97` | VOLLWERTIG |

**Urteil Punkt 5: TEILWEISE.** Der Kern-Baustein ist gut, aber die Desktop-App umgeht ihn konstruktionsbedingt. Die Aussage in `CLAUDE.md` „serverseitig durchsetzen“ trifft nur für API und Workflow zu.

**Maßnahmen**
1. Wirkungsklassen (löschen, senden, starten, installieren, außerhalb des Projekts schreiben) als Dienste im Kern, die immer durch das Freigabe-Tor gehen; die Oberfläche ruft nur diese Dienste. Aufwand groß, Risiko mittel.
2. Claude-Zustimmung an eine erklärte Wirkungsklasse je Befehl binden (`ProgramCmd` um `Effect` erweitern) statt an Textmuster. Aufwand mittel.
3. Vorgabe für Claude auf „Sicher“ prüfen bzw. bei „Voll“ den Arbeitsordner technisch begrenzen; Schalter gegen die echte CLI belegen. Aufwand klein bis mittel; Entscheidung liegt beim Nutzer (Freigabe C1–C6 „volle Rechte“).
4. AuditLog: letzten Hash im Speicher halten, Datei rotieren, Kette optional mit Schlüssel aus dem Secret-Speicher. Aufwand klein bis mittel.
5. Secret-Speicher für Linux (libsecret) oder Linux-Edition ehrlich als „ohne gespeicherte Anmeldungen“ kennzeichnen. Aufwand mittel.

---

## 6. Architekturprobleme

| Thema | Befund | Beleg | Urteil |
|---|---|---|---|
| Größe `MainWindow` | **338 Teildateien, 76 446 Zeilen** eine einzige Klasse; größte Dateien `MainWindow.axaml.cs` 1417, `KreativStudios4.cs` 1382, `KreativStudios3.cs` 1029, `ClaudeChat.cs` 972. 474 private Felder. Gesamte App: 412 Dateien, 86 115 Zeilen – 89 % in einer Klasse | Zählung `src/Trendo.App/MainWindow*.cs` | FEHLT (Struktur) |
| Kopplung | Jede Teildatei sieht jedes Feld aller anderen. Oberfläche wird in Code gebaut (kein XAML je Programm, keine ViewModels); Fachlogik teils im Fenster (z. B. PNG-Kodierung im Rückgängig-Weg, `MainWindow.Kuenstlerstudio.cs:164-176`) | – | FEHLT |
| Statischer Zustand | 83 statische `*Override`-Eigenschaften, statisches `Programs`-Verzeichnis (`MainWindow.ProgramShell.cs:60`), statischer `PromptAmbient.Current` für Adapter (`ConnectedAdapters.cs:38`), statische Sperren im AuditLog | – | TEILWEISE (testbar nur seriell) |
| Leistung Tests | App-Tests 42 min 26 s für 1341 Tests, etwa 1,9 s je Test, weil je Test ein ganzes Hauptfenster entsteht | `reports/volltest-T-0670/volltest-0ff66ff50.txt:7` | TEILWEISE |
| Startzeit | Letzte Messung: 0,81–0,84 s bis zum ersten Bild bei 0.9.4 (`requirements.yaml:350`, `reports/d7-startmessung/`, 25.10.2026). Im Setup-Test 12,4 s beim ersten Start nach Installation, 3,7 s und 2,5 s danach (`reports/c4-setup/c4-setup-test-2026-10-25.txt`). Für 0.9.103 mit 83 Studios: keine Messung gefunden | – | NICHT GETESTET (aktuell) |
| Doppelte Systeme | **Rückgängig:** `ProgramDocument` plus mindestens 14 eigene Stapel (`_cadUndo`, `_hbUndo`, `_msRollUndo`, `_pdfOrgUndo`, `_calUndo`, `_todoUndo…`, `_dkUndo`, `_shUndo`, `_txUndo`, `_builderUndo`, `_undo3d`, `_planUndo`, `_wfUndo`; `MainWindow.Cad.cs:22`, `Haushaltsbuch.cs:14`, `MusikNoten.cs:16`, `PdfOrganize.cs:18`, `Pim.cs:422/576`, `Praesentation.cs:25`, `Tabelle.cs:29`, `Text.cs:31`, `W4Builder.cs:22`, `W4Studio3D.cs:27`, `W4Suite.cs:283`, `Workflows.cs:194`). **Notsicherung:** `programme/sicherung` und `wiederherstellen/`. **Assistent:** vier Wege (Punkt 2.1). **Reife:** `CapabilityLevel` und `Capability`. **Kataloge:** `StudioCatalog`, `CapabilityCatalog`, `ProgramCatalog`, `StudioRegistry`, `katalog.json`, `StudioKnowledge`, `StudioTools.Required`. **Engines:** Godot, Unity, Unreal und Browser-Spiel parallel als Ziel | siehe Spalte | FEHLT (Einheitlichkeit) – widerspricht „Kein zweiter …“ in `CLAUDE.md` |
| Doku gegen Code | `docs/STUDIO_REBUILD_4_0/*` nennt **58 Studios, 98 Programme** (`TRENDO_REA_AUDIT_4.0.md:9`, `TRENDO_STUDIO_INVENTORY_4.0.md:5`, `TRENDO_STUDIO_FUNKTIONSMATRIX.md:5`); Code: 83 Studios, 125 Programme. Die dortige Aussage „Nur 12 Studios haben ein Dokumentprogramm“ (`TRENDO_UNIVERSAL_STUDIO_PHASEN.md:20`) ist überholt (heute 39 Programme) | – | TEILWEISE (veraltet) |
| Regel „Merge nur nach OK“ | `CLAUDE.md` (Rollentabelle) verlangt für Merge nach `main` ein OK je PR; Aufträge wiederholen das (`tasks/T-0600-pilot-videoschnitt.md:18`, `tasks/T-0505-import-rest.md:15`). `docs/claude/TRENDO_GODMODE_ENTSCHEIDUNGEN_2026-10-23.md` enthält dazu keine abweichende Regel (Suche ohne Treffer). Eine neuere Vorgabe „immer selbst nach main“ steht **nicht im Repo**. Falls sie gilt, widerspricht `CLAUDE.md` ihr und muss nachgezogen werden | `CLAUDE.md`, Suche in `docs/claude`, `docs/spec/decisions.yaml` | Widerspruch nicht im Repo belegbar; Regelwerk nachführen |
| Windows-Edition | Gebaut, laufend getestet, Setup über Velopack | Punkt 4 und 7 | TEILWEISE (siehe 7) |
| Linux USB/SSD | Abbild-Bau über live-build und ein handgestarteter Workflow (`.github/workflows/trendo-os-abbild.yml`, nur `workflow_dispatch`). Letzter Testbericht 21.10.2026 mit App-Stand 0.3.0/0.6.1, nur QEMU; LUKS, echte Hardware, Secure Boot, NVIDIA: NOT_RUN (`reports/p2/P2_TESTBERICHT.md`). Phase P2 „IN_PROGRESS“ (`requirements.yaml:36-38`). Secret-Speicher dort nur im Arbeitsspeicher. Laut Nutzervorgabe nicht mehr im Verkaufsangebot | – | NICHT GETESTET (aktueller Stand) |
| Pi 4 + Windows-PC | Begleiter `src/Trendo.Companion` mit 349 Zeilen, Belege `reports/w7/` (Box/QEMU). Phase P3 „APPROVED_SPEC“ (`requirements.yaml:39-41`) | – | TEILWEISE (Kleinstumfang), echte Hardware NICHT GETESTET |

**Urteil Punkt 6: TEILWEISE.** Das Produkt läuft, aber die Struktur trägt 125 Programme nicht dauerhaft: eine Klasse, viele Parallelsysteme.

**Maßnahmen**
1. Programmvertrag festlegen (Dokument, Befehle mit Wirkungsklasse, Rückgängig, Speichern, Export, Assistenz) und Programme schrittweise aus `MainWindow` in eigene Klassen je Programm verschieben; zuerst die 85 Programme ohne Hülle auf die Hülle heben. Aufwand sehr groß, Risiko hoch; nur etappenweise mit Funktions-Baseline (`scripts/function_baseline.py`).
2. Ein Rückgängig-, ein Notsicherungs-, ein Reife- und ein Katalogsystem. Aufwand groß.
3. Dienste (Prozessstart, Netz, Uhr, Dateidialog) als eingespritzte Schnittstellen statt 243 Einzelhaken. Aufwand groß, senkt Testdauer und Wackler.
4. Leichtes Testfenster (nur die geprüfte Seite aufbauen) und parallele Testläufe. Aufwand mittel, Ziel unter 15 min.
5. `docs/STUDIO_REBUILD_4_0` als veraltet kennzeichnen oder aus Code neu erzeugen; `CLAUDE.md` an die geltende Merge-Regel und an die Editionen-Entscheidung anpassen. Aufwand klein.
6. Startzeit und Speicher für 0.9.103 neu messen und als Grenzwert im Release-Ablauf prüfen. Aufwand klein.

---

## 7. Installation, Update, Deinstallation

| Thema | Befund | Beleg | Urteil |
|---|---|---|---|
| Setup-Bau | Velopack 1.2.161, Installation je Benutzer ohne Admin, `vpk pack`, SHA-256-Liste | `src/Trendo.App/Trendo.App.csproj:22`, `installer/build-setup.sh` | VOLLWERTIG (Skript) |
| Release-Ablauf | `installer/release.sh` prüft: auf `main`, saubere Arbeitskopie, gleich `origin/main`, Version in der Projektdatei, danach `dotnet test`, Schwachstellen-Scan, Setup, Begleiter, `gh release create` mit Titel „(unsigniert)“ | `installer/release.sh` | TEILWEISE |
| Widerspruch Release-Weg | Das Skript nutzt `gh`; laut Nutzervorgabe sollen Releases per API mit eigenem Zugang und nie über `gh` laufen. Im Repo steht dazu nichts – das Skript passt nicht zur gelebten Praxis | `installer/release.sh` (letzte Zeilen) | Widerspruch, nachführen |
| Codesignatur | Fehlt; keine Signier-Parameter in `installer/` oder Projektdatei gefunden. Windows SmartScreen warnt bei jedem Setup | `installer/release.sh`, `requirements.yaml:350` | FEHLT (bewusst offen) |
| Update in der App | `VelopackUpdater`: prüfen, laden, anwenden, beim Beenden anwenden, Sicherung der laufenden Version, Rückfall | `src/Trendo.App/TrendoUpdater.cs:24-60`, `MainWindow.Updates.cs:19` | TEILWEISE |
| Update automatisch geprüft | Nur gegen Nachbildung `FakeUpdater` (`tests/Trendo.App.Tests/UpdatesUiTests.cs:12`); `VelopackUpdater` selbst hat keinen Test | – | NICHT GETESTET (automatisiert) |
| Echter Setup-Lauf | Einmalig belegt am 25.10.2026 für 0.9.6 → 0.9.7: Deinstallieren, alte Version, Update, Rückfall, Deinstallieren, Daten byte-gleich – „ALLE BESTANDEN“; weitere Belege für 0.9.10 (`reports/r0910-setup`) | `reports/c4-setup/c4-setup-test-2026-10-25.txt` | Damals VOLLWERTIG, für 0.9.103 NICHT GETESTET |
| Setup-Testskript | Ein Skript, das diesen Lauf wiederholt, liegt weder unter `scripts/` noch unter `installer/` (dort nur `build-package.ps1`, `install.ps1`, `uninstall.ps1`, `systemcheck.ps1`, `build-setup.sh`, `release.sh`) | Verzeichnislisten | FEHLT (Wiederholbarkeit) |
| Deinstallation | `installer/uninstall.ps1` entfernt Programmordner und Startmenü-Eintrag, Daten bleiben außer mit `-RemoveData`. In der App zusätzlich `UninstallPcOverride`-Weg mit Datenfrage (`MainWindow.SetupAbbild.cs:26-29`), Beleg `reports/c4-setup/deinstallation-datenfrage-2026-10-25.txt` | – | TEILWEISE |
| Reste nach Deinstallation | Werkzeuge liegen bewusst außerhalb des Programmordners (`Dependencies.cs:237`, `%LOCALAPPDATA%\TrendoWerkzeuge`), dazu Einträge in der Windows-Anmeldeinformationsverwaltung (`Secrets.cs:47`). Ob beides entfernt wird: nicht geprüft | – | nicht geprüft |
| Dauerlauf (CI) | Kein Workflow, der Tests oder Setup bei jedem Stand baut; einziger Workflow ist das Linux-Abbild auf Handstart | `.github/workflows/` | FEHLT |
| Nachweis auf dem PC des Nutzers | Aus der Entwicklungsumgebung gestartete Installationen landen laut Projektnotiz in einem umgeleiteten Container; ein echter Nachweis braucht den Start durch den Nutzer | – (Projektnotiz, nicht im Repo) | Einschränkung |

**Urteil Punkt 7: NICHT GETESTET für den aktuellen Stand**, Bausteine vorhanden.

**Maßnahmen**
1. Setup-Rundlauf (Installieren alt → Update → Rückfall → Deinstallieren, Daten byte-gleich, Startzeit) als Skript ins Repo und als Pflichtschritt vor jedem Release mit Beleg. Aufwand mittel, Risiko gering.
2. Prüfung der Reste nach Deinstallation (Werkzeugordner, Anmeldeinformationen, Verknüpfungen) ergänzen. Aufwand klein.
3. Codesignatur beschaffen und in `vpk pack` einbinden (Kosten, Entscheidung des Nutzers). Aufwand klein technisch.
4. `release.sh` an den tatsächlichen Veröffentlichungsweg anpassen. Aufwand klein.
5. Automatischer Bau und Kurztest je Stand (ohne die 42-Minuten-Suite: Core, Api, Paket-Tests, Start-Rauchtest). Aufwand mittel.

---

## Die 10 wichtigsten Architektur-Maßnahmen (priorisiert)

| Rang | Maßnahme | Warum zuerst | Aufwand | Risiko |
|---|---|---|---|---|
| 1 | **Datenverlust schließen:** Notsicherung, Versionen und `zuletzt.json` atomar schreiben; `.autosave`-Nebendatei anbieten; kaputte Sicherungen überspringen; Sperrdatei gegen zweites Fenster; Test mit echtem Prozessabbruch | Einziger Bereich, in dem Arbeit des Nutzers still verloren gehen kann (`ProgramDocument.cs:223`, `:215`) | klein bis mittel | gering |
| 2 | **Dateiformat mit Version, Schema und Migration;** `dokument_setzen` prüft gegen das Schema | Ohne das zerstört jeder Formatwechsel und jede falsche KI-Änderung Dokumente (`ProgramDocument.cs:131`, `:202`) | mittel bis groß | mittel |
| 3 | **Große Binärdaten aus dem JSON nehmen, Rückgängig als Delta mit Speichergrenze** | Zeichen-, Bild- und Videoprogramme skalieren sonst nicht (`MainWindow.Kuenstlerstudio.cs:164-176`) | groß | hoch |
| 4 | **KI-Aktionen ehrlich machen:** 67 Text/Plan-Aktionen an echte Fachfunktionen binden oder als Anleitung umbenennen; Reife messen statt erklären | Kern des Leitsatzes „keine Scheinfunktionen“; heute 1 von 110 `Ready` | sehr groß | mittel |
| 5 | **Ein Assistenzweg:** Workflow-Adapter, Programm-Assistent, Brücke und Plan-Zeilen zusammenführen; Ergebnis landet im offenen Dokument; Befehle tragen eine Wirkungsklasse | Beseitigt vier Parallelwege und macht Zustimmung prüfbar statt textmusterbasiert | groß | hoch |
| 6 | **Freigaben in den Kern:** Wirkungen (löschen, senden, starten, installieren, außerhalb schreiben) nur über Kern-Dienste mit Freigabe-Tor; Claude-Vorgabe und Arbeitsordner technisch begrenzen | Heute schützt nur die Oberfläche; 728-mal fester Besitzer | groß | mittel |
| 7 | **Programmvertrag und Abbau von `MainWindow`:** je Programm eine Klasse; die rund 85 Programme ohne Hülle auf Hülle, ein Rückgängig-, ein Sicherungssystem | 76 446 Zeilen in einer Klasse, 15 Rückgängig-Systeme | sehr groß | hoch (nur etappenweise) |
| 8 | **Testschichten und Dienste statt 243 Haken:** Kategorien ersetzt / echt-lokal / echt-PC; stille `return;` zu `Skip`; leichtes Testfenster; Ziel Volltest unter 15 min | Macht sichtbar, was wirklich geprüft ist, und beseitigt Wackler | groß | mittel |
| 9 | **Pflicht-Echtläufe je Release mit Beleg:** Blender, Unreal, Godot, Unity, LibreOffice, Tesseract, ACE-Step, sd-cli, Ollama, Claude; Setup-Rundlauf als Skript; `StudioTools.Required` für alle Studios | Ende-zu-Ende ist für die meisten Werkzeuge und für das Setup seit 0.9.10 unbelegt | mittel bis groß | gering |
| 10 | **Wahrheitsquelle aufräumen:** `requirements.yaml` aus Code und Belegen erzeugen (Umlaute reparieren, veraltete Belege verfallen), `docs/STUDIO_REBUILD_4_0` kennzeichnen, `CLAUDE.md` an Merge-, Release- und Editionen-Regel anpassen, AuditLog skalierbar machen | Status und Regeln stimmen in beide Richtungen nicht mit dem Code überein | mittel | gering (braucht Freigabe für `requirements.yaml`) |

## Nicht geprüft

- `StudioIntel`, `AgentCenter`, `IntelligenceCore`, `WorkflowEngine` im Einzelnen (nur Einstiegspunkte gelesen).
- Ob eine äußere Fehlerbehandlung den Wurf in `MainWindow.ProgramShell.cs:525` fängt.
- Verhalten bei fehlendem sd-cli, ACE-Step und Ollama in der Oberfläche.
- Ob `--restricted` von der installierten Claude-Code-Fassung unterstützt wird.
- Welche zwei App-Tests im Lauf T-0670 zusätzlich übersprungen wurden und welcher Core-Test dort wackelte.
- Aktuelle Startzeit, Speicherbedarf und Reste nach Deinstallation bei 0.9.103.
- Der gerade laufende Volltest wurde nicht abgewartet; alle Zahlen stammen aus Belegen unter `reports/`.
