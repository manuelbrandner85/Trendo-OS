# T-0788: Neues Studio „Stammbaum“ (Etappe 0.9.115 „Wissen & Lernen E“)

**Ziel:** Studio der Kategorie Wissen & Lernen mit echtem Genealogie-Kern in der Programm-Hülle: Personen, Familien, Ereignisse (Geburt, Heirat, Tod …), Orte und Quellen; Baum der Vorfahren und Nachkommen mit Zoom; GEDCOM 5.5.1 ein und aus (UTF-8, ANSEL erkennen, unbekannte Tags erhalten); Prüfung auf Widersprüche; Bericht als PDF; lebende Personen in Ausgaben optional ausblenden.
**Freigabe:** Nutzer, 26.11.2026 (Etappe 0.9.115), Paket-Brief der Hauptsitzung vom 28.11.2026.
**Branch:** claude/T-0788-stammbaum (Basis main 0.9.114 = 9bc135aaa)
**Grundlage:** `docs/audit/2026-11-26-neubewertung/fehlende-studios.md` (Wissen & Lernen Rang 10 „neuer Kern mit GEDCOM-Import/Export“), Paket-Brief, Studio-Brief.
**Dateien/Bereich:** neu `src/Trendo.Core/Wissen/Gedcom.cs`, `GenDate.cs`, `Stammbaum.cs`, `StammbaumAusgabe.cs`, `src/Trendo.Core/PromptAction/StammbaumAdapter.cs`, `src/Trendo.App/MainWindow.Stammbaum.cs`, `FamilyTreeCanvas.cs`, Bild `Assets/studios/STUDIO-FAMILY-01.jpg`; Einträge am Ende von StudioCatalog, StudioIcons, PromptChips, CapabilityCatalog, StudioKnowledge, StudioProfiles, StudioTeams, IntelligencePromptModel, W6, `tools/studio_bilder.py`, `docs/design/STUDIOBILDER.json`; klein in `Zeitleiste.cs` (Titel für SVG/PDF wählbar); Tests, Belege `reports/T-0788/`, Anforderungen `docs/nachbau/stammbaum/`.
**Fertig, wenn:**
- [x] Vorbilder recherchiert, Anforderungen mit Prüfung je Zeile (Clean-Room).
- [x] Studio in der Hülle (Sicherung/Wiederherstellung der Hülle, Rückgängig, Einfach/Profi, Rechtsklick, Ziehen, Tasten).
- [x] Klick bis Datei getestet, Rot-Grün belegt.
- [x] Fremdprogramme: Bericht in LibreOffice, Baum in Inkscape, Liste in Calc (Rohlog + SHA-256); GEDCOM-Fremdprogramm nicht vorhanden → ehrlich BLOCKIERT.
- [x] KI-Fachaktion (Vorschau → „Ja, behalten“ → ein Rückgängig), Echtlauf mit lokaler KI.
- [x] requirements.yaml-Vorschlag im Bericht (Datei gesperrt).
**Nicht tun:** requirements.yaml ändern, QA-015 ändern, Blender-Transport (T-0785) anfassen, Claude-Echtlauf, Volltest, Merge, Release, nach main pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0788-stammbaum.bericht.md`.
