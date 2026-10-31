# T-0277 – Schaltung: Platinen-Layout (PCB)

- Freigabe: Manuel im Chat am 31.10.2026 („Erst die Restpunkte“)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): KiCad, Fritzing; kein Code übernommen
- Umgesetzt: Durchsteck-Gehäuse in Normmaßen (Widerstand 0207 Raster 10,16, Diode DO-41, Kondensator/LED radial 2,54, Schraubklemme 5,08, TO-92 auf 2,54 gebogen, DIP-8 mit Pin 2/3/6 für den OPV), Platinen-Ansicht (Lötstopplack, goldene Pads mit Bohrung, Bestückungsdruck, Befestigungslöcher, Bahnen Oberseite/Unterseite, Luftlinien), automatisch platzieren (Raster 1,27 mm, Platz für Bahnen), Bauteile mit der Maus ziehen und mit R drehen (Rückgängig), Luftlinien als minimaler Spannbaum je Netz, Autorouter auf zwei Lagen (Winkel und Umwege, Mindestabstand 0,3 mm), Prüfung (Bahn an fremdem Anschluss, Bahnen zu nah, außerhalb der Platine, offene Verbindungen), Fertigungsdaten als ZIP (Gerber RS-274X: F_Cu, B_Cu, F_SilkS, Edge_Cuts; Excellon-Bohrdatei).
- Gefunden und behoben: TO-92 mit 1,27 mm ließ keine Bahn vorbei (jetzt 2,54 mm); nach dem Ziehen wirkte R erst nach einem weiteren Klick
- Grenzen: einfacher Router (keine Optimierung, keine Durchkontaktierungen außer den THT-Pads), keine SMD-Gehäuse, keine Kupferflächen; Gerber vor der Bestellung im Viewer des Herstellers prüfen
- Tests: Core 3 (Platzieren, Luftlinien, Routing ohne Konflikt, Prüfung, Drehen, Gerber/Bohrdaten/ZIP, Speichern), App 1 (echte Maus/Taste)
