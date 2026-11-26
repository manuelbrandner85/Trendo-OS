# PDF-Werkstatt – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0672). Kategorie: Arbeiten & Büro. Studio-Id `STUDIO-PDFTOOLS-01`, Programm `pdf-werkstatt-programm`.

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Grafiken)

| Vorbild | Warum | Lizenz / Zugang | Quelle |
|---|---|---|---|
| PDF24 Creator / PDF24 Tools | ein Werkzeug je Aufgabe, Dateiliste, „Ausführen“, Ergebnis als neue Datei; Komprimieren mit Stufen | Freeware, proprietär → nur Verhalten | Werkzeugseiten auf tools.pdf24.org (öffentlich) |
| PDFsam Basic | Teilen nach Bereichen/alle n Seiten, Zusammenfügen mit Reihenfolge, Drehen | AGPL-3 → nur Vorbild, kein Code | pdfsam.org Handbuch |
| Stirling-PDF | Werkzeugsammlung im Browser (Schwärzen, Vergleichen, Metadaten, Seitenzahlen, Wasserzeichen) | Kern MIT laut Repository; **kein Code übernommen**, nur Ideen | github.com/Stirling-Tools/Stirling-PDF (README, Funktionsliste) |
| Adobe Acrobat (Werkzeuge) | Schwärzen entfernt Inhalt wirklich, Vergleichen, Stempel, Schutz | proprietär | helpx.adobe.com Acrobat-Hilfe (öffentlich) |

Hinweis: Die Vorbilder wurden aus öffentlicher Doku und Bedienkonzept abgeleitet (Stand 15.11.2026), ohne Live-Analyse mit REA.

Wiederverwendet (kein zweites PDF-System): `PdfTools` aus „Trendo PDF“ (Merge, Split, Rearrange, Rotate, Encrypt/Decrypt, ParsePages), `PdfView` (PDFium: Seitenbilder, Text), `PngCodec`, `Writing.Diff`, `FriendlyError`, `ProgramDocument`. Neu in `src/Trendo.Core/Sys/PdfWerkstatt.cs`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| PW-01 | Zusammenfügen, Teilen | Liste in Reihenfolge zusammenfügen (▲/▼); teilen nach Bereichen „1-3; 4-z“ oder alle n Seiten; freie Namen, nie überschreiben | Core `PdfWerkstattTests.Merge_and_split_ranges`, UI `Merge_by_clicks`, `Split_and_images` | PDFsam „Split by page ranges“ | 1 |
| PW-02 | Seiten ordnen | Vorschaubilder; anklicken (Strg/Umschalt), drehen, löschen, nach vorn/hinten, über mehrere Dateien | Core `Organize`, UI `Organize_by_clicks` | PDF24 „Seiten sortieren“ | 1 |
| PW-03 | Bilder zu PDF | PNG/JPG je eine Seite, A4 eingepasst (Quer-/Hochformat nach Bild) oder Seite in Bildgröße | Core `Images_to_pdf`, UI `Split_and_images` | PDF24 „Bilder zu PDF“ | 1 |
| PW-04 | Komprimieren | Bilder auf höchstens dpi der Stufe verkleinern, Flate oder JPEG (SkiaSharp), nur ersetzen wenn kleiner; Bytes vorher/nachher gemessen; nicht kleiner → Kopie + ehrlicher Hinweis | Core `Compress_measured`, UI `Compress_by_clicks` (JPEG im Ergebnis, < 1/3 der Größe) | PDF24 „Komprimieren“ | 1 |
| PW-05 | Wasserzeichen, Stempel | Text schräg, halbdurchsichtig, wählbare Seiten; Stempel im Rahmen mit Datum in einer Ecke | Core `Watermark_and_stamp`, UI `Batch_tools` | Acrobat „Wasserzeichen“, „Stempel“ | 1 |
| PW-06 | Seitenzahlen | Format „Seite {n} von {N}“, Lage, Startzahl, Deckblatt auslassen | Core `Page_numbers`, UI `Batch_tools` (Text „Seite 2 von 2“) | PDF24 „Seitenzahlen“ | 1 |
| PW-07 | Metadaten | Titel, Verfasser, Thema, Stichwörter setzen; alles entfernen inkl. „erzeugt von“, XMP-Verweis und Kommentar der Bibliothek | Core `Metadata` | Stirling-PDF „Change Metadata“ | 1 |
| PW-08 | Passwort | setzen (AES-256) und nur mit bekanntem Passwort entfernen; Passwort nie im Dokument/Protokoll; falsches Passwort → verständliche Meldung | Core `Password`, UI `Batch_tools` | qpdf/Acrobat „Schützen“ | 1 |
| PW-09 | Schwärzen | Balken mit der Maus ziehen; Seite mit Balken wird zum Bild (150 dpi) mit eingebrannten Balken – Text darunter ist aus der Datei entfernt; andere Seiten bleiben Text | Core `Redact_removes_content` (keine Text-Operatoren mehr), UI `Redact_by_mouse` (PDFium findet den Text nicht mehr) | Acrobat „Schwärzen“ | 1 |
| PW-10 | Vergleichen | Text zweier PDFs Zeile für Zeile, Leerzeichen egal, Seitenangabe A/B, Ansicht + TXT | Core `Compare`, UI `Compare_by_clicks` | Acrobat „Dateien vergleichen“ | 1 |
| PW-11 | Stapel | Werkzeuge Teilen, Komprimieren, Wasserzeichen, Stempel, Seitenzahlen, Metadaten, Passwort laufen für jede Datei der Liste | UI `Batch_tools` (2 Dateien → 2 Ergebnisse) | PDF24 Stapelverarbeitung | 1 |
| PW-12 | Original bleibt | jede Aktion schreibt neue Dateien unter „Ergebnisse“ | UI `Merge_by_clicks` (SHA-256 gleich), `Redact_by_mouse` | alle Vorbilder | 1 |
| PW-13 | KI-Auftrag | Adapter `pdf.auftrag`: Seitenangaben werden aufgelöst, Schritte im Studio, keine Nebenwirkung | Core `PdfWerkstattTests.Adapter` | – | 1 |
| PW-14 | KI im offenen Programm (T-0774) | Auftragszeile → KI-Plan „pdf.seiten_heraus“ (Seiten wie „1-3“) oder „pdf.ausfuehren“ (Werkzeug) → Vorschau ohne Datei, falsche Seiten schon dort gemeldet → „Ja, behalten“ → neue PDF unter „Ergebnisse“, ein Rückgängig-Schritt, Rückgängig legt die PDF beiseite, Wiederholen holt sie zurück | UI `T0774KiDokument_Ui.Pdf_pages_out_into_open_program`, `Pdf_bad_pages_stop_in_preview`, Echtlauf `Real_local_ki` (`reports/T-0774/`) | Acrobat-KI-Assistent (nur Vorbild) | 1 |

## Grenzen (ehrlich)
- Schwärzen macht die betroffene Seite zum Bild: dort ist kein Text mehr auswählbar oder durchsuchbar (◐ gegenüber Acrobat, das nur den Text unter dem Balken entfernt).
- Komprimieren verändert nur Bilder mit 8 Bit RGB/Grau (Flate ohne Vorhersage-Filter, JPEG); CMYK-, 16-Bit- und Maskenbilder bleiben unverändert.
- Vergleich nur über den Text, nicht über das Aussehen.
