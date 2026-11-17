# T-0729: Temp-Ordner garantiert aufräumen

**Ziel:** Trendo (Produkt und Testläufe) lässt in %TEMP% nichts mehr liegen; verwaiste eigene Reste verschwinden beim nächsten Start.
**Branch:** claude/T-0729-temp-aufraeumen (Basis claude/T-0685-b1-integration)
**Befund (17.11.2026, %LOCALAPPDATA%\Temp):** über 100 GB seit 07.11. – 585 `trendo-chrome-*` (je 64–76 MB), über 150 000 leere `trendo-ui-*`,
tausende `trendo-ext-t*`, `trendo-s02/s03/s10*`, `trendo-web*`, `trendo-w7*`, `trendo-lo*`, `trendo-mui-*`; große Arbeitsordner `trendo-ue-e2e` (5,7 GB),
`trendo-ps2` (2,6 GB), `trendo-tools-b` (2,5 GB), `trendo-abbild-latest`/`-0.9.84` (je 1,7 GB); ein Hang-Dump (`dotnet test --blame-hang`, voller Abzug) mit 22 GB.
**Dateien/Bereich:** neue Hilfe `src/Trendo.Core/TempDir.cs`; Chrome-Vorschau, LibreOffice-Export, Texterkennung, Erweiterungen, Web-Mods; App-Start;
Testprojekte (ModuleInitializer, feste Prüfordner); `tests/Directory.Build.props`; Echtlauf-/Prüfskripte unter `tools/`.
**Fertig, wenn:**
- [x] jede Stelle, die Temp-Ordner anlegt, ist gelistet (Bericht)
- [x] eine gemeinsame Hilfe `TempDir` (using/try-finally, Löschen mit Wiederholung, Kindprozesse vorher beendet)
- [x] Aufräumen beim App-Start und beim Testlauf-Start: nur eigene Präfixe, älter als 24 h, nie `trendo-auftrag-*`, nie `%TEMP%\claude`, nie fremde, nie junge Ordner
- [x] Hang-Dumps: ohne ausdrückliche Angabe kein Abzug
- [x] Tests: typischer Ablauf hinterlässt nichts; Aufräumung mit Gegenprobe
**Nicht tun:** fremde Ordner löschen; Volltest; CLAUDE.md ändern.
**Am Ende:** Branch pushen, Bericht `tasks/T-0729-temp-aufraeumen.bericht.md`.
