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

---

# Teil 2: Trendo Text (T1–T10)

**Auftrag:** „Trendo text“ (Manuel, 2026-10-26), Stufe 1 freigegeben am 2026-10-25.

## Umgesetzt
| Nr. | Funktion | Wo |
|---|---|---|
| T1 | Neu, Öffnen, Speichern, Speichern unter (.docx, .odt, .txt), zuletzt geöffnete Dateien | `src/Trendo.Core/Office/DocxFormat.cs`, `OdtFormat.cs`, `TextFiles` |
| T2 | Rückgängig/Wiederholen (80 Schritte), Ausschneiden, Kopieren, Einfügen ohne Format, Absatz markieren | `MainWindow.Text.cs` |
| T3 | Fett, Kursiv, Unterstrichen (Knöpfe und Strg+B/I/U), Durchgestrichen, Hoch-/Tiefgestellt (schließen sich aus), Größe A+/A− in Writer-Schritten, GROSS/klein | `TextDoc.Format/Toggle/Grow/ChangeCase` |
| T4 | Absatzarten Titel, Untertitel, Überschrift 1–3, Text, Zitat; Aufzählung, Nummerierung; links, zentriert, rechts, Blocksatz | `TextDoc`, echte Formatvorlagen in DOCX/ODT |
| T5 | Tabelle einfügen (Zeilen × Spalten), Zellen direkt bearbeiten, Zeile/Spalte anfügen und entfernen | `Table`, `TableControl` |
| T6 | Bild einfügen (PNG/JPG/GIF), kleiner/größer, Seitenumbruch | `Picture`, `PageBreak` |
| T7 | Suchen & Ersetzen (auch in Tabellen, Groß/klein, ganze Wörter; Ersetzen behält das Format), Wörter/Zeichen/Absätze zählen (immer in der Statuszeile) | `TextDoc.Find/ReplaceAll/Count` |
| T8 | Rechtschreibung Deutsch offline: rote Unterstreichung, Vorschläge, „Ins Wörterbuch“ | `Spelling.cs` |
| T9 | Als PDF: eigener A4-Satz mit Umbruch, Formaten, Listen, Blocksatz, Tabellen, Bildern, Seitenzahlen und Lesezeichen für Überschriften. Drucken über die PDF (Druckdialog von SumatraPDF aus dem Vollpaket) | `TextPdf.cs` |
| T10 | Vorlagen: Lebenslauf, Rechnung, Protokoll (mit Platzhaltern in [Klammern]), Privat- und Geschäftsbrief DIN 5008 | `TextTemplates.cs` |

**Bedienung (Leitlinie „das Maximum, aber leicht verständlich“):**
- Oben stehen die häufigsten Aktionen: Neu, Öffnen, Speichern, PDF, Drucken, Rückgängig, Absatzart, F/K/U, Listen, Ausrichtung, Suchen, Rechtschreibung.
- Alles Weitere liegt unter „Mehr …“.
- Bearbeitet wird absatzweise. Der Absatz mit dem Cursor ist ein Eingabefeld, alle anderen zeigen ihr Format. Ein Klick in einen Absatz setzt den Cursor genau an die Stelle.
- Enter teilt den Absatz, die Rücktaste am Absatzanfang verbindet ihn mit dem vorigen, Pfeiltasten wechseln den Absatz. Formate wandern beim Tippen mit.

## Bibliotheken
- **WeCantSpell.Hunspell 7.0.1:** steht unter MPL 1.1 / GPL 2.0 / LGPL 2.1, nicht unter MIT wie in meinem Vorschlag vom 2026-10-25 behauptet. Das ist korrigiert. Trendo nutzt die Bibliothek unverändert unter MPL 1.1.
- **Deutsches Wörterbuch „frami“ (GPL 2/3):** liegt nicht in Trendo, sondern im mitgelieferten LibreOffice-Paket des Vollpakets. Trendo Text liest es von dort; fehlt es, sagt Trendo, woher es kommt.
- DOCX und ODT schreibt und liest Trendo selbst, ohne Fremdbibliothek.

## Abgleich mit LibreOffice 26.2.4 (aus dem Vollpaket)
`reports/t0223-office/text-abgleich/`:
- Ein Beispieldokument mit allen Funktionen aus Stufe 1 hat Trendo als DOCX und ODT gespeichert.
- LibreOffice hat es headless umgewandelt (DOCX→ODT, ODT→DOCX, DOCX→PDF), mit eigenem Profil im Temp-Ordner, damit das installierte LibreOffice unberührt bleibt.
- Trendo hat die Ergebnisse wieder eingelesen. **Beide Richtungen sind gleich** (`ERGEBNIS.txt`, `SHA256SUMS`).

**Der erste Abgleich fand echte Fehler, alle behoben:**
- **Formatvorlagen im ODT:** Die deutschen Namen der Vorlagen machten bei LibreOffice „(user)“-Kopien. Jetzt verwendet Trendo die internen Namen wie LibreOffice, und beim Lesen werden „(user)“-Vorlagen erkannt.
- **Rechtsbündig:** „end“ hat LibreOffice nicht übernommen, jetzt schreibt Trendo „right“.
- **Listenvorlagen:** stehen bei LibreOffice in `styles.xml`.
- **Zitat:** Die Namen „Quote“, „Quotations“ und „Blockzitat“ werden erkannt.
- **Seitenumbruch in DOCX:** steht jetzt als „Seitenumbruch davor“ am nächsten Absatz wie in Word, ohne leeren Absatz.
- **Leerzeichen in ODT:** Leerzeichen zwischen Formatbereichen gingen verloren (XML und ODF-Regel `<text:s/>`).

Optisch: Seite 1 der PDF von Trendo und der PDF von LibreOffice sind im Render praktisch gleich. LibreOffice legte zusätzlich Lesezeichen an, die hat Trendo jetzt auch.

## Tests
- **Core `T0223_TrendoText`:** 10 Tests.
  - Formate und Bearbeiten
  - Suchen, Ersetzen und Zählen
  - DOCX-, ODT- und TXT-Rundweg, DIN-Vorlage
  - PDF mit Seiten und Lesezeichen
  - Vorlagen, Rechtschreibung mit dem echten Wörterbuch
  - LibreOffice-Abgleich, nur mit `TRENDO_LO_ABGLEICH`
- **App `T0223_TrendoTextUi`:** 3 Tests.
  - Tippen, Enter, Rücktaste, Formate per Knopf und Tastatur, Absatzart, Liste, Ausrichtung, Rückgängig/Wiederholen
  - Tabelle, Seitenumbruch, Suchen/Ersetzen, Speichern/Öffnen, PDF, Vorlage
  - Rechtschreibung ohne Wörterbuch

## Nicht geprüft / offen
- Zeichenformate sieht man im gerade bearbeiteten Absatz erst, wenn man ihn verlässt. Avalonia hat kein Eingabefeld für formatierten Text.
- Kopf- und Fußzeilen, Fußnoten, Inhaltsverzeichnis, Spalten, Kommentare, Änderungsverfolgung: nicht in Stufe 1. Beim Öffnen fremder Dateien gehen sie verloren oder werden zu Text.
- Drucken nur über die PDF mit Druckdialog von SumatraPDF, ohne eigene Druckvorschau.
- Nicht am echten Bildschirm getestet, nur headless gerendert.

## Testergebnis Teil 2
`dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler.

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 699 | 1 | 0 | 700 |
| Trendo.App.Tests | 337 | 2 | 0 | 339 (27 min 32 s, Exitcode 0) |
| Pakettests | 8 | – | 0 | 8 |
