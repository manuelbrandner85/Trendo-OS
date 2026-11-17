# Dokumente scannen – Anforderungen (Clean-Room)

Studio `STUDIO-DOCSCAN-01`, Seite `scannen-programm`, Kategorie Arbeiten & Büro. Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0673).

## Vorbilder (nur Verhalten und Bedienung, kein Code)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Microsoft Lens | Modi Dokument, Whiteboard, Visitenkarte, Foto; gute Kantenerkennung und Aufbereitung | proprietär (App wird eingestellt) | pdfgear.com/scan-pdf/microsoft-lens-alternatives.htm |
| Adobe Scan | Live-Kantenerkennung mit Eckpunkten, Ecken ziehen, Seiten ordnen/drehen, Whiteboard ohne Rand, Texterkennung | proprietär | adobe.com/devnet-docs/adobescan/android/en/scan.html |
| NAPS2 | Desktop-Scannen: Seiten ordnen, drehen, zuschneiden, leere Seiten weglassen, PDF mit OCR | GPL-2 → nur Vorbild, kein Code | naps2.com (Funktionsliste) |

## Vorhandenes genutzt
- Entzerren: `VisionPlus.Homography` (`src/Trendo.Core/W6/Robotics2.cs`).
- Bilder: `Pixmap`/`PngCodec` des Künstlerstudios, Fotos lesen mit SkiaSharp (EXIF-Drehung).
- PDF: PdfSharp (schon im Kern); durchsuchbare PDF: `PdfTools.OcrSearchableAsync` + `Ocr` (Tesseract, wie in „Trendo PDF“ und „Briefe“).
- Weiterbearbeiten: `OpenPdf` öffnet die Ablage in „Trendo PDF“.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| SC-01 | Blatt finden | hell/dunkel nach Otsu, größte Fläche, die nicht der Rand ist, Ecken aus Extrempunkten; ohne Blatt das ganze Bild | Core `DocScanTests.Find_page` (Testbild selbst erzeugt) | Adobe Scan (Eckpunkte) | 1 |
| SC-02 | Ecken ordnen, Zielgröße | Reihenfolge egal; Seitenverhältnis nahe 1 : 1,414 wird genau DIN A | Core `Order_and_size` | Lens | 1 |
| SC-03 | Entzerren | Perspektive mit Homographie, bilinear; Tisch verschwindet, Zeilen waagrecht | Core `Warp_straightens` | Lens/Adobe Scan | 1 |
| SC-04 | Modus Dokument | adaptiver Schwellwert (Mittel im Fenster), Schatten weg, nur Schwarz/Weiß | Core `Document_mode` | Lens „Dokument“ | 1 |
| SC-05 | Modi Whiteboard, Graustufen, Foto, Original | Tafelhintergrund herausrechnen, Farben kräftig; Graustufen gestreckt; Foto je Kanal | Core `Other_modes` | Lens „Whiteboard“, Adobe Scan Filter | 1 |
| SC-06 | Drehen, Zuschneiden, Reihenfolge | 90°-Schritte, Rand in %, Seiten hoch/runter, entfernen | Core `Rotate_crop`, UI `Pages_rotate_order` | NAPS2 | 1 |
| SC-07 | Mehrseitige PDF | A4 (Hoch/Quer nach Bild) oder „wie das Bild“ | Core `Pdf`, UI `Add_and_save_pdf` | NAPS2, Adobe Scan | 1 |
| SC-08 | Ablage | Name „JJJJ-MM-TT Titel.pdf“, verbotene Zeichen raus, nie überschreiben, Ordner nach Art (Rechnungen, Briefe …); leere Seiten weglassen | Core `Naming_and_blank`, UI `Add_and_save_pdf` | Lens (Speichern), NAPS2 (leere Seiten) | 1 |
| SC-09 | Ecken von Hand | im Original ziehen; Ecken zu dicht → Meldung | UI `Corners_by_hand` | Adobe Scan | 1 |
| SC-10 | Ordner einlesen | alle Bilder, natürlich sortiert („2“ vor „10“) | UI `Folder_and_ocr_hint` | NAPS2 (Import) | 1 |
| SC-11 | Texterkennung | ◐ nur mit eingerichtetem Tesseract: durchsuchbare PDF; ohne Tesseract klarer Einrichtungs-Hinweis, nichts wird geladen | UI `Folder_and_ocr_hint` | Adobe Scan OCR | 2 |
| SC-12 | Scanner WIA/TWAIN | ◐ nicht gebaut – Hinweis „Scanner …“ | UI `Folder_and_ocr_hint` | NAPS2 | 3 |
| SC-13 | Kamera | ◐ nicht gebaut – Trendo hat keinen Foto-Aufnahmeweg; Handyfotos hinzufügen | – | Lens | 3 |
| SC-14 | KI-Adapter | Titel/Datum/Modus → Scan-Auftrag mit Dateiname und Ordner, ohne Nebenwirkung | Core `Adapter` | Trendo-Kette | 1 |

## Bewusst nicht gebaut
- Kein Hochladen in Cloud-Dienste, kein Versand per E-Mail.
- Keine Wölbungskorrektur von Buchseiten (Adobe Scan „curvature“), keine Visitenkarten-Erkennung.
