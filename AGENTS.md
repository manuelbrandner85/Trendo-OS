# AGENTS.md – Verbindliche Regeln für Grok Build im Trendo-OS-Projekt

Grok Bot ist Dispatcher und Reviewer. **Grok Build (dieser Agent) schreibt den Code** auf dem Windows-PC.
Aufträge kommen als Datei aus `tasks/` (siehe `tasks/README.md`). Antworte und schreibe UI-Texte auf Deutsch.

## Projekt
- Trendo OS: Desktop-/Studio-Oberfläche (.NET 10, Avalonia) mit lokalem KI-Assistenten.
- Code: `src/` (Trendo.Core, Trendo.App, Trendo.Api, Trendo.Companion), Tests `tests/`, Status `requirements.yaml`, Build-Log `docs/build-log/<datum>-<modul>.md` (eine Datei pro Eintrag). Spezifikation und Belege nicht vorladen, siehe unten.
- Bauen/Testen (PowerShell): `dotnet build Trendo.sln -c Release` und `dotnet test tests/Trendo.Core.Tests -c Release` (ebenso `Trendo.App.Tests`, `Trendo.Api.Tests`). Die Testausgabe ist deutsch („Bestanden!/Fehler!“).

## Spezifikation nur bei Bedarf
Volltexte unter `docs/spec/` nicht vorladen und nicht in den Kontext kopieren. Fachvorgaben, wenn der Auftrag sie braucht, zuerst aus `docs/spec/KURZFASSUNG.md`. `MASTERPROMPT.md`, `SYSTEMBIBEL.md` und `design-41/GROK_BUILD_DESIGN_41_MASTERPROMPT.md` nur öffnen, wenn die Kurzfassung nicht reicht, und dann nur diese eine Datei. `reports/` bleibt im Git und steht in `.cursorignore`.

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
- Logo: Entwurf A „Obsidian-Kachel“ (von Manuel am 22.10.2026 gewählt), Quelle `src/Trendo.App/Assets/logo/trendo-logo-a.svg`. SPEC-07 ist abgelehnt – nicht bauen.
- Kostenpflichtige KI-APIs bleiben optional; Trendo darf sie nie voraussetzen.
- UI: höchstens 2 Navigationsebenen, deutsche, alltagsnahe Beschriftungen. Studio-Namen kommen zentral aus `src/Trendo.App/StudioCatalog.cs`.
- W1-Issues (#14, #15, #17–#22) und W7-Issues (#64–#66) bleiben offen, bis die Module fertig sind.

## Bericht (`tasks/<task-id>.bericht.md`)
Auftrag · Branch · geänderte Dateien · Testergebnis je Projekt (Zahlen aus der Ausgabe) · offene Punkte/Risiken · was du NICHT geprüft hast.

## Cursor Cloud specific instructions

Cloud-Agenten laufen auf Linux. Die PowerShell-Regeln weiter oben gelten für Grok Build auf dem Windows-PC. Auf dem Cloud-Agenten sind normale Shell-Befehle in Ordnung.

- .NET SDK 10 liegt unter `/usr/share/dotnet`; `dotnet` ist über `/usr/local/bin` erreichbar. `poppler-utils` und `python3-yaml` sind vorinstalliert.
- Bauen und testen wie in der README: `dotnet build Trendo.sln -c Release` und `dotnet test Trendo.sln -c Release`.
- Lokale API prüfen: `dotnet run --project src/Trendo.App -c Release -- --api-selftest --offline --no-first-run --data-root /tmp/trendo-api`.
- Oberfläche ohne Fenster: `dotnet run --project tools/Trendo.Render -c Release -- /tmp/trendo.png`. Das Fenster auf dem vorhandenen Display: `dotnet run --project src/Trendo.App -c Release -- --no-first-run --page start`.
- `SYS-03-P14` (Seite als PDF, A4) schlägt auf diesem Image fehl, sobald `google-chrome` im PATH liegt. Das Image-Chrome druckt headless im Format US Letter (612×792 pt) und beendet sich nach `--print-to-pdf` nicht von selbst. Der Fehlschlag kommt vom Papierformat dieses Chrome. Die App bleibt unverändert.
