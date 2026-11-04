# Trendo OS – Programmkatalog

Erzeugt aus dem Repository (angemeldete Programme, Menübänder, Quelltext, Funktionskataloge, Vorbild-Analysen, Tests). Nicht von Hand pflegen – neu erzeugen mit `TRENDO_KATALOG=. dotnet run --project tools/Trendo.Render -c Release`.

94 Programme in 55 Studios.

| Ampel | Programm | Studio | Bereich | Arbeitsfläche | Funktionen | Tests | Vorbild-Analyse | Öffnen (warm) | Trendo OS (Linux) | Modulstatus | Doku |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 🟡 75 | Briefe schreiben | Briefe schreiben (STUDIO-LETTER-01) | Arbeiten & Büro | Briefseite nach DIN 5008, Texteditor | – | 25 | ja | 873 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](brief/README.md) |
| 🔴 40 | Adressbuch | E-Mails & Termine (SYS-09) | Arbeiten & Büro | keine eigene erkannt | – | 7 | ja | 250 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](kontakte/README.md) |
| 🟡 80 | Aufgaben | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Aufgabentafel | – | 11 | ja | 250 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](aufgaben/README.md) |
| 🟢 85 | E-Mail & Kontakte | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Texteditor | – | 18 | ja | 67 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](post/README.md) |
| 🟡 80 | Kalender | E-Mails & Termine (SYS-09) | Arbeiten & Büro | Monatskalender | – | 14 | ja | 225 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](kalender/README.md) |
| 🔴 50 | Posteingang | E-Mails & Termine (SYS-09) | Arbeiten & Büro | keine eigene erkannt | – | 12 | ja | 197 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](posteingang/README.md) |
| 🔴 62 | Medien | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 26 | ja | 211 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](medien/README.md) |
| 🔴 47 | Übersicht | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 30 | – | 214 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](alltag-start/README.md) |
| 🔴 47 | Werkzeuge: Notizen, Rechner, Wecker | Kleine Helfer & Musik (SYS-02) | Arbeiten & Büro | keine eigene erkannt | 25 | 31 | – | 185 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](werkzeuge/README.md) |
| 🔴 40 | Vorhaben | Neues Projekt starten (X-11) | Arbeiten & Büro | keine eigene erkannt | – | 8 | – | 11 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](vorhaben/README.md) |
| 🔴 54 | Start | Texte & Tabellen (SYS-10) | Arbeiten & Büro | keine eigene erkannt | 25 | 25 | – | 38 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](doku-start/README.md) |
| 🔴 59 | Trendo PDF | Texte & Tabellen (SYS-10) | Arbeiten & Büro | keine eigene erkannt | 25 | 30 | ja | 320 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](pdf/README.md) |
| 🟢 99 | Trendo Präsentation | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Folienansicht | 25 | 27 | ja | 22 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](praesentation/README.md) |
| 🟢 99 | Trendo Tabelle | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Tabellenraster | 25 | 48 | ja | 50 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](tabelle/README.md) |
| 🟢 99 | Trendo Text | Texte & Tabellen (SYS-10) | Arbeiten & Büro | Dokumentseite (Textverarbeitung) | 25 | 47 | ja | 27 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](text/README.md) |
| 🟢 85 | Geld | Geld & Rechnungen (SPEC-11) | Business & Geld | Diagramm | – | 12 | ja | 97 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](finanzen/README.md) |
| 🟡 70 | Verkaufen | Online verkaufen (STUDIO-ONLINE-BUSINESS-01) | Business & Geld | Kasse mit Warenkorb | – | 4 | ja | 223 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](verkaufen/README.md) |
| 🔴 25 | Freie Bilder & Klänge | Freie Bilder & Klänge finden (X-07) | Kreativ & Medien | keine eigene erkannt | – | 6 | – | 170 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](assets/README.md) |
| 🔴 45 | KI-Vorlagen | KI-Anweisungen schreiben (CORE-12) | Kreativ & Medien | keine eigene erkannt | – | 11 | ja | 270 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-vorlagen/README.md) |
| 🔴 30 | Skills | KI-Anweisungen schreiben (CORE-12) | Kreativ & Medien | keine eigene erkannt | – | 7 | – | 75 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-skills/README.md) |
| 🔴 50 | Kleidung & Produkte | Kleidung & Produkte entwerfen (SPEC-08) | Kreativ & Medien | keine eigene erkannt | 18 | 19 | ja | 178 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](mode-programm/README.md) |
| 🟡 80 | Live streamen | Live streamen (SPEC-15) | Kreativ & Medien | Bildansicht, Diagramm/Zeichnung | 20 | 39 | ja | 242 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](sendung/README.md) |
| 🔴 50 | Trendo Musik | Trendo Musik (STUDIO-MUSIK-01) | Kreativ & Medien | keine eigene erkannt | – | 37 | ja | 238 ms | ja | IN_PROGRESS / Test NOT_RUN | [Paket](musik-studio/README.md) |
| 🔴 55 | TV & Live | TV & Live (STUDIO-TV-01) | Kreativ & Medien | keine eigene erkannt | – | 19 | ja | 12 ms | ja | IN_PROGRESS / Test NOT_RUN | [Paket](tv/README.md) |
| 🟡 70 | Bildbearbeitung | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | Bild-Arbeitsfläche (Ebenen) | – | 4 | ja | 196 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](bild/README.md) |
| 🔴 55 | Medien bearbeiten | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | keine eigene erkannt | – | 11 | ja | 129 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](kreativ-medien/README.md) |
| 🟢 85 | Videoschnitt | Videos & Fotos bearbeiten (CORE-08) | Kreativ & Medien | Video-Zeitleiste | – | 9 | ja | 8 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](schnitt/README.md) |
| 🟡 70 | 3D | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | CAD-/3D-Zeichenfläche | – | 23 | ja | 191 ms | eingeschränkt (Windows-Explorer) | IMPLEMENTED / Test PC_VERIFIED | [Paket](3d/README.md) |
| 🔴 35 | Trendo 3D-Druck | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | keine eigene erkannt | – | 4 | ja | 9 ms | eingeschränkt (Windows-Explorer) | IMPLEMENTED / Test PC_VERIFIED | [Paket](3d-druck/README.md) |
| 🟡 60 | Trendo CAD | 3D-Modelle erstellen (CORE-05) | 3D, Spiele & VR | CAD-/3D-Zeichenfläche | – | 4 | ja | 188 ms | eingeschränkt (Windows-Explorer) | IMPLEMENTED / Test PC_VERIFIED | [Paket](cad/README.md) |
| 🔴 50 | Aus Fotos | Gegenstände einscannen (SPEC-09) | 3D, Spiele & VR | keine eigene erkannt | 20 | 24 | ja | 178 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](scan3d/README.md) |
| 🟢 85 | Punktwolke | Gegenstände einscannen (SPEC-09) | 3D, Spiele & VR | Punktwolken-Ansicht | 20 | 23 | ja | 9 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](punktwolke/README.md) |
| 🔴 45 | Spiele bauen | Spiele bauen (CORE-06) | 3D, Spiele & VR | keine eigene erkannt | – | 5 | ja | 14 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](spiele-programm/README.md) |
| 🔴 55 | Werkstatt | Spiele bauen (CORE-06) | 3D, Spiele & VR | keine eigene erkannt | – | 27 | ja | 13 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](spiele/README.md) |
| 🟡 75 | VR & AR gestalten | VR & AR gestalten (SPEC-04) | 3D, Spiele & VR | 3D-Ansicht | 16 | 15 | ja | 956 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](xr/README.md) |
| 🟡 80 | Auto & Fahrten | Auto & Fahrten (SPEC-16) | Technik & Fachgebiete | Diagramm/Zeichnung | 20 | 18 | ja | 152 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](fahrzeug/README.md) |
| 🟢 85 | CNC-Werkstatt | Fräsen & Fertigen (SPEC-14) | Technik & Fachgebiete | Werkzeugbahn-Ansicht | 20 | 11 | ja | 108 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](cnc/README.md) |
| 🟢 85 | Geräte überwachen | Geräte überwachen (SPEC-10) | Technik & Fachgebiete | Diagramm/Zeichnung | 20 | 20 | ja | 39 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](zwilling/README.md) |
| 🟢 85 | Kameras & Roboter steuern | Kameras & Roboter steuern (CORE-09) | Technik & Fachgebiete | Bildansicht, Roboter-Ansicht | 20 | 27 | ja | 82 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](roboter/README.md) |
| 🟡 80 | Karten & Touren | Routen & Karten (SPEC-13) | Technik & Fachgebiete | Karte | 20 | 14 | ja | 189 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](karten/README.md) |
| 🟢 85 | Schaltung & Simulation | Schaltungen berechnen (SPEC-02) | Technik & Fachgebiete | Schaltplan-Zeichenfläche | 16 | 20 | ja | 11 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](elektronik/README.md) |
| 🟡 80 | Tragwerk & Mechanik | Statik nachrechnen (SPEC-03) | Technik & Fachgebiete | Tragwerks-Zeichenfläche | 16 | 17 | ja | 176 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](technik/README.md) |
| 🟡 80 | Grundriss & Wohnen | Wohnung planen (SPEC-01) | Technik & Fachgebiete | Grundriss-Zeichenfläche | 16 | 23 | ja | 158 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](architektur/README.md) |
| 🟡 80 | Lernen mit Karteikarten | Lernen mit Karteikarten (SPEC-06) | Wissen & Lernen | Diagramm/Zeichnung, Lernkarte | 16 | 16 | ja | 167 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](lernen/README.md) |
| 🟡 80 | Messdaten & Auswertung | Messwerte auswerten (SPEC-05) | Wissen & Lernen | Diagramm/Zeichnung, Tabellenraster | 16 | 23 | ja | 219 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](wissenschaft/README.md) |
| 🔴 55 | Recherche im Lesemodus | Sicher recherchieren (SYS-08) | Wissen & Lernen | keine eigene erkannt | – | 16 | ja | 10 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](recherche/README.md) |
| 🟡 75 | Wissen für die KI | Wissen merken (CORE-10) | Wissen & Lernen | Texteditor | – | 16 | ja | 317 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-wissen/README.md) |
| 🔴 55 | Abläufe (Automatisch erledigen) | Aufgaben automatisieren (SYS-05) | KI & Automatik | keine eigene erkannt | – | 19 | ja | 8 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ablaeufe/README.md) |
| 🔴 50 | Produktion | Großprojekte umsetzen (X-01) | KI & Automatik | keine eigene erkannt | – | 9 | ja | 159 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](produktion/README.md) |
| 🔴 55 | KI | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 43 | ja | 9 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki/README.md) |
| 🔴 55 | KI fragen | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 17 | ja | 143 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-fragen/README.md) |
| 🔴 35 | Mit Claude verbinden | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 10 | – | 166 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](claude-verbinden/README.md) |
| 🔴 35 | Online-Dienste | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 41 | – | 162 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](keys/README.md) |
| 🔴 40 | Verbindungen | KI fragen (CORE-02) | KI & Automatik | keine eigene erkannt | – | 12 | – | 143 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](verbindungen/README.md) |
| 🔴 35 | KI-Rollen und Teams | KI-Team einsetzen (CORE-03) | KI & Automatik | keine eigene erkannt | – | 11 | – | 177 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-rollen/README.md) |
| 🔴 25 | KI-Bibliothek | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 6 | – | 161 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-bibliothek/README.md) |
| 🔴 35 | KI-Quellen | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 33 | – | 166 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-quellen/README.md) |
| 🔴 25 | Kostenlose Internet-KIs | Kostenlose KI finden (X-05) | KI & Automatik | keine eigene erkannt | – | 6 | – | 188 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](ki-internet/README.md) |
| 🔴 50 | Baukasten | Apps zusammenklicken (X-03) | Entwickeln | keine eigene erkannt | – | 30 | ja | 169 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](baukasten/README.md) |
| 🔴 25 | Sauberer Nachbau | Programme nachbauen (X-10) | Entwickeln | keine eigene erkannt | – | 7 | – | 165 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](nachbau/README.md) |
| 🔴 35 | Open Source | Projekte von GitHub holen (X-06) | Entwickeln | keine eigene erkannt | – | 25 | – | 192 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](oss/README.md) |
| 🔴 25 | Erweiterungen | Trendo erweitern (X-04) | Entwickeln | keine eigene erkannt | – | 7 | – | 176 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](erweiterungen/README.md) |
| 🟡 80 | Software | Webseite bauen (CORE-07) | Entwickeln | Bildansicht | – | 51 | ja | 212 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](dev/README.md) |
| 🟡 65 | Ordner im Heimnetz teilen | Dateien teilen (SYS-06) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 183 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](teilen/README.md) |
| 🟡 65 | Freigaben | Daten schützen (CORE-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 10 | – | 164 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](approvals/README.md) |
| 🟡 70 | Schutz & Datenschutz | Daten schützen (CORE-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 150 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](schutz/README.md) |
| 🟡 55 | Sicherung | Daten schützen (CORE-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 5 | – | 169 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](sicherung/README.md) |
| 🟡 65 | Alle Studios | Desktop einrichten (CORE-01) | System & Sicherheit | Bildansicht | – | 44 | – | 212 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](studios/README.md) |
| 🟡 55 | Desktop-Symbole | Desktop einrichten (CORE-01) | System & Sicherheit | Bildansicht | – | 4 | – | 179 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](desktop-symbole/README.md) |
| 🔴 35 | Ersteinrichtung | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 12 | – | 162 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](einrichtung/README.md) |
| 🔴 40 | Projekte | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 37 | – | 148 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](projects/README.md) |
| 🔴 40 | Startbildschirm | Desktop einrichten (CORE-01) | System & Sicherheit | keine eigene erkannt | – | 87 | – | 6 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](start/README.md) |
| 🟡 70 | Selbstheilung | Einrichten & Reparieren (X-02) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 106 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](heilung/README.md) |
| 🟡 70 | Geräte & Laufwerke | Geräte erkennen (SYS-07) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 116 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](geraete/README.md) |
| 🟡 65 | Handy verbinden | Handy verbinden (X-08) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 25 | – | 164 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](handy/README.md) |
| 🟡 65 | Speicher & Leistung | Leistung anzeigen (X-09) | System & Sicherheit | Diagramm/Zeichnung, Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 32 | – | 180 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](ressourcen/README.md) |
| 🟡 65 | Geräteverbund (PC, Pi, Android) | PC, Pi & Handy verbinden (CORE-04) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 19 | – | 175 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](verbund/README.md) |
| 🟡 70 | Programme verwalten | Programme installieren (SYS-04) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 146 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](appcenter/README.md) |
| 🟡 64 | Apps | Programme starten (SYS-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 41 | – | 154 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](apps/README.md) |
| 🟡 64 | Hinzufügen | Programme starten (SYS-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 12 | – | 191 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](hinzufuegen/README.md) |
| 🟡 69 | Pflegen | Programme starten (SYS-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 18 | – | 148 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](prog-pflege/README.md) |
| 🟡 64 | Zusatzprogramme | Programme starten (SYS-11) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 55 | – | 213 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](components/README.md) |
| 🟡 71 | Fortschritt | Qualität prüfen (SYS-13) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 13 | – | 218 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](fortschritt/README.md) |
| 🟡 71 | Leistung | Qualität prüfen (SYS-13) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 13 | – | 177 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](qual-leistung/README.md) |
| 🟡 71 | System & Sicherheit | Qualität prüfen (SYS-13) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 26 | – | 174 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](system/README.md) |
| 🟡 71 | Systemcheck | Qualität prüfen (SYS-13) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 17 | – | 151 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](systemcheck/README.md) |
| 🟡 72 | Einstellungen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | Bildansicht, Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 66 | – | 429 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](settings/README.md) |
| 🟡 77 | Personen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 22 | – | 160 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](personen/README.md) |
| 🟡 77 | Sehen | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 18 | – | 160 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](bedien-sehen/README.md) |
| 🟡 77 | Trendo-Updates | Schrift & Farben anpassen (SYS-12) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 18 | – | 160 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](updates/README.md) |
| 🟡 65 | Betriebsmodus | Startmodus wählen (SYS-01) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 15 | – | 151 ms | ja | IN_PROGRESS / Test PC_VERIFIED | [Paket](modus/README.md) |
| 🟡 72 | Downloads | Surfen & Herunterladen (SYS-03) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 47 | – | 22 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](downloads/README.md) |
| 🟡 72 | Sicher prüfen | Surfen & Herunterladen (SYS-03) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | 25 | 17 | – | 20 ms | eingeschränkt (Registry) | IMPLEMENTED / Test PC_VERIFIED | [Paket](netz-pruefen/README.md) |
| 🟡 65 | Sicherheits-Werkzeuge & Beweise | System auf Gefahren prüfen (SPEC-12) | System & Sicherheit | Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11) | – | 19 | – | 163 ms | ja | IMPLEMENTED / Test PC_VERIFIED | [Paket](forensik/README.md) |
