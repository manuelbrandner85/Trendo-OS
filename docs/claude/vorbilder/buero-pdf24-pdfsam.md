# Vorbild-Analyse PDF-Werkzeuge: PDF24, PDFsam, Stirling-PDF, Acrobat (Clean-Room)

- Programme: „PDF-Werkstatt“
- Vorbilder: PDF24 Creator (Freeware), PDFsam Basic (AGPL-3 – nur Vorbild), Stirling-PDF (Kern MIT – nur Ideen, kein Code), Adobe Acrobat Werkzeuge (proprietär)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Baut auf dem PDF-Kern von „Trendo PDF“ auf. Anforderungen mit Prüfungen: `docs/nachbau/pdf-werkstatt/anforderungen.md`. Stand 15.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Zusammenfügen in wählbarer Reihenfolge, Teilen nach Bereichen oder alle n Seiten
- ✓ Seiten ordnen mit Vorschaubildern: drehen, löschen, verschieben – auch über mehrere Dateien
- ✓ Bilder zu PDF (A4 eingepasst oder Bildgröße)
- ✓ Komprimieren in drei Stufen, Bilder neu kodiert (JPEG/Flate), Größe vorher/nachher gemessen
- ✓ Wasserzeichen, Stempel mit Datum, Seitenzahlen mit Format und Lage
- ✓ Metadaten setzen und vollständig entfernen
- ✓ Passwort setzen (AES-256) und mit bekanntem Passwort entfernen
- ✓ Stapel: ein Werkzeug auf alle Dateien der Liste
- ✓ Vergleichen (Text, mit Seitenangabe)
- ◐ Schwärzen: Inhalt wird wirklich entfernt, die betroffene Seite wird dabei aber zum Bild (nicht mehr durchsuchbar)
- ✗ PDF/A umwandeln, Office ↔ PDF in der Werkstatt (geht über „Trendo Text“), Texterkennung im Stapel (geht in „Trendo PDF“)
- ✗ Ziehen und Ablegen von Dateien aus dem Explorer

## Bedienkonzept
Wie PDF24: links die Werkzeugleiste (ein Werkzeug je Aufgabe), in der Mitte der Kasten „Werkzeug“ mit den Einstellungen und „Ausführen“, darunter Dateiliste (▲/▼/✕), Seitenübersicht mit Vorschaubildern bzw. Schwärz-Ansicht und die Ergebnisse mit „In Trendo PDF öffnen“ und „Ordner“. Menüband: Dateien, Seiten, Ergebnis. Rechts Angaben zur Datei (Seiten, Größe, Metadaten).

## Nächste Schritte
1. Schwärzen nur des Texts unter dem Balken (Inhaltsstrom bearbeiten), Seite bleibt Text
2. Dateien per Ziehen und Ablegen hinzufügen
3. PDF/A und Texterkennung als Stapel-Werkzeug (Kern von „Trendo PDF“ wiederverwenden)
