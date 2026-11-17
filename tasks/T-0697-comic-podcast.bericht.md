# Bericht T-0697 – D4b: Comics & Storyboards und Podcast aufnehmen als echte Programme

**Branch:** `claude/T-0697-comic-podcast` (Basis `claude/T-0685-b1-integration`).

## Was war (Audit 2026-11-16)
- STUDIO-COMIC-01: Formular – feste Aufteilungen, Textfelder je Bild, „Bild wählen“; kein Zeichnen, keine frei gesetzten Sprechblasen; Export nur SVG/Markdown.
- STUDIO-PODCAST-01: „Aufnehmen“ sprang ins Live-Studio, kein Schnitt, keine Wellenform; Lautheit/Kapitel ohne Test mit echtem FFmpeg.
- Design-Audit Abschnitt 3: Comic ohne sichtbaren Weg zu Text/Bild, Podcast mit drei Startknöpfen.

## Was ist jetzt
### Comics & Storyboards (`MainWindow.ComicStudio.cs`, `ComicControls.cs`, `Core/Kreativ/ComicBook.cs`)
- Mehrere Seiten (Seitenleiste, „+ Seite“, Bild ↑/↓). Freie Bildfelder: Raster nur Vorschlag, Felder verschieben, Ecken einzeln ziehen, neues Feld aufziehen (F).
- Zeichnen direkt im Bildfeld mit dem Mal-Kern des Künstlerstudios (Pinsel, Radierer, Farbe, Stärke, Ebenen je Seite); der Strich bleibt im Feld, in dem er beginnt.
- Bilder einsetzen (Knopf, Rechtsklick, Ablegen auf ein Feld), Ausschnitt mit Alt+Ziehen / Alt+Mausrad / Regler.
- Sprechblase, Gedankenblase, Ausruf, Textkasten frei, Größe ziehbar, Dorn mit Griff, Schriftgröße, automatischer Zeilenumbruch.
- Storyboard-Ansicht (Beschreibung, Kamera, Dauer je Feld), Übergabe an „Filme machen“ über `FilmImport` (jedes Feld eine Einstellung).
- Ausgabe: Vektor-PDF (DIN A4 je Seite), PNG je Seite (PDFium aus dem PDF, 2000 px breit), CBZ mit ComicInfo.xml, SVG, Bildfelder als PNG-Folge, Storyboard-Liste.
- KI: „Geschichte mit KI“ schreibt ins offene Heft, ein Schritt, rückgängig.
- Erster Schritt im Leerzustand, eine goldene Hauptaktion, Kürzel V/F/B/E/T, Entf, Strg+D, Pfeile, F2, Esc, Bild ↑/↓, Rechtsklick-Menüs.
- Alte Dokumente (Aufteilung + Texte je Bild) werden gelesen und umgewandelt.

### Podcast aufnehmen (`MainWindow.PodcastStudio.cs`, `Core/Kreativ/PodcastEdit.cs`)
- „● Aufnehmen“/„■ Stopp“ (R) im Programm über den vorhandenen FFmpeg-Mikrofonweg (dshow), je Aufnahme eine Sprecherspur. Ohne FFmpeg/Mikrofon/Erlaubnis oder im Testmodus: ehrlicher Hinweis, nichts vorgetäuscht.
- Wellenform je Spur, Entf löscht die markierte Stelle aus allen Spuren (Kapitel rücken mit), Pausen kürzen, Kapitel an der Marke (K), Anhören (Leertaste), Rechtsklick je Spur.
- Dateien laden/hineinziehen (WAV/MP3 direkt, M4A/OGG/FLAC per FFmpeg nach WAV); Intro/Outro mit Blende.
- „Folge fertig machen (MP3)“: −16 LUFS in zwei Durchgängen (loudnorm), MP3 mit ID3v2.3-Tags und Kapiteln; RSS nur als Datei. T-0681-Merker unverändert korrekt.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~T0697|FullyQualifiedName~KreativKits"` → Bestanden, 43/43.
- App gezielt (`Trendo.App.Tests.exe -class …`), alle Fehler 0: T0697ComicPodcast_Ui 6/6, KreativStudios_Ui 5/5, T0681_Ui 6/6, ProgramHeadDesign_Ui 6/6, Kie_Ui 3/3, EinfachBedienen_Ui 11/11, SimpleMode_Ui 5/5, ClaudePrograms_Ui 6/6, ClaudePolish_Ui 3/3, ClaudeOperate_Ui 3/3, ClaudeBridge_Ui 7/7, ClaudeLive_Ui 3/3, ClaudeLiveSync_Ui 3/3, ClaudeRunFix_Ui 3/3, StudioUebergabe_Ui 4/4, FilmSchnitt_Ui 5/5.
- Beleg: `TRENDO_BELEG=reports/T-0697 Trendo.App.Tests.exe -class Trendo.App.Tests.T0697ComicPodcast_Ui` (Rohlog `reports/T-0697/app-tests-T0697.log`, 6/6), danach Fremdprüfung mit Python/ffprobe/ffmpeg → `reports/T-0697/pruefung.txt` mit SHA-256.
- Fremdprüfung: PDF 3 Seiten, MediaBox 595 × 842 pt (A4), Schriften Comic Sans/Segoe eingebettet; CBZ zip-Prüfsumme ok, 001–003.png (2000 × 2829) + ComicInfo.xml; MP3 16,5 s, Tags title/artist/album/genre/track/date, 2 Kapitel (0–12 s „Begrüßung“, 12–16,5 s „Teil zwei“), ebur128 integriert **−16,4 LUFS** (Ziel −16 ±1).

## Abnahmekriterien
| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Aus leerer Vorlage echtes Ergebnis | VOLLWERTIG | `Comic_von_der_leeren_Seite_bis_PDF`, `Podcast_aufnehmen_schneiden_fertig` (echtes FFmpeg) |
| 2 | Fremddatei rein, bearbeiten, raus, im Fremdprogramm | TEILWEISE | WAV/PNG rein; MP3 von ffprobe/ffmpeg, PDF von PDFium, CBZ/PNG/PDF mit Python geprüft (`pruefung.txt`); kein Comic-Reader-Programm geprüft |
| 3 | Absturz verliert ≤ 30 s | VOLLWERTIG (Hülle) | Programm-Hülle (Autosicherung 20 s, AtomicFile); Mikrofon schreibt direkt in eine WAV; nicht neu getestet |
| 4 | KI im offenen Dokument, rückgängig | TEILWEISE | Comic `Comic_KI_ins_offene_Dokument` (KI-Antwort ersetzt); Podcast ohne neue KI-Funktion |
| 5 | Test vom Klick bis zur Datei | VOLLWERTIG | beide App-Tests über Maus-/Tastenwege |
| 6 | Einfach/Profi, Kürzel, Rechtsklick, Ziehen und Ablegen | TEILWEISE | Kürzel, Rechtsklick, Maus-Ziehen getestet; Datei-Drop nur über dieselbe Funktion, nicht über das echte Drop-Ereignis |

## Ehrlich ersetzt / nicht geprüft
- **Mikrofon ersetzt:** Im Ende-zu-Ende-Test liefert `PodcMicOverride` beim „Stopp“ eine im Test erzeugte WAV („ersatz-mikrofon-*.wav“). Schnitt, Lautheit, MP3, Kapitel und Tags laufen mit echtem FFmpeg 9.0.2. Echte Mikrofonaufnahme nicht automatisch geprüft.
- KI-Antwort im Comic-Test ersetzt (`ComicKiOverride`).
- Arbeitsfläche zeichnet mit Avalonia, PNG/CBZ aus dem PDF – Schriftbild kann leicht abweichen.
- Wellenform mit Spuren nicht im Render-Bild geprüft; Comic leer/Beispiel/Storyboard/Profi und Podcast leer/Interview im Bild geprüft.
- Kein Volltest (macht die Hauptsitzung).

## Angepasster bestehender Test
- `KreativStudios_Ui.Comic`: prüft die frei gesetzten Blasen im Modell statt `d.List("panels")[i]["speech"]`; gleiche Aussagen, kein Assert entfernt.

## Offene Punkte
- Echte Mikrofonaufnahme von Hand prüfen; keine Pegelanzeige während der Aufnahme.
- Mehrere Mikrofone gleichzeitig (derzeit nacheinander, erstes Mikrofon).
- Comic-Reader als Gegenprobe für CBZ; Bild-KI je Bildfeld nicht in diesem Paket.

## Status
Implementiert und gezielt getestet; nicht nach main/Integrationszweig gemergt.
