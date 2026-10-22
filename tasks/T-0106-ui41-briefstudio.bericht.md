# T-0106 Bericht: Briefe schreiben

**Auftrag:** Dreiteilige Briefseite nach Referenz 04. Studioname bleibt „Briefe schreiben“.
**Branch:** `cursor/bauplan-t0005-t0012-f36c`

Die Seite liegt auf `main` (PR #237). Bild: `reports/design-41/vergleich/04_BRIEFSTUDIO.jpg`. Navigation, Formular, DIN-Vorschau, Speichern, Exportieren, Als PDF, Senden. Diese Sitzung hat das Briefstudio nicht neu geschrieben.

## Geänderte Dateien

Keine an der Briefseite. Dieser Bericht.

## Testergebnis

App 222 grün, darin die Briefprüfungen der bestehenden Suite. Ein frischer DOCX- und PDF-Nachweis mit Datei-Hash aus diesem Lauf liegt nicht extra unter `reports/`.

## Offene Punkte

- `SYS-03-P14` (PDF, A4) kann auf diesem Image scheitern, sobald `google-chrome` im PATH liegt. Die App ist dafür unverändert. In dem genannten App-Lauf war die Suite grün (222/222).
- Ohne Tesseract fragt „Briefe schreiben“ aus dem Startmenü zuerst nach dem Zusatzprogramm.

## Nicht geprüft

- Kein Windows-PC, kein zweiter manueller Brief mit echten Kontakten, keine Codesignatur.
