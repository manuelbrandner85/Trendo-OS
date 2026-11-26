# Bildbearbeitung – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `bild` · Stand: 0.9.110 (Stand 0eb9d52)

18 Tests sprechen dieses Programm an.

- Bild-Dokument: Ebenen-Reihenfolge, Treffer oberste Ebene, Zuschnitt verschiebt Ebenen und Striche, Filter-Grenzen, Farbmatrix Invertieren
- Bildbearbeitung Profi (Profi 24): Zauberstab wählt die Farbfläche, Maske daraus, Lasso umfährt, inhaltsbasiert füllen entfernt Fleck, Kurvenpunkte frei ziehen
- Bildbearbeitung UI: Bild als Ebene, Pinsel malt (Pixel), Rechteck aufziehen, Text setzen, Ebene verschieben, Filter Schwarzweiß, Zuschneiden, Rückgängig, Export PNG
- Bildbearbeitung wie Krita/Inkscape: Vektorpinsel ergibt glatten Pfad, Mischmodus je Ebene, Pinsel-Deckkraft, SVG speichern und wieder öffnen
- Bildbearbeitung wie Photoshop/GIMP: Füllen mit Toleranz, Reparieren entfernt Fleck, Klonen mit Quelle, Verlauf ziehen, Maske aus Auswahl, Tonwerte, Kurve, Auto-Tonwerte
- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt
- Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü – Menüeinträge lösen dieselben Befehle aus
- T-0710: schmales Fenster – hintere Menüs stehen unter „…“, die Kopfzeile bleibt eine Zeile; Alt+B öffnet „Bearbeiten“ trotzdem und Enter macht rückgängig
- T-0733 Bild: TIFF (echtes FFmpeg) als Ebene öffnen, malen, als WebP exportieren – ffprobe liest webp 400×200, WebP wieder als Ebene
- T-0733 Gegenprüfung: Zoom-Werkzeug in der Leiste – „Einfach“ mit Symbol und Wort, Hinweis mit Taste Z, gewählt = goldener Rand (nicht golden gefüllt, kein Blau)
- T-0733 Tasten: Bildbearbeitung V B E M T Z im Menü „Werkzeuge“ und im Tooltip, ohne Konflikt; Z + Klick zoomt an der Stelle, Alt+Klick zurück; Künstlerstudio M = Auswahl
- T-0764 H1 (Kern): „Einstellungen einlesen“ übernimmt nie Rolle, Name, Bild, Projektordner, Sperren – für jede Rolle; Bedienfelder schon
