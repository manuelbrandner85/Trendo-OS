# Trendo OS – Professionelles Bestandsaudit (Phase A)

Stand: Arbeitskopie `ba52927a8` (Version 0.9.103, noch nicht auf main), 16.11.2026.
Geprüft: 83 Studios, 125 Programme, 110 KI-Aktionen und das gemeinsame System.

**Methode:** Sechs unabhängige Prüfläufe haben nur gelesen – kein Build, kein Testlauf. Jede Aussage ist in den Teilberichten mit Datei und Zeile belegt. Vorhandener Code zählt nicht als fertig. Ein Test, in dem das eigentliche Werkzeug ersetzt ist, zählt nicht als Ende-zu-Ende-Nachweis.

**Grenzen dieses Audits:** Die Prüfer haben nichts ausgeführt, also sind Laufzeitverhalten, Startzeit und Speicherbedarf nicht gemessen. Einige Seiten sind nur stichprobenhaft gelesen; jede Teildatei listet das am Ende unter „Nicht geprüft“. Die Gesamturteile sind Einschätzungen aus Code und Testquelltext, keine Abnahme.

Teilberichte im selben Ordner, je Studio mit Kriterientabelle 1–15, Lücken, Abnahmekriterien, Aufwand und Dateipfaden:

- `audit-1-buero-business.md`
- `audit-2-kreativ-medien.md`
- `audit-3-3d-spiele-technik.md`
- `audit-4-ki-wissen.md`
- `audit-5-entwickeln-system.md`
- `audit-6-architektur.md`

---

## Kurzfassung

Trendo OS ist keine Attrappe: Fast überall rechnet, speichert und exportiert echter Code. Es ist aber auch noch kein professionelles System.

**Urteile über die 83 Studios**

| Urteil | Anzahl | Studios |
|---|---|---|
| VOLLWERTIG | 2 | PDF-Werkstatt; Dateien teilen (im engen Zuschnitt) |
| TEILWEISE | 78 | – |
| FEHLT | 1 | „Filme machen“: es entsteht kein Film |
| NICHT GETESTET | 1 | Konsolenspiele portieren |
| abgelehnt | 1 | SPEC-07 |

**Die sechs Hauptursachen**

1. **Arbeit kann verloren gehen.** Die Notsicherung wird nicht absturzsicher geschrieben und nie zum Wiederherstellen angeboten. Rund 85 der 125 Programme laufen außerhalb der gemeinsamen Programm-Hülle; ein Teil davon hat gar kein Autosave (3D-Studio, CAD, Code-Editor, Briefe).
2. **Die KI liefert meist Text statt Produkt.** Von 110 KI-Aktionen erzeugen 18 ein Fachformat, 67 nur Text oder Markdown, 25 sind gesperrt. Claude kann das offene Dokument nur in 39 Programmen wirklich ändern.
3. **Viele Programme sind Formulare mit Vorschau.** Auf der Arbeitsfläche lässt sich nichts anfassen, zum Beispiel bei Animation, Comics, Logos, Podcast, Karten, Bewerbung und Tagesplaner.
4. **Namen versprechen mehr als die Leistung.** Beispiele: Filme machen, Videos mit KI, KI-Agenten bauen, Apps zusammenklicken, Programme nachbauen, System auf Gefahren prüfen, Podcast aufnehmen.
5. **Der echte Weg ist selten nachgewiesen.**
   - 243 Ersatz-Haken in den Tests.
   - Kein Test mit echtem Claude.
   - Unreal, Godot, Unity, LibreOffice, Tesseract, Bild-KI und Musik-KI sind im Volltest ersetzt oder übersprungen.
   - Mindestens 15 Tests zählen ohne Werkzeug still als bestanden.
   - Installation und Update sind zuletzt für 0.9.10 echt belegt.
6. **Freigaben schützen nur in der Oberfläche.** Die Desktop-App arbeitet überall als fester Besitzer, und an 28 Stellen erteilt sich die Freigabe im selben Klick selbst.

---

## A. Tatsächlicher Reifegrad aller Studios

Legende: **V** vollwertig · **T+** teilweise, stark · **T** teilweise · **T−** teilweise, schwach (Formular) · **F** fehlt · **NG** nicht getestet.

Die Zusätze + und − sind nur für die Gruppe Kreativ & Medien aus dem Teilbericht übernommen. Die anderen Teilberichte unterscheiden nicht so fein; ihre Studios stehen einheitlich als T. Wo ein Teilbericht „nah an vollwertig“ vermerkt, steht das in der Spalte „Größte Lücke“.

### Arbeiten & Büro, Business & Geld

16 Studios: 1 V, 15 T.

| Studio | Urteil | Größte Lücke | Aufwand |
|---|---|---|---|
| SYS-10 Texte & Tabellen (Text, Tabelle, Präsentation, PDF) | T | Fremde DOCX/XLSX/PPTX werden beim Speichern ohne Warnung ärmer. Text ist ein Eingabefeld je Absatz. Abgleich mit Word/Excel/PowerPoint nie gelaufen. | sehr groß |
| SYS-09 E-Mails & Termine | T | Mail liest nur 50 Kopfzeilen und sendet Klartext ohne Anhang. Kein Server-Abgleich, nie gegen einen echten Anbieter getestet. Die Seite „E-Mail & Kontakte“ fehlt als Fachprogramm. | groß |
| SYS-02 Kleine Helfer & Musik | T | Ton nur mit nachinstalliertem Player, Video in fremdem Fenster. | mittel |
| STUDIO-LETTER-01 Briefe | T | Formular-Assistent, kein Autosave, öffnet keine fremden DOCX. | mittel |
| X-11 Neues Projekt | T | Kein Export. Die KI-Aktion legt etwas anderes an als das Programm. | mittel |
| STUDIO-CV-01 Bewerbung | T | Formular mit Vorschau, kein Import, KI nur TXT. | mittel |
| STUDIO-CARDS-01 Karten | T | Kein freies Gestalten auf der Karte. | groß |
| STUDIO-CONTRACTS-01 Verträge | T | Liste und Formular, Dokumente nur verknüpft. | mittel |
| STUDIO-PDFTOOLS-01 PDF-Werkstatt | **V** | KI-Aktion nur Text, Schwärzen macht die ganze Seite zum Bild, kein Ziehen und Ablegen. | klein–mittel |
| STUDIO-FORMS-01 Formulare | T | Nur an selbst erzeugten Formularen getestet, kein XFA. | mittel |
| STUDIO-DECKDESIGN-01 Präsentationen gestalten | T | Generator ohne Bearbeitung auf der Folie. | mittel–groß |
| STUDIO-DOCSCAN-01 Scannen | T | Kein Scanner, Texterkennung im Test ersetzt, nur künstliche Testfotos. | mittel–groß |
| STUDIO-DAYPLAN-01 Tagesplaner | T | Listen statt Zeitleiste, kein Verschieben, nichts geht in den Kalender. | mittel |
| SPEC-11 Geld & Rechnungen | T | Rechnung nur als HTML ohne Steuernummer. Kein PDF, kein Bankimport, kein DATEV. | groß |
| STUDIO-ONLINE-BUSINESS-01 Online verkaufen | T | Shop ist eine statische Vorschau ohne Bestellung und Zahlung. | groß |
| STUDIO-POD-01 Print on Demand | T | Anbieter nur gegen Nachbildung getestet, Gelato fehlt. | mittel |

### Kreativ & Medien

14 Studios: 13 T, 1 F.

| Studio / Programm | Urteil | Größte Lücke | Aufwand |
|---|---|---|---|
| Videoschnitt | T+ | Vorschau zeigt Farbe, Übergänge, Einblendungen und Keyframes erst nach dem Export. | groß |
| Bildbearbeitung | T+ | Kein PSD/TIFF/WebP, keine Werkzeug-Tastenkürzel. | mittel |
| Audio bearbeiten | T+ | Eine Datei je Spur, Aufnahme fest 10 s, Effekte mit festen Werten. | mittel |
| Medien bearbeiten | T | Umwandler mit 7 festen Aufgaben. | klein |
| X-07 Freie Bilder & Klänge | T | KI-Aktion darf nichts laden. | klein |
| SPEC-08 Kleidung | T | Schnitt nur aus Vorlagen. | groß |
| SPEC-15 Live streamen | T | Echtes Senden an eine Plattform nie geprüft. | mittel |
| STUDIO-ANIM-01 Animation | T− | Schlüsselbilder über Textfelder, keine Bühne zum Anfassen. | groß |
| STUDIO-ART-01 Künstlerstudio | T+ | Keine Auswahl-, Transformier- und Textwerkzeuge. PSD nur am Dateikopf geprüft. | mittel |
| STUDIO-BOOK-01 Bücher | T | Ein Markdown-Feld, kein Satz, kein PDF/DOCX. | mittel |
| STUDIO-COMIC-01 Comics | T− | Formular, kein Zeichnen, keine freien Sprechblasen. | groß |
| STUDIO-FILM-01 Filme machen | **F** | Es entsteht kein Film, nur Drehbuch und Listen. | sehr groß |
| STUDIO-LOGO-01 Logos | T− | Generator mit 5 Formen, kein Vektor-Editor. | groß |
| STUDIO-MUSIK-01 Trendo Musik | T+ | Lied-Erzeugung in den Tests ersetzt. Die KI-Aktion schreibt nur einen Liedplan. | mittel |
| STUDIO-PHOTOS-01 Fotos | T | Keine Bildansicht im Programm. | klein |
| STUDIO-PODCAST-01 Podcast | T− | Nimmt nicht auf. Fehler: ohne FFmpeg bleibt ein Knopf bis zum Neustart tot. | groß |
| STUDIO-TV-01 TV & Live | T | Abspielen und Aufnehmen in allen UI-Tests ersetzt. | klein |

### 3D, Spiele & VR, Technik & Fachgebiete

13 Studios: 12 T, 1 NG.

| Studio | Urteil | Größte Lücke | Aufwand |
|---|---|---|---|
| CORE-05 3D, CAD, 3D-Druck | T | 3D ohne Autosave, Wiederholen und Absturzschutz, keine Netzbearbeitung. CAD ohne Import. G-Code nur über Bambu Studio. | groß |
| CORE-06 Spiele bauen | T | Spielbar ist nur der 2D-Baukasten mit vier festen Spielarten. Unreal, Godot und Unity sind in allen automatischen Tests ersetzt. | sehr groß |
| STUDIO-CONSOLE-01 Konsolenspiele | NG | Startpult für Claude Code, kein Beleg eines echten Laufs im Repo. | groß |
| SPEC-04 VR & AR (entfernt) | T | Modell-Prüfer, kein Gestaltungswerkzeug. | groß |
| SPEC-09 Einscannen | T | Fotoscan nie echt belegt. | mittel |
| SPEC-01 Wohnung planen | T | Kein DXF/IFC-Import, DXF ohne Rundreise-Test. Nah an vollwertig. | mittel |
| SPEC-02 Schaltungen | T | Kein Import, Gerber ungeprüft. Nah an vollwertig. | mittel |
| SPEC-03 Statik | T | Nur ebenes Stabwerk, keine Gegenprobe. | mittel |
| SPEC-10 Geräte überwachen | T | Anzeige ohne Dokument. | mittel |
| SPEC-13 Karten | T | Routing nur mit eigenem Server. Nah an vollwertig. | mittel |
| SPEC-14 Fräsen | T | Kein CAM, nur drei feste Formen. | groß |
| SPEC-16 Auto (entfernt) | T | Listenprogramm. | klein |
| CORE-09 Roboter (entfernt) | T | Nur Simulation. | groß |

### KI & Automatik, Wissen & Lernen

19 Studios, alle T. SPEC-07 ist abgelehnt.

| Studio | Größte Lücke | Aufwand |
|---|---|---|
| CORE-02 KI fragen | Kein Dateianhang, zwei Chat-Seiten, kein echter Ende-zu-Ende-Test. | mittel |
| CORE-03 KI-Team | Textkette Rolle → Rolle, keine Werkzeuge, keine Parallelität. | groß |
| CORE-12 KI-Anweisungen | Formular, Skill-Wirkung nie echt geprüft. | mittel |
| X-05 Kostenlose KI | Am weitesten in der Gruppe. Modelle ohne feste Prüfsumme. | mittel |
| STUDIO-SUMMARY-01 | Satzauswahl nach Worthäufigkeit statt Sprachmodell. | mittel |
| STUDIO-IMAGEAI-01 | Bild-KI nie echt getestet, nur Text→Bild. | mittel |
| STUDIO-VIDEOAI-01 | Diashow aus Standbildern ohne Ton. | groß |
| STUDIO-AGENTS-01 | Schreibt eine Datei, es läuft kein Agent. | groß |
| STUDIO-MODELS-01 | Nur Einzelfragen von Hand. | mittel |
| STUDIO-TRAIN-01 | Trainiert echt (Naive Bayes), das Modell wird nirgends genutzt. | mittel bis sehr groß |
| STUDIO-CODEAI-01 | „Mit Claude ändern“ ändert keine Datei. | mittel |
| STUDIO-VISION-01 | Bildprüfung statt Erkennung. | mittel–groß |
| SYS-05 Abläufe | Läuft echt, aber nur bei offenem Trendo. 7 Schritte, keine Wiederholung nach Fehler. | groß |
| X-01 Großprojekte | 4 feste Ketten. | groß |
| CORE-10 Wissen | Nur Stichwortsuche, kein Export. | mittel |
| SPEC-05 Messwerte | Nah an vollwertig. CSV nur über eingetippten Pfad. | mittel |
| SPEC-06 Karteikarten | Kein Anki-Import, kein Rückgängig. | mittel |
| STUDIO-CITE-01 Quellen | Nah an vollwertig. Kein DOI/ISBN-Import. | mittel |
| SYS-08 Recherche | Keine Suche, Sitzung wird bei jedem Start gelöscht. | mittel–groß |

### Entwickeln, System & Sicherheit

20 Studios: 1 V, 19 T.

| Studio | Urteil | Größte Lücke | Aufwand |
|---|---|---|---|
| CORE-07 Webseite bauen | T | Das Web-Design-Studio ist stark. Der Software-Teil baut nur vier Hallo-Vorlagen, „Testen“ ist nur ein Build, es gibt kein Paket. Der Code-Editor hat kein Autosave. | groß |
| X-03 Apps zusammenklicken | T | Es entsteht keine App, nur eine statische Seite. | groß |
| X-04 Trendo erweitern | T | Erweiterungen sind nur Daten. | groß |
| X-06 GitHub-Projekte | T | Nur mit nachgebildetem Netz getestet. | mittel |
| X-10 Programme nachbauen | T | Verwaltungswerkzeug, entwickelt nichts. | mittel |
| CORE-01 Desktop | T | Dateiaktionen ohne Rückgängig. | mittel |
| CORE-04 Geräteverbund | T | Echte Pi- und Android-Hardware nie geprüft. | groß |
| CORE-11 Daten schützen | T | Eine halbe ZIP kann als neueste Sicherung geführt werden. Unverschlüsselt. | mittel |
| SPEC-12 Gefahren prüfen | T | Keine Systemprüfung. | groß |
| SYS-01, SYS-03, SYS-07, SYS-11, SYS-12, SYS-13, X-02, X-09 | T | Anzeigen und Verwaltung; Details im Teilbericht. | klein–mittel |
| SYS-04 Programme installieren | T | Installieren und Entfernen ohne die versprochene Rückfrage. Echter Lauf ungetestet. | mittel |
| SYS-06 Dateien teilen | **V** | Nur Lesen, unverschlüsselt im Heimnetz. | klein |
| X-08 Handy | T | Kein Lauf mit echtem Handy. | groß |

---

## B. Bereits vollständige Fähigkeiten (belegt)

- **PDF-Werkstatt:** zusammenfügen, teilen, ordnen, komprimieren (gemessen), echtes Schwärzen, Vergleich.
- **Dateien teilen** im Heimnetz.
- **Tabelle und Präsentation:** echte Arbeitsflächen, Formeln, Export, Autosave mit Wiederherstellung.
- **Videoschnitt:** Export mit FFmpeg ist echt und getestet.
- **Bildbearbeitung, Audio, Künstlerstudio:** Die Kernfunktionen laufen real.
- **Web-Design-Studio:** echte Projektdateien, responsive Ansichten, geprüfter Export, Tests in echtem Chrome.
- **Fachkerne mit echter Gegenprobe:** IFC-Export, Schaltungssimulation (gegen SpiceSharp), Open3D, glTF-Prüfung, Vektorkarten, MQTT, Blender-Rundlauf „Für Engine“.
- **Abläufe:** Zeitplan, Auslöser, Freigabe, Not-Stopp und Protokoll laufen wirklich.
- **Sicherung:** Die Rundreise ist gut getestet, restic auch echt.
- **Fehlende Werkzeuge** werden überwiegend sauber gemeldet, nicht simuliert.
- **Programm-Hülle** für 39 Programme: Speichern, Versionen, 100 Rückgängig-Schritte, Bedienstufen.

## C. Teilweise implementierte Fähigkeiten

- **Speichern und Wiederherstellen:** vorhanden, aber nicht absturzsicher und uneinheitlich (14 eigene Rückgängig-Systeme, zwei Notsicherungssysteme).
- **Office:** liest und schreibt DOCX/XLSX/PPTX, verliert aber Inhalte fremder Dateien ohne Warnung.
- **Spiele:** Die Wege zu Unreal, Godot und Unity sind gebaut. Echt belegt sind sie nur durch Einzelläufe vom 06./07.11.
- **Claude:** Die Brücke ins offene Dokument funktioniert für 39 Programme. Es gibt vier getrennte Assistenzwege statt einer Kette.
- **KI-Studios:** Lokale Modelle, Bild-KI und Musik-KI sind angebunden, im Test aber durchweg ersetzt.
- **Rechte:** Sie sind in der lokalen API und im Workflow serverseitig erzwungen, in der Desktop-App nicht.

## D. Fehlende Funktionen für vollwertige Fachprogramme

1. **Ein Film entsteht nirgends.** Die Kette Drehbuch → Aufnahmen → Schnitt → Export fehlt.
2. **Spiele-Studio:** Ein spielbares Spiel jenseits von vier festen 2D-Arten gibt es nicht mit regelmäßigem Nachweis.
3. **3D:** Punkte, Kanten und Flächen lassen sich nicht bearbeiten. **CAD:** kein Import. **CNC:** kein CAM aus Zeichnung oder Modell.
4. **Software-Studio:** Es gibt kein echtes Testen, Paketieren und Veröffentlichen, kein Terminal und keinen Debugger.
5. **Office:** Es fehlen ein durchgehender Texteditor mit Seitenansicht, verbundene Zellen und Zellformate. Fremde Dateien sind nicht geschützt.
6. **Mail:** Anhänge, HTML, Ordner und Server-Abgleich für Kalender und Kontakte fehlen.
7. **Rechnungen:** PDF und E-Rechnung fehlen im Hauptprogramm. Die Bausteine dafür liegen in der unfertigen Business-Etappe.
8. **Direkte Bearbeitung auf der Fläche** fehlt bei Animation, Comics, Logos, Karten, Tagesplaner, Bewerbung und Präsentationen gestalten.
9. **Echte Aufnahme** fehlt bei Podcast und bei Audio (länger als 10 s).
10. **KI-Studios:** Es fehlen laufende Agenten, echtes KI-Video und Bild-Erkennung mit einem Modell. „Mit Claude ändern“ müsste die Datei wirklich ändern.
11. **Bedienung:** Ziehen und Ablegen aus dem Explorer auf Arbeitsflächen, Tastenkürzel und Rechtsklick-Menüs gibt es fast nur im Videoschnitt.

## E. Technische Fehler und Architekturprobleme

**Echte Fehler mit Fundstelle**

| Fehler | Fundstelle |
|---|---|
| Notsicherung nicht atomar; `.autosave`-Nebendatei wird nie angeboten | `src/Trendo.Core/Programs/ProgramDocument.cs:223`, `:215` |
| Sicherung kann eine halbe ZIP als neueste führen | `src/Trendo.Core/Backup.cs:21-29` |
| Podcast: ohne FFmpeg bleibt ein Merker gesetzt, der Knopf ist bis zum Neustart tot | `src/Trendo.App/MainWindow.KreativStudios.cs:278-279` |
| Abläufe: Fehlschlag setzt trotzdem „zuletzt gelaufen“; beschädigte Datei wird still leer gelesen | `src/Trendo.Core/W2/Workflows.cs:230`, `:86`, `:261` |
| „Ausführen“ und „Testen“ blockieren die Oberfläche bis 120/180 s | `src/Trendo.App/MainWindow.CodeEditor.cs:94` |
| App-Center installiert ohne die im Text versprochene Rückfrage | `src/Trendo.App/MainWindow.AppCenter.cs:30-32` |
| Strg+S überschreibt fremde Office-Originale mit verarmtem Inhalt | `src/Trendo.App/MainWindow.Text.cs:54`, `:423` |
| Vorschau-Chrome läuft mit `--no-sandbox` | `src/Trendo.Core/Website/WebsitePreview.cs:164` |
| `requirements.yaml` hat kaputte Umlaute | Zeilen 538, 540, 545 |

**Architekturprobleme**

- Das Hauptfenster besteht aus 338 Teildateien mit 76 446 Zeilen, das sind 89 % der App.
- Es gibt 83 statische Ersatz-Haken.
- Rückgängig kopiert je Schritt das ganze Dokument samt eingebetteter Bilder.
- Das Dateiformat schreibt `"version": 1`, liest es aber nie; es gibt keine Migration.
- Es gibt sieben Katalogsysteme und zwei Reife-Systeme.
- Der Volltest dauert etwa 45 Minuten.

**Doku passt nicht zum Code**

- `CapabilityCatalog` beschreibt mehrere Studios falsch.
- `katalog.json` führt entfernte Studios als geprüft und ordnet Tests falsch zu.
- `requirements.yaml` weicht in beide Richtungen ab.
- `docs/STUDIO_REBUILD_4_0` nennt noch 58 Studios.

**Widerspruch, der eine Entscheidung braucht:** Der Code verbietet die claude.ai-Anmeldung in Trendo an einer Stelle (`src/Trendo.Core/W2/ClaudeCode.cs:46`) und bietet sie an anderer eingebettet an (`ClaudeWeb.cs:14`). Zusätzlich ist Claude über die bezahlte API wählbar, obwohl die Vorgabe „nie API“ lautet.

## F. Dateipfade und Abhängigkeiten

- **Programm-Hülle:** `src/Trendo.App/MainWindow.ProgramShell.cs`, `src/Trendo.Core/Programs/ProgramDocument.cs`. Davon hängen alle 39 Hüllen-Programme ab; die übrigen müssen umziehen.
- **KI-Kette:** `src/Trendo.Core/PromptAction/` (Adapter, `StudioWorkflow`, `CapabilityCatalog`, `StudioKnowledge`), `src/Trendo.App/MainWindow.ClaudeBridge.cs`, `TrendoMcp.cs`, `src/Trendo.Core/Programs/Assist.cs`.
- **Rechte:** `ApprovalGate`, `Rbac`, `AuditLog`, `LocalApi.cs:33-51`.
- **Externe Werkzeuge:** `StudioTools.Required` deckt nur 7 Studios ab. Die Belege echter Läufe liegen unter `reports/paket01/` und `reports/alle-studios/`.
- **Rohliste aller 243 Ersatz-Haken:** steht im Teilbericht 6 mit Datei und Zeile.

Die Dateipfade je Studio stehen in den Teilberichten.

## G. Priorisierte Maßnahmen mit Aufwand und Risiko

| Rang | Maßnahme | Aufwand | Risiko |
|---|---|---|---|
| 1 | Datenverlust schließen: atomar schreiben, Wiederherstellung anbieten, Sperre gegen zweites Fenster, Test mit echtem Prozessabbruch; Sicherung prüflesen | klein–mittel | gering |
| 2 | Fremde Office-Dateien schützen: Hinweis auf nicht übernommene Inhalte, Speichern als Kopie statt Überschreiben | klein–mittel | gering |
| 3 | Gefundene Fehler beheben (Podcast-Knopf, Abläufe, App-Center-Rückfrage, blockierende Oberfläche) | klein | gering |
| 4 | Ehrliche Namen und ehrlicher Status: stille Test-Rückkehr wird „übersprungen“, `requirements.yaml` und Kataloge aus Code und Belegen | mittel | gering |
| 5 | Pflicht-Echtläufe mit Beleg je Release: Claude, Blender, Unreal, Godot, FFmpeg, LibreOffice, Bild-/Musik-KI, Setup-Rundlauf als Skript | mittel–groß | gering |
| 6 | Freigaben in den Kern: Wirkungen nur über Kern-Dienste mit Freigabe-Tor | groß | mittel |
| 7 | Ein Assistenzweg: Claude ändert das offene Dokument in jedem Programm, das Ergebnis ist eine echte Datei | groß | hoch |
| 8 | Alle Programme in die Programm-Hülle (ein Speicher-, ein Rückgängig-System), Dateiformat mit Version und Migration | sehr groß | hoch, nur etappenweise |
| 9 | Fachausbau je Studio (siehe I) | sehr groß | mittel |
| 10 | Testschichten statt Ersatz-Haken, Volltest unter 15 Minuten | groß | mittel |

## H. Abnahmekriterien

**Für jedes Studio** – erst dann gilt es als VOLLWERTIG:

1. Aus einer leeren Vorlage entsteht über die Oberfläche ein echtes Ergebnis des Fachs, ohne Umweg über Text.
2. Eine echte Fremddatei des Fachs wird importiert, bearbeitet und exportiert. Der Export öffnet sich im Vorbild- oder Fremdprogramm; der Nachweis liegt als Beleg unter `reports/`.
3. Ein Prozessabbruch mitten in der Arbeit verliert höchstens 30 Sekunden. Beim nächsten Start wird die Wiederherstellung angeboten.
4. Claude führt im Programm eine Fachaufgabe aus. Das Ergebnis steht im offenen Dokument und ist rückgängig machbar, mit Zustimmung bei Wirkung nach außen.
5. Ein Test fährt vom Klick bis zur echten Datei mit dem echten Werkzeug. Fehlt das Werkzeug, heißt der Test „übersprungen“ und das Studio „BLOCKIERT“.
6. Einfach- und Profi-Stufe, Tastenkürzel, Rechtsklick und Ziehen und Ablegen funktionieren auf der Arbeitsfläche.

**Je Studio:** Die fachlichen Abnahmesätze (3–5 je Studio) stehen in den Teilberichten jeweils unter „Abnahmekriterien“.

## I. Implementierungsplan in Arbeitspaketen (Vorschlag)

Jedes Paket bekommt einen eigenen Zweig, Tests und einen Bericht mit Belegen. Merge und Release erfolgen nur nach Freigabe.

**Phase B – Fundament.** Zuerst, weil alle Studios davon abhängen.

- **B1 Datenverlust schließen** (Rang 1–3): Notsicherung, Wiederherstellung, Sicherung, Schutz fremder Office-Dateien, die gefundenen Fehler. Klein bis mittel.
- **B2 Ehrlichkeit:** stille Tests werden „übersprungen“, Status und Kataloge werden erzeugt, Namen und Beschreibungen passen zur Leistung. Mittel.
- **B3 Echtlauf-Station:** ein Skript, das Claude, Blender, Unreal, Godot, FFmpeg, LibreOffice, Bild- und Musik-KI und das Setup wirklich laufen lässt und Belege schreibt. Mittel bis groß.
- **B4 Freigaben in den Kern.** Groß.
- **B5 Ein Assistenzweg:** Claude arbeitet im offenen Dokument. Groß.
- **B6 Programm-Hülle für alle,** in Etappen je Kategorie, mit Dateiformat-Version. Sehr groß.

**Phase C – Software, Web, 3D, Spiele.**

- **C1 Spiele:** Ein echtes, frei gestaltbares Spiel je Engine mit regelmäßigem Echtlauf. Sehr groß.
- **C2 3D und CAD:** Netzbearbeitung, Import, Autosave. Groß.
- **C3 Software-Studio:** bauen, testen, paketieren, Terminal. Groß.
- **C4 Web:** Veröffentlichen über das Freigabe-Tor mit echtem Nachweis. Mittel.

**Phase D – Medien, Office, weitere Fachstudios.**

- **D1 Office:** durchgehender Texteditor, Zellformate, Abgleich mit Fremdprogrammen. Sehr groß.
- **D2 Film:** Kette vom Drehbuch bis zum fertigen Film. Sehr groß.
- **D3 Videoschnitt-Vorschau mit Effekten.** Groß.
- **D4 Formular-Programme auf direkte Bearbeitung umbauen:** Animation, Comics, Logos, Podcast, Karten, Tagesplaner. Je groß.
- **D5 Mail und Rechnungen.** Groß.
- **D6 KI-Studios echt machen:** Agenten, Code mit KI, Bilder erkennen, Videos mit KI. Groß.
- **D7 Übrige Fachstudios** nach den Lücken der Teilberichte.

**Phase E – Abnahme.** Echtläufe auf Zielhardware, Leistung, große Projekte, Installation, Update und Deinstallation.

### Offene Entscheidungen des Nutzers

1. **Claude-Anmeldung:** Soll die eingebettete claude.ai-Anmeldung bleiben, und soll der API-Weg entfernt werden?
2. **Büro-Etappe (7 Studios, 0.9.103) und Business-Etappe (11 Studios, unterbrochen):**
   - Büro: Nach diesem Audit sind 6 der 7 Studios TEILWEISE. Sollen sie so nach main, oder erst nach dem Ausbau?
   - Business: fertigbauen oder zurückstellen, bis das Fundament steht?
3. **Die übrigen 45 neuen Studios:** Empfehlung ist, keine weiteren neuen Studios zu bauen, bevor Phase B steht. Sonst wächst die Zahl der Teilweise-Programme weiter.
4. **Entfernte Studios (SPEC-04, SPEC-16, CORE-09):** Soll der Code bleiben oder gelöscht werden?
