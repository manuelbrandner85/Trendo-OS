# T-0002 Bericht: Studio-Seitenüberschriften

**Auftrag:** Jede Studio-Seite zeigt als Überschrift den Namen aus `StudioCatalog`, nicht einen zweiten fest eingetragenen Text.
**Branch:** `grok/T-0002-studio-ueberschriften`

## Geänderte Dateien

- `src/Trendo.App/StudioCatalog.cs` – Hilfsmethode `NameOf(id, fallback)`. Kein Name im Katalog geändert.
- `src/Trendo.App/MainWindow.axaml.cs` – `Page()` setzt die Überschrift über `StudioCatalog.NameOf`. Die Zuordnung Seite → Modul kommt aus `PageModules` (gesetzt in `RegisterTab`) und `StudioPages`. Zusätzlich die Einstiege, die `PageForStudio` ohne Registerkarte öffnet: Brief (`brief`, `akten`, `akte`, `ausgabe`, `briefkoepfe` → STUDIO-LETTER-01) und Dokumente (`office` → SYS-10). Die Überschrift heißt `page-title`.
- `tests/Trendo.App.Tests/StudioHeadingTests.cs` – neuer Test.

Registerkarten, Sucheinträge und `PageTitle` (Fenstertitelleiste, „Zuletzt benutzt“) sind unverändert. Seiten ohne Studio und Studios ohne Katalogeintrag behalten den bisherigen Text als Fallback.

## Alte und neue Überschrift

Gleiche neue Überschrift bei mehreren Seiten: alle Reiter eines Moduls zeigen den einen Katalognamen. Die Reiterbeschriftung bleibt der kurze Text.

| Seite | Modul | bisher | jetzt |
| --- | --- | --- | --- |
| brief | STUDIO-LETTER-01 | Briefstudio · Schnell schreiben | Briefe schreiben |
| akten | STUDIO-LETTER-01 | Briefstudio · Akten | Briefe schreiben |
| akte | STUDIO-LETTER-01 | Briefstudio · Akte | Briefe schreiben |
| ausgabe | STUDIO-LETTER-01 | Briefstudio · Prüfen & Ausgabe | Briefe schreiben |
| briefkoepfe | STUDIO-LETTER-01 | Briefstudio · Briefköpfe | Briefe schreiben |
| office | SYS-10 | Dokumente | Texte & Tabellen |
| doku-pdf | SYS-10 | PDF | Texte & Tabellen |
| doku-schreiben | SYS-10 | Schreiben | Texte & Tabellen |
| doku-ordnen | SYS-10 | Ordnen | Texte & Tabellen |
| post | SYS-09 | E-Mail & Kontakte | E-Mails & Termine |
| vorhaben | X-11 | Vorhaben | Neues Projekt starten |
| alltag-dateien | SYS-02 | Dateien | Kleine Helfer & Musik |
| alltag-bilder | SYS-02 | Bilder & Medien | Kleine Helfer & Musik |
| alltag-zeit | SYS-02 | Zeit & Kalender | Kleine Helfer & Musik |
| alltag-helfer | SYS-02 | Kleine Helfer | Kleine Helfer & Musik |
| business | STUDIO-ONLINE-BUSINESS-01 | Business-Studio | Online verkaufen |
| finanzen | SPEC-11 | Geld | Geld & Rechnungen |
| kreativ-medien | CORE-08 | Medien-Studio | Videos & Fotos bearbeiten |
| sendung | SPEC-15 | Sendung & Stream | Live streamen |
| mode | SPEC-08 | Mode & Produkt | Kleidung & Produkte entwerfen |
| assets | X-07 | Freie Assets | Freie Bilder & Klänge finden |
| ki-vorlagen | CORE-12 | Vorlagen | KI-Anweisungen schreiben |
| 3d | CORE-05 | 3D-Studio | 3D-Modelle erstellen |
| spiele | CORE-06 | Spiele-Studio | Spiele bauen |
| xr | SPEC-04 | VR & AR | VR & AR gestalten |
| scan3d | SPEC-09 | 3D-Scan | Gegenstände einscannen |
| architektur | SPEC-01 | Räume & Wohnen | Wohnung planen |
| elektronik | SPEC-02 | Elektronik | Schaltungen berechnen |
| technik | SPEC-03 | Technik & Statik | Statik nachrechnen |
| zwilling | SPEC-10 | Digitaler Zwilling | Geräte überwachen |
| karten | SPEC-13 | Karten & GPS | Routen & Karten |
| cnc | SPEC-14 | CNC & Fertigung | Fräsen & Fertigen |
| fahrzeug | SPEC-16 | Fahrzeug | Auto & Fahrten |
| roboter | CORE-09 | Sehen & Roboter | Kameras & Roboter steuern |
| wissenschaft | SPEC-05 | Messdaten | Messwerte auswerten |
| lernen | SPEC-06 | Lernen | Lernen mit Karteikarten |
| ki-wissen | CORE-10 | Wissen | Wissen merken |
| recherche | SYS-08 | Recherche (Lesemodus) | Sicher recherchieren |
| ki-fragen | CORE-02 | KI fragen | KI fragen |
| ki-rollen | CORE-03 | KI-Rollen | KI-Team einsetzen |
| ki-quellen | X-05 | KI-Quellen | Kostenlose KI finden |
| ablaeufe | SYS-05 | Abläufe | Aufgaben automatisieren |
| produktion | X-01 | Produktion | Großprojekte umsetzen |
| dev | CORE-07 | Software-Studio | Webseite bauen |
| baukasten | X-03 | Baukasten | Apps zusammenklicken |
| oss | X-06 | Open Source | Projekte von GitHub holen |
| erweiterungen | X-04 | Erweiterungen | Trendo erweitern |
| nachbau | X-10 | Sauberer Nachbau | Programme nachbauen |
| modus | SYS-01 | Betriebsmodus | Startmodus wählen |
| schutz | CORE-11 | Schutz | Daten schützen |
| forensik | SPEC-12 | Sicherheits-Werkzeuge | System auf Gefahren prüfen |
| netz-pruefen | SYS-03 | Sicher prüfen | Surfen & Herunterladen |
| netz-laden | SYS-03 | Laden & Speichern | Surfen & Herunterladen |
| netz-browser | SYS-03 | Browser | Surfen & Herunterladen |
| appcenter | SYS-04 | Programme verwalten | Programme installieren |
| teilen | SYS-06 | Teilen im Heimnetz | Dateien teilen |
| geraete | SYS-07 | Geräte | Geräte erkennen |
| prog-pflege | SYS-11 | Programme pflegen | Programme starten |
| prog-start | SYS-11 | Programme starten | Programme starten |
| prog-pruefen | SYS-11 | Programme prüfen | Programme starten |
| bedien-sehen | SYS-12 | Bedienung – Sehen | Schrift & Farben anpassen |
| bedien-hilfen | SYS-12 | Bedienung – Hilfen | Schrift & Farben anpassen |
| bedien-pruefen | SYS-12 | Bedienung – Prüfen | Schrift & Farben anpassen |
| qual-leistung | SYS-13 | Qualität – Leistung | Qualität prüfen |
| qual-rueckfall | SYS-13 | Qualität – Rückfall & Lizenzen | Qualität prüfen |
| heilung | X-02 | Selbstheilung | Einrichten & Reparieren |
| handy | X-08 | Handy | Handy verbinden |
| verbund | CORE-04 | Geräteverbund | PC, Pi & Handy verbinden |
| ressourcen | X-09 | Speicher & Leistung | Leistung anzeigen |

Unverändert, weil kein Katalogeintrag: `biz-markt` „Markt“ (BUS-003), `biz-marke` „Marke & Kunden“ (BUS-013), `biz-betrieb` „Betrieb“ (BUS-019), `biz-start` „Start & Übergabe“ (BUS-030).

Unverändert, weil keine Studio-Seite: unter anderem Alle Studios, Projekte, Freigaben, Suche, KI, Apps, Werkzeuge, Medien, Browser & Downloads, Einstellungen, Ersteinrichtung, Systemcheck, Sicherung, System & Sicherheit, Fortschritt, Zusatzprogramme, Dienste & Schlüssel.

## Testergebnis

Release, `dotnet test`, Ausgabe „Bestanden!“:

- `Trendo.Core.Tests`: Fehler 0, erfolgreich 423, übersprungen 0, gesamt 423, Dauer 18 s
- `Trendo.App.Tests`: Fehler 0, erfolgreich 130, übersprungen 0, gesamt 130, Dauer 4 m 48 s (darin der neue Test, 2 Fälle)
- `Trendo.Api.Tests`: Fehler 0, erfolgreich 14, übersprungen 0, gesamt 14, Dauer 2 s

## Offene Punkte / Risiken

- Mehrere Reiter eines Studios tragen dieselbe große Überschrift. Welcher Reiter offen ist, steht weiter auf dem Reiter selbst.
- Die Fenstertitelleiste und „Zuletzt benutzt“ im Startmenü nutzen `PageTitle` und damit die kurze Reiterbeschriftung, nicht den Katalognamen.
- CORE-01 öffnet den Startbildschirm. Der hat keine Überschrift aus `Page()`.
- SPEC-07 hat keine Seite. Katalog und Seite wurden nicht angefasst.

## Nicht geprüft

- Kein Lauf der installierten Desktop-App mit Maus. Geprüft hat der Headless-UI-Test: jede zugeordnete Studio-Seite öffnen und die Überschrift `page-title` lesen.
- Fenstertitelleiste, Startmenü-Suche und Kacheln nicht neu gestaltet und nicht einzeln durchgeklickt. Kacheln und Karten nutzen den Katalog bereits.
- QA-017, QA-019 und Codesignatur nicht als bestanden gewertet.
- `requirements.yaml` nicht geändert.
