# SYS-10 – Office Documents & Productivity Center: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-10-P01 | Office-Erkennung | LibreOffice/MS Office finden | Pfad korrekt, fehlt → Hinweis | vorhanden |
| SYS-10-P02 | Neues Dokument | DOCX/ODT/XLSX aus Vorlage | Öffnet in Office | umgesetzt, Box getestet |
| SYS-10-P03 | Konvertieren | DOCX↔ODT↔PDF über LibreOffice | Hash im Protokoll, Original bleibt | vorhanden |
| SYS-10-P04 | Zuletzt verwendet | Letzte Office-Dateien im Projektordner | Liste korrekt sortiert | umgesetzt, Box getestet |
| SYS-10-P05 | PDF zusammenfügen | Mehrere PDFs zu einem (qpdf/LO) | Seitenzahl = Summe | umgesetzt, Box getestet |
| SYS-10-P06 | PDF teilen | Seitenbereiche extrahieren | Seitenzahl stimmt | umgesetzt, Box getestet |
| SYS-10-P07 | PDF-Text | Text aus PDF (pdftotext/OCR) | Text durchsuchbar | umgesetzt, Box getestet |
| SYS-10-P08 | Scan zu PDF | Bild → durchsuchbares PDF (Tesseract) | Text in PDF auffindbar | umgesetzt, Box getestet |
| SYS-10-P09 | Vorlagen-Bibliothek | Versionierte Vorlagen (Brief, Rechnung, Protokoll) | Version im Dateinamen | umgesetzt, Box getestet |
| SYS-10-P10 | Serienbrief | CSV + Vorlage → Einzeldokumente | N Zeilen = N Dateien | umgesetzt, Box getestet |
| SYS-10-P11 | Tabellen-Schnellansicht | CSV/XLSX lesen ohne Office | Erste 1000 Zeilen korrekt | umgesetzt, Box getestet |
| SYS-10-P12 | Protokoll-Vorlage | Besprechung mit Aufgabenliste | Aufgaben als .ics exportierbar | umgesetzt, Box getestet |
| SYS-10-P13 | Rechtschreibung | Hunspell de-DE lokal | Testsatz mit Fehlern erkannt | umgesetzt, Box getestet |
| SYS-10-P14 | Wortzähler/Lesbarkeit | Wörter, Sätze, Lesbarkeitsindex | Zahlen stimmen | umgesetzt, Box getestet |
| SYS-10-P15 | Dokumentvergleich | Zwei DOCX/TXT, Unterschiede | Änderungen markiert | umgesetzt, Box getestet |
| SYS-10-P16 | Metadaten entfernen | Autor/Firma aus DOCX/PDF | Nach Export keine Metadaten | umgesetzt, Box getestet |
| SYS-10-P17 | Druckvorschau | PDF-Vorschau vor Druck | Seite A4, Ränder korrekt | umgesetzt, Box getestet |
| SYS-10-P18 | Drucken | Über Systemdrucker (Bestätigung) | Nie ohne Klick | umgesetzt, Box getestet |
| SYS-10-P19 | Versionen | Automatische Sicherung vor Überschreiben | v###-Kopien vorhanden | umgesetzt, Box getestet |
| SYS-10-P20 | Suche in Dokumenten | Volltext über DOCX/ODT/PDF | Treffer mit Fundstelle | umgesetzt, Box getestet |
| SYS-10-P21 | Aufgabenliste | Lokale To-dos mit Fälligkeit | Erinnerung als .ics | umgesetzt, Box getestet |
| SYS-10-P22 | Unterschrift einfügen | Bild-Signatur (keine qualifizierte) | Hinweis: nicht rechtsverbindlich | umgesetzt, Box getestet |
| SYS-10-P23 | Formulare ausfüllen | PDF-Formularfelder (lokal) | Felder gespeichert | umgesetzt, Box getestet |
| SYS-10-P24 | Export-Paket | Dokument + Anlagen als ZIP mit SHA-Liste | SHA-Liste stimmt | umgesetzt, Box getestet |
| SYS-10-P25 | Barrierefreie PDF-Hinweise | Tags/Alt-Texte prüfen | Bericht mit Mängeln | umgesetzt, Box getestet |

Stand: 2 vorhanden (vorher), 23 umgesetzt und auf der Linux-Box getestet, 0 teilweise, 0 offen. PC-Prüfung steht für alle aus.
