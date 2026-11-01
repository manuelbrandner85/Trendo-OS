# G0 – Prüfbericht „Studio-Perfektion“ (Stand 01.11.2026, Version 0.9.30)

Grundlage: Paket „TRENDO OS 10 Studio Perfection & Claude Workspace“, Stufe G0 (nur lesen, nichts ändern).
Erhoben aus dem Quelltext (`src/`), dem Programmkatalog (`docs/programme/katalog.json`) und den Testläufen.
Die Merkmale je Programm stammen aus einer Textsuche in den Quelldateien der jeweiligen Seite. Das ist ein **Hinweis, kein Funktionsnachweis**:
„–“ heißt „im Quelltext der Seite nicht gefunden“, nicht „sicher kaum vorhanden“. Programme in der gemeinsamen Programmhülle
(`ProgramShell` / `ProgramDocument`) bekommen Speichern, Rückgängig und automatisches Sichern von der Hülle.

## Zusammenfassung

| Bereich (Paket-Nr.) | Iststand | Lücke |
|---|---|---|
| 01 Oberfläche | 91 Programme in 54 Studios; Gold-Blau-Farben zentral in `MainWindow` (Accent, Edge, Muted …) | keine benannten Design-Werte für Abstände/Schriftgrößen; keine Prüfung bei 125/150/200 % DPI |
| 02 Fenster & Vollbild | F11 = Fokusmodus (echtes Vollbild) für alle Programme; Vollbild außerdem in Präsentation, PDF, TV, Text, Neuerungen | Esc beendet den Fokusmodus nicht überall; Fensterposition/-größe wird nicht gespeichert; Mehrschirm nur im TV-Vollbild berücksichtigt |
| 03 Rechtsklick & Rückgängig | Kontextmenüs in Text, Tabelle, Präsentation, CAD, Kalender/Aufgaben/Posteingang, Desktop; Rückgängig in 21 Programmen + allen 11 Hüllen-Programmen | 82 von 91 Programmen ohne eigenes Kontextmenü; keine zentrale Befehlsliste (jede Seite baut ihre Knöpfe selbst) |
| 04 Claude-Arbeitsbereich | Claude-Bereich rechts in jedem Studio (Terminal, claude.ai, Trendo-Agent); seit 01.11. nur Zwischenablage bzw. Eintippen ohne Enter | Studio-Kontext (aktive Datei/Auswahl) wird noch nicht mit Vorschau übergeben, nur Rolle + Fachregeln + Auftrag |
| 05 Fachfunktionen | 76 Programme IMPLEMENTED, 15 IN_PROGRESS (Startseite, Studios, Projekte, Desktop-Symbole, Einrichtung, Einstellungen, Personen, Bedienung, Updates, Modus, Handy, Ressourcen, Verbund, Brief, TV) | 57 Programme ohne erkannte eigene Arbeitsfläche (viele davon Verwaltungsseiten, wo das passt); wenig Tests bei Bild (3), Verkaufen (4), 3D-Druck (4), CAD (4), Desktop-Symbole (4) |
| 06 KI-Kontext | Rolle + Fachregeln je Studio; Freigaben über ApprovalGate | keine Vorschau „das bekommt die KI zu sehen“ mit Freigabe je Datei |
| 07 Speichern & Wiederherstellen | `ProgramDocument`: Speichern über temporäre Datei + Ersetzen, Sicherung alle 30 s (`.autosave`), Wiederherstellen nach Absturz; Text/Tabelle/Präsentation mit eigener Sicherung | Autosave-Datei selbst wird direkt geschrieben (nicht atomar); Programme außerhalb der Hülle (z. B. 3D, Spiele, Roboter, Zwilling, Fahrzeug, Lernen, Brief) ohne automatische Sicherung |
| 08 Tempo | Seitenmessung in „Qualität & Leistung“; Speicherlecks durch DedupeRegistries behoben | App-Tests dauern 20–48 min; alle Studios werden beim Start registriert (kein Laden erst bei Bedarf) |
| 09 Profi-Editoren | Menüband + Eigenschaften + Statuszeile in den 11 Hüllen-Programmen; echte Arbeitsflächen in Text, Tabelle, Präsentation, Bild, Schnitt, CAD, Architektur, Elektronik … | Tastenkürzel fehlen in etwa der Hälfte der Programme (z. B. Architektur, Elektronik, Karten, Bild) |
| 10 QA & Abnahme | Volltest je Version mit Rohlog + SHA-256 unter `reports/`; Status nur mit Beleg | Klick-Audit über alle Knöpfe zuletzt am **22.10.2026** (73 Befunde: 36 stumm, 6 Abstürze); muss neu laufen; keine echten Fenstertests bei 150/200 % DPI und zweitem Bildschirm (NOT_RUN) |

## Befund Datenschutz (beim G0 gefunden, behoben)

Das Werkzeug für Katalog und Vorschaubilder (`tools/Trendo.Render`) lief nicht im Testmodus. Die Seite „Start“ von Texte & Tabellen las
dabei „Zuletzt benutzt“ aus dem echten Dokumente-Ordner, und diese Dateinamen standen im öffentlichen Katalog
(`docs/programme/katalog.json`, `docs/programme/doku-start/FEATURE_MATRIX.md`, seit Commit `ab682fbf`). Behoben: Das Werkzeug läuft jetzt immer im Testmodus,
der Katalog ist neu erzeugt und enthält keine Dateinamen mehr. Ältere Commits enthalten sie weiterhin (Entscheidung über das Umschreiben des Verlaufs liegt beim Projektinhaber).

## Empfohlenes Pilotstudio (G1)

**Architektur & Innenräume (SPEC-01, Seite `architektur`)**, weil es in der gemeinsamen Programmhülle läuft: Was dort für Rechtsklick,
Tastenkürzel, Fensterposition und sicheres Autosave gebaut wird, bekommen sofort auch die anderen 10 Hüllen-Programme
(Bild, Schnitt, Punktwolke, Spiele-Programm, Kleidung & Produkte, CNC, Karten, Elektronik, Technik, Wissenschaft).
Heute fehlen dort: Kontextmenü, Tastenkürzel, Esc für den Fokusmodus. Vorhanden: Grundriss-Zeichenfläche, Speichern/Laden, Rückgängig, Autosave.

## Programme im Einzelnen

Spalten: Tests = Anzahl Tests im Katalog · Speichern / Rückgängig / Autosave / Rechtsklick / Tasten = im Quelltext der Seite gefunden (✓) oder nicht (–).

| Programm | Studio | Arbeitsfläche | Tests | Status | Speichern | Rückgängig | Autosave | Rechtsklick | Tasten |
|---|---|---|---|---|---|---|---|---|---|
| Briefe schreiben | STUDIO-LETTER-01 | Briefseite nach DIN 5008, Texteditor | 12 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Adressbuch | SYS-09 | keine eigene erkannt | 7 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Aufgaben | SYS-09 | Aufgabentafel | 11 | IMPLEMENTED | ✓ | ✓ | – | ✓ | ✓ |
| E-Mail & Kontakte | SYS-09 | Texteditor | 18 | IMPLEMENTED | – | – | – | – | – |
| Kalender | SYS-09 | Monatskalender | 11 | IMPLEMENTED | ✓ | ✓ | – | ✓ | ✓ |
| Posteingang | SYS-09 | keine eigene erkannt | 12 | IMPLEMENTED | ✓ | ✓ | – | ✓ | ✓ |
| Medien | SYS-02 | keine eigene erkannt | 26 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Übersicht | SYS-02 | keine eigene erkannt | 30 | IMPLEMENTED | – | ✓ | – | – | – |
| Werkzeuge: Notizen, Rechner, Wecker | SYS-02 | keine eigene erkannt | 31 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Vorhaben | X-11 | keine eigene erkannt | 8 | IMPLEMENTED | – | ✓ | – | – | – |
| Start | SYS-10 | keine eigene erkannt | 25 | IMPLEMENTED | – | – | – | – | – |
| Trendo PDF | SYS-10 | keine eigene erkannt | 27 | IMPLEMENTED | – | ✓ | – | – | ✓ |
| Trendo Präsentation | SYS-10 | Folienansicht | 27 | IMPLEMENTED | ✓ | ✓ | ✓ | ✓ | ✓ |
| Trendo Tabelle | SYS-10 | Tabellenraster | 33 | IMPLEMENTED | ✓ | ✓ | ✓ | ✓ | ✓ |
| Trendo Text | SYS-10 | Dokumentseite (Textverarbeitung) | 33 | IMPLEMENTED | ✓ | ✓ | ✓ | ✓ | ✓ |
| Geld | SPEC-11 | Diagramm | 12 | IMPLEMENTED | ✓ | ✓ | – | – | ✓ |
| Verkaufen | STUDIO-ONLINE-BUSINESS-01 | Kasse mit Warenkorb | 4 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Freie Bilder & Klänge | X-07 | keine eigene erkannt | 6 | IMPLEMENTED | – | – | – | – | ✓ |
| KI-Vorlagen | CORE-12 | keine eigene erkannt | 11 | IMPLEMENTED | ✓ | – | – | – | – |
| Skills | CORE-12 | keine eigene erkannt | 7 | IMPLEMENTED | ✓ | – | – | – | – |
| Kleidung & Produkte | SPEC-08 | Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 19 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Live streamen | SPEC-15 | Bildansicht, Diagramm/Zeichnung | 39 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| TV & Live | STUDIO-TV-01 | keine eigene erkannt | 14 | IN_PROGRESS | – | – | – | – | ✓ |
| Bildbearbeitung | CORE-08 | Bild-Arbeitsfläche (Ebenen), Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 3 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Medien bearbeiten | CORE-08 | keine eigene erkannt | 11 | IMPLEMENTED | ✓ | – | – | – | – |
| Videoschnitt | CORE-08 | Video-Zeitleiste, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 7 | IMPLEMENTED | ✓ | ✓ | ✓ | – | ✓ |
| 3D | CORE-05 | CAD-/3D-Zeichenfläche | 17 | IMPLEMENTED | ✓ | ✓ | – | – | ✓ |
| Trendo 3D-Druck | CORE-05 | keine eigene erkannt | 4 | IMPLEMENTED | ✓ | – | – | – | – |
| Trendo CAD | CORE-05 | CAD-/3D-Zeichenfläche | 4 | IMPLEMENTED | ✓ | ✓ | – | ✓ | ✓ |
| Aus Fotos | SPEC-09 | keine eigene erkannt | 24 | IMPLEMENTED | ✓ | ✓ | – | – | ✓ |
| Punktwolke | SPEC-09 | Punktwolken-Ansicht, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 23 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Spiele bauen | CORE-06 | Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 5 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Werkstatt | CORE-06 | keine eigene erkannt | 20 | IMPLEMENTED | ✓ | – | – | – | – |
| VR & AR gestalten | SPEC-04 | 3D-Ansicht | 15 | IMPLEMENTED | – | – | – | – | – |
| Auto & Fahrten | SPEC-16 | Diagramm/Zeichnung | 18 | IMPLEMENTED | ✓ | – | – | – | – |
| CNC-Werkstatt | SPEC-14 | Werkzeugbahn-Ansicht, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 11 | IMPLEMENTED | ✓ | ✓ | ✓ | – | ✓ |
| Geräte überwachen | SPEC-10 | Diagramm/Zeichnung | 20 | IMPLEMENTED | ✓ | – | – | – | – |
| Kameras & Roboter steuern | CORE-09 | Bildansicht, Roboter-Ansicht | 27 | IMPLEMENTED | ✓ | – | – | – | – |
| Karten & Touren | SPEC-13 | Karte, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 14 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Schaltung & Simulation | SPEC-02 | Schaltplan-Zeichenfläche, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 20 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Tragwerk & Mechanik | SPEC-03 | Tragwerks-Zeichenfläche, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 17 | IMPLEMENTED | ✓ | ✓ | ✓ | – | ✓ |
| Grundriss & Wohnen | SPEC-01 | Grundriss-Zeichenfläche, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 23 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Lernen mit Karteikarten | SPEC-06 | Diagramm/Zeichnung, Lernkarte | 16 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Messdaten & Auswertung | SPEC-05 | Diagramm/Zeichnung, Tabellenraster, Menüband, Eigenschaften, Statusleiste, Dokument-Reiter | 23 | IMPLEMENTED | ✓ | ✓ | ✓ | – | – |
| Recherche im Lesemodus | SYS-08 | keine eigene erkannt | 16 | IMPLEMENTED | – | – | – | – | ✓ |
| Wissen für die KI | CORE-10 | Texteditor | 16 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Abläufe (Automatisch erledigen) | SYS-05 | keine eigene erkannt | 18 | IMPLEMENTED | – | ✓ | – | – | ✓ |
| Produktion | X-01 | keine eigene erkannt | 9 | IMPLEMENTED | ✓ | – | – | – | – |
| KI | CORE-02 | keine eigene erkannt | 43 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| KI fragen | CORE-02 | keine eigene erkannt | 17 | IMPLEMENTED | – | – | – | – | ✓ |
| Online-Dienste | CORE-02 | keine eigene erkannt | 41 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| KI-Rollen und Teams | CORE-03 | keine eigene erkannt | 11 | IMPLEMENTED | ✓ | – | – | – | – |
| KI-Bibliothek | X-05 | keine eigene erkannt | 6 | IMPLEMENTED | – | – | – | – | – |
| KI-Quellen | X-05 | keine eigene erkannt | 28 | IMPLEMENTED | – | – | – | – | – |
| Kostenlose Internet-KIs | X-05 | keine eigene erkannt | 6 | IMPLEMENTED | ✓ | – | – | – | – |
| Baukasten | X-03 | keine eigene erkannt | 23 | IMPLEMENTED | ✓ | ✓ | – | – | ✓ |
| Sauberer Nachbau | X-10 | keine eigene erkannt | 7 | IMPLEMENTED | – | – | – | – | – |
| Open Source | X-06 | keine eigene erkannt | 25 | IMPLEMENTED | – | – | – | – | ✓ |
| Erweiterungen | X-04 | keine eigene erkannt | 7 | IMPLEMENTED | – | – | – | – | – |
| Software | CORE-07 | Bildansicht | 19 | IMPLEMENTED | ✓ | – | – | – | – |
| Ordner im Heimnetz teilen | SYS-06 | keine eigene erkannt | 15 | IMPLEMENTED | – | – | – | – | ✓ |
| Freigaben | CORE-11 | keine eigene erkannt | 10 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Schutz & Datenschutz | CORE-11 | keine eigene erkannt | 15 | IMPLEMENTED | – | – | – | – | – |
| Sicherung | CORE-11 | keine eigene erkannt | 5 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Alle Studios | CORE-01 | Bildansicht | 41 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Desktop-Symbole | CORE-01 | Bildansicht | 4 | IN_PROGRESS | – | – | – | ✓ | – |
| Ersteinrichtung | CORE-01 | keine eigene erkannt | 12 | IN_PROGRESS | ✓ | – | – | – | – |
| Projekte | CORE-01 | keine eigene erkannt | 37 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Startbildschirm | CORE-01 | keine eigene erkannt | 83 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Selbstheilung | X-02 | keine eigene erkannt | 15 | IMPLEMENTED | – | – | – | – | – |
| Geräte & Laufwerke | SYS-07 | keine eigene erkannt | 15 | IMPLEMENTED | – | – | – | – | – |
| Handy verbinden | X-08 | keine eigene erkannt | 25 | IN_PROGRESS | – | – | – | – | – |
| Speicher & Leistung | X-09 | Diagramm/Zeichnung | 32 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Geräteverbund (PC, Pi, Android) | CORE-04 | keine eigene erkannt | 19 | IN_PROGRESS | – | – | – | – | – |
| Programme verwalten | SYS-04 | keine eigene erkannt | 15 | IMPLEMENTED | – | – | – | – | – |
| Apps | SYS-11 | keine eigene erkannt | 41 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Hinzufügen | SYS-11 | keine eigene erkannt | 12 | IMPLEMENTED | – | – | – | – | – |
| Pflegen | SYS-11 | keine eigene erkannt | 18 | IMPLEMENTED | – | – | – | – | – |
| Zusatzprogramme | SYS-11 | keine eigene erkannt | 53 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Fortschritt | SYS-13 | keine eigene erkannt | 13 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Leistung | SYS-13 | keine eigene erkannt | 13 | IMPLEMENTED | – | – | – | – | – |
| System & Sicherheit | SYS-13 | keine eigene erkannt | 26 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Systemcheck | SYS-13 | keine eigene erkannt | 17 | IMPLEMENTED | ✓ | – | – | – | – |
| Einstellungen | SYS-12 | Bildansicht | 59 | IN_PROGRESS | ✓ | – | – | ✓ | ✓ |
| Personen | SYS-12 | keine eigene erkannt | 18 | IN_PROGRESS | ✓ | – | – | – | ✓ |
| Sehen | SYS-12 | keine eigene erkannt | 18 | IN_PROGRESS | ✓ | – | – | – | – |
| Trendo-Updates | SYS-12 | keine eigene erkannt | 18 | IN_PROGRESS | ✓ | – | – | – | – |
| Betriebsmodus | SYS-01 | keine eigene erkannt | 7 | IN_PROGRESS | – | – | – | – | – |
| Downloads | SYS-03 | keine eigene erkannt | 47 | IMPLEMENTED | ✓ | – | – | – | ✓ |
| Sicher prüfen | SYS-03 | keine eigene erkannt | 17 | IMPLEMENTED | ✓ | ✓ | – | – | – |
| Sicherheits-Werkzeuge & Beweise | SPEC-12 | keine eigene erkannt | 19 | IMPLEMENTED | – | – | – | – | – |

## Nicht geprüft

- Verhalten im echten Fenster bei 125/150/200 % DPI, auf zweitem Bildschirm, nach Absturz (NOT_RUN).
- Klick-Audit über alle Seiten mit dem aktuellen Stand (läuft als Nächstes, Ergebnis wird hier ergänzt).
- Ob jede gefundene Funktion fachlich vollständig ist – dafür gibt es die Tests der Programme.
