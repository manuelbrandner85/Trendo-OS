# Bericht T-0257 – Grundriss-Ausbau nach Vorbild-Analyse

## Vorbild-Analyse
- Sweet Home 3D 7.5 (GPL) als offizielle JAR geladen (Zustimmung Manuel), mit REA statisch untersucht: Inventar (4.087 Dateien), 47 Bedienzustände des Plan-Editors, Menübefehle. Ergebnis: `docs/claude/vorbilder/grundriss-sweethome3d.md`. Kein Code und keine Grafik übernommen. Das Programm wurde nicht ausgeführt.

## Umgesetzt
- KI „Raum anlegen“ schreibt zusätzlich einen echten Grundriss (.tdoc), sichtbar im Programm unter „Zuletzt“ (`MeasurePromptAdapter.cs`)
- Geschosse (Reiter, Obergeschoss/Keller, Höhenlage, Name, löschen, Geschoss darunter blass, 3D aller Geschosse, Wohnfläche gesamt)
- Dachschräge je Raum (ein-/beidseitig, Kniestock, Tiefe) mit WoFlV-Rechnung
- Vorlage-Bild (PNG/JPG/BMP, Größe aus dem Dateikopf), Maßstab über zwei Klicks + echte Länge, Deckkraft, Lage, ein-/ausblenden
- Werkzeuge Bemaßen (3 Klicks) und Text; Raum Punkt für Punkt (Doppelklick/Enter); Wand teilen; Möbel spiegeln; Winkelfang 15° (Umschalt frei)
- Export 3D (OBJ + MTL, alle Geschosse), PDF eine Seite je Geschoss, Raumbuch mit Geschoss und Wohnfläche je Raum
- Layout: Werkzeuge zweispaltig, Zeichenfläche schrumpft statt zu überlaufen, Raumliste oben in den Eigenschaften

## Tests (gezielt)
- Core: T0256 + T0257 + PromptAction: 96/96 bestanden
- App SPEC01_Ui: 6/6 bestanden (u. a. echter Maus-Doppelklick erkennt Raum, Vieleck mit Enter, Bemaßen mit drei Klicks, Geschoss anlegen/wechseln, Vorlage-Bild laden und kalibrieren, OBJ-Export)
- Volltest: siehe `reports/volltest-0.9.25/`

## Nicht geprüft / offen
- Bedienung am echten PC (nur headless). Bilder werden im Test nicht wirklich gezeichnet (Headless-Grafik), die Bildgröße kommt aus dem Dateikopf.
- Dachschräge wirkt nur auf die Wohnfläche, nicht auf 3D und Wandflächen.
- Wandbögen, Polylinien, Kompass, virtueller Besuch, Foto/Video, Texturen aus dem Vorbild: noch nicht.
