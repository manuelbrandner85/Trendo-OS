# T-0711: B6 „Programm-Hülle für alle“ – Etappe 1b

**Ziel:** Die übrigen elf bearbeitbaren Dokumente ohne Autosave über `ProgramLink` an Rückgängig, Notsicherung und Wiederherstellen der Programm-Hülle anschließen und die Sperre gegen ein zweites Fenster auch für diese Sicherungen gelten lassen – ohne zweites System, ohne Funktionsverlust.
**Branch:** claude/T-0711-huelle-etappe-1b (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/Programs/ProgramLink.cs` (Sperre), neu `src/Trendo.App/MainWindow.LinkSlots.cs` (allgemeiner Anschluss), `MainWindow.ProgramLinks.cs` (Takt, Briefe mit Sperre); die elf Programme: `Mail.cs` (nur Entwurf), `Helfer.cs` (Editor), `PdfOrganize.cs`/`Pdf.cs` (Seiten ordnen), `MusikStudio.cs`/`MusikNoten.cs`/`MusikTexte.cs`, `W4Game.cs`, `Core09.cs`, `Research.cs`, `Wissen.cs`, `Prompts.cs`, `Skills.cs`, `W5CleanRoom.cs`; atomares Speichern in Core (`GameStudio`, `Knowledge`, `KnowledgeWiki`, `Prompts`, `Research`, `Mail`); `tests/Trendo.CrashProbe` (Modus `link-editor`); Umzugsliste.
**Fertig, wenn:**
- [x] ProgramLocks gilt für ProgramLink-Sicherungen (zwei Fenster bieten nicht dieselbe Sicherung an), mit Test
- [x] elf Programme: Rückgängig über mehrere Schritte, Notsicherung, Wiederherstellen-Karte, eigenes Format lesbar, Speichern atomar
- [x] UI-Test je Programm (ändern → ohne Speichern schließen → neues Fenster: Karte → Wiederherstellen; Rückgängig mehrere Schritte), Absturztest `link-editor`
- [x] UebersichtEinfach_Ui, ProgramHeadDesign_Ui, DesignPaket3_Ui, FreigabenKernUi, T0708_HuelleEtappe1_Ui und Testklassen der berührten Programme grün; Funktions-Baseline grün
- [x] Umzugsliste aktualisiert
**Nicht tun:** ClaudeBridge/PromptAction (B5, T-0707), Kopfzeile/Knöpfe der Mail (T-0709), `MainWindow.ProgramShell.cs` höchstens eine Zeile (hier: keine); kein Volltest, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0711-huelle-etappe-1b.bericht.md`.
