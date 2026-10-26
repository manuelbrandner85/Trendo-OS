# Bericht T-0226 / T-0227 / T-0228: Programme verwalten, KI-Bibliothek, ruhigere Studio-Oberfläche

**Auftrag:** Vorgaben vom 26.10.2026:
- Deinstallation entfernt alle Programme mit
- GitHub-Programme installieren und einzeln entfernen
- Vollbild-Einrichtung
- Logo „Saphir-Kristall“
- ausführliche Beschreibungen zu jedem Programm
- KI-Modelle nach Arten, wirklich integriert
- schönere, verständlichere Oberfläche je Studio
- kein fester Nutzer im Produkt

**Branch/Worktree:** `claude/T-02xx`, Worktree `Trend Os-worktrees/integration`

## Geänderte Dateien (Auszug)
- **Logo:**
  - `src/Trendo.App/TrendoLogo.cs` (eine Quelle für Programm-Logo, SVG und App-Symbol)
  - `Assets/logo/*`
  - `scripts/make-logo.py`
  - Render-Werkzeug mit `TRENDO_LOGO`
- **Deinstallation:**
  - `src/Trendo.App/UninstallPrompt.cs` (Werkzeugordner immer entfernen, in eigenem Prozess)
  - `DataLocation.RemovableTools`
- **Programme:**
  - `src/Trendo.Core/GitHubApps.cs`
  - `src/Trendo.Core/ProgramGuide.cs`
  - `Vollpaket.Uninstall`
  - `src/Trendo.App/MainWindow.Programme.cs`
  - `MainWindow.W4Oss.cs`
- **Einrichtung:**
  - `src/Trendo.App/MainWindow.InstallOverlay.cs`
  - `MainWindow.Setup.cs` (Knopf ausgegraut, Vollbild)
- **KI-Bibliothek:**
  - `src/Trendo.Core/W2/KiLibrary.cs`
  - `KiLibraryStore.cs`
  - `src/Trendo.App/MainWindow.KiLibrary.cs`
- **Studio-Oberfläche** (gilt für alle Studios):
  - Fensterhintergrund deckend statt durchscheinend (`GlassStrong` 95 %)
  - KI-Auftragsleiste als eine ruhige Zeile; Stand, Vorschau, Rückgängig, Abbrechen und Profi erst bei Bedarf (`MainWindow.PromptAction.cs`)
  - Kopfkarte mit großem Studio-Symbol (`PageHead`)
  - Zuletzt benutzte Medien als Kartenreihe
- **Kein fester Nutzer:**
  - Namen in Kommentaren, Tests, Design-Entwürfen und im Linux-Skript neutralisiert
  - Test `NoFixedUserTests`
  - Regel in `CLAUDE.md`
- **Neue Tests:**
  - `GitHubAppsTests`
  - `KiLibraryTests`
  - `NoFixedUserTests`
  - `GitHubProgramsUiTests`
  - `KiLibraryUiTests`
  - `SetupOverlayTests`
  - erweitert: `LogoTests`, `UninstallPromptTests`

## Testergebnis
Volltest (Release): Build 0 Warnungen/0 Fehler · Api 15/15 · Core 714 + 1 übersprungen · App 347 + 2 übersprungen · Paket-Tests OK · Exit 0. Details: `docs/build-log/2026-10-26-programme-ki.md`.

## Belege
`reports/t0226-programme/`:
- `logo-b-512.png`
- `einrichtung-vollbild.png`
- `ki-bibliothek.png`
- `studio-vorher.png` und `studio-nachher.png`

## Nicht geprüft
- **KI-Modelle:** Nicht echt am PC geladen und ausgeführt; das sind mehrere GB, für Video braucht es eine Grafikkarte. Getestet sind Prüfung, Ablage und Bedienung, mit Test-Dateien.
- **GitHub-Installation:** Nur mit einem Test-ZIP geprüft, nicht mit einem echten Projekt.
- **Deinstallation:** Das Löschen im eigenen Prozess nach dem Velopack-Hook ist nicht am PC durchlaufen.
- **Musik-KI (ACE-Step):** Nur im Katalog sichtbar, noch nicht eingebaut (Stufe 2).

## Status
- Umgesetzt und automatisch getestet.
- Prüfung am PC steht aus.
