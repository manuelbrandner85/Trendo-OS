# T-0109: Neues Trendo-Logo A „Obsidian-Kachel“ überall statt V-Logo

**Ziel:** Das alte V-Konzeptlogo wird überall durch das von Manuel am 22.10.2026 gewählte Logo A ersetzt.
**Branch:** claude/T-0109-logo-a
**Quelle (verbindlich, nicht verändern):** `src/Trendo.App/Assets/logo/trendo-logo-a.svg`
(viewBox 64×64: dunkle Kachel rx 15 mit Verlauf #1B3150→#060D1A und Rahmen #335578, weißer Querbalken #F0F6FF, Stamm mit Verlauf #7FD8FF→#2A7FFF).

**Dateien/Bereich:**
- `src/Trendo.App/MainWindow.axaml.cs`: `Logo(...)` (V-Pfad `M0,0 L100,0 L50,90 Z …`) durch Avalonia-Nachbau von Logo A ersetzen (Kachel + Querbalken + Stamm, gleiche Farben). Tooltip/Status-Texte „Logo-Konzept – nicht freigegeben“ / „Logo: Konzept, nicht freigegeben“ / „Logo: Entwurf, noch nicht freigegeben“ (auch `MainWindow.Look.cs`) ersetzen durch „Trendo-Logo“ bzw. entfernen.
- Startknopf `start-button` in `src/Trendo.App/MainWindow.StartMenu.cs`: statt `Mdi.MicrosoftWindows` das Logo A (nur diese eine Zeile ändern, sonst nichts am Startmenü).
- App-Symbol: **liegt fertig vor** (nicht neu erzeugen, kein Python/ImageMagick nötig): `src/Trendo.App/Assets/logo/trendo.ico` (16, 20, 24, 32, 40, 48, 64, 128, 256 px), `trendo-32.png`, `trendo-256.png`, `trendo-512.png`; Erzeugung reproduzierbar mit `scripts/make-logo.py` (läuft auf dem Linux-Testrechner). In `Trendo.App.csproj` `<ApplicationIcon>Assets\logo\trendo.ico</ApplicationIcon>` setzen und das .ico als AvaloniaResource einbinden; `Icon` des Hauptfensters (Fenster- und Taskleistensymbol) darauf setzen (`new WindowIcon(AssetLoader.Open(new Uri("avares://…/Assets/logo/trendo.ico")))`).
- Setup/Velopack: `installer/build-setup.sh` um `--icon src/Trendo.App/Assets/logo/trendo.ico` ergänzen.
- README.md und docs: Logo A einbinden (README oben, `trendo-256.png`), Hinweise auf das V-/Konzeptlogo aktualisieren (`Dependencies.cs` Eintrag „Hintergrund + Logo-Konzept“ → „Hintergrund (KI-generiert) + Trendo-Logo (eigener Entwurf)“). THIRD_PARTY/Lizenz: Logo ist eigenes Werk von Trendo OS.

**Fertig, wenn:**
- [ ] Neuer Test (App.Tests), z. B. `LogoTests`: kein Quelltext unter `src/` enthält mehr den V-Pfad `L50,90` oder „Logo-Konzept“/„nicht freigegeben“ zum Logo; `trendo.ico` existiert und enthält mindestens 16, 32, 48 und 256 px; das Hauptfenster hat ein `Icon`; `start-button` zeigt das Logo (kein `MicrosoftWindows`-Pfad).
- [ ] Logo ist bei 16 px erkennbar (Querbalken und Stamm nicht verschwommen) – Kontrollbild im Bericht beschreiben.
- [ ] QA-019 und W0b-01 (verbotene Wörter) weiterhin grün.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht).

**Arbeitsweise:** Jeden Befehl einzeln ausführen (keine Ketten mit `;`/`&&`/`|`), nur die in `.mcp.json` erlaubten Befehle (git, dotnet build/test, rg, Get-ChildItem/Get-Content). Dateien mit dem Edit-Werkzeug ändern.
**Nicht tun:** Die SVG nicht umgestalten; kein anderes Design-41-Thema (Farben, Fensterverwaltung, Startmenü-Inhalt) anfassen; keine Status-Werte auf PC_VERIFIED setzen; nicht auf main pushen; nichts aus den KI-Referenzbildern übernehmen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0109-logo-a.bericht.md`.
