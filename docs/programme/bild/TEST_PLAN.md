# Bildbearbeitung – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `bild` · Stand: 0.9.36 (Stand 2963379)

22 Tests sprechen dieses Programm an.

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
- Dateimanager-Verhalten je System: Ziehen verschiebt (Strg kopiert), Kopiename je System, Finder-Zeilenschalter benennt um, Entf/⌘⌫ legt in den Papierkorb, Leertaste zeigt Vorschau
- Mehrfachauswahl wie im System: Umschalt-Klick Bereich, Strg-Klick dazu/weg, Rahmen mit der Maus aufziehen, Alles auswählen, mehrere kopieren/einfügen und in den Papierkorb, Klick ins Leere hebt auf
- Profi-Rahmen: Bildbearbeitung mit Werkzeugleiste links (aktives Werkzeug markiert), Werkzeuge nicht mehr im Menüband; Werkzeuge und Eigenschaften ein-/ausklappbar und gemerkt
- Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü ☰ – Menüeinträge lösen dieselben Befehle aus
- Registerkarten wie Explorer/Finder/Dateien: Strg/⌘+T neu, Mittelklick-Ordner in neuer Karte, Wechsel per Klick und Strg+Bild↓, eigener Verlauf je Karte, Strg/⌘+W schließt erst die Karte, dann das Fenster
- Stil Trendo bleibt unverändert: Trendo-Kopf, Seitenspalten, Dock – keine Fremd-Leisten
- Studio-Fenster wie im System: Mac und GNOME – KI-Zeile ist die Titelleiste (Ampel bzw. Schließen, Titel, Zurück), Windows behält die Titelleiste; Schließen in der Kopfleiste schließt das Fenster
- Wie GNOME: schwarze Leiste mit Aktivitäten und Uhr in der Mitte, Übersicht mit Suche und Dash, nur Schließen-Knopf im Fenster, Dateien mit „Andere Orte“
- Wie Windows 11: Taskleiste mit Start/Suche/Uhr, Startmenü mit Angeheftet und Suche (Trendo KI), Widgets mit allen Trendo-Karten, eckige Fensterknöpfe, Datei-Explorer mit „Dieser PC“
- Wie im Original: eine Kopfleiste in GNOME-Dateien/Finder, Uhr öffnet Mitteilungen + Monatskalender, Schnelleinstellungen mit Kacheln, „Nicht stören“, GNOME-Suche per Lupe/Lostippen, „Andere Orte“, Explorer springt beim Tippen
- Wie macOS: Menüleiste mit Menüs und Uhr, Dock mit Launchpad und Papierkorb, Spotlight, Ampel-Knöpfe, ⌘W schließt, Finder mit Seitenleiste und Symbolansicht
