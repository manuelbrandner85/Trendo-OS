# T-0762: Projekte von GitHub holen (X-06) und Programme nachbauen (X-10) – Profi-Ausbau

**Ziel:** X-06 echter Lauf mit Beleg, Installer nur nach Signaturprüfung und eigener Bestätigung, geholter Quellcode mit einem Klick im Code-Editor samt Bauanleitung; X-10 Anforderungsliste als bearbeitbares Dokument mit Test-Verknüpfung und Abgleich, Treffer nebeneinander, Bericht mit Herkunft und Datum.
**Branch:** claude/T-0762-github-nachbau (Basis main = Release 0.9.109)
**Freigabe:** Nutzer, Restliste Rang 38+39, Audit `docs/audit/2026-11-16-profi-audit/audit-5-entwickeln-system.md` Abschnitte X-06 und X-10
**Dateien/Bereich:** `src/Trendo.Core/OpenSource.cs`, `GitHubApps*.cs`, `CleanRoom*.cs`, `PromptAction/CapabilityCatalog.cs`; `src/Trendo.App/MainWindow.W4Oss.cs`, `MainWindow.GitHubPlus.cs`, `MainWindow.Programme.cs` (nur Installer-Zweig), `MainWindow.CodeEditor.cs` (eine Zeile: Bauanleitung im Terminal), `MainWindow.W5CleanRoom.cs`; Render-Vorführung in `tools/Trendo.Render`.
**Fertig, wenn:**
- [x] echter Lauf Suche → Holen mit Prüfsumme → Starten → per PID beenden → Entfernen ohne Rest, Beleg unter `reports/T-0762/`
- [x] falsche Prüfsumme abgewiesen
- [x] Installer: Authenticode-Ampel, Ausführen nur mit eigenem Klick, Gelb/Rot zusätzlich mit ausdrücklicher Risiko-Bestätigung
- [x] geholtes Projekt im Code-Editor geöffnet und gebaut (wenn Werkzeug vorhanden)
- [x] Anforderungsliste: Tabelle, sicher gespeichert, Rückgängig, Export .md, Test-Verknüpfung, Abgleich erfüllt/teilweise/offen
- [x] Ähnlichkeitstreffer nebeneinander, Bericht mit Herkunft und Datum jedes Eintrags
- [ ] Volltest (macht die Hauptsitzung am Schluss)
**Nicht tun:** kein winget/msiexec, nichts außerhalb des Trendo-Datenordners installieren, keine GitHub-Anmeldung, kein fremder Code im Nachbau, kein Volltest, kein Merge.
**Am Ende:** Branch pushen, Bericht `tasks/T-0762-github-nachbau.bericht.md`.
