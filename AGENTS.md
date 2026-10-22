# AGENTS.md – Verbindliche Regeln für Grok Build im Trendo-OS-Projekt

Grok Bot ist Dispatcher und Reviewer. **Grok Build (dieser Agent) schreibt den Code** auf dem Windows-PC.
Aufträge kommen als Datei aus `tasks/` (siehe `tasks/README.md`). Antworte und schreibe UI-Texte auf Deutsch.

## Projekt
- Trendo OS: Desktop-/Studio-Oberfläche (.NET 10, Avalonia) mit lokalem KI-Assistenten.
- Code: `src/` (Trendo.Core, Trendo.App, Trendo.Api, Trendo.Companion), Tests `tests/`, Spezifikation `docs/spec/` (Kataloge `docs/spec/kataloge/`), Status `requirements.yaml`, Belege `reports/`, Build-Log `docs/build-log/<datum>-<modul>.md` (eine Datei pro Eintrag).
- Bauen/Testen (PowerShell): `dotnet build Trendo.sln -c Release` und `dotnet test tests/Trendo.Core.Tests -c Release` (ebenso `Trendo.App.Tests`, `Trendo.Api.Tests`). Die Testausgabe ist deutsch („Bestanden!/Fehler!“).

## Arbeitsweise (Pflicht)
1. Nur PowerShell. Pfade immer in Anführungszeichen (der Projektordner enthält ein Leerzeichen).
2. Nie auf `main` committen oder pushen. Pro Auftrag einen Branch `grok/<task-id>-<kurz>` anlegen, kleine Commits, am Ende `git push -u origin <branch>`. Kein `--force`, kein Rebase bereits gepushter Commits.
3. Vor dem Push alle drei Testprojekte laufen lassen; nur pushen, wenn alles grün ist. Wenn etwas rot bleibt: nicht pushen, Ursache berichten.
4. Kleine, gezielte Änderungen; nichts umbauen, was der Auftrag nicht verlangt. Andere Builder arbeiten parallel – nur die im Auftrag genannten Dateien/Bereiche anfassen.
5. Nach Abschluss einen Bericht nach `tasks/<task-id>.bericht.md` schreiben (siehe unten) und mitcommitten.
6. Wird ein Befehl abgelehnt oder bräuchte er eine Freigabe, nicht warten und nicht wiederholen. Stattdessen die erlaubte Form nehmen (z. B. einfach `git commit -m "…"`; Name/E-Mail sind im Repo gesetzt, kein `git -c`). Sonst sofort aufhören und im Bericht unter „Offene Punkte“ den abgelehnten Befehl nennen.
7. Terminalbefehle immer einzeln und einfach: keine Pipes (`|`), kein `;`, kein `&&`, keine Umleitungen außer bei `dotnet test`, keine Variablen. Nicht erlaubte oder verkettete Befehle beenden die Sitzung sofort (dontAsk). Dateien lieber mit read_file/grep auswerten.

## Verboten
- Admin-Rechte, systemweite Installationen, `winget`/`msiexec`/`choco`, Registry-Änderungen, Dienste, Treiber, Firmware, Datenträger, Windows-Einstellungen.
- Blender, Unreal oder Visual Studio deinstallieren/ändern.
- Secrets in Dateien/Commits; Benutzernamen, Rechnernamen oder Pfade mit Benutzernamen im öffentlichen Repo (in Logs durch `C:\Users\<user>` / `<pc>` ersetzen).
- Nachrichten senden, etwas kaufen, Formulare absenden, Releases erstellen (Releases macht nur Grok Bot aus grünem `main` über `installer/release.sh`).
- Dateien außerhalb dieses Repos ändern oder löschen; Ausnahme: eigene Temp-Ordner `%TEMP%\trendo*`, die du am Ende aufräumst.

## Fachregeln
- Status ehrlich: `APPROVED_SPEC` ist nicht `IMPLEMENTED`. `TESTED`/`PC_VERIFIED` nur mit Rohlog + SHA-256 unter `reports/`. QA-017, QA-019 und Codesignatur nie als bestanden markieren.
- Das Logo bleibt ein nicht freigegebenes Konzept. SPEC-07 ist abgelehnt – nicht bauen.
- Kostenpflichtige KI-APIs bleiben optional; Trendo darf sie nie voraussetzen.
- UI: höchstens 2 Navigationsebenen, deutsche, alltagsnahe Beschriftungen. Studio-Namen kommen zentral aus `src/Trendo.App/StudioCatalog.cs`.
- W1-Issues (#14, #15, #17–#22) und W7-Issues (#64–#66) bleiben offen, bis die Module fertig sind.

## Bericht (`tasks/<task-id>.bericht.md`)
Auftrag · Branch · geänderte Dateien · Testergebnis je Projekt (Zahlen aus der Ausgabe) · offene Punkte/Risiken · was du NICHT geprüft hast.
