# T-0684: Design-Paket 1 – kleine, sofort sichtbare Korrekturen

**Ziel:** Sieben Vorschläge aus dem Bild-Audit „Anordnung und Design“ vom 16.11.2026 umsetzen, die ohne Umbau der Programm-Hülle sofort sichtbar wirken.
**Freigabe:** Der Nutzer hat am 16.11.2026 alle 22 Vorschläge V-01 bis V-22 mit Ja freigegeben. Dieses Paket enthält V-05, V-06, V-10, V-13, V-16, V-18, V-19.
**Branch:** claude/T-0684-design-1 (von origin/main f68974c6d)
**Dateien/Bereich:** `src/Trendo.App/` – Wort-an-Knopf (`GlyphIcons.cs`, `SimpleWords.cs`), Einklappen (`MainWindow.SimpleMode.cs`, `MainWindow.Uebersicht.cs`), Seitenkopf und Fenstertitel (`MainWindow.axaml.cs`, `MainWindow.StartMenu.cs`, `MainWindow.StudioFolds.cs`), Auftragszeile (`MainWindow.PromptAction.cs`), einzelne Seiten (Kalender, Geld, Adressbuch, Aufgaben, Projekte, Großprojekte, Systemcheck, Protokoll), Leiste am unteren Rand (`MainWindow.DockLook.cs`). Tests in `tests/Trendo.App.Tests/`.

**Fertig, wenn:**
- [x] V-05: Blätter-Pfeile heißen „Nächster Monat“ / „Voriger Monat“ usw., nie „Abspielen“; mehrdeutige Zeichen werden nicht geraten; kein Knopf „Kopieren“ unter kurzen Hinweisen.
- [x] V-06: „Mehr anzeigen“ versteckt nie den Abschnitt direkt unter einer Überschrift und nennt, was folgt; Einleitung und Leerzustände werden nicht gekürzt.
- [x] V-10: Fenstertitel und Seitenkopf nennen die Seite, die man sieht.
- [x] V-13: Seiten mit eigenem KI-Gespräch haben eine Eingabe; „Gehört hierher:“ heißt „Verwandte Programme“ und steht im Menü „Mehr“.
- [x] V-16: Adressbuch füllt das Fenster; leere Kästen (Projekte, Großprojekte) werden nicht gezeichnet; ein Leerzustand je Seite (Aufgaben, Adressbuch).
- [x] V-18: Bau-Fortschritt und rohe Protokollzeilen nur in „Professionell“; Systemcheck als ein Satz mit einem Knopf.
- [x] V-19: Wörter unter den Symbolen der Leiste am unteren Rand.
- [x] Jede Korrektur hat einen UI-Test und ein Vorher/Nachher-Bild.
- [ ] alle drei Testprojekte grün – Volltest macht die Hauptsitzung; hier laufen die betroffenen Klassen (Zahlen im Bericht).

**Nicht tun:** `MainWindow.ProgramShell.cs`, `ProgramDocument.cs`, `MainWindow.Text*.cs`, `MainWindow.Tabelle*.cs`, `MainWindow.Praesentation*.cs`, `MainWindow.OfficeRecovery.cs`, `MainWindow.KreativStudios.cs`, `MainWindow.AppCenter.cs`, `MainWindow.CodeEditor.cs`, `Backup.cs`, `W2/Workflows.cs`, `W2/KiSources.cs`, `MainWindow.Claude.cs` anfassen (parallele Aufträge). Keine Funktion entfernen. Kein Push, kein Merge, kein Release.
**Am Ende:** Bericht `tasks/T-0684-design-1.bericht.md`. Push und Merge macht die Hauptsitzung.
