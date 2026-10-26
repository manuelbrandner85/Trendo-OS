# T-0226: Programme verwalten – Logo B, Deinstallation mit allen Programmen, GitHub-Programme, Beschreibungen, Vollbild-Einrichtung

**Ziel:** Die Wünsche von Manuel vom 26.10.2026 umsetzen:
- „wenn trendo os deinstalliert wird sollen auch alle zugehörigen programme auch deinstalliert werden“
- „programme von git hub … automatisch … auf trendo os installiert werden … aber auch das deinstallieren einiger programme soll möglich sein“
- „beim setup soll sobald zustimmen und installieren geklickt wird … der button ausgegraut werden und eine fortschrittsanzeige … im vollbild das nicht wegklickbar ist bis eingerichtet ist“
- „jedes programm auch github sollen eine ausführliche leicht verständliche beschreibung haben auch funktionsweise“
- Logo: Vorschlag 2 „Saphir-Kristall“

**Branch:** claude/T-02xx
**Dateien/Bereich:**
- Logo: `TrendoLogo.cs`, `Assets/logo/*`
- Deinstallation: `UninstallPrompt.cs`, `DataLocation.RemovableTools`
- Programme: `GitHubApps.cs`, `ProgramGuide.cs`, `Vollpaket.Uninstall`, `MainWindow.Programme.cs`, `MainWindow.W4Oss.cs`
- Einrichtung: `MainWindow.InstallOverlay.cs`, `MainWindow.Setup.cs`
- Darstellung: Render-Werkzeug (`TRENDO_LOGO`, `TRENDO_SHOT=einrichtung`)

**Fertig, wenn:**
- [ ] Logo B überall (Programm, Fenster-Symbol, Setup-Symbol). Die SVG wird aus derselben Geometrie erzeugt, ein Test prüft die Gleichheit.
- [ ] Die Deinstallation entfernt `TrendoWerkzeuge` immer, auch bei stiller Deinstallation, in einem eigenen Prozess. Die Daten-Frage bleibt.
- [ ] GitHub: Nur das passende Windows-ZIP bzw. die passende .exe wird installiert, nie MSI oder Setup. Downloads nur über HTTPS von GitHub. Prüfsumme, sobald GitHub eine nennt. Kein Zip-Slip. Eigener Ordner `gh-…`, Liste, Audit-Log.
- [ ] Mitgelieferte Programme und GitHub-Programme lassen sich einzeln entfernen, mit Rückfrage im Knopf.
- [ ] Alle 20 mitgelieferten Programme haben eine Beschreibung. GitHub-Treffer bekommen eine Beschreibung aus den Projektangaben, ohne Erfindungen.
- [ ] Einrichtung: Der Knopf wird ausgegraut. Eine Vollbild-Fläche mit Animation und Fortschritt erscheint. Klicks, Tasten und Schließen bleiben bis zum Ende gesperrt. „Bewegung reduzieren“ zeigt ein Standbild.
- [ ] Alle drei Testprojekte und die Paket-Tests sind grün.

**Nicht tun:**
- keine Installer mit Admin-Rechten
- keine fremden Programme außerhalb von `TrendoWerkzeuge` löschen
- keine Downloads von anderen Hosts
