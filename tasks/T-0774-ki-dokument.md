# T-0774: KI schreibt ins offene Dokument – PDF-Werkstatt und Dokumente scannen (Querschnitt Q1)

**Ziel:** Die KI arbeitet in der PDF-Werkstatt (STUDIO-PDFTOOLS-01) und in „Dokumente scannen“ (STUDIO-DOCSCAN-01) über die vorhandene Kette (`AssistChain`) mit der echten Fachaktion: Das Ergebnis ist eine echte PDF im offenen Programm, mit genau einem Rückgängig-Schritt – statt nur `pdf-auftrag.txt` / `scan-auftrag.txt`.
**Freigabe:** Nutzer, 26.11.2026 (Etappe 0.9.112).
**Branch:** claude/T-0774-ki-dokument (Basis main 0.9.111 = 2b1ffa923)
**Grundlage:** `docs/audit/2026-11-26-neubewertung/restliste.md` (Q1, Rang 1 PDF-Werkstatt, Rang 31 Dokumente scannen), `BERICHT.md` derselben Neubewertung; Vorbild `reports/T-0707` (Tagesplaner, Bücher, Messwerte).
**Dateien/Bereich:** `src/Trendo.App/MainWindow.Assist.cs`, `MainWindow.ProgramShell.cs`, `MainWindow.PdfWerkstatt.cs`, `MainWindow.DocScan.cs`, neuer Test `tests/Trendo.App.Tests/T0774KiDokumentUiTests.cs`, Belege `reports/T-0774/`, Anforderungslisten `docs/nachbau/pdf-werkstatt/`, `docs/nachbau/scannen/`.
**Fertig, wenn:**
- [x] PDF-Werkstatt: „Seiten 1–3 heraus“ ergibt über die Auftragszeile eine neue PDF im offenen Programm, ein Rückgängig-Schritt.
- [x] Dokumente scannen: KI-Aktion „Fotos → PDF“ ins offene Dokument, mit Rückgängig.
- [x] Knopf „Scanner …“ ehrlich umbenannt (keine WIA-Anbindung).
- [x] Je ein Echtlauf mit lokaler KI dieses PCs, Rohlog + SHA-256 unter `reports/T-0774/`.
- [x] Tests vom Klick bis zur echten PDF, Rot-Grün belegt.
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** Logos & Marken (Paket T-0775), WIA/TWAIN-Anbindung, neuer KI-Kern, Volltest, Merge, Release; echte Installation unter `%LOCALAPPDATA%\TrendoOS` nicht anfassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0774-ki-dokument.bericht.md`.
