# T-0719: D4 (Rest) – Karten & Touren und Tagesplaner: direkt auf der Fläche bearbeiten

**Ziel:** Die letzten zwei Formular-Programme aus Audit-Paket D4 werden direkt bedienbar: auf der Karte Punkte setzen/ziehen/löschen, Routen zeichnen und ändern; im Tagesplaner eine Zeitleiste mit ziehbaren Blöcken statt Listen.
**Branch:** claude/T-0719-karten-tagesplaner (Basis: claude/T-0685-b1-integration)
**Grundlage:** Audit 2026-11-16 (`docs/audit/2026-11-16-profi-audit/BERICHT.md`, D4; SPEC-13 Karten, STUDIO-DAYPLAN-01 „Listen statt Zeitleiste, kein Verschieben“), Vorbild T-0696/T-0697.
**Dateien/Bereich:** `MapCanvas.cs`, `MainWindow.Spec13.cs`, `Trendo.Core/Karten/MapDoc.cs` (GPX-Import/-Ausgabe), neu `DayTimeline.cs`, `MainWindow.Tagesplaner.cs`, `MainWindow.KreativStudios4.cs` (nur Abschnitt Tagesplaner); Tests; `reports/T-0719/`.
**Fertig, wenn:**
- [x] Karten: Wegpunkte setzen/ziehen/löschen, Routenpunkte ziehen, aus der Mitte einfügen, Rechtsklick-Menü, F2, jeder Schritt ein Rückgängig der Hülle
- [x] Karten: GPX öffnen/auf die Karte ziehen, jede Spur/Route einzeln; Export GPX 1.1 (Reihenfolge wpt → rte → trk) und KML; keine neuen Kachelquellen
- [x] Tagesplaner: Zeitleiste – Block ziehen, Kante ziehen, Doppelklick neu, im Block umbenennen, Rechtsklick, Pfeiltasten, Entf, Rückgängig; ICS mit Ical.Net geprüft
- [x] Erster Schritt im Leerzustand, eine goldene Hauptaktion, Dunkel-Gold, Einfach verliert nichts, Menüleiste und Assistenzweg funktionieren; alte Dateien lesbar
- [x] Pflicht-Testklassen grün, Bilder Einfach/Profi angesehen
**Nicht tun:** Programm-Hülle, Kachelquellen, Routing-Dienste, Wochenansicht als eigenes Format; kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0719-karten-tagesplaner.bericht.md`.
