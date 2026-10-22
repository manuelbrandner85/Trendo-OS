# T-0044: Paket 44, Fachprogramme nur bei Bedarf

**Auftrag:** Fachprogramme nicht vorinstallieren und nicht dauerhaft laden. Eine gemeinsame Verbindung zum Erkennen, Anbinden und Aufrufen, plus eine Prüfung mit ehrlichem Stand.
**Branch:** `cursor/paket-44-fachprogramme-a21a`
**Grundlage:** `docs/spec/MASTER_BIS_94.md` auf dem offenen Branch `cursor/master-bis-94-5d61` (nicht gemergt). Zeile 44: „Fachprogramme wie Blender/Unreal nur bei Bedarf über geprüfte optionale Adapter integrieren.“ Die zehn Einzelnamen stehen dort nicht. Sie wurden nicht erfunden. Der Katalog hier sind die 11 schweren Programme, die schon in `DependencyCatalog` stehen.

## Geänderte Dateien

- `src/Trendo.Core/Fachprogramm.cs`
- `src/Trendo.App/MainWindow.Programs.cs`
- `tests/Trendo.Core.Tests/FachprogrammTests.cs`
- `tests/Trendo.App.Tests/FachprogrammUiTests.cs`
- `docs/build-log/2026-10-22-paket-44-fachprogramme.md`

## Testergebnis

`dotnet test Trendo.sln -c Release` am 22.10.2026, vor der letzten Umordnung der Knöpfe unter den Text:

- Api: 15 bestanden, 0 Fehler
- Core: 536 bestanden, 10 übersprungen (`WinRealTests`), 0 Fehler
- App: 224 bestanden, 0 Fehler

Danach nur die Seite „Externe Programme“: Knopf unter den Text gelegt, Block nach oben. Erneuter Lauf `FullyQualifiedName~Paket44|FullyQualifiedName~T0203`: 7 bestanden, 0 Fehler. Die Gesamtsuite ist danach nicht noch einmal gelaufen.

## Offene Punkte

- Die alten Knöpfe „Starten“ und „Einrichten“ auf derselben Seite und in den Studios bleiben. Sie gehen nicht durch die neue Prüfung. 3D und Spiele (#251, #253) sind nicht umgehängt.
- LibreOffice kann im Einrichtungs-Assistenten weiter vorausgewählt sein. Das ist der alte Stand. Dieser Auftrag installiert dort nichts zusätzlich.
- Kein Programm aus dem Katalog braucht einen Schlüssel. Der Secret-Store wird nur gelesen, wenn ein Eintrag das ausdrücklich verlangt. Im mitgelieferten Katalog ist das bei keinem der Fall.
- Paket 44 ist damit nicht fertig im Sinn des Masters. Der Stand bleibt geplant, nicht abgenommen.

## Nicht geprüft

- Kein Blender, kein Unreal, kein LibreOffice, kein Krita, kein Inkscape, kein Kdenlive, kein Audacity, kein OBS, kein COLMAP, kein Meshroom, kein VS Code auf diesem Image. `ffmpeg` liegt unter `/usr/bin/ffmpeg` und gehört nicht zu diesem Katalog. Eine echte Programmdatei wurde hier nicht gestartet.
- Kein Windows-Lauf, nicht der PC DESKTOP-9JVIIPU.
- Kein winget, kein Download, keine Herstellerseite geöffnet.
- QA-017, QA-019 und die Codesignatur sind nicht bestanden.
- #252 KI-Laden ist nicht angefasst. 23, 25, 34, 86 und Language Studio 2.0 bleiben aus. 35 ist nicht aktiviert.
