# T-0764: Rechte im Kern, Freigaben, Programm-Seite (SYS-11), Geräte auswerfen (SYS-07)

**Ziel:** Die Rolle der angemeldeten Person wirkt im Kern: Ein Kinderprofil installiert und entfernt über keinen Weg ein Programm und erteilt keine Freigabe; Programme, die Trendo eingerichtet hat, lassen sich an einem Ort starten, aktualisieren und entfernen; Wechseldatenträger lassen sich sicher auswerfen.
**Branch:** claude/T-0764-rechte-programme (Basis main = Release 0.9.109)
**Grundlage:** Restliste Rang 41 Teil 2; Audit `docs/audit/2026-11-16-profi-audit/audit-5-entwickeln-system.md` Abschnitte SYS-07, SYS-11, SYS-12 und „Auffälligkeiten der Gruppe → Rechte und Freigaben“. Vom Nutzer ausdrücklich freigegeben (sicherheitsrelevant).
**Dateien/Bereich:** `src/Trendo.Core/Rbac.cs`, Installationswege im Kern (`Sys/Sys11.cs`, `Vollpaket.cs`, `Dependencies.cs`, `GitHubApps.cs`, `SourceApps.cs`, `WebApps.cs`, `W2/KiLibraryStore.cs`, `W3/Packages.cs`, `Fachprogramm.cs`, `Media/PluginHost.cs`), neu `Sys/TrendoPrograms.cs`, `W3/Devices.cs`, neu `W3/DeviceEject.cs`; App-Aufrufer, `MainWindow.Sys11.cs`, neu `MainWindow.ProgrammSeite.cs`, `MainWindow.Devices.cs`; Render-Hilfe `tools/Trendo.Render/Program.cs` (nur Beispieldaten fürs Bild).
**Fertig, wenn:**
- [x] Neues Recht `ManageSoftware` (nur Besitzer); jeder Installations-/Entfernen-Weg im Kern nimmt die Person (`Principal`) und lehnt ein Kinderprofil ab, bevor etwas geladen, entpackt, gestartet oder gelöscht wird – Nachweis per Core-Test.
- [x] Kinderprofil kann keine Freigabe erteilen (Tor, Brief, Audio-Plugin, Not-Stopp fortsetzen) – Core-Test.
- [x] Alle `Approvals.Approve(`-Stellen geprüft und aufgelistet (Bericht).
- [x] SYS-11: Programm-Seite je Programm; Entfernen ohne Windows-Einstellungen in eine Sicherung, Wiederherstellen, endgültig löschen mit Rückfrage.
- [x] SYS-07: Auswerfen (cfgmgr32, ohne Admin, nie System-/Trendo-Laufwerk), neuer Stick binnen 2 s sichtbar, Problemcode mit nächstem Schritt.
- [x] Rot-Grün-Tests; gezielte Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** kein Volltest, kein Merge, kein Release, kein `gh`; keinen echten Datenträger auswerfen ohne eigenen Test-Stick; keine Treiber/Registry/Admin.
**Am Ende:** Branch pushen, Bericht `tasks/T-0764-rechte-programme.bericht.md`.
