# T-0203 Bericht: Externe Programme mit der aktuellen Datei öffnen

**Auftrag:** Im Studio, in dem gearbeitet wird, öffnet ein Klick Blender, Unreal, VS Code, LibreOffice, OBS oder mpv mit der aktuellen Datei oder dem Projekt. Fehlt das Programm, fragt die bestehende Werkzeugprüfung einmal nach und richtet es erst nach Zustimmung ein.
**Branch:** `cursor/dateiauswahl-programme-430c`

## Geänderte Dateien

- `src/Trendo.App/MainWindow.Programme.cs` – Leiste „In … öffnen“ / „Einrichten“ / „Ja, einrichten“ / „Später“. Öffnen übergibt genau eine Datei oder einen Ordner.
- `src/Trendo.Core/Integrations.cs` – `OpenWithFile` prüft Programm und Datei und startet über `OpenWith` (kein Shell-Parsing).
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – nur die Öffnen-Leiste für Blender. Anlegen, Vorschau und Umwandeln sind unberührt.
- `src/Trendo.App/MainWindow.W4Game.cs`, `W4Dev.cs`, `W4Media.cs`, `Spec15.cs`, `W1Office.cs` – Unreal, VS Code, mpv, OBS, LibreOffice.
- `src/Trendo.App/MainWindow.W1Apps.cs` – Liste unter dem Reiter „Programme“: erkannte Programme mit „Starten“, sonst „Einrichten“.
- `src/Trendo.Core/Dependencies.cs`, `ToolCheck.cs` – VS Code (`Microsoft.VisualStudioCode`, Prüfung `--version`) und OBS (`OBSProject.OBSStudio`, nur Datei vorhanden, kein Start der Oberfläche). Unreal bleibt geführt, ohne winget.
- `tests/Trendo.App.Tests/ProgrammeUiTests.cs`, `UiTests.cs`, `W4GameUiTests.cs`, `tests/Trendo.Core.Tests/Tests.cs`, `DateiUndProgrammeTests.cs` – der Fake-Launcher merkt sich Programmpfad und Dateiargument getrennt und ruft weiterhin `Open` mit dem bisherigen Text auf.

„Später“ schreibt `deps.declined` und installiert nichts. „Ja, einrichten“ nutzt Zustimmung, Freigabe und `InstallOne`. Geführte Programme (Unreal) öffnen nur die offizielle Seite. Danach baut die Seite neu auf, ohne Neustart der App.

## Testergebnis

Dieselbe Ausgabe wie bei T-0201:

- `Trendo.Core.Tests`: Passed 420, Failed 0, Skipped 10, Total 430
- `Trendo.App.Tests`: Passed 140, Failed 1, Skipped 0, Total 141
- `Trendo.Api.Tests`: Passed 14, Failed 0, Skipped 0, Total 14

Der Fehler `SYS-03-P14` kommt von Chrome in dieser Linux-Umgebung (Prozess beendet sich nicht). Nicht von diesem Auftrag.

Neue Prüfungen: Fake-Blender, -Unreal, -VS-Code, -LibreOffice, -OBS und -mpv bekommen die Modelldatei, die `.uproject`, den Projektordner, das Dokument, den Sendungsordner und die Mediendatei. Vor „Ja, einrichten“ ist die Aufrufliste leer. „Später“ installiert nicht. Nach „Ja“ steht winget mit `OBSProject.OBSStudio` in der Liste, und „In OBS öffnen“ ist auf derselben Seite aktiv. In der Programmliste startet „Starten“ Blender mit der Modelldatei.

## Offene Punkte

- Die Seitenüberschrift der Liste bleibt „Apps“, weil ein bestehender Test genau das erwartet. Der Reiter heißt „Programme“, der Knopf „Starten“.
- Keine echte Installation und kein echter Start von Blender, Unreal, VS Code, LibreOffice oder OBS. Unreal wird nicht per winget installiert.
- Nicht `PC_VERIFIED`. QA-017, QA-019 und Codesignatur sind nicht geprüft und nicht als bestanden markiert.
- Logo und die innere 3D-Bearbeitung wurden nicht angefasst.

## Nicht geprüft

Echtes Öffnen der Programme auf einem Windows-PC, ob der Epic-Launcher nach der geführten Einrichtung ohne Neustart erkannt wird, ob winget die Pakete wirklich installiert.
