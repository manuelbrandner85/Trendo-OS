# Trendo OS – Programmkatalog

Erzeugt aus dem Repository (angemeldete Programme, Menübänder, Quelltext, Funktionskataloge, Vorbild-Analysen, Tests). Nicht von Hand pflegen – neu erzeugen mit `TRENDO_KATALOG=. dotnet run --project tools/Trendo.Render -c Release`.

89 Programme in 54 Studios.

| Programm | Studio | Bereich | Arbeitsfläche | Funktionen | Tests | Vorbild-Analyse | Modulstatus | Doku |
|---|---|---|---|---|---|---|---|---|
| Briefe schreiben | Briefe schreiben (STUDIO-LETTER-01) | Arbeiten & Büro | Texteditor | – | 11 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](brief/README.md) |
| Aufgaben | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Aufgabentafel | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](aufgaben/README.md) |
| E-Mail & Kontakte | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Texteditor | – | 18 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](post/README.md) |
| Kalender | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Monatskalender | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](kalender/README.md) |
| Posteingang | E-Mails & Termine (SYS-09) | Arbeiten & Büro | keine eigene erkannt | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](posteingang/README.md) |
| Medien | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 24 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](medien/README.md) |
| Übersicht | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 27 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](alltag-start/README.md) |
| Werkzeuge: Notizen, Rechner, Wecker | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 31 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](werkzeuge/README.md) |
| Vorhaben | Neues Projekt starten (X-11) | Arbeiten & Büro | keine eigene erkannt | – | 6 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](vorhaben/README.md) |
| Start | Texte & Tabellen (SYS-10) | Arbeiten & Büro | keine eigene erkannt | 25 | 25 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](doku-start/README.md) |
| Trendo PDF | Texte & Tabellen (SYS-10) | Arbeiten & Büro | keine eigene erkannt | 25 | 26 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](pdf/README.md) |
| Trendo Präsentation | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Folienansicht | 25 | 27 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](praesentation/README.md) |
| Trendo Tabelle | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Tabellenraster | 25 | 33 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](tabelle/README.md) |
| Trendo Text | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Dokumentseite (Textverarbeitung) | 25 | 33 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](text/README.md) |
| Geld | Geld & Rechnungen (SPEC-11) | Business & Geld | Diagramm | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](finanzen/README.md) |
| Verkaufen | Online verkaufen (STUDIO-ONLINE-BUSINESS-01) | Business & Geld | Diagramm | – | 3 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](verkaufen/README.md) |
| Freie Assets | Freie Bilder & Klänge finden (X-07) | Kreativ & Medien | keine eigene erkannt | – | 4 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](assets/README.md) |
| KI-Vorlagen | KI-Anweisungen schreiben (CORE-12) | Kreativ & Medien | keine eigene erkannt | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-vorlagen/README.md) |
| Kleidung & Produkte | Kleidung & Produkte entwerfen (SPEC-08) | Kreativ & Medien | keine eigene erkannt | 18 | 18 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](mode-programm/README.md) |
| Live streamen | Live streamen (SPEC-15) | Kreativ & Medien | Bildansicht, Diagramm/Zeichnung | 20 | 39 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](sendung/README.md) |
| TV & Live | TV & Live (STUDIO-TV-01) | Kreativ & Medien | keine eigene erkannt | – | 12 | – | IN_PROGRESS / Test NOT_RUN | [Paket](tv/README.md) |
| Bildbearbeitung | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | Bild-Arbeitsfläche (Ebenen) | – | 3 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](bild/README.md) |
| Medien bearbeiten | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | keine eigene erkannt | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](kreativ-medien/README.md) |
| Videoschnitt | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | Video-Zeitleiste | – | 7 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](schnitt/README.md) |
| 3D | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | CAD-/3D-Zeichenfläche | – | 17 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](3d/README.md) |
| Trendo 3D-Druck | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | keine eigene erkannt | – | 4 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](3d-druck/README.md) |
| Trendo CAD | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | CAD-/3D-Zeichenfläche | – | 4 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](cad/README.md) |
| Aus Fotos | Gegenstände einscannen (SPEC-09) | 3D, Spiele & VR | keine eigene erkannt | 20 | 23 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](scan3d/README.md) |
| Punktwolke | Gegenstände einscannen (SPEC-09) | 3D, Spiele & VR | Punktwolken-Ansicht | 20 | 23 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](punktwolke/README.md) |
| Spiele bauen | Spiele bauen (CORE-06) | 3D, Spiele & VR | keine eigene erkannt | – | 5 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](spiele-programm/README.md) |
| Werkstatt | Spiele bauen (CORE-06) | 3D, Spiele & VR | keine eigene erkannt | – | 20 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](spiele/README.md) |
| VR & AR gestalten | VR & AR gestalten (SPEC-04) | 3D, Spiele & VR | 3D-Ansicht | 16 | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](xr/README.md) |
| Auto & Fahrten | Auto & Fahrten (SPEC-16) | Technik & Fachgebiete | Diagramm/Zeichnung | 20 | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](fahrzeug/README.md) |
| CNC-Werkstatt | Fräsen & Fertigen (SPEC-14) | Technik & Fachgebiete | Werkzeugbahn-Ansicht | 20 | 11 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](cnc/README.md) |
| Geräte überwachen | Geräte überwachen (SPEC-10) | Technik & Fachgebiete | Diagramm/Zeichnung | 20 | 20 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](zwilling/README.md) |
| Kameras & Roboter steuern | Kameras & Roboter steuern (CORE-09) | Technik & Fachgebiete | Bildansicht, Roboter-Ansicht | 20 | 27 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](roboter/README.md) |
| Karten & Touren | Routen & Karten (SPEC-13) | Technik & Fachgebiete | Karte | 20 | 14 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](karten/README.md) |
| Schaltung & Simulation | Schaltungen berechnen (SPEC-02) | Technik & Fachgebiete | Schaltplan-Zeichenfläche | 16 | 20 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](elektronik/README.md) |
| Tragwerk & Mechanik | Statik nachrechnen (SPEC-03) | Technik & Fachgebiete | Tragwerks-Zeichenfläche | 16 | 17 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](technik/README.md) |
| Grundriss & Wohnen | Wohnung planen (SPEC-01) | Technik & Fachgebiete | Grundriss-Zeichenfläche | 16 | 23 | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](architektur/README.md) |
| Lernen mit Karteikarten | Lernen mit Karteikarten (SPEC-06) | Wissen & Lernen | Diagramm/Zeichnung, Lernkarte | 16 | 13 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](lernen/README.md) |
| Messdaten & Auswertung | Messwerte auswerten (SPEC-05) | Wissen & Lernen | Diagramm/Zeichnung, Tabellenraster | 16 | 20 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](wissenschaft/README.md) |
| Recherche im Lesemodus | Sicher recherchieren (SYS-08) | Wissen & Lernen | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](recherche/README.md) |
| Wissen für die KI | Wissen merken (CORE-10) | Wissen & Lernen | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-wissen/README.md) |
| Abläufe (Automatisch erledigen) | Aufgaben automatisieren (SYS-05) | KI & Automatik | keine eigene erkannt | – | 17 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ablaeufe/README.md) |
| Produktion | Großprojekte umsetzen (X-01) | KI & Automatik | keine eigene erkannt | – | 8 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](produktion/README.md) |
| KI | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 43 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki/README.md) |
| KI fragen | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 10 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-fragen/README.md) |
| Online-Dienste | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 41 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](keys/README.md) |
| KI-Rollen und Teams | KI-Team einsetzen (CORE-03) | KI & Automatik | keine eigene erkannt | – | 11 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-rollen/README.md) |
| KI-Bibliothek | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 6 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-bibliothek/README.md) |
| KI-Quellen | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 20 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-quellen/README.md) |
| Kostenlose Internet-KIs | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 6 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-internet/README.md) |
| Baukasten | Apps zusammenklicken (X-03) | Entwickeln | keine eigene erkannt | – | 13 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](baukasten/README.md) |
| Sauberer Nachbau | Programme nachbauen (X-10) | Entwickeln | keine eigene erkannt | – | 7 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](nachbau/README.md) |
| Open Source | Projekte von GitHub holen (X-06) | Entwickeln | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](oss/README.md) |
| Erweiterungen | Trendo erweitern (X-04) | Entwickeln | keine eigene erkannt | – | 7 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](erweiterungen/README.md) |
| Software | Webseite bauen (CORE-07) | Entwickeln | Bildansicht | – | 19 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](dev/README.md) |
| Ordner im Heimnetz teilen | Dateien teilen (SYS-06) | System & Sicherheit | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](teilen/README.md) |
| Freigaben | Daten schützen (CORE-11) | System & Sicherheit | keine eigene erkannt | – | 10 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](approvals/README.md) |
| Schutz & Datenschutz | Daten schützen (CORE-11) | System & Sicherheit | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](schutz/README.md) |
| Sicherung | Daten schützen (CORE-11) | System & Sicherheit | keine eigene erkannt | – | 5 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](sicherung/README.md) |
| Alle Studios | Desktop einrichten (CORE-01) | System & Sicherheit | Bildansicht | – | 41 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](studios/README.md) |
| Desktop-Symbole | Desktop einrichten (CORE-01) | System & Sicherheit | Bildansicht | – | 4 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](desktop-symbole/README.md) |
| Ersteinrichtung | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 12 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](einrichtung/README.md) |
| Projekte | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 37 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](projects/README.md) |
| Startbildschirm | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 77 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](start/README.md) |
| Selbstheilung | Einrichten & Reparieren (X-02) | System & Sicherheit | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](heilung/README.md) |
| Geräte & Laufwerke | Geräte erkennen (SYS-07) | System & Sicherheit | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](geraete/README.md) |
| Handy verbinden | Handy verbinden (X-08) | System & Sicherheit | keine eigene erkannt | – | 23 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](handy/README.md) |
| Speicher & Leistung | Leistung anzeigen (X-09) | System & Sicherheit | keine eigene erkannt | – | 31 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](ressourcen/README.md) |
| Geräteverbund (PC, Pi, Android) | PC, Pi & Handy verbinden (CORE-04) | System & Sicherheit | keine eigene erkannt | – | 19 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](verbund/README.md) |
| Programme verwalten | Programme installieren (SYS-04) | System & Sicherheit | keine eigene erkannt | – | 15 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](appcenter/README.md) |
| Apps | Programme starten (SYS-11) | System & Sicherheit | keine eigene erkannt | 25 | 41 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](apps/README.md) |
| Hinzufügen | Programme starten (SYS-11) | System & Sicherheit | keine eigene erkannt | 25 | 12 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](hinzufuegen/README.md) |
| Pflegen | Programme starten (SYS-11) | System & Sicherheit | keine eigene erkannt | 25 | 18 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](prog-pflege/README.md) |
| Zusatzprogramme | Programme starten (SYS-11) | System & Sicherheit | keine eigene erkannt | 25 | 46 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](components/README.md) |
| Fortschritt | Qualität prüfen (SYS-13) | System & Sicherheit | keine eigene erkannt | 25 | 13 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](fortschritt/README.md) |
| Leistung | Qualität prüfen (SYS-13) | System & Sicherheit | keine eigene erkannt | 25 | 13 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](qual-leistung/README.md) |
| System & Sicherheit | Qualität prüfen (SYS-13) | System & Sicherheit | keine eigene erkannt | 25 | 26 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](system/README.md) |
| Systemcheck | Qualität prüfen (SYS-13) | System & Sicherheit | keine eigene erkannt | 25 | 17 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](systemcheck/README.md) |
| Einstellungen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | Bildansicht | 25 | 59 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](settings/README.md) |
| Personen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | keine eigene erkannt | 25 | 18 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](personen/README.md) |
| Sehen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | keine eigene erkannt | 25 | 18 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](bedien-sehen/README.md) |
| Trendo-Updates | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | keine eigene erkannt | 25 | 16 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](updates/README.md) |
| Betriebsmodus | Startmodus wählen (SYS-01) | System & Sicherheit | keine eigene erkannt | – | 7 | – | IN_PROGRESS / Test PC_VERIFIED | [Paket](modus/README.md) |
| Downloads | Surfen & Herunterladen (SYS-03) | System & Sicherheit | keine eigene erkannt | 25 | 44 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](downloads/README.md) |
| Sicher prüfen | Surfen & Herunterladen (SYS-03) | System & Sicherheit | keine eigene erkannt | 25 | 17 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](netz-pruefen/README.md) |
| Sicherheits-Werkzeuge & Beweise | System auf Gefahren prüfen (SPEC-12) | System & Sicherheit | keine eigene erkannt | – | 19 | – | IMPLEMENTED / Test PC_VERIFIED | [Paket](forensik/README.md) |
