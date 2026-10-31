# Vorbild-Analyse Statik: edubeam (Clean-Room)

- Quelle: öffentliche Web-App run.edubeam.app und README auf github.com/janvorisek/edubeam (GPL-3.0); Recherche-Alternativen: Ftool, LinPro, Frame2D, PlenFrem
- Werkzeug: Bedienung im eingebauten Browser angesehen (Bedienelemente, Beispielmodelle). REA-Webanalyse war nicht möglich: Sie braucht einen Browser mit Debug-Schnittstelle (CDP), die hier nicht verfügbar ist. Kein Download, kein Code übernommen.
- Beobachtet: Zeichenfläche mit Lineal-Raster; Schalter für verformte Form, N, V, M, Lagerreaktionen, Beschriftungen; untere Reiter Knoten/Elemente/Lasten/Materialien/Querschnitte/Ergebnisse; Rückgängig/Wiederholen, Einpassen; Beispielmodelle; Einheitensystem; Lager mit Reaktionspfeilen und Werten; Momentenlinie auf der Zugseite.
- README: Timoshenko-Balken und Fachwerkstäbe, Endgelenke, schräge Lager, Punkt-/Strecken-/Trapezlasten, Momente, Lagersenkung, Temperatur; Mechanismus-Erkennung; Querschnitts-Editor; Export SVG/PNG/CSV.

## Übernommen in Trendo „Tragwerk & Mechanik“ (0.9.26)
Zeichenfläche mit Raster/Fang, Stäbe zeichnen, Lager (Fest/Los/Einspannung), Gelenke, Knoten-/Strecken-/Einzellasten, Schalter M/V/N/Biegelinie/Reaktionen, Mechanismus-Meldung, Vorlagen, Export SVG/CSV + PDF-Bericht; dazu eigene Nachweise (Spannung, L/300, Euler-Knicken) und Profilvorschlag.

## Noch nicht
Trapezlasten, Lagersenkung, Temperaturlasten, schräge Lager, Querschnitts-Editor für freie Formen, Einheitenwahl (nur SI).
