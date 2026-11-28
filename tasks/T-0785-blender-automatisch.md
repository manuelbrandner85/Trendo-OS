# T-0785: Blender startet automatisch, Claude kann alles darin tun, die MCP-Verbindung reißt nicht ab

**Ziel:** Wenn Trendo Blender braucht, startet Trendo es selbst (sichtbar, mit Projekt, mit eingeschalteter MCP-Brücke). Die Verbindung wird überwacht und nach einem Abbruch oder Absturz von selbst wiederhergestellt (Wunsch des Nutzers vom 28.11.2026).
**Branch:** claude/T-0785-blender-automatisch (Basis: claude/T-0784-sammel-0.9.113, Ziel Release 0.9.114)
**Dateien/Bereich:** neu `src/Trendo.Core/BlenderLink.cs` und `src/Trendo.App/MainWindow.BlenderLink.cs`; gezielt geändert: `StudioMcp.cs`, `MainWindow.StudioMcp.cs`, `MainWindow.ClaudeLos.cs`, `MainWindow.ClaudeChat.cs` (nur der Claude-Start), `MainWindow.W4Studio3D.cs` („In Blender öffnen“), `MainWindow.Kreatur.cs` („In Blender öffnen“). Dazu Tests und Belege.
**Fertig, wenn:**
- [x] Blender startet von selbst, sichtbar und mit dem Projekt, wenn Claude mit dem Blender-MCP arbeitet, bei „In Blender öffnen“ (3D-Studio, Kreatur) und beim Programmstart über `StudioLaunchProgram("blender")`
- [x] Ein offenes, verbundenes Blender wird weiterbenutzt. Es startet kein zweites Blender, und es gibt keinen doppelten Start
- [x] Fehlt Blender: ehrliche Meldung, Einrichten nur per Knopf
- [x] Claude bekommt den vollen Werkzeugumfang des Blender-MCP (alle Werkzeuge des Servers `trendo-blender` sind freigegeben)
- [x] Herzschlag, Neuverbinden mit Wartezeit, Neustart nach Absturz, das Projekt wird wieder geöffnet (aus einer 30-s-Sicherung)
- [x] Unterbrochene Aufträge laufen weiter oder werden klar gemeldet. Lange Läufe gelten nicht als Abbruch
- [x] Statusfeld (verbunden / verbindet neu / getrennt mit Grund). Jeder Abbruch und jedes Neuverbinden steht im Protokoll (`blender.link.*`)
- [x] Tests mit Ersatz-MCP-Brücke und ein ECHT-Test mit dem installierten Blender, eigenem Port und eigenem Profil (Rohlog und SHA-256 unter `reports/T-0785/`)
**Nicht tun:** Den Blender-MCP unter C:\tools\blender-mcp-1.0.1 und das Blender-Profil der Person nicht verändern. Fremde Blender-Prozesse nie beenden. Kein winget und keine Downloads. Den WebView2-Datenordner (MainWindow, SelfHealing.cs) nicht anfassen. Kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0785-blender-automatisch.bericht.md`.
