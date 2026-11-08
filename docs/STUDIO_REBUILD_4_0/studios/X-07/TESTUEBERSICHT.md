# Testübersicht: Freie Bilder & Klänge finden

Studio X-07 · Freie Bilder & Klänge finden · Bereich Kreativ & Medien

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 83. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

- Freie Bilder & Klänge Profi: ähnliche Bilder, Merken in neue Liste, Merklisten-Reiter mit Namensnennungen, Schriftvorschau mit eigenem Text; Claude sucht, prüft Lizenzen, merkt und schreibt die Namensnennung
- Medienbrowser wie Openverse/Unsplash/Google Fonts: beide Quellen gemischt, Ausrichtung filtern, Namensnennung als HTML, Bild in der Bildbearbeitung öffnen, Schriften suchen und laden
- Medienbrowser: Bilder suchen, Raster mit Vorschaubild, Vorschau mit Lizenz und Urheber, nur freie Lizenzen, Laden mit Herkunft, Namensnennung kopieren; Klänge vorhören; Bibliothek
- Medienbrowser: Testmodus ohne nachgebildetes Netz sucht nicht im Internet
- Symbole-Reiter: Sätze laden (nur freie sichtbar), suchen, Klick speichert SVG; CC BY mit Namensnennung in Lizenzen.txt; versteckte Symbole fehlen
- X-07 (UI): Suche und Laden mit Lizenzdatei (Netz simuliert)
- X-07: Echter Download eines HDRI von Poly Haven mit Prüfsumme (übersprungen ohne Internet)
- X-07: Mischen abwechselnd ohne doppelte Dateien; Ausrichtung quer/hoch/quadrat (±8 %) und Mindestbreite
- X-07: Namensnennung als Text, HTML (maskiert, mit Links) und Markdown
- X-07: Openverse: Lizenzname, Dauer in Sekunden, HTML-Zeichen, ohne „ND/NC“ und fremde Formate; Filter frei/Nennung; Suchadresse nur erlaubte Lizenzen
- X-07: Pfade aus dem Katalog können nicht aus dem Ordner ausbrechen, Prüfsumme wird geprüft
- X-07: Poly-Haven-Katalog suchen (Umlaute/Groß egal), Download-Plan mit Texturen
- X-07: Schriften: nur offene Lizenzen und Latin, Art-Filter, Wortanfang zuerst, Datei-Adresse mit nächster Stärke

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
