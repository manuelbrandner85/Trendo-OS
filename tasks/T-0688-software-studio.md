# T-0688: C3 – Software-Studio baut, testet und paketiert wirklich

**Ziel:** Das Software-Studio (CORE-07 Software-Teil, Code-Editor, „Code mit KI“) erstellt funktionsfähige Anwendungen aus echten Vorlagen, baut, testet und paketiert sie mit den echten Werkzeugen; „Mit Claude ändern“ ändert Dateien im Projektordner.
**Branch:** claude/T-0688-software-studio (Basis: claude/T-0685-b1-integration)
**Grundlage:** Audit 2026-11-16, Teilbericht 5 CORE-07 (Software-Teil, Code-Editor), Teilbericht 4 STUDIO-CODEAI-01.
**Dateien/Bereich:** `src/Trendo.Core/DevStudio.cs`, neu `DevBuild.cs`, `DevTemplates.cs`, `CodeDrafts.cs`; kleine Ergänzungen `Cli.cs` (Kodierung für `RunStreamAsync`), `CodeProject.cs` (ausgeblendete Ordner), `W2/ClaudeCode.cs` (FolderSnapshot überspringt Paket-/Testordner); `src/Trendo.App/MainWindow.CodeEditor.cs`, `MainWindow.W4Dev.cs`, Code-mit-KI-Abschnitt in `MainWindow.KreativStudios4.cs`; Tests und `reports/T-0688/`.
**Fertig, wenn:**
- [x] Vorlagen: .NET-Konsole, .NET-Desktop (Avalonia), Python, Web-App (statisch + Node-Server), Node – je mit echter Funktion und echtem Test
- [x] Bauen/Ausführen/Testen im Hintergrund mit laufender Ausgabe und Abbrechen; `dotnet test` statt nur Bauen; Ergebnis je Test; Klick springt zu Datei/Zeile; fehlendes Werkzeug → BLOCKIERT mit Einrichten-Weg
- [x] Paket: .NET self-contained single-file + ZIP, Python/Node als ZIP mit Startskript, Web als Ordner + ZIP; Größe, SHA-256, Startprobe aus leerem Ordner, „Ordner öffnen“
- [x] Code-Editor: Entwürfe/Wiederherstellen nach Absturz, „Automatisch speichern“, Terminal im Projektordner (protokolliert, Abbrechen), Fehlerliste, Suche (vorhanden)
- [x] „Mit Claude ändern“: Claude Code im Projektordner, vorher Sicherung, Diff je Datei, Übernehmen/Zurücknehmen (alle oder eine Datei), ohne Anmeldung klare Meldung
- [x] Belege mit echtem dotnet/python/node und echtem Claude Code unter `reports/T-0688/`
**Nicht tun:** `MainWindow.ProgramShell.cs`, Web-Design-Studio (`WebDesign*`, `Website*`), `SimpleWords.cs`, Office-Seiten; kein Volltest; kein Push nach main/Integrationszweig.
**Am Ende:** Branch pushen, Bericht `tasks/T-0688-software-studio.bericht.md`.
