# Bericht T-0223: Trendo Office – Teil 1: Trendo PDF (P1–P6)

**Auftrag:** „Ja, Stufe 1 komplett frei (T1–T10, C1–C8, P1–P6) inklusive PDFtoImage und PDFsharp. Fang mit Trendo PDF an.“ (Manuel, 2026-10-25)
**Branch/Worktree:** `claude/T-02xx`, Worktree `integration`
**Vorher:** REA-Analyse und Auswahl in `reports/t0223-office/funktionen.md` (Commit f2170f4).

## Umgesetzt (Trendo PDF)
| Nr. | Funktion | Wo |
|---|---|---|
| P1 | Ansehen: erste/vorige/nächste/letzte Seite, Gehe zu Seite, ganze Seite, Breite, 100 %, ± Zoom, Doppelseite, Ansicht drehen, Vollbild | `src/Trendo.App/MainWindow.Pdf.cs`, `PdfView.cs` |
| P2 | Suchen im ganzen Dokument (Treffer mit Ausschnitt), Lesezeichen mit Ebenen, Text der Seite kopieren | `PdfView.cs` (PDFium direkt) |
| P3 | Zusammenfügen, Seiten auswählen (Angabe wie qpdf: `1-3,5,8-z`, `z`, `r1`, rückwärts), Teilen (n Seiten je Datei), Drehen (90/180/270, auch nur bestimmte Seiten), neue leere PDF | `src/Trendo.Core/Sys/PdfTools.cs` |
| P4 | Passwort setzen (AES-256, R = 6), mit Passwort öffnen, Passwortschutz entfernen, Infos (Seiten, geschützt, Titel) | `PdfTools.cs`, Passwortabfrage in der Oberfläche |
| P5 | Markieren, Unterstreichen, Durchstreichen (Ziehen auf der Seite), Notiz als Freitext (Klicken) | `PdfTools.cs`, `MainWindow.Pdf.cs` |
| P6 | Formularfelder erkennen (Text, Ankreuzfeld, Auswahl), ausfüllen, als neue Datei speichern, unbekannte Felder melden | `PdfTools.cs` |

**Grundregeln:**
- Die geöffnete Datei wird nie überschrieben. Anmerkungen landen in „Name (Trendo).pdf“, die Werkzeuge schreiben neue Dateien daneben, z. B. „-zusammen“, „-seiten“, „-teile“, „-gedreht“, „-geschuetzt“, „-offen“ und „-ausgefuellt“.
- Bricht eine Bearbeitung ab, bleibt keine halbe Datei liegen.
- Passwörter kommen nie ins Protokoll.
- Erreichbar über „Trendo PDF“ im Studio „Texte & Tabellen“ (SYS-10) und über die Suche.

## Bibliotheken
- **PDFsharp 6.2.4 (MIT):** die letzte MIT-Fassung, ab 6.5.0 gilt AGPL-3.0. Das hätte Trendo selbst unter AGPL gezwungen.
- **PDFtoImage 5.2.1 (MIT) mit PDFium (bblanchon, Apache-2.0):** Ab Version 5.3 braucht PDFtoImage SkiaSharp 4, Avalonia 12 nutzt SkiaSharp 3. Suchen und Lesezeichen laufen direkt über PDFium, ohne dritte Bibliothek.
- **System.Security.Cryptography.Pkcs:** PDFsharp brachte 8.0.1 mit bekannten Sicherheitslücken mit (NU1902/NU1903). Die Version ist direkt auf 10.0.14 angehoben, damit der Build ohne Warnungen bleibt.
- `THIRD_PARTY_NUGET.md` ist neu erzeugt. Die Komponenten „pdfsharp“ und „pdftoimage“ stehen im Katalog als „Im Setup enthalten“.

## Abgleich mit dem Vorbild qpdf 12.4.2
`reports/t0223-office/qpdf-abgleich.log`: 12 von Trendo PDF erzeugte Dateien, geprüft mit qpdf aus dem Vollpaket.
- **Seitenzahlen:** stimmen alle.
- **Verschlüsselung:** „R = 6“ (AES-256) bzw. „not encrypted“ nach dem Entfernen.
- **`--check`:** 11 von 12 Dateien ohne Befund.
- **Ausnahme `ausgefuellt.pdf`:** qpdf warnt „input stream is complete but output may still be valid“. Ursache ist die von PDFsharp eingebettete Schrift: Die Daten sind vollständig (46 392 von 46 392 Bytes), es fehlt nur das Ende-Zeichen des Datenstroms. Das ist ein Fehler in PDFsharp und bleibt als bekannte Einschränkung offen.
- **Behobener Fehler:** Beim ersten Abgleich fehlte leeren Seiten das Pflichtfeld `/Resources`. Trendo ergänzt es jetzt vor jedem Speichern.

## Tests
- **Core `T0223_TrendoPdf`:** 6 Tests – Seitenangaben, Zusammenfügen/Auswählen/Teilen/Drehen mit Seitenreihenfolge, Passwort, Anmerkungen, Formular, leere PDF.
- **App `T0223_TrendoPdfUi`:** 2 Tests.
  - Öffnen, Blättern, Gehe zu, Suchen, Lesezeichen, Doppelseite. Gezeichneter Text auf der Seite ist nachgewiesen.
  - Markieren in der Kopie (Original byte-gleich), Seiten auswählen, Passwort setzen und mit Passwort öffnen.
- Volltest: siehe unten.

## Belege
`reports/t0223-office/`: `qpdf-abgleich.log`, `trendo-pdf-1672x941.png` (Render mit einem echten Brief), REA-Belege und `SHA256SUMS`.

## Nicht geprüft / offen
- Kein echtes Fenster am Bildschirm, nur headless gerendert und getestet. Das installierte Setup hat Trendo PDF noch nicht.
- Sehr große PDFs: Jede Seite wird beim Anzeigen neu gezeichnet, noch ohne Zwischenspeicher.
- Markieren folgt dem gezogenen Rechteck, nicht der genauen Textzeile.
- PDF-Dateien öffnen sich im Explorer noch nicht automatisch in Trendo PDF.
- Weiter mit Trendo Text (T1–T10) und Trendo Tabelle (C1–C8).

## Testergebnis
`dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler.

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 685 | 1 | 0 | 686 |
| Trendo.App.Tests | 331 | 2 | 0 | 333 (28 min 45 s, Exitcode 0) |
| Pakettests | 8 | – | 0 | 8 |

**Vor dem grünen Lauf behoben:**
- Die Seite „pdf“ war keinem Modul zugeordnet, jetzt gehört sie zu SYS-10.
- Das Suchwort „notiz“ hat im Startmenü den Treffer „Werkzeuge“ verdrängt, es ist entfernt.
- Die PDF-Seite hatte keine Seitenüberschrift im Trendo-Muster, jetzt hat sie eine.
