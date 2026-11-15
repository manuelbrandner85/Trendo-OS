# Vorbild-Analyse Malen: Krita, Procreate, Clip Studio Paint, Rebelle (Clean-Room)

- Programme: „Künstlerstudio“
- Vorbilder: Krita (frei, GPL-3 – nur Vorbild), Procreate, Clip Studio Paint, Rebelle (kostenpflichtig; Funktionsumfang aus Handbuch, Produktseiten und Fachvergleichen)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Pinsel, Grafiken oder Texte. Dateiformat ORA nach der offenen OpenRaster-Spezifikation. Anforderungen mit Prüfungen: `docs/nachbau/kuenstlerstudio/anforderungen.md`. Stand 15.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Pinsel-Engine aus Tupfern mit Härte, Abstand, Fluss, Körnung; Strich wird indirekt gesammelt (Marker baut sich nicht auf, Airbrush/Aquarell schon)
- ✓ Stiftdruck steuert Größe und/oder Deckkraft (Grafiktablett); Maus zählt als voller Druck
- ✓ Pinselsatz: Bleistift, Tusche, Malpinsel, Airbrush, Marker, Aquarell, Kreide, Radierer
- ✓ Glätten der Eingabe (wie StreamLine), Stärke 0–100
- ✓ Ebenen: anlegen, kopieren, verschieben, verbinden, leeren, löschen, ausblenden, sperren, Deckkraft sperren, Deckkraft, Umbenennen
- ✓ Mischarten: Normal, Multiplizieren, Negativ multiplizieren, Überlagern, Abdunkeln, Aufhellen, Addieren, Differenz, Farbig abwedeln, Weiches Licht
- ✓ Farbrad (Farbton-Ring, Sättigung/Helligkeit), Hex-Eingabe, zuletzt benutzte Farben, Pipette
- ✓ Paletten im GPL-Format laden/speichern, Farben aus einem Foto (k-means), Harmonien (Komplementär, Analog, Triade, Teil-Komplementär, Quadrat)
- ✓ Spiegeln senkrecht, waagrecht, beide, radial (Mandala, 2–32 Teile)
- ✓ Zeichenhilfen: gerade Linie, Lineal, Perspektive mit 1, 2 oder 3 Fluchtpunkten (Striche rasten ein), Raster
- ✓ Referenzbild neben der Arbeit mit Farbaufnahme; Posenpuppe (Gliederpuppe, 8 Posen, drehbar) als gesperrte Skizzenebene
- ✓ Ideenskizze mit lokaler Bild-KI – nur als Referenz, nie als Ebene
- ✓ Zeitraffer: jeder Strich ein Bild, Video per FFmpeg, Übergabe an den Videoschnitt
- ✓ Skizze säubern (Linien auf neue Ebene, Krümel weg, Skizze bleibt) und Vorfärben geschlossener Flächen
- ✓ Export PNG (mit/ohne Hintergrund), ORA und PSD mit Ebenen, Druckdateien A4/A3/Postkarte mit 300 dpi und Beschnitt, Portfolio-Webseite, Übergabe an Print on Demand
- ✓ Geführte Malkurse (5 Kurse), jeder Schritt prüft das Bild
- ◐ Zoomen (Strg+Mausrad) und Verschieben (mittlere Taste/Leertaste); Drehen der Leinwand fehlt
- ✗ Eigene Pinsel bauen (Pinselspitze aus Bild, Struktur), Natur-Farbmischung wie Rebelle (Nass-in-Nass)
- ✗ Auswahlwerkzeuge und Transformieren innerhalb der Ebene, Füllwerkzeug

## Bedienkonzept (Profi-Bauplan A1–A3)
Bauplan erfüllt: Pinsel links als Werkzeugleiste, Optionsleiste über der Fläche (Größe, Deckkraft, Glätten, letzte Farben), große Leinwand, rechts Farbe, Palette, Ebenen, Zeichenhilfen, Referenz, Zeitraffer und Malkurs. Menüband oben: Ebene, Bild, Zeitraffer, Teilen, Ansicht.

## Nächste Schritte
1. Leinwand drehen und spiegeln (Ansicht), Füllwerkzeug mit Lückenschluss
2. Auswahl und Transformieren (Verschieben, Skalieren) auf der Ebene
3. Eigene Pinselspitzen und Nass-in-Nass-Farbmischung
