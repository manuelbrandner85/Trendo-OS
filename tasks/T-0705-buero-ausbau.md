# T-0705: Büro-Etappe ausbauen – Klick bis Datei, Echtlauf mit Fremdprogrammen, Absturzschutz, Ziehen/Rechtsklick

**Ziel:** Die sieben Büro-Studios (Bewerbung, Karten, Verträge, PDF-Werkstatt, Formulare, Präsentation gestalten, Scannen) nach den Abnahmekriterien des Profi-Ausbaus nachweisen und die wichtigsten Bedienlücken schließen.
**Freigabe (Chat, 17.11.2026):** „Büro-Etappe mit aufnehmen und ausbauen“.
**Branch:** claude/T-0705-buero-ausbau (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.App/MainWindow.{Bewerbung,Karten,Vertraege,PdfWerkstatt,Formulare,DeckDesign,DocScan}.cs`, neu `MainWindow.BueroAusbau.cs` (gemeinsame Bedienhilfen), `src/Trendo.Core/Office/DeckDesign.cs` (Abschnitte umsortieren), Tests, `tools/echtlauf/buero/`, Belege `reports/T-0705/`.
**Fertig, wenn:**
- [x] je Studio ein Klicktest vom leeren Zustand über den ersten Schritt bis zur echten Datei (Stufe „Einfach“)
- [x] Fremddatei-Rundreise mit echten Werkzeugen (LibreOffice, qpdf, Tesseract, PDFium), Belege mit Rohlog und SHA256SUMS unter `reports/T-0705/`, ohne Benutzerpfade
- [x] gemeinsamer Absturztest über alle sieben (Hülle, Notsicherung, „Wiederherstellen“)
- [x] Ziehen und Ablegen von Dateien, Umsortieren mit der Maus und Rechtsklick dort, wo es zum Fach passt – Test über echte Maus-/Ablege-Ereignisse
- [x] fehlende Fachfunktionen geprüft, wichtigste Lücken klein geschlossen
- [x] `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui` grün, Funktions-Baseline grün; kein Volltest (macht die Hauptsitzung)
**Nicht tun:** `MainWindow.ProgramShell.cs`, `MainWindow.PromptAction.cs`, Programm-Hülle und zentrale Design-Dateien (Design-Paket 3); keine zweite Engine; nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0705-buero-ausbau.bericht.md`.
