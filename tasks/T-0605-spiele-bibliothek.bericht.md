# Bericht T-0605: Spiele-Bibliothek im Konsolen-Studio

**Zweig:** `claude/T-0605-spiele-bibliothek`, Worktree `spiele-bibliothek`. Nicht auf `main`.

## Inhalt

- Bibliothek als Startseite des Konsolen-Studios: 15 Einträge mit eigenem Cover und Beschreibung, Filter nach Art. Der Disc-Weg bleibt erreichbar.
- 8 eigene Spielideen, die Claude mit Godot baut, darunter eine Open-World-Stadt. Wunschfeld für frei beschriebene Spiele.
- 5 Godot-Demoprojekte (MIT, fester Stand `6ad6167e`), geholt per Git-Teilabruf.
- 2 Homebrew-Spiele (Libbet, µCity) im Emulator mGBA 0.10.5, alle Downloads mit fester SHA-256.
- Ändern per Text und Export-Auftrag für Windows, Linux, Mac, Android und Browser.
- Geänderte Dateien: `GameLibrary.cs`, `MainWindow.GameLibrary.cs`, `MainWindow.ConsolePort.cs`, `StudioProfiles.cs`, `THIRD_PARTY.md`, `tools/spiele_cover.py`, `Assets/bibliothek/`, zwei Testdateien.

## Grenze

Bestehende kommerzielle Spiele werden nicht nachgebaut, auch nicht umbenannt oder leicht verändert. Aus einem Wunsch übernimmt Claude nur Genre und Spielmechaniken, die Regel steht in jedem Auftrag und im Profil des Studios. PlayStation-Homebrew fehlt, weil es ein Konsolen-BIOS bräuchte. SuperTuxKart und OpenTTD fehlen, weil ihre Quellpakete ohne getrennte Grafikdaten nicht laufen.

## Testergebnis

Volltest auf `3ff83a4f0`, alle Exitcodes 0: Api 16 bestanden; Core 2151 bestanden, 5 übersprungen; App 949 bestanden, 4 übersprungen; Paket-Tests 9 OK. Ein vorheriger Lauf war rot: `QA-019`, weil der Download `HttpClient` im Kern nutzte. Behoben über `IWebFetch`.

## Nicht geprüft

- Echter Abruf und Start einer Godot-Demo: Die Rechteprüfung hat die Ausführung von Code aus dem Netz abgelehnt. Git, Godot und Emulator sind in den Tests nachgebildet.
- Echter Start von mGBA und der Homebrew-Spiele.
- Bau eines eigenen Spiels durch Claude und Qualität des Ergebnisses.
- Export auf andere Geräte.
- Keine Abnahme durch eine Person. Die Cover wurden lokal erzeugt und per Sichtprüfung kontrolliert, zwei wegen Emblem und Schriftzug ersetzt.

## Status

Fertig zur Prüfung. Merge nach `main` nur nach OK.
