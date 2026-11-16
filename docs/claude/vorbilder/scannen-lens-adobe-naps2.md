# Vorbild-Analyse Scannen: Microsoft Lens, Adobe Scan, NAPS2 (Clean-Room)

- Programme: „Dokumente scannen“
- Vorbilder: Microsoft Lens, Adobe Scan (proprietär), NAPS2 (GPL-2 – nur Vorbild, kein Code)
- Werkzeug: Funktionsumfang aus Hilfe und Fachvergleichen (adobe.com/devnet-docs/adobescan, pdfgear.com Vergleich); eigener Kern `src/Trendo.Core/Office/DocScan.cs`, Entzerren mit der vorhandenen Homographie. Anforderungen: `docs/nachbau/scannen/anforderungen.md`. Stand 15.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Blattkanten automatisch finden, Ecken von Hand ziehen
- ✓ Perspektive entzerren, DIN-A-Verhältnis einrasten
- ✓ Modi Dokument (adaptiver Schwellwert), Graustufen, Foto, Whiteboard, Original – je Seite oder für alle
- ✓ Drehen, Zuschneiden, Reihenfolge, Seite entfernen; Fotos einzeln oder ganzen Ordner einlesen (EXIF-Drehung beachtet)
- ✓ Mehrseitige PDF (A4 oder wie das Bild), leere Seiten weglassen
- ✓ Ablage: „JJJJ-MM-TT Titel.pdf“, Ordner nach Art, nie überschreiben; Öffnen in „Trendo PDF“
- ◐ Texterkennung: durchsuchbare PDF nur, wenn Tesseract eingerichtet ist
- ◐ Scanner (WIA/TWAIN) und Kamera nicht angeschlossen – Hinweis im Programm
- ✗ Wölbung von Buchseiten ausgleichen, Visitenkarten erkennen

## Bedienkonzept
Wie NAPS2 auf dem Desktop mit dem Zuschneiden von Lens/Adobe Scan: links die Seitenleiste mit Vorschau, in der Mitte das Original mit vier ziehbaren Ecken neben dem fertigen Ergebnis, links in der Werkzeugleiste der Modus der Seite. Rechts Modus für alle, Zuschnitt, Ablage (Titel, Datum, Ordner, Seitenformat, leere Seiten) und der Stand der Texterkennung. Menüband: Seiten, Ecken, Ablegen.

## Nächste Schritte
1. Scanner über WIA (Windows) mit Seitenzufuhr und Duplex
2. Kamera-Aufnahme mit Live-Kantenerkennung
3. Wölbungskorrektur für Buchseiten
