# Bildbearbeitung – Testergebnisse

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `bild` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 21 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Bild-Dokument: Ebenen-Reihenfolge, Treffer oberste Ebene, Zuschnitt verschiebt Ebenen und Striche, Filter-Grenzen, Farbmatrix Invertieren | ✓ bestanden |
| Bildbearbeitung Profi (Profi 24): Zauberstab wählt die Farbfläche, Maske daraus, Lasso umfährt, inhaltsbasiert füllen entfernt Fleck, Kurvenpunkte frei ziehen | ✓ bestanden |
| Bildbearbeitung UI: Bild als Ebene, Pinsel malt (Pixel), Rechteck aufziehen, Text setzen, Ebene verschieben, Filter Schwarzweiß, Zuschneiden, Rückgängig, Export PNG | ✓ bestanden |
| Bildbearbeitung wie Krita/Inkscape: Vektorpinsel ergibt glatten Pfad, Mischmodus je Ebene, Pinsel-Deckkraft, SVG speichern und wieder öffnen | ✓ bestanden |
| Bildbearbeitung wie Photoshop/GIMP: Füllen mit Toleranz, Reparieren entfernt Fleck, Klonen mit Quelle, Verlauf ziehen, Maske aus Auswahl, Tonwerte, Kurve, Auto-Tonwerte | ✓ bestanden |
| CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei | ✓ bestanden |
| CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen | ✓ bestanden |
| CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt | ✓ bestanden |
| CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich | ✓ bestanden |
| CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben | ✓ bestanden |
| CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell | ✓ bestanden |
| Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt | ✓ bestanden |
| Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü – Menüeinträge lösen dieselben Befehle aus | ✓ bestanden |
| T-0710: schmales Fenster – hintere Menüs stehen unter „…“, die Kopfzeile bleibt eine Zeile; Alt+B öffnet „Bearbeiten“ trotzdem und Enter macht rückgängig | ✓ bestanden |
| T-0733 Bild: TIFF (echtes FFmpeg) als Ebene öffnen, malen, als WebP exportieren – ffprobe liest webp 400×200, WebP wieder als Ebene | ✓ bestanden |
| T-0733 Gegenprüfung: Zoom-Werkzeug in der Leiste – „Einfach“ mit Symbol und Wort, Hinweis mit Taste Z, gewählt = goldener Rand (nicht golden gefüllt, kein Blau) | ✓ bestanden |
| T-0733 Tasten: Bildbearbeitung V B E M T Z im Menü „Werkzeuge“ und im Tooltip, ohne Konflikt; Z + Klick zoomt an der Stelle, Alt+Klick zurück; Künstlerstudio M = Auswahl | ✓ bestanden |
| T-0764 H1 (Kern): „Einstellungen einlesen“ übernimmt nie Rolle, Name, Bild, Projektordner, Sperren – für jede Rolle; Bedienfelder schon | ✓ bestanden |
| T-0779 2. Gegenprüfung: Bilder im Kurs am Inhalt geprüft (PNG/JPG/GIF/WEBP-Kennung, SVG mit Wurzel svg) – falsche Inhalte sind Fehler und werden nicht eingebettet | ✓ bestanden |
| T-0779 Kurs: SCORM-1.2-Paket – Manifest mit Namensräumen, ein SCO auf index.html, Bestehensgrenze; Seite spricht die SCORM-API an; keine lokalen Pfade, Bilder eingebettet | ✓ bestanden |
| T-0779 M2: Kurs bettet nur Bilder (PNG/JPG/GIF/WEBP/SVG, höchstens 15 MB) ein – andere Dateien sind ein Fehler in der Prüfung und fehlen im Export | ✓ bestanden |
