# T-0710: Klassische Menüleiste in allen Programmen

**Ziel:** Eine schmale Textzeile Datei · Bearbeiten · Ansicht · (fachliche Menüs) · KI · Hilfe in jedem Programm – wie früher im Windows-Editor oder Explorer (Freigabe 17.11.2026: „Das Menüband in den Programmen wie bei Windows, wo Datei usw. steht, so machen“, gewählt: „Klassische Menüleiste“).
**Branch:** claude/T-0710-menueleiste (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Programm-Hülle (`MainWindow.ProgramShell.cs`, neuer Baustein `MainWindow.MenuBar.cs`, Modell `Trendo.Core/Programs/ProgramMenu.cs`), Fenster-Titelleiste (`MainWindow.Windows.cs`), Seitenaufbau (`MainWindow.Sys13.cs`), Windows-Stil (`MainWindow.OsPrograms.cs`), Render-Werkzeug (Menü offen zeigen).
**Fertig, wenn:**
- [x] Ein Baustein für alle; Einträge rufen dieselben Handler wie die Knöpfe, keine zweite Befehlslogik
- [x] Kürzel rechts, Trennlinien, ausgegraute Einträge, Untermenüs; Alt/F10, Alt+Buchstabe, Pfeile, Enter, Esc
- [x] alle Hüllen-Programme, die Programme mit eigener Werkzeugleiste (V-14) und die über ProgramLink angeschlossenen (T-0708/T-0711)
- [x] nichts golden in der Leiste, eine Zeile, in allen Stufen sichtbar; „Einfach“ verliert keinen Eintrag aus „Erweitert“
- [x] UI-Tests mit echten Tastaturereignissen in fünf Fächern, Pflicht-Testklassen grün
**Nicht tun:** kein Office-Menüband mit großen Knöpfen, keine Prüfung lockern, kein Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0710-menueleiste.bericht.md`.
