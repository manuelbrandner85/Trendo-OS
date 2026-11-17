# T-0724 – Abnahme Phase E: Echtläufe, Installation, Update, Deinstallation (16.11.2026)

PC: AMD Ryzen 7 5700X (8 Kerne), 32 GB RAM, Windows 11 Pro 10.0.26340. Alle Läufe echt, nichts simuliert. Rohlogs in den Unterordnern, Prüfsummen in `SHA256SUMS`.

## 1 Setup-Rundlauf (abgeschottet) – `setup/`
Skript `scripts/setup-rundlauf-portabel.ps1`, Ergebnis **ALLE BESTANDEN** (`setup/setup-rundlauf-portabel.log`).

| Schritt | Ergebnis |
|---|---|
| Pakete bauen (vpk, eigene Kennung `TrendoOSRundlauf`, 0.0.1 und 0.0.2) | Portable-ZIP 119 MB, Update-Paket 119 MB |
| Installieren = Portable-ZIP auspacken nach `%TEMP%\trendo-t0724-setup\installiert` | 421 Dateien, 262 MB, Version 0.0.1 |
| Start: `--api-selftest --offline --no-first-run --data-root <temp>` | Exit 0 (alt und neu) |
| Update: `Update.exe apply --norestart --package <0.0.2-full.nupkg>` | Exit 0 nach 11,1 s, Version 0.0.2, App nicht selbst gestartet |
| Daten erhalten (eigene Notiz + 6 Dateien der App im Temp-Datenordner) | 0 fehlend, 0 verändert; Notiz auch nach Start der neuen Version gleich |
| Deinstallieren = Programmordner entfernen | entfernt; erlaubter Rest nur der eigene Temp-Datenordner (danach gelöscht) |
| Echtes System (vorher/nachher, nur gelesen) | HKCU-Uninstall-Einträge, Startmenü, Desktop, `%LOCALAPPDATA%` (Trendo/Velopack), `%APPDATA%` (Trendo), `%APPDATA%\TrendoOS` (37 806 Dateien, Größe + Änderungszeit) unverändert |

**BLOCKIERT: Setup.exe-Weg.** Velopacks `Setup.exe` schreibt immer einen Deinstallations-Eintrag in die Registry (HKCU) und startet die App nach der Installation ohne Argumente mit dem echten Datenordner `%APPDATA%\TrendoOS`; abschalten lässt sich das nicht. Damit sind Registry-Eintrag, Install-Hook (`VollpaketStart.RememberFromInstaller`), Deinstallations-Hook (`UninstallPrompt`, Datenfrage) und der Startwächter nach Setup NICHT geprüft. Voraussetzung: eigener Windows-Testbenutzer oder VM, dann `scripts/setup-rundlauf.ps1 -Ausfuehren`.

## 2 Unreal-Echtlauf – `unreal/` und `../echtlauf/2026-11-16/unreal.log`
Neuer Fachlauf „unreal“ der Echtlauf-Station (`--langsam`): Trendo legt wie „Bauen“ ein C++-Spiel (3D-Level) im eigenen Temp-Ordner an (`GameDesign.BuildUnreal`), dann genau die Schritte von „Kompilieren und prüfen“. UE 5.8 (CL 58902052):
- Kompilieren (Build.bat, 14 Aktionen): Exit 0, `Result: Succeeded`, 85,9 s
- Unreal-Tests (UnrealEditor-Cmd, Automation RunTests Trendo): 3 von 3 bestanden (Daten, Regeln, Spielstand), 45 s
- Startlauf (`-game -trendocheck -nullrhi`): TRENDO-OK – Welt=level, Objekte=13, Gegner=1, Figur=ja, Boden=ja, 14 s
Zeitgrenzen 30/15/15 min (wie in der App), nicht erreicht. Unreal selbst und Projekte des Nutzers unberührt; UBT schreibt wie immer sein eigenes Protokoll nach `%LOCALAPPDATA%\UnrealBuildTool`. Projektordner (3,1 GB) danach gelöscht.
Nicht geprüft: Open World (lädt 275 MB Fotomaterial), MetaHuman, Startlauf mit Grafik/Bild, gepacktes Spiel.

## 3 Echtlauf-Station vollständig – `../echtlauf/2026-11-16/`
`python tools/echtlauf/echtlauf.py --langsam`: 17 Werkzeuge, 15 ECHT-GRÜN, 2 ECHT-GRÜN (teilweise) – Chrome (pdfinfo fehlt) und Tesseract (pdftoppm fehlt). Claude war im ersten Lauf ROT („5-hour limit“ des Abos erreicht) und wurde nach dem Zurücksetzen mit `--nur claude` wiederholt: ECHT-GRÜN (Antwort 42). Ollama diesmal ECHT-GRÜN (Antwort 42; im Lauf von 02:37 antwortete das Modell „32“).

## 4 Leistung (grob, ohne Ziele) – `leistung/` und `setup/*-start.txt`
App-Start (installiertes Paket, eigener Datenordner, Zeit bis erstes Bild laut `--startup-log`):
| Lauf | window.opened | first.frame |
|---|---|---|
| 0.0.1 kalt (erster Start nach Auspacken) | 11 460 ms | 11 510 ms |
| 0.0.1 warm | 1 938 ms | 1 990 ms |
| 0.0.2 erster Start nach Update | 2 185 ms | 2 228 ms |
| 0.0.2 warm | 1 475 ms | 1 515 ms |
Der kalte Wert enthält das erste Laden der frisch ausgepackten 262 MB (Virenscanner, Dateicache); der Selbsttest davor brauchte 23,8 s, nach dem Update 3 s.

Großes Projekt öffnen über die echte Oberfläche (Avalonia Headless, `LeistungT0724UiTests`, 3 Läufe):
| Projekt | Datei lesen/Import | bis Ansicht aufgebaut |
|---|---|---|
| Tabelle 1000 × 100 = 100 000 Zellen (XLSX 280 KB) | 455 / 312 / 262 ms | 959 / 694 / 584 ms |
| 3D-Szene 40 000 Dreiecke (OBJ 1 MB) | 862 / 578 / 464 ms | 3 041 / 2 330 / 2 169 ms |
Headless heißt: ohne GPU-Ausgabe; im sichtbaren Fenster nicht gemessen.
