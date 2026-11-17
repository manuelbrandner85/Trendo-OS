# Bericht T-0719 – D4 (Rest): Karten & Touren und Tagesplaner direkt bearbeiten

**Zweig:** `claude/T-0719-karten-tagesplaner` (von `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%`.

## Was war → was ist

| Punkt | Vorher (Audit D4, SPEC-13, STUDIO-DAYPLAN-01) | Jetzt | Beleg |
|---|---|---|---|
| Karte: Punkte | Wegpunkt nur setzen, danach nur über Felder/Löschen | Wegpunkte ziehen (am Kopf greifen, kein Sprung); Punkte der gewählten Route/Spur/Fläche als Griffe ziehen; aus der Mitte eines Abschnitts ziehen fügt einen Punkt ein; ab 400 Punkten keine Griffe (aufgezeichnete Spuren). Vorschau nur während des Ziehens, ins Dokument beim Loslassen → **ein** Rückgängig-Schritt („Punkt verschoben“, „Punkt eingefügt“, „Wegpunkt verschoben“) | `T0719_KartenTagesplaner_Ui.Karte_direkt_bearbeiten` (echte Maus) |
| Karte: Rechtsklick | keiner | Punkt löschen · Wegpunkt umbenennen/löschen · Punkt hier einfügen · Richtung umkehren · Route/Spur/Fläche löschen · Wegpunkt hier setzen · Route hier beginnen · Alles zeigen; F2 benennt um (öffnet die Eigenschaften auch in „Einfach“) | ebd. |
| Karte: erster Schritt | leere Karte ohne Hinweis | Karte „Erster Schritt: Route zeichnen“ mit „Route zeichnen“ / „GPX-Datei öffnen …“ (neutral – die eine goldene Hauptaktion bleibt „Für Navi oder Uhr“, `DesignPaket3_Ui` verlangt das) | Bilder `bild-map-leer-*.png` |
| GPX rein | Pfadfeld/„Datei wählen“; alle `trk`/`rte` einer Datei zu **einer** Linie zusammengelegt | „GPX-Datei öffnen …“ im Menüband/Menüleiste, Datei auf die Karte ziehen; jede `<trk>` und `<rte>` eine eigene Linie mit Namen (Route bleibt Route), Segmente einer Spur zusammen, Höhen/Zeiten, Erweiterungen ignoriert; ein Rückgängig-Schritt | `Karte_import_alt_und_assistenz`, Core `T0719_MapGpx` |
| GPX raus | Spur/Route in Listenreihenfolge → `trk` vor `rte` möglich (verstößt gegen GPX 1.1) | immer `wpt → rte → trk` | Core-Test + `reports/T-0719/pruefung.txt` mit Negativprobe |
| Kartenkacheln | OSM-Zwischenspeicher/Offline-Paket, offline Gitter | **unverändert** – keine neue Quelle, nichts Neues geladen | – |
| Tagesplaner | Listen, kein Verschieben (Audit) | Zeitleiste (`DayTimeline`): Termine und Plan-Blöcke als Blöcke; ziehen verschiebt (5-Min-Raster), Unterkante ziehen ändert die Dauer (Aufgabe übernimmt die Minuten), Doppelklick ins Leere = neue Aufgabe mit Namensfeld im Block, Doppelklick/F2 auf Block = umbenennen, Rechtsklick (Aufgabe/Termin/Pause hier · umbenennen · erledigt · ±15 min · löschen/aus dem Plan nehmen), Pfeil hoch/runter ±5 min, Entf; überlappende Blöcke nebeneinander; Arbeitszeit und Jetzt-Linie. Jede Aktion = ein `d.Edit` = ein Rückgängig-Schritt | `Tagesplaner_direkt_bearbeiten` |
| Tagesplaner: erster Schritt | keiner | „Ersten Block anlegen“ (golden) – Block zur nächsten Viertelstunde bzw. Arbeitsbeginn, Name direkt im Block | Bild `bild-day-leer-einfach.png` |
| Kalenderdatei | ICS (nur Aufgaben/Pausen) aus gespeichertem Plan | ICS aus dem, was auf der Zeitleiste steht (auch von Hand verschoben); zusätzlich Befehl „Kalenderdatei mit Terminen“ (Gruppe „Tag“, in „Einfach“ unter „Mehr“) | Ical.Net-Gegenprobe im Test, `pruefung.txt` |
| Alte Dateien | – | Format unverändert (`map` v1; `appts` HH:mm, `plan` s/e, `tasks`); alte Pläne mit Termin-Kopien: Zeitleiste zeigt den Termin einmal, Verschieben hält Termin und Kopie gleich | `Karte_import_alt_und_assistenz`, `Tagesplaner_alt_und_assistenz` |
| Farben | Termine/Infotexte blau (#5FA8D3, #7FD3FF) | Termine grau, Aufgaben gold, Pausen dunkelgrau; Infotexte gold | Bilder |

## Tests (gezielt, kein Volltest)
- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `T0719_MapGpx|T0260_Map|…Map/Geo/Karten/Gpx/DayPlan/KreativKits4` → Bestanden 52/52; QA-019 (`DisplayName~QA-019`) → Bestanden 3/3.
- App neu: `T0719_KartenTagesplaner_Ui` **4/4** (Rohlog `reports/T-0719/app-tests-T0719.log`).
- App Pflicht/berührt, nach der letzten Änderung: UebersichtEinfach_Ui 2/2, T0710_MenuBar_Ui 8/8, KreativStudios4_Ui 5/5, AssistChain_Ui 6/6, DesignPaket3_Ui 14/14. Vorher im selben Stand (nur ohne den letzten Umbau „Kalenderdatei mit Terminen“ vom Export zum Befehl): ProgramHeadDesign_Ui 6/6, DesignPaket4_Ui 8/8, SPEC13_Ui 2/2, SPEC13_UiNet 1/1, SPEC13_UiPack 1/1, VectorMapUi 1/1, StudiosTechnikWissen_Ui 44/44, ProgramLevel_Ui 2/2, SimpleMode_Ui 5/5, Einfachheit_Ui 2/2, EinfachBedienen_Ui 11/11, ClaudePrograms_Ui 6/6, ClaudePolish_Ui 3/3, ClaudeBridge_Ui 7/7, StudioUebergabe_Ui 4/4, BueroStudios_Ui 5/5, KreativStudios_Ui/2/3 je 5/5, FreigabenKernUi 5/5.
- **Rot, nicht aus diesem Paket:** `ClaudeOperate_Ui.Ribbon_and_context` („Ereignis kam nicht am Fenster an“, Seite „architektur“) – auf dem unveränderten Integrationszweig in einer eigenen Kopie gleich rot nachgeprüft.
- `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK.
- Rot-Grün: die neuen Tests nutzen `DayTimeline`, `DayBlocks`, `MapCanvas.MidOf`, `mk-first`, `mk-menu-*`, `dp-menu-*` – auf altem Code nicht übersetzbar; inhaltlich: alter GPX-Import gab 1 statt 4 Linien, alte Ausgabe `trk` vor `rte` (Negativprobe).
- Keine Tests geändert oder geschwächt.

## Belege `reports/T-0719/` (SHA256SUMS)
Exporte aus dem Klick-Test (`karte-export.gpx/.kml`, `tagesplan.ics`, `tagesplan-mit-terminen.ics`), Prüfskript `pruefe_gpx_kml_ics.py` (Python-Standardbibliothek, Regeln aus GPX-1.1-Schema und RFC 5545) mit Ergebnis `pruefung.txt` (alle bestanden, Negativprobe erkannt), Bilder Einfach/Profi beider Programme (Render-Werkzeug, angesehen).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (Karte: Route mit der Maus → GPX; Tagesplaner: erster Block → ICS).
2. Fremddatei rein/bearbeiten/raus/Fremdprogramm: **TEILWEISE** – ICS von Ical.Net (fremde Bibliothek) gelesen; GPX/KML nur mit eigenem Python-Prüfskript nach Schema-Regeln geprüft (gpxpy, GDAL, QGIS, Navi/Uhr nicht vorhanden). Import: **BLOCKIERT für eine echte Fremddatei** – auf dem PC liegt keine GPX eines anderen Programms, Herunterladen braucht eine Freigabe; getestet mit einer von Hand im Garmin-Aufbau geschriebenen GPX (zwei Segmente, Erweiterungen, Route, zwei Spuren). Voraussetzung: eine echte GPX von Uhr/Handy/Navi.
3. Abbruch ≤ 30 s: **TEILWEISE (geerbt)** – jede Aktion ist eine Dokumentänderung der Hülle (Autosicherung, AtomicFile); kein eigener Absturztest.
4. KI im offenen Dokument, rückgängig: **TEILWEISE** – Assistenzweg (T-0707) mit **ersetzter KI**: Wegpunkt in die Karte, Block in den Tagesplan, je ein Rückgängig-Schritt, danach in Karte/Zeitleiste sichtbar. Echte KI/Claude nicht geprüft.
5. Test vom Klick bis zur Datei: **VOLLWERTIG** (GPX über „Für Navi oder Uhr“, ICS über Export-Knopf und Befehl).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen und Ablegen: **VOLLWERTIG** für Maus-Ziehen, Rechtsklick, Doppelklick, Tasten (Enter, Esc, Entf, Pfeile, F2) und Datei-Ablegen über die echten Ereignisse des Headless-Fensters; Einfach verliert nichts (`UebersichtEinfach_Ui`). Echtes Ziehen aus dem Windows-Explorer nicht geprüft.

## Nicht geprüft / ehrlich
- Headless zeichnet ohne Grafik: Griffe der gewählten Route und das Ziehen selbst nur im Test-Modell geprüft, nicht im Bild (das Render-Werkzeug kann nichts anwählen).
- Wochenansicht: **nicht gebaut** – ein Tagesplaner-Dokument ist ein Tag; eine Woche bräuchte ein neues Format.
- „Tag planen“ verteilt neu und überschreibt von Hand verschobene Blöcke (wie vorher); kein Festpinnen.
- OSM-Kacheln werden (wie bisher) beim Ansehen online geholt, nicht über ApprovalGate; offline nur Zwischenspeicher/Paket/Gitter. Nicht geändert.
- Echte Geräte (Garmin, Uhr) und Kalenderprogramme (Outlook, Thunderbird) nicht vorhanden.

## Offene Punkte
- Echte Fremd-GPX prüfen, sobald eine vorliegt (Freigabe zum Laden oder Datei vom Nutzer).
- Wochenansicht und Festpinnen von Blöcken beim Neuplanen.
- `ClaudeOperate_Ui.Ribbon_and_context` ist auf dem Integrationszweig rot (fremd).

## Berührte Dateien
- `src/Trendo.App/MapCanvas.cs`, `src/Trendo.App/MainWindow.Spec13.cs`, `src/Trendo.Core/Karten/MapDoc.cs`
- `src/Trendo.App/DayTimeline.cs` (neu), `src/Trendo.App/MainWindow.Tagesplaner.cs` (neu), `src/Trendo.App/MainWindow.KreativStudios4.cs` (nur Abschnitt Tagesplaner)
- `tests/Trendo.App.Tests/T0719KartenTagesplanerUiTests.cs`, `tests/Trendo.Core.Tests/T0719MapGpxTests.cs` (neu)
- `reports/T-0719/*`, `tasks/T-0719-karten-tagesplaner.md`, `tasks/T-0719-karten-tagesplaner.bericht.md`

ENTWICKLUNGSBERICHT – Sprachen: C# (Avalonia), Python (Prüfskript). Rust eingesetzt: NEIN (Oberflächenlogik im bestehenden C#-Projekt, kein Rechenengpass).
