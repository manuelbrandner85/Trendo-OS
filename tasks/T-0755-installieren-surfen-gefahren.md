# T-0755: Programme installieren, Downloads & Links, System auf Gefahren prüfen

**Ziel:** Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 33 bis 35 (vom Nutzer am 23.11.2026 freigegeben): SYS-04, SYS-03 und SPEC-12 von TEILWEISE weiter in Richtung vollwertig bringen.
**Branch:** claude/T-0755-installieren-surfen-gefahren
**Dateien/Bereich:** `src/Trendo.Core/W3/Packages.cs`, `src/Trendo.App/MainWindow.AppCenter.cs` (SYS-04); `src/Trendo.Core/Sys/DownloadQueue.cs`, `Sys/Sys03.cs`, `src/Trendo.App/MainWindow.DownloadManager.cs`, `MainWindow.W1Web.cs`, `MainWindow.Sys03.cs` und die Namensstellen (SYS-03); `src/Trendo.Core/Sys/Authenticode.cs`, `Sys/Gefahren.cs`, `src/Trendo.App/MainWindow.Gefahren.cs`, `MainWindow.Forensik.cs` (SPEC-12).
**Fertig, wenn:**
- [x] SYS-04: Die Rückfrage nennt den genauen Befehl. Der Nutzer startet den Lauf selbst mit dem Knopf der Rückfrage. Trendo zeigt Befehl, Fortschritt und Ausgabe, schreibt ein Protokoll je Lauf, „Abbrechen“ beendet den Lauf, danach prüft Trendo, ob das Programm installiert ist.
- [x] SYS-04: Tests mit einem ERSETZTEN winget (eigenes Testprogramm), ausdrücklich so gekennzeichnet. Der echte winget-Lauf bleibt NICHT GETESTET.
- [x] SYS-03: Name überall „Downloads & Links“, alte IDs (`SYS-03`, `downloads`, `netz-*`, Dock `browser`) und Dateien bleiben. SHA-256 wird immer angezeigt und mit der Angabe verglichen; Verlauf; Anhalten/Fortsetzen; Webseiten öffnen im Standard-Browser; Links sammeln.
- [x] SPEC-12: nur lesend Autostart-Ordner, Run-Schlüssel, geplante Aufgaben, installierte Programme; je Eintrag Pfad, Herausgeber, Authenticode-Prüfung, Ampel, Erklärung, Tipp; Bericht HTML/CSV; keine Änderung am System. Tests mit echten Dateien.
- [x] Gezielte Tests der berührten Klassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** winget/msiexec/choco starten (auch nicht zum Testen), Registry schreiben, eigenen Browser bauen, nach `main` pushen, mergen, Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0755-installieren-surfen-gefahren.bericht.md`.
