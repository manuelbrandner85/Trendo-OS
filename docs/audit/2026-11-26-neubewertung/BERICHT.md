# Trendo OS – Neubewertung aller Studios (T-0769)

Stand: `main` 542dc1ce7 = Release 0.9.110, geprüft am 26.11.2026.
Grundlage: die 6 Abnahmekriterien aus `docs/audit/2026-11-16-profi-audit/BERICHT.md`, Abschnitt H.
Zum Vergleich: Profi-Audit vom 16.11.2026 (Stand 0.9.103, 83 Studios).

**Methode.** Fünf Prüfläufe, je einer pro Studiogruppe wie im Audit vom 16.11. Geprüft wurden Code (Datei:Zeile), Testquelltext (Suche nach `*Override`), Rohlogs unter `reports/` und gerenderte Bilder.

- **Bewertung je Kriterium**
  - ✓ heißt belegt: Code plus ein echter Test ohne Ersatzwerkzeug oder ein Rohlog.
  - ◐ heißt teilweise: nur mit Ersatz getestet, nur in einem Teil der Programme vorhanden oder nur Code.
  - ✗ heißt fehlt.
  - `–` heißt fachlich nicht anwendbar, zum Beispiel weil das Studio kein Dateiformat hat.
- **Regel für K4 (KI):**
  - ✓: Ergebnis im offenen Dokument, rückgängig, mit echtem Lauf (Test ohne Ersatz oder Rohlog).
  - ◐: Es gibt eine KI-Aktion, aber sie schreibt nur eine Projekt- oder Textdatei, zeigt nur etwas an, oder der Weg ins Dokument ist nicht echt getestet.
  - ✗: Es gibt gar keine KI-Aktion für das Studio, oder sie ist bewusst gesperrt.
- **Schreibweise der Urteile:** VOLLWERTIG, TEILWEISE, TEILWEISE (Grenzfall), FEHLT, NICHT GETESTET. Die Zusätze (+) und (−) stehen nur in der Spalte „Urteil 16.11.“, wie im Audit vom 16.11. für Kreativ & Medien.
- **Ersatz:** Ersetzt sein darf nur die Dateiauswahl des Systems (`PickOverride`). Ein ersetztes Fachwerkzeug, Mikrofon, Netz oder Modell zählt nicht.
- **Programm-Hülle:** Für Programme in der Hülle gilt Kriterium 3 als ✓. Es stützt sich auf den allgemeinen Test mit hartem Prozessabbruch (`tests/Trendo.Core.Tests/ProgramCrashTests.cs:128` `Kill_while_autosaving`) und auf die angebotene Wiederherstellung (`tests/Trendo.App.Tests/ProgramCrashUiTests.cs:36`). Einen eigenen Abbruchtest für jedes einzelne Programm gibt es meist nicht.
- **Claude-Läufe in T-0698 und T-0742:** Sie setzen `ClaudeRunOverride` so, dass das echte `claude.exe` startet. Sie gelten hier als echt, sind aber formal ein Ersatz-Haken.

**Selbst ausgeführt am 26.11.** Alle Läufe waren grün, keiner wurde übersprungen.

| Testklasse | Tests | Dauer | Echte Werkzeuge |
|---|---|---|---|
| `BueroEchtlauf_Ui` | 6/6 | 102 s | LibreOffice, qpdf, Tesseract 5.4 |
| `BueroAusbau_Ui` | 12/12 | 21 s | – |
| `T0725_Echtlauf_Ui` | 4/4 | 44 s | LibreOffice, qpdf |
| `VectorStudios_Ui` | 5/5 | 17 s | FFmpeg 9.0.2, Chrome |
| `T0697ComicPodcast_Ui` | 8/8 | 23 s | FFmpeg; Mikrofon und Comic-KI ersetzt |
| `GameFree_Ui` | 5/5 | 37 s | Chrome, Godot 4.7.2 |
| `T0765_AppBaukasten_Ui` | 3/3 | 14 s | – |
| Core `T0755_Gefahren` + `T0763` | 27/27 | 29 s | Authenticode, Windows-Leistungsindikatoren |

Dazu 13 Bilder (`bilder/`). Alle übrigen Testergebnisse stammen aus Berichten und Rohlogs, siehe „Nicht geprüft“.

---

## 1. Kurzfassung

| Urteil | 16.11.2026 (83 Studios) | 26.11.2026 (80 Studios) |
|---|---|---|
| VOLLWERTIG | 2 (PDF-Werkstatt, Dateien teilen) | **0** |
| TEILWEISE | 78 | **79**, davon 5 Grenzfälle |
| FEHLT | 1 (Filme machen) | **0** |
| NICHT GETESTET | 1 (Konsolenspiele) | **1** (Konsolenspiele, Werkzeuge BLOCKIERT) |
| abgelehnt oder gelöscht | 1 (SPEC-07) | 3 nicht mehr gezählt (SPEC-07, SPEC-16, CORE-09) |

Die 80 Studios sind die 79 aktiven plus SPEC-04 „3D-Modelle ansehen“: aus der Übersicht entfernt, die Seite gibt es aber weiter.

**Ehrlich gelesen.**

- **Weniger vollwertige Studios:** Es sind jetzt 0 statt 2. Das ist kein Rückschritt im Code.
  - Am 16.11. war VOLLWERTIG eine Einschätzung. Heute werden alle 6 Kriterien streng angelegt.
  - Die PDF-Werkstatt scheitert nur an Kriterium 4: Die KI schreibt `pdf-auftrag.txt` statt eine PDF zu ändern (`src/Trendo.Core/PromptAction/BueroPdfPromptAdapters.cs:92`).
  - „Dateien teilen“ scheitert an Kriterium 4 (KI nur als Adapterliste getestet) und Kriterium 6 (keine Kürzel, kein Rechtsklick).
  - Mit derselben strengen Regel wäre auch am 16.11. kein Studio vollwertig gewesen.
- **Großer echter Fortschritt** zeigt sich in den einzelnen Kriterien:
  - Ein echtes Ergebnis aus der leeren Vorlage über die Oberfläche (Kriterium 1) ist jetzt bei 66 von 80 Studios belegt.
  - Ein Test vom Klick bis zur Datei mit dem echten Werkzeug (Kriterium 5) bei 62 von 80 Studios.
  - Am 16.11. waren Unreal, Godot, LibreOffice, Tesseract, Bild-KI und Musik-KI im Test durchweg ersetzt. Heute laufen sie echt (Godot, LibreOffice, Tesseract am 26.11. selbst erneut ausgeführt).
- **Filme machen:** steigt von FEHLT auf TEILWEISE. Aus Drehbuch und Material entsteht eine gerenderte MP4 mit Untertiteln (`FilmSchnitt_Ui.Render_click_to_file_real`, `reports/T-0693/ui-rohlog.txt`).
- **Grenzfälle:** Fünf Studios fehlen nur an einem Kriterium (eins ◐, keins ✗):
  - nur K4: PDF-Werkstatt, Dokumente scannen, Logos & Marken
  - nur K2: Tagesplaner
  - nur K6: Bücher schreiben

**Kriterien über alle 80 Studios**

| Kriterium | ✓ | ◐ | ✗ | – |
|---|---|---|---|---|
| K1 Ergebnis aus leerer Vorlage | 66 | 13 | 1 | 0 |
| K2 Fremddatei rein/raus, Fremdprogramm öffnet | 8 | 61 | 7 | 4 |
| K3 Absturz ≤ 30 s, Wiederherstellung | 39 | 34 | 0 | 7 |
| K4 KI im offenen Dokument, rückgängig | 5 | 74 | 1 | 0 |
| K5 Klick bis Datei mit echtem Werkzeug | 62 | 16 | 2 | 0 |
| K6 Stufen, Kürzel, Rechtsklick, Ablegen | 14 | 52 | 14 | 0 |

**Die Engpässe sind Kriterium 4 (KI im Dokument), Kriterium 2 (Fremdprogramm) und Kriterium 6 (Bedienung).** Die Fachkerne selbst sind fast überall echt.

---

## 2. Tabelle aller 80 Studios

Spalten K1–K6 entsprechen den Abnahmekriterien 1–6. Urteile in der Schreibweise aus den Bewertungsregeln. Lücke und nächster Schritt je Studio stehen in `restliste.md`.

### Arbeiten & Büro (13)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| STUDIO-PDFTOOLS-01 | PDF-Werkstatt | VOLLWERTIG | TEILWEISE (Grenzfall) | ✓ `BueroAusbau_Ui` Klick bis Datei, 12/12 am 26.11. | ✓ LibreOffice-PDF rein, qpdf/LibreOffice prüfen; `BueroEchtlauf_Ui` 6/6, `reports/T-0705/pdf-werkstatt/` | ✓ Hülle + `Crash_all_seven` (`BueroAusbauUiTests.cs:321`) | ◐ KI schreibt nur `pdf-auftrag.txt` (`BueroPdfPromptAdapters.cs:92`) | ✓ Echtlauf qpdf + LibreOffice | ✓ Ziehen, Rechtsklick, Ablegen getestet (`BueroAusbau_Ui`) |
| STUDIO-DOCSCAN-01 | Dokumente scannen | TEILWEISE | TEILWEISE (Grenzfall) | ✓ Fotos → entzerrtes PDF (`BueroAusbau_Ui`) | ✓ LibreOffice-PNG + Tesseract, qpdf, pdftotext (`BueroEchtlauf_Ui`, 26.11.) | ✓ `Crash_all_seven` + Hülle | ◐ Adapter schreibt nur `scan-auftrag.txt` (`DeckScanPromptAdapters.cs:91`) | ✓ echtes Tesseract 5.4 | ✓ Seiten ziehen, Rechtsklick, Fotos ablegen getestet |
| STUDIO-DAYPLAN-01 | Tagesplaner | TEILWEISE | TEILWEISE (Grenzfall) | ✓ Zeitleiste → ICS (`T0719KartenTagesplanerUiTests.cs:208`) | ◐ keine fremde .ics einlesbar; ICS nur mit Ical.Net/eigenem Skript (`reports/T-0719/pruefung.txt`) | ✓ Hülle | ✓ lokale KI qwen3:14b ins Dokument, rückgängig (`reports/T-0707/echtlauf-lokale-ki.md`) | ✓ Klick → ICS, Ical.Net liest | ✓ Ziehen, Rechtsklick, F2, Entf (`T0719`-Test) |
| STUDIO-CONTRACTS-01 | Verträge-Ordner | TEILWEISE | TEILWEISE | ✓ leerer Ordner → ICS + Kündigungs-PDF (`BueroAusbau_Ui`) | ◐ kein Fremdimport; ICS nie in Kalenderprogramm | ✓ `Crash_all_seven` | ◐ nur `vertrag.txt` (`BueroPromptAdapters.cs:216`) | ✓ qpdf/LibreOffice öffnen das PDF (`BueroEchtlauf_Ui`) | ✓ Rechtsklick an der Zeile, Ablegen getestet |
| STUDIO-FORMS-01 | Formulare ausfüllen | TEILWEISE | TEILWEISE | ✓ Formular öffnen → ausgefüllt/geglättet (`BueroAusbau_Ui`) | ✓ AcroForm aus LibreOffice, qpdf liest Werte (`reports/T-0705/formulare/`) | ✓ `Crash_all_seven` | ◐ nur `formular-zuordnung.txt` (`BueroPdfPromptAdapters.cs:138`) | ✓ Echtlauf qpdf/LibreOffice | ◐ kein Rechtsklick; Gesetztes nicht verschiebbar (`Formulare.cs:360`) |
| STUDIO-DECKDESIGN-01 | Präsentationen gestalten | TEILWEISE | TEILWEISE | ◐ Folien nur aus Gliederungstext, keine Bearbeitung auf der Folie | ✓ LibreOffice-PPTX rein, Trendo-PPTX in LibreOffice (`BueroEchtlauf_Ui`) | ✓ `Crash_all_seven` | ◐ KI-Entwurf nur mit `DeckAiOverride`; Adapter-PPTX nicht im offenen Dokument | ✓ Echtlauf LibreOffice/qpdf | ✓ Folien ziehen, Rechtsklick, PPTX ablegen getestet |
| STUDIO-CV-01 | Bewerbung & Lebenslauf | TEILWEISE | TEILWEISE | ✓ leerer Lebenslauf → PDF/Word (`BueroAusbau_Ui`) | ◐ vorhandener Lebenslauf (DOCX/PDF) nicht einlesbar; nur Anlage-PDF | ✓ `Crash_all_seven` | ◐ KI-Anschreiben nur mit `CvAiOverride` | ✓ LibreOffice öffnet DOCX, qpdf prüft Mappe | ◐ Ablegen ja, kein Rechtsklick (`MainWindow.Bewerbung.cs`) |
| STUDIO-CARDS-01 | Karten & Einladungen | TEILWEISE | TEILWEISE | ✓ Vorlage → Druck-PDF; Gestalten nur über Felder/Regler | ◐ kein Fremdimport; nur Export in LibreOffice geöffnet | ✓ `Crash_all_seven` | ◐ nur `karte.txt` (`BueroPromptAdapters.cs:146`) | ✓ qpdf/LibreOffice öffnen Druck-PDF | ◐ Foto/CSV ablegen; kein Rechtsklick, kein freies Ziehen |
| SYS-10 | Texte & Tabellen | TEILWEISE | TEILWEISE | ✓ durchgehender Editor (`TextEditorUiTests.cs:27`), Tabelle/Präsentation → Datei | ✓ LibreOffice Writer/Calc/Impress-Rundreise `reports/T-0701/rohlog.txt` (23/23); MS Office nie | ✓ harte Abbrüche `link-text`/`link-tabelle` (`T0714_HuelleE2Tests.cs:143`) | ◐ Text: Vorschlag annehmen/verwerfen; sonst „Noch nicht im Dokument möglich“ | ✓ LibreOffice-Abgleich echt, sonst „übersprungen“ | ◐ Rechtsklick ja; kein Datei-Ablegen; Trendo PDF ohne Rechtsklick |
| STUDIO-LETTER-01 | Briefe schreiben | TEILWEISE | TEILWEISE | ✓ Assistent → DIN-Brief DOCX/PDF | ✓ LibreOffice-DOCX → Trendo → LibreOffice/qpdf (`T0725_Echtlauf_Ui`, 4/4 am 26.11.) | ✓ ProgramLink, 16 harte Abbrüche (T-0708) | ◐ Claude-Änderung als ein Rückgängig-Schritt, nur ersetzt (`ClaudeLetterUiTests`) | ✓ Echtlauf LibreOffice/qpdf | ◐ Ablegen ja; kein Rechtsklick |
| SYS-09 | E-Mails & Termine | TEILWEISE | TEILWEISE | ✓ Konto → Mail mit Anhang → Zustellung (`MailFullUiTests.cs:172`) | ◐ fremde MIME gelesen, Python-`email` als Fremdleser (`reports/T-0689/`); Thunderbird/Outlook nie | ✓ Entwurf nach 2 s, Neustart ohne Schließen (`Write_program`) | ◐ KI-Antwort ins Schreibfenster, nur ersetztes Modell (`Ki_only_drafts`) | ◐ nur eigener Testserver, `ImapStreamOverride` (`MailFullUiTests.cs:65`) | ◐ Posteingang Kürzel + Rechtsklick; Adressbuch ohne |
| X-11 | Neues Projekt starten | TEILWEISE | TEILWEISE | ✓ Ziel → Schritte + Gantt (`GanttUiTests`) | ◐ CSV/ICS raus, Calc öffnet CSV (`T0725_Echtlauf_Ui`); kein Import | ◐ sofort atomar + `ItemUndo`, kein Absturztest | ◐ `projekt.anlegen` schreibt nur in `ProjectStore`, nicht ins offene Vorhaben (`MeasurePromptAdapter.cs:37`) | ✓ Echtlauf LibreOffice Calc + Ical.Net | ◐ Strg+Z über Menü; kein Rechtsklick/Ablegen |
| SYS-02 | Kleine Helfer & Musik | TEILWEISE | TEILWEISE | ✓ Bereich wählen → PNG, Notizbuch/Editor | ◐ fremde MP3 gespielt; PNG nie im Fremdprogramm | ✓ Editor `Kill_while_autosaving_editor_link` (`T0711_LinkLocksTests.cs:105`) | ◐ `helfer.rechnen` legt Notiz an; Musik ohne KI | ✓ echtes Tongerät (`T0731HelferUiTests.cs:48`, `reports/T-0731/wiedergabe-echt.log`) | ◐ kein Rechtsklick/Ablegen in Player, Galerie, Helfer |

### Business & Geld (3)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| SPEC-11 | Geld & Rechnungen | TEILWEISE | TEILWEISE | ✓ leer → Rechnung PDF/ZUGFeRD/XRechnung (`GeldProgrammUiTests.cs:22`) | ◐ CAMT/MT940 nur handgeschrieben; XRechnung nie extern validiert | ◐ AtomicFile + `.bak`, nicht in der Hülle, kein Abbruchtest | ◐ „Rechnung aus Beschreibung“ rückgängig, ohne KI-Lauf; Adapter nur HTML | ✓ echtes pdftotext (`GeldRechnungenTests.cs:208`, `reports/T-0694/rohlog-pdftotext.txt`) | ◐ Rechtsklick/Ziehen vorhanden, nicht getestet |
| STUDIO-ONLINE-BUSINESS-01 | Online verkaufen | TEILWEISE | TEILWEISE | ✓ leerer Shop → Artikel/Varianten/CSV/HTML (`SellEditUiTests.cs:30`) | ◐ Shopify-CSV nur mit PowerShell/Python gelesen (`reports/T-0753/csv-fremdprogramme.log`) | ◐ Stammdaten atomar; Kassieren schreibt 4 Dateien ohne Transaktion | ◐ nur `Geschäftsplan rechnen` als Markdown (`CapabilityCatalog.cs:33`); kein KI-Weg für Artikel/Kasse/Shop (T-0753) | ✓ Klick bis CSV/HTML | ◐ Strg+Z, Rechtsklick; Ablegen nur über Methode getestet |
| STUDIO-POD-01 | Print on Demand | TEILWEISE | TEILWEISE | ✓ Designer in der Hülle (`PodDesignUiTests.cs:30`) | ◐ nur PNG rein; Anbieter BLOCKIERT (kein Konto) | ✓ Hülle | ◐ Adapter `preisrechnung.txt` | ◐ Anbieter nachgebildet (`reports/T-0753/pod-docserver.log`), `PodPngOverride` | ◐ Kürzel ja; kein Rechtsklick, kein Bild-Ablegen |

### Kreativ & Medien (14)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| STUDIO-LOGO-01 | Logos & Marken | TEILWEISE (−) | TEILWEISE (Grenzfall) | ✓ leere Vorlage, Formen/Pfad mit Maus (`VectorStudios_Ui.LogoDraw`, 5/5 am 26.11.) | ✓ SVG in Chrome gerendert (Abweichung 0,55/255), PDF in PDFium (`reports/T-0696/logo-exporte.log`) | ✓ Hülle | ◐ „Ideen mit Claude“ ruft `AssistFromProgram` (`MainWindow.AssistChain.cs:55`): Kette ins offene Dokument mit Vorschau Ja/Nein und Rückgängig bzw. Claude-Brücke; `logo-claude` in keinem Test | ✓ `LogoExports` ohne Ersatz, echtes Chrome | ✓ V/R/E/P/T, Rechtsklick `VectorEditor.cs:630`, Ablegen `:198` |
| CORE-08 | Videos & Fotos bearbeiten (schnitt, bild, ton, kreativ-medien) | TEILWEISE (+) | TEILWEISE | ✓ alle vier: Vorlage → MP4/WebP/WAV/MP3 (`VideoLook_Ui.Look_click_to_file_real`, `T0733_BildMalen_Ui.Tiff_in_webp_out`) | ◐ Bild ✓ TIFF→WebP mit Pillow (`reports/T-0733/webp-tiff-fremdpruefung.log`); Schnitt/Ton nur ffprobe | ◐ Hülle ✓; Ton: Absturz während Aufnahme lässt WAV unwiederhergestellt | ◐ nur allgemeiner Claude-Weg, ersetzt | ✓ Schnitt (`reports/T-0695/ui-rohlog.txt`), Bild, Medien mit echtem FFmpeg; Ton-Mikrofon ersetzt | ◐ Schnitt ✓ (J/K/L, Ablegen, Rechtsklick); Bild/Ton ohne Rechtsklick-Menü und Ablegen |
| STUDIO-FILM-01 | Filme machen | FEHLT | TEILWEISE | ✓ Drehbuch/Material → MP4 + SRT (`FilmSchnitt_Ui.Render_click_to_file_real`, `reports/T-0693/ui-rohlog.txt`) | ◐ Fountain/MP4/PNG rein; MP4 nur ffprobe; kein FDX | ✓ Hülle | ◐ „Drehbuch mit KI“ ins Dokument, rückgängig, nur `FilmKiOverride` | ✓ echtes FFmpeg | ✓ Tasten, Rechtsklick, Stufen, Ablegen (`MainWindow.FilmSchnitt.cs`) |
| STUDIO-ANIM-01 | Animation & Motion | TEILWEISE (−) | TEILWEISE | ✓ Form → Schlüssel per Ziehen → MP4 (`VectorStudios_Ui.AnimRealVideo`) | ◐ MP4/GIF nur mit FFmpeg selbst geprüft (`reports/T-0696/anim-video.log`) | ✓ Hülle | ◐ „Bewegung mit Claude planen“ ruft `AssistFromProgram` (`MainWindow.AssistChain.cs:55`, Knopf `MainWindow.KreativStudios2.cs:332`); `anim-claude` in keinem Test | ✓ echtes FFmpeg, 26.11. grün | ✓ Bühne, Rauten ziehen, Rechtsklick Kurve (`KeyTimeline.cs:170`) |
| STUDIO-COMIC-01 | Comics & Storyboards | TEILWEISE (−) | TEILWEISE | ✓ leere Seite → Zeichnen/Blasen → PDF/CBZ (ohne Ersatz) | ◐ nur PNG rein; PDF in PDFium, CBZ mit Python (`reports/T-0697/pruefung.txt`); kein CBZ-Import | ✓ Hülle | ◐ „Geschichte mit KI“ rückgängig, nur `ComicKiOverride` | ✓ `T0697ComicPodcast_Ui` 8/8 am 26.11. | ✓ V/F/B/E/T, Rechtsklick, Ziehen, Ablegen |
| STUDIO-PODCAST-01 | Podcast aufnehmen | TEILWEISE (−) | TEILWEISE | ✓ Spuren → Schnitt → MP3 −16,4 LUFS mit Kapiteln | ◐ WAV rein; MP3 nur ffprobe/ebur128 (`reports/T-0697/podcast-ebur128.txt`) | ◐ Hülle; Absturz während Aufnahme ungeprüft | ◐ keine eigene KI, Claude-Weg ersetzt | ◐ Mikrofon ersetzt (`PodcMicOverride`), kein Mikrofon am PC | ✓ R/K/Leertaste/Entf, Rechtsklick je Spur, Ablegen |
| STUDIO-BOOK-01 | Bücher schreiben | TEILWEISE | TEILWEISE (Grenzfall) | ✓ Kapitel → EPUB/DOCX/PDF/ODT (`T0725_Fachstudios_Ui.Book_open_and_export`) | ✓ LibreOffice-DOCX rein, Export in LibreOffice (`T0725_Echtlauf_Ui`, `reports/T-0725/echtlauf.log`) | ✓ Hülle | ✓ lokale KI qwen3:14b ins Dokument, „Ja, behalten“, Rückgängig (`reports/T-0707/echtlauf-lokale-ki.md`, `buch-programm-nach-ki.tdoc` SHA-256 stimmt) | ✓ Klick → 3 Exporte | ◐ Ablegen, Stufen; kein Rechtsklick, keine eigenen Kürzel |
| STUDIO-ART-01 | Künstlerstudio | TEILWEISE (+) | TEILWEISE | ✓ malen, Auswahl umsetzen → PNG/ORA/PSD (`T0733_BildMalen_Ui.Art_select_move_scale`) | ◐ PSD nur Dateikopf „8BPS“ geprüft; Krita liegt im Trendo-Werkzeugordner, Gegenprobe dort nicht gemacht | ✓ Hülle | ◐ `KunstPromptAdapter` nur Malvorlage | ✓ Export-Klicks ohne Fremdwerkzeug | ◐ Tasten M/B/E; kein Rechtsklick, kein Ablegen (`ArtControls.cs`) |
| STUDIO-MUSIK-01 | Trendo Musik | TEILWEISE (+) | TEILWEISE | ✓ Beschreibung → 30-s-MP3 mit echtem ACE-Step (`T0738_KiEchtlaeufe_Ui.Musik_echt`) | ◐ MP3 nur ffprobe; Import eigener Aufnahme nicht echt belegt | ✓ ProgramLink + `Kill_while_autosaving_editor_link` | ◐ KI erzeugt echt (`reports/T-0738/musik-lauf.log`), Lied aber nicht rückgängig im Dokument | ✓ `Musik_echt` ohne Ersatz (nur mit `TRENDO_ECHT_KI`) | ◐ wenige Kürzel, 1 Rechtsklick, kein Ablegen |
| SPEC-15 | Live streamen | TEILWEISE | TEILWEISE | ✓ Sendung an lokalen RTMP mit Kopie und Neuverbinden (`T0746_StreamTv_Echt_Ui.Live_echt`) | ◐ nur ffprobe; keine Fachdatei | ✓ `Kill_while_saving_broadcast` (T-0715) | ◐ `ClaudeLiveRegie_Ui` nur ersetzt | ✓ `reports/T-0746/live-lauf.log` | ✗ keine Kürzel, kein Rechtsklick, kein Ablegen (`MainWindow.Live*.cs`) |
| STUDIO-TV-01 | TV & Live | TEILWEISE | TEILWEISE | ✓ HLS laden und aufnehmen → .ts (`T0746_StreamTv_Echt_Ui.Tv_echt`) | ◐ fremde M3U/HLS rein; .ts nur ffprobe | – kein Dokument (T-0715) | ◐ Senderfilter-KI nur ersetzt | ✓ `reports/T-0746/tv-lauf.log` | ◐ Vollbild/Leertaste; Rechtsklick/Ablegen ungetestet |
| STUDIO-PHOTOS-01 | Fotos verwalten | TEILWEISE | TEILWEISE | ✓ Ordner → Probelauf → Einsortieren → Rückgängig (`KreativStudios2_Ui.Photos`) | ◐ nur Platzhalter-JPGs nach Dateidatum; keine Kamerafotos/EXIF | ✓ Hülle (Einstellungen); Kopieren nicht abbruchgeprüft | ◐ nur allgemeiner Claude-Weg, ersetzt | ✓ Klick → echte Kopien | ◐ Rechtsklick auf Kachel; keine Kürzel, kein Ablegen |
| SPEC-08 | Kleidung & Produkte entwerfen | TEILWEISE | TEILWEISE | ◐ Teile frei umformen → DXF/SVG/PDF; neues Teil von Null nicht möglich | ◐ SVG in Blender maßgleich (`reports/T-0745/svg-blender-T-0745.log`); kein Fremdimport | ✓ Hülle | ◐ kein eigener KI-Befehl | ✓ `GarmentEdit_Ui.Click_to_file`; Anprobe ersetzt (`GarmentRunOverride`) | ✓ Punkte ziehen, Rechtsklick, Kürzel; kein Datei-Ablegen |
| X-07 | Freie Bilder & Klänge finden (in CORE-08) | TEILWEISE | TEILWEISE | ◐ Suchen/Laden über Oberfläche nur mit simuliertem Netz | ◐ Übergabe an Bildbearbeitung, Lizenz je Datei | – kein Dokument; Download atomar `.teil` + MD5 | ◐ „Quelle holen“ lädt nach Freigabe echt (`reports/T-0735/x07-echt.log`), kein Rückgängig | ◐ `W4_X07_Ui.Live` schluckt Netzfehler und zählt offline als bestanden (`W4AssetsUiTests.cs:36`) | ◐ nicht belegt |

### KI & Automatik (14)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| CORE-02 | KI fragen | TEILWEISE | TEILWEISE | ✓ Ollama mit Anhang, Antwort beim Entstehen (`ChatAttachT0734_Ui.Real_ollama_attachment_and_stream`, `reports/T-0734/testlauf-roh.txt`) | ◐ TXT/PDF/DOCX/PNG rein; Export nur Markdown, nie fremd geöffnet | ◐ atomar je Antwort, kein Absturztest; Anhänge gehen verloren | ◐ Antwort nur im Gespräch; Claude-Abo mit Anhang nicht getestet | ✓ derselbe Echtlauf | ◐ Ablegen gebaut, nicht als Ereignis getestet; kein Rechtsklick |
| CORE-03 | KI-Team einsetzen (in CORE-02) | TEILWEISE | TEILWEISE | ◐ Echtlauf legt `einladung.md` an (`reports/T-0741/team-ablauf.log`) | ✗ kein Import/Export | ◐ Stand atomar, fortsetzbar (`KiTeamT0741_Ui`); kein Prozessabbruch | ◐ Rollen schreiben Projektdateien, kein Rückgängig | ✓ `KiTeamEchtlauf_Ui` (Ollama qwen3:14b), `reports/T-0741/SHA256.txt` | ✗ keine Kürzel, kein Rechtsklick |
| CORE-12 | KI-Anweisungen schreiben (in CORE-02) | TEILWEISE | TEILWEISE | ◐ Editor mit Lücken `{{…}}`, Datei-Rundreise (`T0740_Ui`); Wirkung nie geprüft | ◐ .md/.json ein und aus; Wirkung in Claude Code nie geprüft | ◐ Notsicherung (`T0711_HuelleEtappe1b_Ui.Prompt_editor`), kein Absturz | ◐ Aktion „Vorlage füllen“ fragt keine KI (`CapabilityCatalog.cs:50`); Auftragszeile der Hülle ohne Test | ◐ kein echtes Werkzeug (Claude Code) | ◐ Strg+Z/Y, Rechtsklick; Ablegen nur über Methode |
| X-05 | Kostenlose KI finden (in CORE-02) | TEILWEISE | TEILWEISE | ✓ echter Download bis zur geprüften Installation (`KiChecksumLive_Ui`, `reports/T-0739/pruefsumme/`) | ✗ eigene Modelldatei (GGUF) einbinden nicht belegt | ◐ Ollama setzt fort, nicht mit Abbruch geprüft | ◐ nur „Lokale Quelle prüfen“ (`CapabilityCatalog.cs:76`), keine Fachaufgabe im Dokument | ✓ echte Datei von Hugging Face, manipuliertes Byte verworfen | ◐ nicht belegt |
| SYS-05 | Aufgaben automatisieren | TEILWEISE | TEILWEISE | ✓ Satz → Ablauf → Datei mit echtem Claude-Abo und schtasks (`reports/T-0742/0-ablauf.log`) | ✗ kein Import/Export außer n8n | ✓ atomar; echter Prozessabbruch `T0681SicherungAblaufTests` | ✓ Claude legt Ablauf im offenen Editor an, Rückgängig (`T0742_Ui.Satz_wird_Ablauf_im_Editor` + Echtlauf) | ✓ `T0742_Echtlauf_Ui`, `reports/T-0742/SHA256.txt` | ◐ Strg+Z/Y, Ziehen; kein Rechtsklick |
| X-01 | Großprojekte umsetzen (in X-11) | TEILWEISE | TEILWEISE | ✓ leere Tafel → 2 Karten → 2 PDFs (`GrossprojektUebergabe_Ui.Text_to_pdf_chain`, `reports/T-0743/`) | ◐ PDF mit pdftotext; kein Plan-Import | ◐ Tafel atomar; `uebergaben.json` nicht atomar | ◐ Rückmeldung an die Karte, nicht echt gelaufen | ✓ `Text_to_pdf_chain` ohne Ersatz (`reports/T-0743/testlauf-ende-zu-ende.log`) | ◐ Kürzel, Rechtsklick, Ziehen; keine Stufen |
| STUDIO-SUMMARY-01 | Texte zusammenfassen | TEILWEISE | TEILWEISE | ◐ weiter Satzauswahl, kein Sprachmodell | ◐ Import echt (`reports/T-0725/zusammenfassen`); Export nicht fremd geöffnet | ✓ Hülle | ◐ nur allgemeine Kette, ersetzt | ◐ Echtlauf nur für den Import | ◐ Ablegen, Hüllen-Kürzel; kein Rechtsklick |
| STUDIO-IMAGEAI-01 | Bilder mit KI | TEILWEISE | TEILWEISE | ✓ sd-cli SDXL Turbo 1024² (`T0738_KiEchtlaeufe_Ui.ImageAi_echt`, `reports/T-0738/bild-ki-lauf.log`) | ✗ kein Bild-zu-Bild, kein Fremdbild-Import | ✓ Hülle | ◐ Bild als „Bild gemalt“ rückgängig; Claude-Aktion schreibt nur `bildauftrag.txt` | ✓ derselbe Echtlauf, SHA 5/5 | ◐ nur Hüllen-Stufen |
| STUDIO-VIDEOAI-01 | Videos mit KI | TEILWEISE | TEILWEISE | ✓ MP4 mit Ton, Untertiteln, Wan-Clip (`VisionVideoT0699_Ui.Video_real_tools`, `reports/T-0699/roh/videos-mit-ki-echt.log`) | ◐ eigene Musik rein; MP4 nur ffprobe | ✓ Hülle | ◐ Wan-Clip rückgängig; Szenenplan ohne KI | ✓ sd-cli, Piper, FFmpeg, Wan echt | ◐ kein Rechtsklick, Szenen nicht ziehbar |
| STUDIO-AGENTS-01 | KI-Agenten bauen | TEILWEISE | TEILWEISE | ✓ „Agent starten“ → echte Datei (`AgentEchtlauf_Ui.Real_ollama/Real_claude`, `reports/T-0698/claude-lauf.md`) | ◐ .md ein/aus, .eml-Entwurf; nicht fremd geöffnet | ◐ Lauf atomar, „Unterbrochen“; kein Fortsetzen | ◐ Ergebnis als Projektdatei, kein Rückgängig | ✓ echtes Claude-Abo (33 s) + Ollama; SHA: Ergebnisdateien und `claude-lauf.json` stimmen, nur `claude-lauf.md` nicht | ◐ Hüllen-Stufen; kein Rechtsklick in der Lauf-Ansicht |
| STUDIO-MODELS-01 | KI-Modelle vergleichen | TEILWEISE | TEILWEISE | ✓ 20/20 echte Ollama-Antworten (`ModelsBatchLive_Ui`, `reports/T-0739/modelle/`) | ◐ CSV/TXT ein, CSV/MD aus; nie in Tabellenprogramm | ✓ Hülle | ◐ Antworten im Dokument, aber keine KI-Bewertung | ✓ derselbe Echtlauf inkl. Abbrechen/Fortsetzen | ◐ keine Kürzel, kein Rechtsklick, kein Ablegen |
| STUDIO-TRAIN-01 | Eigene KI trainieren | TEILWEISE | TEILWEISE | ✓ Training + Anwendung, 29/30 richtig (`T0740_Ui`, `reports/T-0740/`) | ◐ CSV/Ordner ein, CSV aus; Excel/LibreOffice-Gegenprobe fehlt | ◐ Hülle; laufender Stapel geht verloren | ◐ keine KI-Assistenz, nur Naive Bayes | ✓ `reports/T-0740/rohlog-ui-test.txt` | ◐ F5/Strg+E, Rechtsklick; nicht als Tastendruck getestet |
| STUDIO-CODEAI-01 | Code mit KI | TEILWEISE | TEILWEISE | ✓ lokale KI ändert Code, Python-Tests 3/3 grün (`CodeAiKi_Ui.Real_local_ki`, `reports/T-0723/`) | ◐ Ordner rein, .patch raus; `git apply` nicht getestet | ◐ Dateien atomar; Rückgängig überlebt keinen Neustart | ✓ „Mit KI ändern“: Vorschau, Ja/Nein, Rückgängig, echt mit lokaler KI (Claude-Abo nicht getestet) | ✓ echtes `python -m unittest` | ◐ Strg+Z/Y; kein Rechtsklick/Ziehen im Dateibaum |
| STUDIO-VISION-01 | Bilder erkennen | TEILWEISE | TEILWEISE | ✓ YOLOX + Tesseract (`Vision_real_tools`), VLM (`reports/T-0703/vlm-beschreibung.md`) | ◐ echte Fotos rein, XMP/CSV raus; nicht in digiKam | ✓ Hülle | ◐ VLM-Ergebnis rückgängig im Dokument; Claude nur per Skript | ✓ `Vision_real_tools` | ◐ kein Rechtsklick, kein Ordner-Ablegen |

### 3D & Spiele (5)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| CORE-05 | 3D-Modelle erstellen (3d, cad, 3d-druck) | TEILWEISE | TEILWEISE | ✓ Würfel → Netz → Extrudieren → glTF (`Studio3DMeshEditUiTests.Edit_extrude_undo_redo_export`) | ◐ Blender öffnet Export (`reports/T-0691/blender-oeffnen.log`); CAD-STEP nie in FreeCAD; 3D-Druck ohne Fremdexport | ◐ 3D/CAD `Studio3DCadCrashTests` (alter Schreibweg); Druckplatte nur im Speicher | ◐ `Claude3D_Ui` nur ersetzt | ◐ 3D/CAD echt; G-Code nur über Bambu Studio (fehlt am PC) | ◐ 3D/CAD Tasten, Rechtsklick, Ziehen; 3D-Druck ungeprüft |
| CORE-06 | Spiele bauen | TEILWEISE | TEILWEISE | ✓ Vorlage → malen → bauen → in Chrome gewonnen (`GameFree_Ui`, 5/5 am 26.11.) | ◐ eigene PNGs rein, Export in Chrome/Godot (`reports/T-0690/`); kein Fremdprojekt-Import | ✓ Hülle + ProgramLink | ◐ „Spiel aus einem Satz“ nur `GamePromptOverride` | ✓ echtes Chrome + Godot 4.7.2 (26.11.); Unreal `reports/T-0724/unreal` | ◐ Malen, Tasten im Probespiel; kein Bild-Ziehen |
| STUDIO-CONSOLE-01 | Konsolenspiele portieren | NICHT GETESTET | NICHT GETESTET (BLOCKIERT) | ✗ nur mit `FreePortRunnerOverride`; PS2Recomp/CMake fehlen (`reports/T-0752/bestandsaufnahme.log`) | ✗ kein gebautes Programm | ◐ Auswahl in der Hülle; Bau startet nach Absturz neu | ◐ nur Skill-Aufträge, ersetzt | ✗ `ConsolePort_Ui.Free_port_click_to_result` mit Ersatz | ◐ Stufen; kein Rechtsklick, kein Ziehen |
| SPEC-04 | 3D-Modelle ansehen (entfernt) | TEILWEISE | TEILWEISE | ◐ Prüfer liest GLB, schreibt Prüfbericht (`SPEC04_Ui`); kein eigenes 3D-Ergebnis | ◐ echte GLB rein; Web-Paket nicht fremd belegt | – reine Ansicht (T-0716) | ◐ `ModelCheckAdapter` schreibt Bericht, nicht ins Dokument | ✓ `SPEC04_Ui` Datei → Anzeige → Bericht | ◐ Stufen; kein Rechtsklick |
| SPEC-09 | Gegenstände einscannen | TEILWEISE | TEILWEISE | ✓ COLMAP echt, 48 Fotos → 4165 Dreiecke (`T0749EinscannenEcht_Ui.Fotos_zu_netz`, `reports/T-0749/`) | ◐ Export öffnet in Blender (`T-0749-blender-import.log`); Fotos selbst gerendert | ◐ Punktwolke in der Hülle; „Aus Fotos“ ohne Dokument | ◐ keine Scan-KI belegt | ✓ Echttest (nur mit `TRENDO_T0749_FOTOS`) | ◐ Schwenken; kein Kontextmenü |

### Technik & Fachgebiete (6)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| SPEC-01 | Wohnung planen | TEILWEISE | TEILWEISE | ✓ `T0747_DxfUi.Plan_import_edit_export`, `SPEC01_Ui` | ◐ DXF-Leser gegen FreeCAD und echte Pro/E-DXF (`reports/T-0760/`); DXF-Export nie in Fremd-CAD | ✓ Hülle | ◐ `ClaudePrograms_Ui` (architektur) nur ersetzt | ✓ Klick → DXF → wieder gelesen | ◐ Tasten, Ziehen; Rechtsklick nur Wandkette beenden (`FloorPlanView.cs:93`) |
| SPEC-02 | Schaltungen berechnen | TEILWEISE | TEILWEISE | ✓ `SPEC02_Ui`, `T0748_Ui` | ◐ KiCad-Netzliste selbst geschrieben; Gerber im Fremdwerkzeug BLOCKIERT (`reports/T-0748/T-0748-werkzeugsuche.log`) | ✓ Hülle | ◐ nur allgemeiner Claude-Weg, kein Test | ✓ `T0748_Ui.KiCad_import_bis_Gerber` → ZIP | ◐ rechte Maustaste nur Schwenken (`CircuitCanvas.cs:87`) |
| SPEC-03 | Statik nachrechnen | TEILWEISE | TEILWEISE | ✓ `SPEC03_Ui`, `T0748_Ui.Statik_Gegenprobe` | ✗ kein Import, kein Austauschformat | ✓ Hülle | ◐ nur allgemeiner Claude-Weg, kein Test | ✓ Klick → Bericht | ◐ rechte Maustaste nur Schwenken (`FrameCanvas.cs:113`) |
| SPEC-10 | Geräte überwachen (in SPEC-05) | TEILWEISE | TEILWEISE | ◐ PC messen, Regeln, Alarmdateien; kein Dokument | ◐ CSV rein, JSON/CSV raus; nicht fremd geöffnet | ◐ `LiveSlot`, kein eigener Abbruchtest | ◐ nur „Messreihe zeigen“ mit eingetippten Werten (`CapabilityCatalog.cs:65`) | ✓ `TwinAlarm_Ui.File_rule_alarm`, echte Windows-Zähler (`reports/T-0750/`) | ◐ Ziehen auf Pfadfelder; kein Kontextmenü |
| SPEC-13 | Routen & Karten | TEILWEISE | TEILWEISE | ✓ Route mit der Maus → GPX (`T0719_KartenTagesplaner_Ui`) | ◐ GPX handgeschrieben, eigenes Prüfskript (`reports/T-0719/pruefung.txt`) | ✓ Hülle | ◐ Wegpunkt per Assistenz nur `KiBackendsOverride` | ✓ Klick → GPX/KML/ICS | ✓ Ziehen, Rechtsklick-Menü, F2, Entf, Ablegen (`MapCanvas.cs`) |
| SPEC-14 | Fräsen & Fertigen | TEILWEISE | TEILWEISE | ✓ leer → SVG → Bahnen → `.nc` (`T0747_DxfUi.Cnc_import_tabs_export`) | ◐ echte Pro/E-DXF gefräst (`reports/T-0760/echte-dateien/echte-dxf.tsv`); G-Code nie im Fremd-Viewer | ✓ Hülle | ◐ nur allgemeiner Claude-Weg, kein Test | ✓ Klick → G-Code | ◐ Rechtsklick-Menü, Ablegen vorhanden; nicht getestet |

### Wissen & Lernen (5)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| SPEC-05 | Messwerte auswerten | TEILWEISE | TEILWEISE | ✓ CSV → Diagramm-PNG (`T0725_Fachstudios_Ui.Measure_csv_open_and_png`) | ◐ PNG in LibreOffice (`reports/T-0725/echtlauf.log`); kein XLSX/PDF | ✓ Hülle | ✓ Messreihe mit Ollama ins Dokument, rückgängig (`AssistChain_Ui.Real_local_ki`, `reports/T-0707/wissenschaft-nach-ki.tdoc`) | ✓ `T0725_Echtlauf_Ui` mit LibreOffice/qpdf | ◐ CSV-Ablegen; Rechtsklick nicht belegt |
| SPEC-06 | Lernen mit Karteikarten | TEILWEISE | TEILWEISE | ✓ Import + Rückgängig beim Löschen (`T0725_Fachstudios_Ui.Flashcards_file_import_and_undo_delete`) | ◐ Anki-Klartext nachgebildet; .apkg BLOCKIERT | ◐ atomar, außerhalb der Hülle; kein Absturztest | ◐ `karte.anlegen` schreibt nur eine Projektdatei (`StudyPromptAdapter.cs:27`); im Programm keine KI (`MainWindow.Spec06.cs`) | ◐ kein Fremdwerkzeug | ◐ Ablegen; Rechtsklick/Kürzel nicht belegt |
| CORE-10 | Wissen merken | TEILWEISE | TEILWEISE | ✓ Seiten mit Rückverweisen (`T0725_Fachstudios_Ui.Knowledge_export_and_reimport`) | ◐ Markdown nur von Trendo wieder eingelesen | ◐ atomar, außerhalb der Hülle | ◐ `WikiAskAsync` (`MainWindow.Wissen.cs:58`) antwortet, ändert kein Dokument | ◐ Klick → .md, kein Fremdwerkzeug | ◐ nicht belegt |
| SYS-08 | Sicher recherchieren | TEILWEISE | TEILWEISE | ✓ echte Wikipedia-/DuckDuckGo-Suche (`reports/T-0735/sys08-echt.log`) | ◐ Bericht-Export; kein Import | ◐ „Sammlung behalten“ atomar; sonst flüchtig | ◐ nur Übersetzen (`MainWindow.Research.cs:119`), nicht ins Dokument | ◐ Klickweg mit `ResearchSearchFetchOverride` | ◐ Enter; kein Rechtsklick |
| STUDIO-CITE-01 | Recherche mit Quellen | TEILWEISE | TEILWEISE | ✓ DOI → APA → RIS/CSL (`reports/T-0735/cite-echt.log`) | ◐ RIS-Rundreise; Zotero/Citavi/Pandoc BLOCKIERT | ✓ Hülle | ◐ `quelle.holen` landet im Projekt, nicht im Dokument | ◐ UI-Klick mit `CiteFetchOverride` | ◐ Enter; kein Rechtsklick, kein Ziehen |

### Entwickeln (5)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| CORE-07 | Webseite bauen | TEILWEISE | TEILWEISE | ✓ 5 Vorlagen → bauen/testen/Paket mit Startprobe (`DevBuild.cs:419`, `reports/T-0688/vorlagen-zusammenfassung.md`) | ◐ Export in echtem Chrome (`reports/T-0718/chrome/`); HTML/Figma-Import nur mit `WebDesignParseOverride` | ◐ `CodeDrafts.cs:34` AtomicFile alle 2 s; Test schließt nur das Fenster | ◐ Diff/Zurücknehmen nur mit `ClaudeRunOverride`; echter Lauf nur im Kern (`reports/T-0688/claude-echtlauf.log`) | ✓ `T0688_SoftwareStudio_Ui` (echt, Python/.NET) | ◐ F5, Kürzel, Rechtsklick im Baum; kein Ziehen |
| X-03 | Apps zusammenklicken (in CORE-07) | TEILWEISE | TEILWEISE | ✓ Einkaufsliste → Offline-App, bleibt nach Browser-Neustart (`reports/T-0765/rohlog.txt`) | ◐ Export in Chrome; Import nur eigener Exporte | ✓ echter Kill `T0716_HuelleE4Tests.Kill_while_saving_builder` | ◐ `seite.schreiben` rückgängig, aber fester Parser statt Modell | ✓ `T0765_AppBaukasten_Ui` 3/3 am 26.11. | ✓ Strg+Z/Y, F2, Entf, Rechtsklick, Bausteine ziehen (`MainWindow.W4Builder.cs`) |
| X-04 | Trendo erweitern | TEILWEISE | TEILWEISE | ✓ Werkstatt → WASM-Modul → Tabelle (`ExtensionSandbox.cs`, `T0766ErweiterungenUi`) | ◐ echte CSV, Export-ZIP mit tar.exe gelesen (`reports/T-0766/export-zip.log`); Katalog nachgebildet | ◐ `SaveOnPause` + Manifest atomar; kein Kill-Test | ◐ `erweiterung.entwerfen` schreibt nur einen Entwurf, ohne Test (`CapabilityCatalog.cs:84`) | ✓ echte Wasmtime + UI bis zur Datei | ✗ keine Kürzel, kein Rechtsklick, kein Ziehen |
| X-06 | Projekte von GitHub holen (in X-04) | TEILWEISE | TEILWEISE | ✓ ripgrep holen/starten/entfernen, Repo klonen + bauen (`reports/T-0762/x06-echtlauf.log`) | ◐ Installer-Lauf nur `GitHubInstallerRunOverride` | ◐ Freigaben an Fingerabdruck; kein Abbruchtest | ◐ KI erklärt nur die Lizenz, klont nicht (`CapabilityCatalog.cs:83`) | ✓ `T0762_GitHubNachbau_Ui.Pc_run_real_github` | ✗ nicht belegt |
| X-10 | Programme nachbauen (in X-04) | TEILWEISE | TEILWEISE | ✓ Anforderungen → Abgleich mit Python-Testlauf → Bericht (`CleanRoomRequirements.cs`) | ◐ .md nie fremd geöffnet; kein Import | ◐ AtomicFile, kaputte Datei gesichert; kein Kill-Test | ◐ `nachbau.beschreiben` schreibt Regel-.md, ohne Test | ✓ `T0762_GitHubNachbau_Ui.B3_reconcile_lock_switch_cancel` | ◐ gebaut, laut Bericht nicht im Test gedrückt |

### System & Sicherheit (15)

| Id | Name | Urteil 16.11. | Urteil jetzt | K1 | K2 | K3 | K4 | K5 | K6 |
|---|---|---|---|---|---|---|---|---|---|
| SYS-06 | Dateien teilen | VOLLWERTIG (eng) | TEILWEISE | ✓ Ordner teilen, Datei per HTTP, falsches Token abgelehnt (`ShareServer.cs`) | ◐ LocalSend Byte für Byte; kein zweites Gerät, kein Rohlog | – flüchtige Freigabe, kein Dokument | ◐ `datei.teilen` nur als Adapterliste getestet | ✓ UI-Test lädt echte Datei | ✗ nur Esc; keine Kürzel, kein Rechtsklick, kein Ziehen |
| CORE-01 | Desktop einrichten | TEILWEISE | TEILWEISE | ✓ Dateiaktionen mit Rückgängig/Wiederholen (`Shell/FileUndo.cs`, `T0754DateienSchutzUi`) | ◐ echter Windows-Papierkorb hin und zurück; Explorer-Ablegen ungeprüft | ◐ Rückgängig nur je Sitzung | ◐ nur `desktop.zeigen` (`CapabilityCatalog.cs:86`) | ✓ echte Dateien, SUBST-Laufwerk (`reports/T-0754/gruen3-core.log`) | ✓ Strg+Z/Y, Rechtsklick, Ziehen, Tasten je Stil (`OsStyles_Ui`) |
| SYS-01 | Startmodus wählen | TEILWEISE | TEILWEISE | ◐ Anzeige ehrlich; Stick-Schreiber nur Vorschau (`MainWindow.W7.cs`) | ◐ Abbild auf virtuelle Platte geschrieben/zurückgelesen; echter Stick nie | ◐ Abbruch beim Schreiben ungeprüft | ◐ nur `modus.zeigen` (`CapabilityCatalog.cs:87`) | ◐ nur Datei-VHD | ✗ nicht belegt |
| CORE-11 | Daten schützen | TEILWEISE | TEILWEISE | ✓ Sicherung mit Prüflesen (`Backup.CreateChecked`, `BackupUiTests`) | ◐ ZIP nie in 7-Zip; restic fehlt am PC (übersprungen) | ✓ `.teil` + Umbenennen; echter Kill `T0681SicherungAblaufTests.cs:31` | ◐ `geheimnis.zeigen` nennt nur den Speicher (`CapabilityCatalog.cs:88`) | ✓ BackupUi/T0754 mit echten Dateien | ✗ nicht belegt |
| SPEC-12 | System auf Gefahren prüfen | TEILWEISE | TEILWEISE | ✓ Autostart/Aufgaben/Programme mit Authenticode → HTML/CSV (`Sys/Gefahren.cs`, `reports/T-0755/spec12-systempruefung-echt.txt`) | ◐ CSV/HTML nie fremd geöffnet; yara-x nur `YaraRunOverride` | ◐ Bericht atomar; kein Abbruchtest | ✗ keine KI-Aktion: bewusst gesperrt (`CapabilityCatalog.cs:89`, Verbesserung 34) | ✓ `T0755_Gefahren` echte Signaturen (26.11. selbst gelaufen) | ✗ Kürzel/Rechtsklick fehlen (T-0755) |
| SYS-03 | Downloads & Links | TEILWEISE | TEILWEISE | ✓ Download mit SHA-256, Verlauf, Links (`T0755_Ui.Downloads_and_links`) | ◐ nur lokaler Testserver | ◐ `.part`-Fortsetzung; kein Kill-Test | ◐ `datei.laden` nach Freigabe, nur Adapterliste (`PromptActionTests.cs:1563`) | ✓ echtes HTTP, Anhalten/Fortsetzen | ✗ fehlt (T-0755) |
| SYS-04 | Programme installieren | TEILWEISE | TEILWEISE | ◐ Rückfrage, Live-Lauf, Nachprüfung – nur gegen `winget-ersatz.cmd` (`W3/Packages.cs`) | – kein Fachdateiformat | ◐ Protokoll je Lauf; kein Absturztest | ◐ `programm.installieren` nach Freigabe, nur Adapterliste | ✗ echter winget-Lauf nie | ✗ nicht belegt |
| SYS-11 | Programme starten (in SYS-04) | TEILWEISE | TEILWEISE | ✓ Entfernen → Sicherung → Wiederherstellen (`Sys/TrendoPrograms.cs`, `SYS11_TrendoProgramme`) | ◐ CSV/winget-Liste nur innerhalb Trendo | ◐ Abbruch zwischen Umbenennen lässt Ordner in `.entfernt` | ◐ `programm.starten` nur Adapterliste | ✓ echte Ordner/ZIP, mpv (`reports/T-0764/mpv-pruefung-fix10.log`) | ✗ nur Knöpfe |
| SYS-07 | Geräte erkennen (in Einstellungen) | TEILWEISE | TEILWEISE | ◐ Liste echt; Auswerfen gebaut, nie echt (`W3/DeviceEject.cs`) | – kein Dateiformat | – nur Anzeige | ◐ `geraete.nennen` nur Liste (`CapabilityCatalog.cs:93`) | ◐ `SYS07_Auswerfen_Ui` mit ersetzten Laufwerken | ✗ nicht belegt |
| SYS-12 | Schrift & Farben anpassen (in Einstellungen) | TEILWEISE | TEILWEISE | ✓ Einstellungen wirken und bleiben; Kinderprofil im Kern (`RechteKernTests`) | ◐ Update echt nur portabel (`reports/T-0724/setup/setup-rundlauf-portabel.log`); Setup.exe BLOCKIERT | ◐ kein Absturztest | ◐ `darstellung.zeigen` nur Anzeige (`CapabilityCatalog.cs:95`) | ◐ Updater in der UI ersetzt (`RechteKernFix2Ui.Rueckkehr`) | ◐ Suche; Bildschirmleser ungeprüft |
| SYS-13 | Qualität prüfen (in Einstellungen) | TEILWEISE | TEILWEISE | ✓ „Alles prüfen“: 7 Bereiche mit Ampel (`Sys/Sys13.cs`, `T0763SystemAlltagUi.Check_everything`) | – kein Fachformat | – schreibt keine Dateien (`N1_check_only_writes_no_files`) | ◐ `pruefung.starten` ohne Test | ✓ T0763-Core 26.11. grün | ◐ Stufen getrennt; keine Kürzel; zwei goldene „Alles prüfen“ im Bild |
| X-02 | Einrichten & Reparieren (in Einstellungen) | TEILWEISE | TEILWEISE | ✓ Installation gegen Velopack-Paket geprüft, auf Kopie ersetzt (`reports/T-0763/installation-echt.txt`) | ◐ echtes Paket; Nachholen nur `InstallOverride` | ◐ AtomicFile; kein Kill-Test | ◐ KI-Auftrag nur Prüfung, Reparatur gesperrt (`CapabilityCatalog.cs:97`, Verbesserung 35) | ✓ `Real_installation_copy` + UI `Repair_installation` | ✗ nur Rückfrage-Knöpfe |
| X-09 | Leistung anzeigen (in Einstellungen) | TEILWEISE | TEILWEISE | ✓ Messung weicht 0,08/0,13 Prozentpunkte von Windows ab (`reports/T-0763/lastprobe.txt`) | – kein Dateiformat | – Anzeige | ◐ `leistung.lesen` nur Schnappschuss (`CapabilityCatalog.cs:100`) | ✓ `Load_probe_against_performance_counters` (26.11. grün) | ◐ sortier-/durchsuchbar; Rechtsklick nicht belegt |
| X-08 | Handy verbinden (in CORE-04) | TEILWEISE | TEILWEISE | ◐ Kopplung, Dateien in beide Richtungen; Gegenstelle nur Chrome (`reports/T-0767/browser-lauf.log`) | ◐ bitgenau hin und zurück (SHA-256); keine Handy-App | ◐ Teil-Datei + Alles-oder-nichts | ◐ `handy.nennen` nur adb-Liste (`CapabilityCatalog.cs:98`) | ◐ Chrome statt Android; adb fehlt | ◐ Rechtsklick/Ziehen gebaut, nicht ausgelöst |
| CORE-04 | PC, Pi & Handy verbinden | TEILWEISE | TEILWEISE | ◐ Liste und Begleiter-Server; Pi nicht im Angebot | ✗ kein Lauf mit Pi/Fremdgerät | ◐ nicht vertieft geprüft | ◐ `verbund.zeigen` nur Liste (`CapabilityCatalog.cs:99`) | ◐ 14 Katalogtests, alle intern (`reports/echtlauf/testschichten.md`) | ✗ nicht belegt |

---

## 3. Die häufigsten verbleibenden Lücken

1. **K4 – Die KI ändert das offene Dokument fast nirgends nachweisbar.** Nur 5 von 80 Studios haben ✓: Tagesplaner, Bücher schreiben, Messwerte (alle drei mit echter lokaler KI, `reports/T-0707/`), Aufgaben automatisieren und Code mit KI. Nur SPEC-12 hat gar keine KI-Aktion (gesperrt).
   - Die Büro-Adapter liefern seit 0.9.103 unverändert nur TXT-Dateien: Lebenslauf, Karte, Vertrag, PDF-Auftrag, Formular und Scan.
   - Der allgemeine Claude-Weg ins Dokument (`src/Trendo.App/MainWindow.ClaudeProgramDoc.cs`) ist in allen Kreativ- und Technik-Tests mit `ClaudeRunOverride` ersetzt.
   - Die KI-Knöpfe der Kreativ-Programme (z. B. „Ideen mit Claude“ in Logos, „Bewegung mit Claude planen“ in Animation) laufen über `AssistFromProgram` (`MainWindow.AssistChain.cs:55`) ins offene Dokument mit Vorschau und Rückgängig. Sie sind aber in keinem Test ausgelöst.
   - Echte Claude-Abo-Läufe aus der App gibt es nur für Aufgaben automatisieren (`reports/T-0742`) und KI-Agenten (`reports/T-0698`).
   - Programme außerhalb der Hülle melden „Noch nicht im Dokument möglich“. Das betrifft Text, Tabelle, Präsentation, Geld, Verkaufen und Vorhaben.
2. **K2 – Die Prüfung läuft mit dem eigenen Werkzeug statt mit einem Fremdprogramm.**
   - MP4 und MP3 aus FFmpeg liest nur ffprobe. PSD ist nur am Dateikopf geprüft. GPX, ICS und CSV prüfen eigene Skripte.
   - Microsoft Office, Thunderbird, Zotero, Anki, KiCad und gerbv fehlen auf dem PC.
   - FreeCAD 1.1 ist inzwischen vorhanden, wurde aber für CAD-STEP und DXF-Export noch nicht genutzt.
   - Im Trendo-Werkzeugordner liegen außerdem Krita (PSD/ORA), Inkscape (SVG) und 7-Zip (ZIP). Keines davon wurde bisher für eine Gegenprobe genutzt.
   - LibreOffice liegt im Trendo-Werkzeugordner und lief am 26.11. Die Berichte T-0692, T-0694 und T-0753 nennen es dagegen „nicht installiert“. Ein Nachlauf für Text, Geld-XLSX und Shop-CSV ist ohne Installation möglich.
   - **Nachtrag T-0776:** Alle diese Nachläufe sind gemacht (`reports/T-0776/`): LibreOffice (Text, Geld-XLSX, Shop-CSV), FreeCAD (CAD-STEP, CAD-DXF, Wohnungs-DXF), Krita (PSD, ORA), Inkscape (Logo-/Animations-SVG), 7-Zip (Sicherungs- und Erweiterungs-ZIP). Dabei gefunden und behoben: FreeCAD las Trendos STEP ohne Flächen; Krita las PSD-Ebenennamen mit Füllbytes und ohne Hintergrund. Die Studio-Zeilen unten sind der Stand vor T-0776.
3. **K6 – Bedienung auf der Arbeitsfläche ist uneinheitlich.**
   - Rechtsklick fehlt in Bewerbung, Karten, Formulare, Briefe, Vorhaben, POD, Trendo PDF, Bild, Ton, Künstlerstudio, Live streamen und auf fast allen System-Seiten.
   - Ablegen mit echten Fenster-Ereignissen (DragEnter → DragOver → Drop, `tests/Trendo.App.Tests/BueroAusbauUiTests.cs:58-68`) testen die Büro-Studios, Großprojekte, Karten/Tagesplaner, die T-0725-Programme und Medien bearbeiten.
   - Anderswo ist Ablegen nur über Methodenaufrufe (z. B. `DropFiles`, `SellDropFiles`) getestet.
   - Ein Ziehen aus dem echten Windows-Explorer ist nirgends geprüft; die Ereignisse sind headless erzeugt.
4. **K3 – Programme außerhalb der Hülle haben keinen Abbruchtest.**
   - Betroffen sind Geld (Kassieren ohne Transaktion beim Verkaufen), Vorhaben, Karteikarten, Wissen, Druckplatte (nur im Speicher), „Aus Fotos“ und KI fragen (Anhänge).
   - Alle Programme der Gruppe System und Entwickeln stehen im Katalog mit `Shell=false`.
5. **Wege nach außen nie echt.**
   - Nie echt gelaufen sind:
     - winget
     - echter Mail-Anbieter
     - Printful, Printify, Stripe
     - Netlify und echtes WebDAV
     - GitHub-Installer
     - USB-Stick-Auswurf
     - yara-x
     - Android-Handy und Pi
     - Setup.exe-Rückfall
     - Mikrofon
     - rtmps an eine Plattform
   - Ursache meist: Konto, Gerät oder Werkzeug fehlt. Das heißt BLOCKIERT, nicht simuliert.
6. **Doku passt nicht zum Code.**
   - `docs/programme/katalog.json` führt 97 von 123 Programmen als „Test NOT_RUN“, obwohl viele heute echte Läufe haben.
   - Umgekehrt stehen SYS-04, SYS-07, CORE-04 und X-08 auf `PC_VERIFIED`, obwohl ihr Kernweg ersetzt ist.
7. **Stille Tests.** Diese Tests kehren ohne Belegordner oder Netz mit `return` zurück bzw. schlucken Netzfehler und zählen dann als bestanden statt „übersprungen“. Suche: `return;` nach Umgebungs- oder Dateiprüfung ohne `Assert.Skip`, `catch (HttpRequestException) { }`.

   | Test | Fundstelle | Gestütztes Urteil | Rohlog unter reports/ | Folge |
   |---|---|---|---|---|
   | `AssistChain_Ui.Real_local_ki` | `AssistChainUiTests.cs:208` | K4 ✓ Tagesplaner, Bücher, Messwerte | `reports/T-0707/` (die drei `.tdoc` stimmen per SHA) | bleibt ✓ |
   | `W4_X07_Ui.Live` | `W4AssetsUiTests.cs:37` | X-07 K5 | – | war schon ◐ |
   | `CodeAiKi_Ui.Real_local_ki` | `CodeAiKiUiTests.cs:171` | CODEAI K1/K4/K5 ✓ | `reports/T-0723/` (Code-Dateien und Diff stimmen) | bleibt ✓ |
   | `AgentEchtlauf_Ui.Real_ollama` / `Real_claude` | `AgentEchtlaufUiTests.cs:59`, `:79` | AGENTS K1/K5 ✓ | `reports/T-0698/` (Ergebnisdateien, `claude-lauf.json` stimmen) | bleibt ✓ |
   | `KiTeamEchtlauf_Ui` | `KiTeamEchtlaufUiTests.cs:24`, `:35` | CORE-03 K5 ✓ | `reports/T-0741/SHA256.txt` (14/14) | bleibt ✓ |
   | `ChatAttachT0734_Ui.Real_ollama_attachment_and_stream` | `ChatAttachT0734UiTests.cs:265` | CORE-02 K1/K5 ✓ | `reports/T-0734/` (3/3) | bleibt ✓ |
   | `KiChecksumLive_Ui` | `KiChecksumLiveUiTests.cs:20` | X-05 K1/K5 ✓ | `reports/T-0739/pruefsumme/` | bleibt ✓ |
   | `ModelsBatchLive_Ui` | `ModelsBatchUiTests.cs:234` | MODELS K1/K5 ✓ | `reports/T-0739/modelle/` | bleibt ✓ |
   | `T0742_Echtlauf_Ui` | `T0742AblaufAusSatzUiTests.cs:219`, `:298` | SYS-05 K1/K4/K5 ✓ | `reports/T-0742/SHA256.txt` (7/7) | bleibt ✓ |
   | KI-Modell-Echtläufe | `KiModelT0703UiTests.cs:123`, `:161`, `:194`; `KiModelT0712UiTests.cs:51`; Core `KiModelT0712Tests.cs:124` | VISION K1 (VLM), X-05 | `reports/T-0703/` | bleibt |
   | Tagesplaner-Beleg | `T0719KartenTagesplanerUiTests.cs:53` | nur Belegschreiben, Hauptprüfung läuft immer | `reports/T-0719/` (14/14) | keine |
   | Mail-Rundreise | Core `MailRoundtripTests.cs:300` | SYS-09 K5 | – | war schon ◐ |
   | Installation | Core `T0763SystemAlltagTests.cs:218` | X-02 K1/K5 ✓ | `reports/T-0763/installation-echt.txt` | bleibt ✓ |
   | GitHub/Handy-Netz | `W4OssUiTests.cs:26`, `PhoneSendT0767Tests.cs:116` | X-06 (Echtlauf über `Pc_run_real_github` mit Rohlog), X-08 K5 ◐ | `reports/T-0762/`, `reports/T-0767/` | keine |
   | weitere Kleinfälle | Core `MeshEditTests.cs:21`, `PdfToolsTests.cs:25`, `RechteKernFix1Tests.cs:79`, `RechteKernFix4Tests.cs:122`, `RechteKernFix5Tests.cs:58`, `T0755Fix3Tests.cs:20`, `T0755Fix4Tests.cs:19`; App `DesignPaket3UiTests.cs:139` | kein Urteil stützt sich allein darauf | – | keine |

   - **Ergebnis:** Kein ✓ musste herabgestuft werden. Jedes ✓, das sich auf einen stillen Test stützt, hat zusätzlich einen Rohlog unter `reports/`.
   - **Trotzdem:** Alle diese Tests sollten ohne Voraussetzung „übersprungen“ melden (`Assert.Skip`). Sonst zeigt der normale Testlauf grün, obwohl nichts Echtes lief.

8. **Belegfehler (SHA-256 nachgerechnet, mit und ohne CRLF).**
   - `reports/T-0698`: Die Ergebnisdateien und `claude-lauf.json` stimmen (`81fdb708…`), nur `claude-lauf.md` stimmt nicht; ebenso `ollama-lauf.md`.
   - Weitere Beschreibungs- und Rohlog-Dateien stimmen nicht mit ihrer Liste überein:
     - `reports/T-0723/echtlauf-code-mit-ki.md`, `testlauf-roh.txt`
     - `reports/T-0707/echtlauf-lokale-ki.md`, `rohlog-testlauf.txt`, `ausserhalb-der-huelle.md`
     - `reports/T-0703/vlm-laden.md`
   - Wahrscheinlich wurden sie nach dem Hashen noch bearbeitet. Die eigentlichen Ergebnisdateien (Code, Diff, `.tdoc`, Bilder) stimmen.
   - Die Urteile bleiben deshalb, aber die Listen sollten neu erzeugt werden.

## 4. Gestaltung (Nebenbefund aus den Bildern)

Nebenbefunde zur Gold-Regel und zum Leerzustand:

- **Tagesplaner:** „Tag planen“ ist zweimal golden, im Menüband und in der Zeitleiste. Der Knopf „Woche“ ist sichtbar, laut T-0719 ist die Wochenansicht aber nicht gebaut.
- **PDF-Werkstatt (Einfach):** Die Karte „erster Schritt“ verdeckt den Text unter „Dateien (0)“.
- **Animation:** Gold liegt auf „Text“ in der Gruppe EBENE statt auf einer Hauptaktion.
- **KI fragen:** drei goldene Knöpfe, und die zweite Chat-Seite „Trendo KI (lokal)“ besteht weiter.
- **Qualität prüfen:** zweimal goldenes „Alles prüfen“.
- **Dokumente scannen:** Der Knopf „Scanner …“ zeigt nur einen Hinweis.

## 5. Bilder

Alle Bilder liegen in `bilder/` (13 PNG, 1600 × 1000, zusammen 2,2 MB). Vier der fünf Grenzfälle hat die Hauptsitzung zusätzlich selbst angesehen; für Bücher schreiben gibt es kein Bild (Nachtrag aus der Gegenprüfung, K4 über `reports/T-0707` belegt).

| Bild | Studio | Befund |
|---|---|---|
| `g1-pdf-werkstatt.png` | PDF-Werkstatt (Einfach) | Leerzustand, erster Schritt golden; Karte verdeckt Text |
| `g1-scannen-beispiel.png` | Dokumente scannen | echte Fläche: Original mit Eckpunkten, entzerrtes Ergebnis |
| `g1-tagesplaner.png` | Tagesplaner | echte Zeitleiste mit Terminblöcken |
| `g1-geld-rechnungen.png` | Geld & Rechnungen | Formularseite, keine Hülle |
| `g2-logo-marken.png` | Logos & Marken | echte Vektor-Fläche, Inspektor, Vorschau hell/dunkel/klein |
| `g2-filme-machen.png` | Filme machen | Fountain-Drehbuch und gesetzte Seite, „Film rendern“ golden |
| `g2-animation.png` | Animation & Motion | Bühne, Zeitleiste mit Schlüsseln |
| `g3-cnc-tasche.png` | Fräsen & Fertigen | Inspektor, G-Code, Draufsicht mit Bahnen |
| `g3-konsole-freies-spiel.png` | Konsolenspiele | BLOCKIERT: PS2Recomp, CMake, C++20 fehlen |
| `g4-ki-fragen.png` | KI fragen | echtes Chatfenster, drei goldene Knöpfe |
| `g4-agenten-bauen.png` | KI-Agenten bauen | formularartige Fläche, „Agent starten“ golden |
| `g5-baukasten.png` | Apps zusammenklicken | echte Fläche mit Handy-Vorschau |
| `g5-sys13-alles-pruefen.png` | Qualität prüfen | Ampel-Ergebnis „Fast alles in Ordnung“ |

## 6. Nicht geprüft

- **Testläufe:**
  - Selbst gelaufen sind nur die Testklassen aus dem Kopf dieses Berichts.
  - Nicht selbst gelaufen, sondern aus Berichten und Rohlogs übernommen:
    - harte Abbruchtests: `ProgramCrashTests`, `T0714_HuelleE2Tests`, `T0711_LinkLocksTests`
    - Echtläufe aus T-0689, T-0693, T-0694, T-0695, T-0698, T-0699, T-0701, T-0703, T-0707, T-0719, T-0723, T-0731–T-0753, T-0760, T-0762, T-0764, T-0766 und T-0767
- **Belege nachgerechnet:**
  - SHA-256-Summen stichprobenhaft: `reports/T-0691`, `T-0725`, `T-0735`, `T-0738`, `T-0742` und `T-0743` stimmen. In `T-0698`, `T-0703`, `T-0707` und `T-0723` stimmen die Ergebnisdateien, einzelne Beschreibungsdateien nicht (Abschnitt 3.8).
  - Bei den übrigen Rohlogs ist nur geprüft, dass sie existieren.
- **Werkzeuge:** Kein echter Claude-Aufruf durch diese Prüfung; Claude-Belege sind nur gelesen. Es wurde nichts installiert und kein winget-Lauf gestartet.
- **Bilder:** Nur 13 Bilder. Ohne eigenes Bild sind unter anderem Texte & Tabellen, E-Mails, Briefe, Bewerbung, Karten, Verträge, Formulare, Präsentationen gestalten, Videoschnitt, Bild, Ton, Musik, TV, Live, 3D, Spiele, Wohnung, Schaltungen, Statik, Karten und alle System-Seiten außer „Alles prüfen“.
- **Bedienung:** Einfach- und Profi-Stufe sind nur je Gruppe stichprobenhaft angesehen. Tastenkürzel wurden nirgends als echter Tastendruck geprüft.
- **Nicht angesehen:**
  - Wochenansicht des Tagesplaners
  - Bambu-LAN im 3D-Druck
  - Unity-Export
  - OSRM-Routing
  - Multicam und Plugin-Host im Audio
  - Musik-Unterbereiche
  - TV-Programmführer
  - Seiten `einrichtung`, `projects`, `studios`, `start`, `lizenz`, `personen`, `bedien-sehen`, `fortschritt`, `hardware`, `components`, `apps`, `approvals` und `verbund`
- **Bewertungsentscheidungen, die anders ausfallen könnten:**
  - Hülle plus allgemeiner Abbruchtest zählt für K3 als ✓.
  - Die Claude-Läufe mit `ClaudeRunOverride` auf das echte `claude.exe` zählen als echt.
  - Ersetzte Dateiauswahl zählt nicht als Ersatz.
