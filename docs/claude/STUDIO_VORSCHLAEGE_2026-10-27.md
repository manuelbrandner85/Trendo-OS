# Vorschläge: Jedes Studio wie ein hochwertiges Programm – übersichtlich, professionell, für jeden

**Auftrag (27.10.2026):**
- Zu jedem Studio und seinen Programmen Verbesserungen für Funktion und Oberfläche vorschlagen, damit es wie ein hochwertiges Programm wirkt: übersichtlich, aber professionell.
- Mit REA alle Windows-Programme wie Word, Excel und PowerPoint nachbauen und in die passenden Studios bringen. Microsoft Office genau recherchieren.

**Grundlage:**
- Alle 53 Studio-Seiten im aktuellen Stand gerendert und angesehen (SPEC-07 ist abgelehnt).
- Funktionsumfang von Microsoft Office aus der offiziellen Microsoft-Dokumentation: Excel-Funktionen nach Kategorien, Menüband-Reiter von Word und PowerPoint.
- **Microsoft Office ist auf diesem PC nicht installiert** (nur der „Office Hub“-Starter). REA kann Office deshalb nicht untersuchen.
- Installiert und mit REA prüfbar: Editor, Paint, Rechner, Snipping Tool, Fotos, Medienwiedergabe, Kamera, Sprachrekorder, Kurznotizen, To Do, Uhr, Outlook (neu), Clipchamp.

**Regel für jeden Nachbau:**
- REA ermittelt nur, *was* ein Programm kann: Funktionen, Bedienung, Dateiformate.
- Code, Texte, Symbole oder Logos werden nie übernommen (Studio „Programme nachbauen“, X-10).
- Microsoft-Dateiformate (DOCX, XLSX, PPTX) sind offene Standards (ECMA-376) und dürfen gelesen und geschrieben werden.

**So antwortest du:** je Nummer „Ja“, „Nein“ oder „Später“. Beispiel: „G1–G8 ja, W2 später, S-SYS10-3 nein“.

---

## Teil 1: Was mir auf allen Studio-Seiten auffällt (Ist-Stand)

| # | Befund | Beispiel |
|---|---|---|
| B1 | **Reiter-Wolken:** Viele Fachstudios zeigen als Reiter *alle 15 anderen Fachstudios* (Wohnung, Schaltungen, Statik …). Man weiß nicht, in welchem Programm man ist. | SPEC-01 … SPEC-16, CORE-09 |
| B2 | **Gleiche Reiter in fremden Studios:** „Daten schützen“, „Startmodus“, „Geräte erkennen“, „Handy“ usw. zeigen dieselbe Systemleiste mit 12 Punkten. | CORE-11, SYS-01, SYS-04, SYS-06, SYS-07, X-02, X-08, CORE-04 |
| B3 | **Formulare statt Programme:** Viele Seiten sind Stapel aus Eingabefeldern mit „Berechnen“. Ergebnisse stehen als kleiner Text darunter, ohne Grafik und ohne Verlauf. | SPEC-02, SPEC-03, SPEC-13, SPEC-14, SPEC-16 |
| B4 | **„Pfad zur Datei“-Felder:** Man tippt oder wählt Pfade. Es gibt kein Ziehen-und-Ablegen und keine Vorschau der gewählten Datei. | CORE-08, SPEC-05, SPEC-09, SPEC-13, SYS-03 |
| B5 | **Leere Flächen:** Seiten mit einem Knopf und 80 % leerer Fläche. | SYS-07, SYS-06, CORE-02, X-06, SYS-05, SYS-13 |
| B6 | **Kleine Schrift:** Hinweise und Feldbeschriftungen sind 10–11 pt, auf 1440×900 kaum lesbar. | fast alle |
| B7 | **Fachbegriffe vorne:** SHA-256, rtsp://, GPX, CSV, ONVIF, MwSt-Formeln, „Kohärenz“. | SYS-03, CORE-09, SPEC-13, SPEC-05 |
| B8 | **Keine Startseite je Programm:** Office zeigt beim Öffnen „Neu · Vorlagen · Zuletzt“. In Trendo landet man mitten im Formular. | Texte & Tabellen, Briefe, Video |
| B9 | **Echte Nutzerdateien in Listen ohne Ordnung:** Lange Dateilisten ohne Symbole, Filter oder Gruppen. | SYS-10 (Dokumente) |
| B10 | **Rückmeldung nur als Textzeile:** Erfolg und Fehler stehen klein oben. Es gibt keine Benachrichtigung, kein „Rückgängig“ daneben und keinen Fortschrittsbalken. | viele |

---

## Teil 2: Gemeinsame Bauweise für alle Programme (gilt überall)

Ein einheitlicher **Trendo-Programmrahmen**, damit jedes Studio wie ein echtes Programm wirkt und man sich überall sofort zurechtfindet:

| Nr. | Vorschlag | Nutzen |
|---|---|---|
| **G1** | **Programm-Startseite:** große Kacheln „Neu“, Vorlagen mit Vorschaubildern, „Zuletzt geöffnet“ mit Vorschau, „Öffnen“. Wie Backstage in Office, aber ruhiger. | Sofort klar, was man hier tun kann. Behebt B8. |
| **G2** | **Werkzeugleiste mit Gruppen** (vereinfachtes Menüband): 4–6 Gruppen pro Programm (z. B. „Start · Einfügen · Gestalten · Prüfen · Ansicht“). Je Gruppe große Symbole mit Text. Seltenes liegt unter „Mehr“. | Professionell und trotzdem übersichtlich. |
| **G3** | **Linke Navigation nur für Bereiche *dieses* Programms**, nie für fremde Studios. Andere Studios erreicht man über Startmenü und Dock. | Behebt B1/B2. |
| **G4** | **Rechte Seitenleiste „Eigenschaften & Hilfe“:** Optionen zum gewählten Objekt, darunter „So geht's“ in 3 Schritten und „Frag die KI“. | Profi-Optionen ohne Überladung. |
| **G5** | **Statusleiste unten:** Speicherstand („Gespeichert · vor 10 s“), Zoom, Zähler (Wörter, Zellen, Folie 3/12), Fortschritt laufender Aufgaben. | Vertrauen und Überblick. |
| **G6** | **Ziehen und Ablegen plus Vorschau** für jede Dateiauswahl, mit Vorschaubild und Größe statt Pfadfeld. Der Pfad liegt nur noch unter „Mehr“. | Behebt B4. |
| **G7** | **Ergebnisse als Karte mit Grafik:** Jede Berechnung zeigt ein großes Ergebnis, ein passendes Bild oder Diagramm und einen Verlauf der letzten Rechnungen mit „Kopieren“, „In Tabelle übernehmen“ und „Als PDF“. | Behebt B3/B10. |
| **G8** | **Befehlssuche „Was möchtest du tun?“** in jedem Programm (Strg+Q): Sie findet jeden Befehl, auch versteckte. Die KI führt ihn auf Wunsch aus. | Maximum an Funktion bei einfacher Bedienung. |
| **G9** | **Einheitliche Tastenkürzel** wie in Office/Windows (Strg+S, Strg+Z/Y, Strg+F, F1 Hilfe, F11 Vollbild) und eine Übersicht mit Strg+K. | Umsteiger fühlen sich sofort zu Hause. |
| **G10** | **Lesbare Mindestschrift 12 pt** für Hinweise und 13 pt für Beschriftungen. Fachbegriffe stehen nur im Tooltip, z. B. „Prüfsumme (SHA-256)“. | Behebt B6/B7. |
| **G11** | **Leere Zustände mit Beispiel:** Statt leerer Fläche erscheint ein Beispiel zum Ausprobieren („Beispiel laden“) und ein kurzer Clip bzw. eine Schritt-Grafik. | Behebt B5. |
| **G12** | **Rückmeldungen als Benachrichtigung unten rechts** mit „Rückgängig“ und „Ordner öffnen“; lange Aufgaben mit Fortschrittsbalken in der Statusleiste. | Behebt B10. |
| **G13** | **Vollbild- und Fokusmodus** pro Programm: Werkzeugleiste ein- und ausklappen, dazu ein ablenkungsfreies Schreiben bzw. Arbeiten. | Profi-Gefühl. |
| **G14** | **Ein Dokumenten-Hub:** „Zuletzt verwendet“ über alle Programme hinweg, mit Vorschaubildern, Anheften und Suche. Behebt B9. | Alles wiederfinden. |

---

## Teil 3: Windows- und Office-Programme → Trendo-Nachbau → Studio

**Stand:** ✅ vorhanden · 🟡 Grundstufe · ⬜ neu

| Nr. | Vorbild (Windows/Office) | Trendo-Programm | Studio | Stand |
|---|---|---|---|---|
| **N1** | Word | **Trendo Text** | Texte & Tabellen; Briefe | 🟡 Stufe 1 (T1–T10) fertig |
| **N2** | Excel | **Trendo Tabelle** | Texte & Tabellen; Geld & Rechnungen; Messwerte | 🟡 Kern und Dateien fertig (heute), Oberfläche folgt |
| **N3** | PowerPoint | **Trendo Präsentation** | Texte & Tabellen; Live streamen (Folien einblenden) | ⬜ |
| **N4** | Outlook (Mail, Kalender, Kontakte) | **Trendo Post & Kalender** | E-Mails & Termine | 🟡 nur Entwurf und Weitergabe an das Mailprogramm |
| **N5** | OneNote | **Trendo Notizbuch** | Wissen merken | ⬜ |
| **N6** | Access | **Trendo Daten** (einfache Datenbank mit Formularen) | Geld & Rechnungen; Online verkaufen (Kunden, Artikel) | ⬜ |
| **N7** | Publisher | **Trendo Gestalten** (Flyer, Karten, Plakate) | Videos & Fotos bearbeiten (Grafik); Online verkaufen (Marke) | ⬜ |
| **N8** | Visio | **Trendo Diagramm** (Ablauf, Organigramm, Grundriss, Schaltplan) | Schaltungen; Wohnung planen; Großprojekte | ⬜ |
| **N9** | Editor (Notepad) | **Trendo Editor** (Reiter, Suchen/Ersetzen, Kodierung, Zeilennummern) | Kleine Helfer | ⬜ |
| **N10** | Paint | **Trendo Malen** (Pinsel, Formen, Ebenen, Hintergrund entfernen mit KI) | Videos & Fotos bearbeiten | ⬜ (Krita bleibt Profi-Werkzeug) |
| **N11** | Rechner | **Trendo Rechner** (Standard, Wissenschaftlich, Grafik, Programmierer, Umrechner, Datum) | Kleine Helfer | 🟡 Einheiten-Umrechner vorhanden |
| **N12** | Snipping Tool | **Trendo Bildschirmfoto & Aufnahme** (Bereich, Fenster, Verzögerung, Markieren, Text erkennen) | Kleine Helfer | 🟡 Bildschirmfoto vorhanden |
| **N13** | Fotos | **Trendo Fotos** (Galerie nach Datum/Orten/Personen, Bearbeiten, Diashow) | Videos & Fotos bearbeiten | 🟡 Bildbearbeitung vorhanden |
| **N14** | Medienwiedergabe | **Trendo Player** (Musik und Video, Wiedergabelisten, Untertitel) | Kleine Helfer & Musik | 🟡 (mpv) |
| **N15** | Clipchamp | **Trendo Video** (Zeitleiste, Übergänge, Texte, Musik, Export) | Videos & Fotos bearbeiten | 🟡 Clips aneinanderhängen |
| **N16** | Sprachrekorder | **Trendo Aufnahme** (Aufnehmen, Schneiden, Mitschreiben mit Whisper) | Videos & Fotos (Musik) | ⬜ |
| **N17** | Kamera | in **Kameras & Roboter** (Foto, Video, Dokument scannen mit Texterkennung) | Kameras & Roboter | 🟡 |
| **N18** | Kurznotizen | **Trendo Zettel** (Notizzettel auf dem Desktop) | Kleine Helfer; Desktop | ⬜ |
| **N19** | Microsoft To Do | **Trendo Aufgaben** (Listen, Fälligkeit, Erinnerung, „Mein Tag“) | E-Mails & Termine; Neues Projekt | 🟡 Vorhaben-Checkliste |
| **N20** | Uhr (Wecker, Timer, Stoppuhr, Fokus) | **Trendo Uhr** | Kleine Helfer (Zeit) | 🟡 Fokus/Erinnerung vorhanden |
| **N21** | Edge | **Trendo Browser** (Lesemodus, Sammlungen, PDF, Werbeblocker) | Surfen & Herunterladen | 🟡 Lesemodus |
| **N22** | Explorer | **Trendo Dateien** (Doppelfenster, Vorschau, Tags, Suche) | Desktop; Kleine Helfer | 🟡 |
| **N23** | FreeCAD (LGPL) | **Trendo CAD**: parametrische Teile mit Skizzen, Maßen und Bedingungen, Extrudieren und Drehen, Bohrungen, Rundungen, Baugruppen, technische Zeichnung, STEP, STL und 3MF | 3D-Modelle erstellen | ⬜ (Vorgabe 27.10.: „für 3D FreeCAD oder anderes hochwertiges 3D-Programm nachbauen“) |
| **N24** | Bambu Studio (AGPL) und Bambu-Lab-Drucker | **Trendo 3D-Druck**: Bambu Studio im Vollpaket; Trendo-Bedienung mit Slicen, Schichtvorschau, Druckzeit und Filament; Drucker im Heimnetz (LAN-Modus) mit Status, Kamera und Fortschritt; Druckstart nur nach Bestätigung | 3D-Modelle erstellen; Fräsen & Fertigen | ⬜ (Vorgabe 27.10.: „für 3D-Druck soll Bambu Lab genutzt und in Trendo OS eingebunden werden“) |

### Funktionsumfang der Office-Nachbauten (aus der Microsoft-Dokumentation, Stufen)

**W – Trendo Text (Word):**
- **W1 Start:** Formatvorlagen-Galerie mit Vorschau (Überschrift 1–3, Zitat, Titel), Format übertragen
- **W2 Einfügen:** Kopf- und Fußzeile, Seitenzahlen, Textfeld, Formen, Symbole, Verweise/Links, Kommentare
- **W3 Entwurf:** Designs (Farben/Schriften) mit einem Klick, Seitenrand-Linie, Wasserzeichen
- **W4 Layout:** Seitenränder (Normal, Schmal …), Hoch/Quer, Spalten, Einzug und Abstand, Silbentrennung
- **W5 Referenzen:** automatisches Inhaltsverzeichnis, Fußnoten und Endnoten, Beschriftungen, Literaturverzeichnis
- **W6 Sendungen:** Serienbrief aus einer Tabelle (Trendo Tabelle/CSV), Umschläge, Etiketten
- **W7 Überprüfen:** Änderungen nachverfolgen (annehmen/ablehnen), Vergleichen zweier Fassungen, Barrierefreiheit prüfen
- **W8 Ansicht:** Lesemodus, Gliederung, Navigationsbereich, Lineal, zwei Seiten nebeneinander
- **W9 Mit KI und lokal:** Diktieren (Whisper), Vorlesen (Piper), „Umschreiben/Kürzen/Freundlicher“

**E – Trendo Tabelle (Excel):**
- **E1:** Oberfläche mit Raster, Formelleiste, Blattreitern, Ausfüllkästchen (Reihen 1, 2, 3 … Mo, Di …), Fenster fixieren *(Stufe C, als Nächstes)*
- **E2:** Diagramme (Säule, Balken, Linie, Kreis, Fläche, Punkt) mit Vorschau *(Stufe D)*
- **E3:** PDF und Drucken mit Seitenumbruch-Vorschau und „Auf eine Seite anpassen“ *(Stufe D)*
- **E4:** **Funktionen ausbauen:** Excel hat rund 470 Funktionen (Mathematik 83, Statistik 111, Text 42, Nachschlagen 40, Datum 25, Logik 19, Finanzen 55 …). Trendo hat heute 44. Nächster Schritt sind die 150 meistgenutzten, darunter XVERWEIS, INDEX/VERGLEICH, SUMMEWENNS, ZÄHLENWENNS, TEXT, DATEDIF, ARBEITSTAG, RMZ, ZW, BW und EINDEUTIG/SORTIEREN/FILTER.
- **E5:** bedingte Formatierung (Farbskalen, Datenbalken, Regeln)
- **E6:** Datenüberprüfung (Auswahllisten in Zellen)
- **E7:** „Als Tabelle formatieren“ (Kopf, Streifen, Summenzeile)
- **E8:** Blitzvorschau, Text in Spalten, Duplikate entfernen
- **E9:** Kommentare/Notizen, Blattschutz
- **E10:** PivotTable *(spät, laut Stufe-1-Freigabe ausgeklammert)*

**P – Trendo Präsentation (PowerPoint):**
- **P1:** Folien mit Layouts (Titel, Titel+Inhalt, zwei Spalten, Bild), Foliensortierung
- **P2:** Designs (Farbschemata, Schriften, Hintergründe)
- **P3:** Einfügen: Text, Bilder, Formen, Tabellen, Diagramme (aus Trendo Tabelle), Video/Audio
- **P4:** Übergänge und Animationen (einfache Auswahl mit Vorschau)
- **P5:** Bildschirmpräsentation mit Referentenansicht (Notizen, Zeit, nächste Folie), Laserpointer
- **P6:** Export PPTX, PDF, Bilder, Video (FFmpeg); ODP lesen
- **P7:** KI: „Präsentation aus Stichpunkten/Text erstellen“ (lokal)

**O – Trendo Post & Kalender (Outlook):**
- **O1:** Posteingang lesen (IMAP, nur lesen bis zur Freigabe)
- **O2:** Senden nur nach ausdrücklicher Bestätigung je Nachricht
- **O3:** Kalender (Tag/Woche/Monat, ICS ein und aus, Erinnerungen)
- **O4:** Kontakte (VCF)
- **O5:** Aufgaben (N19)
- **O6:** Regeln/Filter *(nur Vorschlag, aktiviert erst nach Freigabe)*

---

## Teil 4: Vorschläge je Studio

Jeder Vorschlag hat eine Nummer. **F** = Funktion, **U** = Oberfläche.

### Arbeiten & Büro
**Briefe schreiben (STUDIO-LETTER-01)**
- *Ist:* Formular links, weiße Vorschau rechts. Gut, aber flach.
- F1: Briefvorlagen-Galerie nach Anlass (Kündigung, Widerspruch, Mahnung, Bewerbung) mit Vorschau
- F2: Fristenrechner mit Kalender-Eintrag
- F3: Serienbrief an mehrere Empfänger (aus Kontakten oder Tabelle)
- F4: Unterschrift einfügen (Bild)
- U1: echte DIN-A4-Vorschau im Maßstab mit Falzmarken und Sichtfenster-Rahmen
- U2: die Felder im Brief direkt anklicken und bearbeiten
- U3: Schrittleiste „Absender → Empfänger → Inhalt → Prüfen → Fertig“

**Texte & Tabellen (SYS-10)**
- *Ist:* Reiterleiste plus lange, unsortierte Dateiliste.
- F1: Startseite nach G1 mit drei großen Kacheln „Text · Tabelle · Präsentation · PDF“ und Vorlagen-Galerie
- F2: W1–W9, E1–E10, P1–P7
- U1: Dateiliste als Kacheln mit Vorschaubild, Typ-Symbol, Filter nach Typ und Datum (G14)
- U2: Jedes Dokument öffnet als eigenes Programmfenster (Trendo Text/Tabelle/Präsentation) statt als Unterseite

**E-Mails & Termine (SYS-09)**
- *Ist:* Formular „Neue E-Mail“ und Kontaktfelder, dazu die Systemleiste (B2).
- F1: O1–O6
- F2: Termin aus E-Mail-Text erkennen (lokale KI)
- U1: klassisches Drei-Spalten-Layout „Ordner · Liste · Lesebereich“, Kalender als eigener Bereich
- U2: Systemleiste entfernen (G3)

**Neues Projekt starten (X-11)**
- *Ist:* Zielkacheln und eine Checkliste.
- F1: Projekt-Vorlagen mit Zeitplan (Gantt light) und Meilensteinen
- F2: Dateien des Projekts aus allen Studios an einem Ort
- U1: Kanban-Ansicht (Offen/In Arbeit/Fertig) neben der Liste
- U2: Fortschrittsring je Projekt

**Kleine Helfer & Musik (SYS-02)**
- *Ist:* 4 Reiter mit vielen Formularblöcken untereinander.
- F1: N9 Editor, N11 Rechner, N12 Bildschirmfoto, N14 Player, N18 Zettel, N20 Uhr als **eigene kleine Programme**
- U1: Startseite als App-Raster mit großen Symbolen (wie ein Werkzeugkasten). Jedes Werkzeug öffnet sich in einem eigenen kleinen Fenster.

### Business & Geld
**Online verkaufen (STUDIO-ONLINE-BUSINESS-01)**
- *Ist:* Bewertungsformular mit Score.
- F1: Kunden- und Artikelliste (N6)
- F2: Rechnung aus Bestellung
- F3: Webshop-Vorschau
- U1: Dashboard mit Kennzahlen-Karten (Umsatz, Gewinn, Kunden) und Diagramm
- U2: Score als Ring mit Ampel statt Text
- U3: Schritte der Gründung als Fortschrittsleiste

**Geld & Rechnungen (SPEC-11)**
- *Ist:* Buchungszeile, Rechnungsfelder, Kleinunternehmer-Prüfung.
- F1: Haushaltsbuch mit Kategorien und Monatsdiagramm
- F2: Kontoauszug-Import mit automatischer Kategorie (lokale KI)
- F3: Rechnungsvorlagen mit Logo des Nutzers
- F4: Budget-Warnung
- F5: Export für den Steuerberater
- U1: Startseite „Übersicht“ mit Saldo-Karte, Ein-/Ausgaben-Diagramm, letzte Buchungen
- U2: Buchungen als Tabelle (Trendo Tabelle eingebettet)

### Kreativ & Medien
**Videos & Fotos bearbeiten (CORE-08)**
- *Ist:* Pfadfeld und lange Liste zuletzt benutzter Dateien.
- F1: N10, N13, N15, N16
- F2: Zuschneiden, Drehen, Filter mit Vorher/Nachher-Schieber
- F3: Hintergrund entfernen
- F4: KI-Bildverbesserung (Real-ESRGAN)
- U1: **Medien-Bibliothek** als Raster mit Vorschaubildern links, Bearbeitungsfläche in der Mitte, Werkzeuge rechts (wie Fotos/Clipchamp)
- U2: Zeitleiste unten für Video

**Live streamen (SPEC-15)**
- *Ist:* großer roter „Stopp“-Balken, Quellen und Qualität.
- F1: Szenen (Bildschirm+Kamera, Nur Kamera, Pause-Bild) mit einem Klick wechseln
- F2: Einblendungen (Name, Logo des Nutzers, Folien aus Trendo Präsentation)
- F3: Aufnahme und Stream gleichzeitig
- U1: großes Vorschaubild mittig, Szenen-Kacheln darunter, Pegelanzeigen für Ton
- U2: „Live“-Knopf mit Countdown; Stopp erst sichtbar, wenn live

**Kleidung & Produkte entwerfen (SPEC-08)**
- *Ist:* vier Rechenblöcke.
- F1: Schnittmuster-Vorschau (Grundschnitt aus Maßen als PDF in Originalgröße)
- F2: Produktkarte mit Bild und Varianten
- F3: Größentabelle
- U1: Figurenansicht mit Maßlinien
- U2: Ergebnisse als Karten (G7)

**Freie Bilder & Klänge finden (X-07)**
- *Ist:* Suchfeld und Linkliste der Quellen.
- F1: Ergebnisse direkt in Trendo als Raster mit Vorschau (Bild, Klang abspielen, 3D drehen) und Lizenz-Abzeichen
- F2: „Zum Projekt hinzufügen“
- U1: Galerie-Layout mit Filtern (Art, Lizenz, Farbe)

**KI-Anweisungen schreiben (CORE-12)**
- *Ist:* Vorlagen-Knöpfe auf leerer Fläche.
- F1: Anweisungs-Bibliothek mit Kategorien, Variablen-Feldern und Testlauf
- F2: Bewertung „gut/schlecht“
- U1: zweispaltig, links die Bibliothek, rechts Editor und Testergebnis

### 3D, Spiele & VR
**3D-Modelle erstellen (CORE-05)**
- *Ist:* Feldbatterie für Formen, Vorschau rechts klein.
- F1: Grundkörper per Klick, Bemaßung im Bild, STL/GLB-Export, Druckprüfung
- U1: **3D-Ansicht groß in der Mitte**, Werkzeugleiste links (Formen), Eigenschaften rechts (G4), Viewcube oben rechts

**Spiele bauen (CORE-06)**
- *Ist:* Browser-Spiel-Vorlage und Unreal-Knöpfe.
- F1: Spiel-Vorlagen-Galerie mit Vorschau-GIF
- F2: Level-Editor im Raster (2D)
- F3: Unreal-Projekte mit Status
- U1: Projektkacheln „Deine Spiele“ mit Vorschaubild und „Spielen“-Knopf

**VR & AR gestalten (SPEC-04)**
- *Ist:* Prüf-Formulare.
- F1: 3D-Vorschau mit Brillen-Grenzen (Polygone, Texturen) als Ampel
- U1: Modellkarte mit Ampel je Gerät (Quest, Vision Pro, Handy-AR)

**Gegenstände einscannen (SPEC-09)**
- *Ist:* Fotos/Video → COLMAP, Formulare.
- F1: geführter Scan-Assistent (Fotos prüfen: Schärfe, Überlappung)
- F2: Ergebnis als drehbares Modell
- U1: Schrittleiste „Fotos → Prüfen → Rechnen → Säubern → Fertig“ mit Fortschritt

### Technik & Fachgebiete
**Wohnung planen (SPEC-01)**
- *Ist:* Raumzeile mit Maßen, aufklappbare Bereiche (B1).
- F1: **Grundriss-Zeichner** (Wände ziehen, Türen/Fenster, Möbel), Flächen automatisch, Materialliste
- U1: Zeichenfläche mittig, Möbelkatalog links, Maße rechts; Reiter-Wolke entfernen (G3)

**Schaltungen berechnen (SPEC-02)**
- *Ist:* vier Rechner-Blöcke.
- F1: N8 Schaltplan-Zeichner
- F2: Farbringe als Bild
- F3: Ergebnis mit E-Reihe
- U1: Rechner als Karten mit Grafik (Widerstand mit echten Farbringen, Spannungsteiler-Schaltbild)

**Statik nachrechnen (SPEC-03)**
- *Ist:* Balken, Wärme, Einheiten.
- F1: Balkenskizze mit Kräften und Durchbiegung als Grafik
- F2: Bericht als PDF
- U1: G7 Ergebnis-Karte mit Ampel „hält / hält nicht“

**Geräte überwachen (SPEC-10)**
- *Ist:* Sensor anlegen, CSV.
- F1: **Dashboard** mit Kacheln je Sensor (Wert, Trend-Linie, Grenze)
- F2: Alarm-Verlauf
- U1: Kachel-Raster mit Live-Linien

**Routen & Karten (SPEC-13)**
- *Ist:* GPX-Feld, Koordinaten-Rechner.
- F1: **Kartenansicht** (OpenStreetMap-Kacheln, offline zwischengespeichert) mit Route, Höhenprofil, Strecke
- U1: Karte groß, Liste der Touren links

**Fräsen & Fertigen (SPEC-14)**
- *Ist:* G-Code-Prüfung, Drehzahl.
- F1: 3D-Bahnvorschau des G-Codes mit Zeitleiste
- F2: Werkzeug-Bibliothek
- U1: Vorschau mittig, Prüfergebnisse als Liste mit Sprung zur Zeile

**Auto & Fahrten (SPEC-16)**
- *Ist:* Fahrzeug anlegen.
- F1: Fahrtenbuch mit Karte
- F2: Verbrauch als Diagramm
- F3: Erinnerungen (HU, Service)
- U1: Fahrzeug-Karte mit Foto und nächsten Terminen

**Kameras & Roboter steuern (CORE-09)**
- *Ist:* Not-Aus-Balken, Kamera-Formular, viel leere Fläche.
- F1: N17
- F2: Mehrfach-Kameraansicht (2×2)
- F3: Roboter-Simulation als 3D
- U1: großes Livebild, Steuerung als Steuerkreuz und Regler, Not-Aus immer sichtbar oben rechts

### Wissen & Lernen
**Messwerte auswerten (SPEC-05)**
- *Ist:* drei CSV-Formulare.
- F1: Messdatei öffnen und sofort Tabelle plus Diagramm (Trendo Tabelle eingebettet), Statistik-Karte
- U1: zweigeteilt, oben Diagramm, unten Daten

**Lernen mit Karteikarten (SPEC-06)**
- *Ist:* Karten anlegen, Liste einfügen.
- F1: Lernmodus als Karte mit Umdrehen-Animation
- F2: Lernkurve
- F3: Bild- und Audio-Karten
- F4: Vorlesen (Piper)
- U1: Stapel-Kacheln mit „fällig heute“-Zahl, großer Lernmodus im Vollbild

**Wissen merken (CORE-10)**
- *Ist:* Notizfeld und Suche.
- F1: N5 Notizbuch (Abschnitte, Seiten, Bilder, Checklisten)
- F2: Suche nach Bedeutung (KI-Bibliothek „Wissen“)
- U1: drei Spalten „Notizbücher · Seiten · Inhalt“ wie OneNote

**Sicher recherchieren (SYS-08)**
- *Ist:* Adressfeld, Startpunkte, Notizen.
- F1: Lesemodus mit Markieren
- F2: Quellenliste mit Zitat-Export
- U1: Leseansicht groß, Notizen rechts

### KI & Automatik
**KI fragen (CORE-02)**
- *Ist:* ein Eingabefeld auf leerer Fläche.
- F1: Chatverlauf mit Gesprächsliste links
- F2: Dateien anhängen (Bild, PDF)
- F3: Modellwahl oben
- F4: Antwort vorlesen
- U1: modernes Chat-Layout mit Vorschlags-Kacheln im leeren Zustand (G11)

**KI-Team einsetzen (CORE-03)**
- *Ist:* Rollenknöpfe, Ziel, Aufgabenliste.
- F1: Ablauf-Ansicht, welche Rolle gerade arbeitet (Live)
- U1: Team-Leiste mit Avataren und Status; Aufgaben als Karten

**Kostenlose KI finden (X-05)**
- *Ist:* Quellenliste.
- F1: KI-Bibliothek ist gebaut (12 Arten)
- U1: Kacheln nach Art mit Symbol und „läuft/geladen“-Abzeichen; Quellen-Reihenfolge unter „Profi“

**Aufgaben automatisieren (SYS-05)**
- *Ist:* Vorlagen-Knöpfe.
- F1: visueller Ablauf-Baukasten „Wenn → Dann“ mit Bausteinen
- F2: Verlauf der Läufe
- U1: Ablauf-Karten mit Schalter an/aus und „Letzter Lauf“

**Großprojekte umsetzen (X-01)**
- *Ist:* Ablauf-Kacheln und Formular.
- F1: Projektplan als Zeitleiste über Studios
- U1: Schrittkette mit Status je Studio

### Entwickeln
**Webseite bauen (CORE-07)**
- *Ist:* viele Optionen untereinander.
- F1: Vorlagen-Galerie mit Vorschau
- F2: Live-Vorschau neben dem Editor (Desktop/Handy)
- U1: links Struktur/Seiten, Mitte Vorschau, rechts Stil

**Apps zusammenklicken (X-03)**
- *Ist:* Bausteinliste.
- F1: Ziehen und Ablegen von Bausteinen in eine Handy-Vorschau
- U1: Baukasten links, Telefonrahmen mittig, Eigenschaften rechts

**Projekte von GitHub holen (X-06)**
- *Ist:* Suchfeld auf leerer Fläche.
- F1: „Beliebt“-Galerie nach Kategorien
- F2: Installieren ist gebaut
- U1: Ergebnis-Kacheln mit Sternen, Lizenz-Abzeichen, „Installieren“

**Trendo erweitern (X-04)** und **Programme nachbauen (X-10)**
- U1: Startseite mit Erklär-Grafik
- F1 (X-10): Nachbau-Fortschritt je Programm (wie Teil 3)

### System & Sicherheit
- **Desktop (CORE-01):** U1 Widgets frei anordnen und Größe ändern; F1 Zettel (N18) auf dem Desktop.
- **Startmodus (SYS-01):** U1 Gerätekarte mit Bild statt Liste; Systemleiste entfernen (G3).
- **Daten schützen (CORE-11):**
  - F1 Sicherungsplan mit Kalender
  - U1 Schutz-Ring „92/100“ groß, Maßnahmen als Karten mit „Jetzt beheben“
- **System auf Gefahren prüfen (SPEC-12):** U1 Ampel-Übersicht (Netzwerk, Dateien, Programme), Details aufklappbar.
- **Surfen & Herunterladen (SYS-03):** F1 N21 Browser; U1 Prüf-Werkzeuge als Assistent „Datei/Link prüfen“ mit großem Ergebnis.
- **Programme installieren (SYS-04):**
  - F1 Programm-Katalog als Kacheln mit Beschreibung (gebaut) und Kategorien
  - U1 Store-Layout
- **Dateien teilen (SYS-06):** U1 großer Freigabe-Knopf, darunter QR-Code und aktive Freigaben als Karten.
- **Geräte erkennen (SYS-07):** U1 Gerätekarten mit Symbol (Drucker, Kamera, USB) statt leerer Fläche.
- **Programme starten (SYS-11):** U1 App-Raster mit Symbolen, Suche, Anheften.
- **Schrift & Farben (SYS-12):** U1 Live-Vorschau eines Beispielfensters beim Verstellen.
- **Qualität prüfen (SYS-13):** U1 Ergebnisse als Diagramm (Ladezeiten je Seite).
- **Einrichten & Reparieren (X-02):** U1 Prüfpunkte als Karten mit Ampel, Systemleiste entfernen.
- **Handy verbinden (X-08):**
  - U1 Assistent mit Bildern je Schritt und QR-Code
  - Nach der Kopplung eine Handy-Karte (Akku, Dateien, Bildschirm)
- **PC, Pi & Handy (CORE-04):** U1 Netzwerk-Karte mit verbundenen Geräten.
- **Leistung anzeigen (X-09):** U1 Verlaufsdiagramme (CPU, RAM, Grafik) statt Momentwerte; Programmliste als Balken.

---

## Teil 5: Empfohlene Reihenfolge

1. **G1–G14** (gemeinsamer Programmrahmen): Davon profitieren alle 53 Studios auf einen Schlag.
2. **Trendo Tabelle fertig** (E1–E3, dann E4–E9). Kern und Dateien sind seit heute fertig.
3. **Trendo Präsentation** (P1–P7) und Ausbau **Trendo Text** (W1–W9). Damit ist das Office-Trio komplett.
4a. **Trendo CAD** (N23) und **3D-Druck mit Bambu Lab** (N24).
4. **Trendo Post & Kalender** (O1–O6), **Notizbuch** (N5), **Editor/Rechner/Uhr/Zettel** (N9, N11, N18, N20).
5. Die Studio-Vorschläge aus Teil 4 nach Bereich.

---

## Teil 6: Mit Tailscale möglich (Vorschläge 27.10.2026, Antwort steht aus)
Alles läuft nur im privaten, verschlüsselten Tailscale-Netz des Nutzers. Nichts ist öffentlich, kein Port im Router.

| Nr. | Vorschlag |
|---|---|
| T1 | Trendo komplett auf dem Handy spiegeln: PC-Bildschirm live im Browser, Tippen = Klicken, Tastatur, Zoom. Beim ersten Mal bestätigt man am PC; „Trennen“ und Not-Aus gibt es immer. |
| T2 | Trendo-Handy-Ansicht: eigene Oberfläche fürs Handy (KI fragen, Aufträge an Studios, Ergebnisse, Projekte) |
| T3 | Freigaben unterwegs bestätigen (Versenden, Installieren, 3D-Druck starten …) |
| T4 | Trendo KI unterwegs: rechnet auf der Grafikkarte des PCs, dazu Spracheingabe (Whisper) und Vorlesen (Piper) |
| T5 | Alle Projekte und Dokumente am Handy ansehen und laden; Hochladen und Ändern nur mit Bestätigung |
| T6 | Musik und Videos vom PC streamen (Trendo Player im Browser) |
| T7 | Handy-Kamera als Scanner (Texterkennung → PDF) und als Kamera-Quelle |
| T8 | Kameras und 3D-Druck (Bambu Lab) unterwegs ansehen |
| T9 | Not-Stopp vom Handy |
| T10 | Fernwartung: Leistung, Fehler, Sicherung, Updates, Neustart |
| T11 | PC aus der Ferne aufwecken (Wake-on-LAN). Braucht ein Gerät, das zuhause immer läuft, z. B. einen Raspberry Pi. |
| T12 | Geräteverbund über Tailscale (Pi, Laptop, zweiter PC) auch außer Haus |
| T13 | HTTPS mit eigener Tailscale-Adresse; Trendo als App auf dem Startbildschirm |
| T14 | Ordner oder Funktionen mit der Familie teilen (eigene Tailscale-Konten) |
