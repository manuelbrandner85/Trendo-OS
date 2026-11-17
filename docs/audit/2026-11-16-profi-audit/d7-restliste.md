# D7 – Übrige Fachstudios: Restliste (T-0725)

Stand: 16.11.2026, Zweig `claude/T-0725-fachstudios-rest` (Basis `claude/T-0685-b1-integration`).
Grundlage: `BERICHT.md` und die Teilberichte `audit-1…5` (Abschnitte „Was fehlt“). Vorgabe des Nutzers: alle Studios bleiben erhalten, müssen aber funktionieren.

## Was nicht auf dieser Liste steht (schon von anderen Paketen abgedeckt)

| Bereich | Paket |
|---|---|
| Texte & Tabellen (SYS-10: Text, Tabelle, Präsentation) | T-0682, T-0692, T-0701 |
| E-Mails (SYS-09 Mail) | T-0689 |
| Spiele (CORE-06), 3D/CAD (CORE-05) | T-0690, T-0691 |
| Webseite bauen / Software-Studio / Web veröffentlichen (CORE-07, X-03 Veröffentlichen) | T-0688, T-0718 |
| Filme machen, Videoschnitt, Animation, Logos, Comics, Podcast | T-0693, T-0695, T-0696, T-0697 |
| Geld & Rechnungen (SPEC-11) | T-0694 |
| KI-Agenten, Bilder erkennen, Videos mit KI | T-0698, T-0699 |
| Büro-Etappe (Bewerbung, Karten & Einladungen, Verträge, PDF-Werkstatt, Formulare, Präsentationen gestalten, Scannen) | T-0702, T-0705 |
| Routen & Karten (SPEC-13), Tagesplaner | T-0719 |
| Hülle/Absturzschutz für alle, Freigaben, Assistenzweg, Menüleiste | T-0680/81, T-0706/07/08/11/13–17, T-0710 |
| SPEC-04 → „3D-Modelle ansehen“ | T-0722 (läuft, nicht angefasst) |
| Code mit KI | T-0723 (läuft, nicht angefasst) |
| SPEC-16, CORE-09 | gelöscht (T-0720); SPEC-07 abgelehnt; SYS-06 Dateien teilen VOLLWERTIG |

## A. In T-0725 bearbeitet (Reihenfolge nach Nutzen)

| # | Studio / Programm | Audit-Urteil | Lücke laut Teilbericht | Jetzt |
|---|---|---|---|---|
| 1 | STUDIO-LETTER-01 Briefe (`brief`) | TEILWEISE | „eine fremde DOCX/ODT lässt sich nicht als Brief öffnen“ (audit-1, Kriterium 4/5, Was fehlt 4); Autosave inzwischen T-0708 | „Brief öffnen (Word …)“ + Ablegen: Anschrift, Ort/Datum, Betreff, Anrede, Text, Gruß, Anlagen erkannt; speichert neuen Brief, Original bleibt |
| 2 | STUDIO-BOOK-01 Bücher (`buch-programm`) | TEILWEISE | „ein Markdown-Textfeld; kein Satz, kein PDF/DOCX, kein Import“ (audit-2, Was fehlt 2+3) | „Manuskript öffnen …“ (DOCX/ODT/TXT/MD, auch Ablegen), Export Word, Druck-PDF (Titelseite, Kapitel je Seite), OpenDocument |
| 3 | STUDIO-PHOTOS-01 Fotos verwalten | TEILWEISE | „keine Bildansicht im Programm“ (audit-2) | Raster mit echten Vorschaubildern, Doppelte markiert, Großansicht mit Datum/Größe/Ziel, Doppelklick/Rechtsklick → Bildbearbeitung |
| 4 | STUDIO-SUMMARY-01 Texte zusammenfassen | TEILWEISE | „kein Dateiimport“ (audit-4, Was fehlt 1) | „Datei öffnen …“ + Ablegen: PDF (PDFium), DOCX, ODT, TXT, MD; Scan-PDF ohne Text ehrlich gemeldet |
| 5 | X-11 Neues Projekt (`vorhaben`) | TEILWEISE | „kein Export des Plans (PDF, CSV, Bild, Projektdatei)“ (audit-1, Kriterium 10, Was fehlt 1) | „Schritte als Tabelle (.csv)“ und „Zeitplan als Kalenderdatei (.ics)“ in die Mappe |
| 6 | CORE-10 Wissen merken (`ki-wissen`) | TEILWEISE | „kein Export in der Oberfläche“ (audit-4, Was fehlt 2) | „Als Markdown sichern …“: je Seite eine .md (Verweise bleiben), über „Einlesen“ wieder einlesbar |
| 7 | SPEC-06 Karteikarten (`lernen`) | TEILWEISE | „Rückgängig für Bewertung und Löschen“, „CSV-Datei-Import per Dialog“ (audit-4, Was fehlt 1+2) | „Datei öffnen …“ (eigene CSV, Excel-Text, Anki-Klartext; auch Ablegen), gelöschte Karte samt Lernstand zurückholen |
| 8 | SPEC-05 Messwerte (`wissenschaft`) | TEILWEISE (nah an vollwertig) | „CSV nur über eingetippten Pfad; kein PNG/PDF/XLSX“ (audit-4, Was fehlt 1+2) | „CSV öffnen …“ (Dateiauswahl) + CSV auf die Tabelle ziehen (Rückgängig), Diagramm als PNG |

## B. Folgeetappen (noch offen, in dieser Reihenfolge)

Ehrlich abgegrenzt: diese Programme hat T-0725 **nicht** angefasst. Reihenfolge nach Nutzen (Häufigkeit im Alltag) und Aufwand.

| Rang | Studio / Programm | Audit-Urteil | Wichtigste Lücke (Teilbericht) | Vorschlag nächster Schritt | Aufwand |
|---|---|---|---|---|---|
| 1 | SYS-02 Kleine Helfer & Musik | TEILWEISE | Ton nur mit nachinstalliertem mpv, Video im fremden Fenster; Bildschirmfoto nur mit ffmpeg (audit-1) | Wiedergabe ohne Zusatzprogramm oder ehrliche BLOCKIERT-Meldung mit Einrichten-Knopf; Bildschirmbereich per Maus | mittel |
| 2 | CORE-08 Audio bearbeiten (`ton`) | TEILWEISE (stark) | Aufnahme fest 10 s, eine Datei je Spur, Effekte mit festen Werten (audit-2) | Aufnahme bis „Stopp“ mit Pegel; Leertaste | mittel |
| 3 | CORE-08 Bildbearbeitung (`bild`) | TEILWEISE (stark) | kein PSD/TIFF/WebP, keine Werkzeug-Kürzel (audit-2) | WebP-Export (SkiaSharp kann es), Werkzeug-Tastenkürzel | mittel |
| 4 | CORE-02 KI fragen | TEILWEISE | kein Dateianhang, Antwort nicht beim Entstehen (audit-4) | Datei an die Frage hängen (lokal/Claude, Hinweis wohin) | mittel |
| 5 | STUDIO-CITE-01 Recherche mit Quellen | TEILWEISE (nah an vollwertig) | kein DOI/ISBN-Import, kein RIS/DOCX (audit-4) | RIS/CSL-JSON-Export offline, DOI-Abruf mit Herkunft | mittel |
| 6 | SYS-08 Sicher recherchieren | TEILWEISE | keine Suche, Sitzung wird bei jedem Start gelöscht (audit-4) | Suchquelle mit Trefferliste; Sammlung wahlweise behalten | mittel–groß |
| 7 | CORE-08 Medien bearbeiten (`kreativ-medien`) | TEILWEISE | Umwandler mit 7 festen Aufgaben, keine Stapelverarbeitung (audit-2) | mehrere Dateien, Fortschritt, Abbruch | klein |
| 8 | STUDIO-ART-01 Künstlerstudio | TEILWEISE (stark) | keine Auswahl-/Transformier-/Textwerkzeuge (audit-2) | Auswahl + Verschieben/Skalieren | mittel |
| 9 | X-07 Freie Bilder & Klänge | TEILWEISE | KI-Aktion lädt nichts (audit-2) | Laden nach Zustimmung, Beleg je Quelle | klein |
| 10 | STUDIO-IMAGEAI-01 Bilder mit KI | TEILWEISE | sd-cli nie echt getestet (audit-4) | Echtlauf mit Beleg oder BLOCKIERT | mittel |
| 11 | STUDIO-MUSIK-01 Trendo Musik | TEILWEISE (stark) | Lied-Erzeugung in Tests ersetzt (audit-2) | Echtlauf-Beleg aus der App | mittel |
| 12 | STUDIO-MODELS-01 KI-Modelle vergleichen | TEILWEISE | nur Einzelfragen von Hand (audit-4) | Fragenreihe in einem Durchlauf | mittel |
| 13 | STUDIO-TRAIN-01 Eigene KI trainieren | TEILWEISE | Modell wird nirgends genutzt (audit-4) | Stapel-Anwendung auf CSV/Ordner | mittel |
| 14 | CORE-03 KI-Team | TEILWEISE | Textkette ohne Werkzeuge, kein Stopp (audit-4) | Stopp und Fortschritt je Rolle | groß |
| 15 | CORE-12 KI-Anweisungen | TEILWEISE | Formular statt Editor, kein Import/Export (audit-4) | Vorlagen als Datei aus/ein | mittel |
| 16 | X-05 Kostenlose KI finden | TEILWEISE (am weitesten) | Modelldateien ohne feste Prüfsumme (audit-4) | SHA-256 je Modell im Katalog | mittel |
| 17 | SYS-05 Aufgaben automatisieren | TEILWEISE | läuft nur bei offenem Trendo; KI-Aktion nur Plan (audit-4; Agent-Schritt kam in T-0698) | KI-Aktion legt echten Ablauf an | groß |
| 18 | X-01 Großprojekte | TEILWEISE | 4 feste Ketten, Karten tun nichts (audit-4) | Karte an Studio übergeben, Ergebnisdatei zurück | groß |
| 19 | SPEC-08 Kleidung | TEILWEISE | Schnitt nur aus Vorlagen (audit-2) | Schnittteile mit Punkten/Kurven ändern | groß |
| 20 | SPEC-15 Live streamen | TEILWEISE | echtes Senden nie geprüft (audit-2) | privater Teststream mit Protokoll | mittel |
| 21 | STUDIO-TV-01 TV & Live | TEILWEISE | Abspielen/Aufnehmen in UI-Tests ersetzt (audit-2) | Echtlauf mit lokalem HLS-Teststream | klein |
| 22 | SPEC-01 Wohnung planen | TEILWEISE (nah an vollwertig) | kein DXF/IFC-Import in den Plan (audit-3) | DXF-Import, Rundreise-Beleg | mittel |
| 23 | SPEC-02 Schaltungen | TEILWEISE (nah an vollwertig) | kein Import, Gerber ungeprüft (audit-3) | Gerber-Beleg mit gerbv/KiCad | mittel |
| 24 | SPEC-03 Statik | TEILWEISE | keine Gegenprobe (audit-3) | Gegenprobe gegen Tabellenwerte/FEM | mittel |
| 25 | SPEC-14 Fräsen | TEILWEISE | kein CAM aus DXF/SVG (audit-3) | Konturen aus DXF/SVG | groß |
| 26 | SPEC-09 Einscannen | TEILWEISE | Fotoscan nie echt belegt (audit-3) | Echtlauf COLMAP/Meshroom oder BLOCKIERT | mittel |
| 27 | SPEC-10 Geräte überwachen | TEILWEISE | Anzeige ohne Dokument (audit-3; Hülle E4 erledigt) | weitere Quellen, Alarmregeln | mittel |
| 28 | STUDIO-CONSOLE-01 Konsolenspiele | NICHT GETESTET | kein Beleg eines echten Laufs (audit-3) | dokumentierter Portierlauf mit freiem Titel | groß |
| 29 | STUDIO-ONLINE-BUSINESS-01 Online verkaufen | TEILWEISE | Shop nur statische Vorschau (audit-1) | Artikel/Kunden ändern, CSV ein/aus | groß |
| 30 | STUDIO-POD-01 Print on Demand | TEILWEISE | Anbieter nie gegen echte Dienste belegt (audit-1) | Lauf im Testmodus (Konto des Nutzers nötig → BLOCKIERT ohne Konto) | mittel |
| 31 | CORE-01 Desktop / Dateien | TEILWEISE | Dateiaktionen ohne Rückgängig (audit-5) | Strg+Z für Verschieben/Löschen | mittel |
| 32 | CORE-11 Daten schützen | TEILWEISE | unverschlüsselt (Prüflesen kam in T-0681) (audit-5) | Verschlüsselung oder deutlicher Hinweis | mittel |
| 33 | SYS-04 Programme installieren | TEILWEISE | echter winget-Lauf ungetestet (Rückfrage kam in T-0706) (audit-5) | Echtlauf nur mit Freigabe des Nutzers | mittel |
| 34 | SYS-03 Surfen & Herunterladen | TEILWEISE | kein eigener Browser (audit-5) | ehrlicher Name oder WebView-Arbeitsplatz | mittel |
| 35 | SPEC-12 Gefahren prüfen | TEILWEISE | keine Systemprüfung (audit-5) | Autostart/Programme mit Signatur, Ampel | groß |
| 36 | X-03 Apps zusammenklicken | TEILWEISE | es entsteht keine App, nur eine Seite (audit-5) | mehrere Seiten + Daten oder Zusammenlegung mit Web-Design | groß |
| 37 | X-04 Trendo erweitern | TEILWEISE | Erweiterungen sind nur Daten (audit-5) | Entscheidung: echte Erweiterung oder Name „Vorlagen & Pakete“ | groß |
| 38 | X-06 GitHub-Projekte | TEILWEISE | nur mit nachgebildetem Netz getestet (audit-5) | Echtlauf-Beleg | mittel |
| 39 | X-10 Programme nachbauen | TEILWEISE | Verwaltungswerkzeug (audit-5) | Anforderungsliste als bearbeitbares Dokument | mittel |
| 40 | CORE-04 / X-08 Geräteverbund, Handy | TEILWEISE | echte Pi-/Android-Hardware nie geprüft (audit-5) | Lauf mit echtem Gerät (Hardware nötig) | groß |
| 41 | SYS-01, SYS-07, SYS-11, SYS-12, SYS-13, X-02, X-09 | TEILWEISE | Anzeige/Verwaltung (audit-5, je Abschnitt) | je ein Alltagsnutzen (z. B. „Alles prüfen“ mit Ampel) | klein–mittel |
