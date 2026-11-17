# T-0743 – Beleg: Großprojekt-Karte → Studio → Ergebnis zurück

Stand 17.11.2026, Zweig `claude/T-0743-grossprojekte`, Basis `main` a90e697aa.

## Ablauf (echter Klickweg im Testfenster, nichts ersetzt außer dem Öffnen von Dateien im System)

Test `Trendo.App.Tests.GrossprojektUebergabe_Ui.Text_to_pdf_chain`, gestartet mit `TRENDO_BELEG_T0743=reports/T-0743`:

1. Großprojekt „Hofladen Flyer“ über „＋ Neues Großprojekt“ angelegt, zwei Karten eingetippt, je Karte das Studio gewählt
   (Karte 1 „Bücher schreiben“, Karte 2 „PDF-Werkstatt“).
2. Karte 1 → „An Studio übergeben“: „Bücher schreiben“ öffnet sich mit vorbereitetem Dokument (Titel und Aufgabe der Karte), gespeichert im Projekt;
   der Auftrag steht als Übergabe `auftrag` X-01 → STUDIO-BOOK-01 in `.trendo/uebergaben.json` (`auftrag-karte1.md`).
3. Im Studio Text ergänzt, Menüband „Druck-PDF (.pdf)“ geklickt → die PDF geht von selbst an die Karte zurück (Beleg mit SHA-256,
   Übergabe `ergebnis` STUDIO-BOOK-01 → X-01, Status „erledigt“, Spalte „Fertig“) → `karte1-ergebnis.pdf`.
4. Karte 2 → „An Studio übergeben“: die PDF-Werkstatt öffnet sich mit der PDF der Karte davor als Eingabe; Werkzeug „Seitenzahlen“ geklickt,
   „Ergebnis als PDF“ geklickt → `karte2-ergebnis.pdf` zurück an Karte 2.
5. Großprojekte: Karte 2 zeigt „✓ erledigt“, Vorschau der ersten Seite (gerendert), „Prüfsumme stimmt“; Gesamtfortschritt 100 %.

## Dateien

| Datei | Inhalt |
|---|---|
| `testlauf-ende-zu-ende.log` | Rohausgabe des Testlaufs (1 Test, 0 Fehler) |
| `ablauf.log` | Protokoll des Tests mit Pfaden im Projekt und SHA-256 der Ergebnisse |
| `auftrag-karte1.md` | Auftrag, den die Karte an das Studio übergeben hat |
| `uebergaben.json` | die vier Übergaben (2 × auftrag, 2 × ergebnis), alle angenommen |
| `tafel.json` | Tafel des Großprojekts nach dem Lauf (Studio, Status, Ergebnis, Prüfsumme je Karte) |
| `karte1-ergebnis.pdf`, `karte2-ergebnis.pdf` | die zurückgekommenen Ergebnisdateien |
| `fremdprogramm-pdftotext.log` | beide PDFs mit `pdftotext` (xpdf 4.06, Fremdprogramm) gelesen: Text der Karte, „Seite 1 von 2“/„Seite 2 von 2“, exit=0 |
| `tafel-erledigt.png`, `tafel-offen.png` | Oberfläche (Trendo.Render, `TRENDO_BIG_DEMO`): erledigte Karte mit Ergebnis-Vorschau bzw. offene Karte mit „An Studio übergeben“ |

Die SHA-256 der PDFs in `ablauf.log` stimmen mit den Dateien überein (siehe `SHA256SUMS`). Textdateien mit LF-Zeilenenden.
