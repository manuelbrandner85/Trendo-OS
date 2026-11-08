# Bericht T-0502: OBJ- und Excel-Import schneller

**Auftrag:** Die langsamsten Importe aus der Messung vom 08.11.2026 in C# beschleunigen, ohne das Ergebnis zu verändern.
**Branch/Worktree:** `claude/T-0502-import-tempo`, Worktree `Trend Os-worktrees/T-0502-import-tempo`, Basis `origin/main` 023a6d742.

## Geänderte Dateien
- `src/Trendo.Core/MeshIO.cs`: OBJ-Zeilen werden ohne Zwischen-Strings zerlegt (`ReadObjPlain`, `ReadObjParts`).
- `src/Trendo.Core/Office/SheetFiles.cs`: Zeilen und gemeinsame Texte werden als Datenstrom gelesen statt als XDocument. Jeder gemeinsame Text wird nur einmal auf Zahl/Datum geprüft.
- `src/Trendo.Core/Office/OfficeZip.cs`: neues `ReadXml` mit denselben Schutzregeln wie `LoadXml`; `LoadXml` nutzt es selbst.
- `tests/Trendo.Core.Tests/ImportTempoTests.cs`: 6 neue Tests.

## Messung vorher/nachher
Median aus 5 Läufen, drei Durchgänge im Wechsel, Windows, Ryzen 7 5700X, Release-Build, PC nicht leerlaufend.

| Import | Größe | Vorher | Nachher | Zwischenspeicher vorher | nachher |
|---|---|---|---|---|---|
| OBJ (`ReadMeshFile`) | 1 Mio. Dreiecke, 122 MB | 1094–1144 ms | 797–824 ms | 1821 MB | 742 MB |
| Excel (`ReadXlsx`) | 1 Mio. Zellen | 2226–2385 ms | 1441–1463 ms | 854 MB | 576 MB |

Der Zwischenspeicher ist die Summe aller Anforderungen eines Laufs, nicht der Spitzenwert im Arbeitsspeicher.

## Gleichheit der Ergebnisse
Alte Fassung (`origin/main`) und neue Fassung lesen dieselben Dateien, verglichen wird eine Textausgabe aller Daten: **byte-identisch**. Abgedeckt sind eine große OBJ- und XLSX-Datei, eine mittlere OBJ für den Teile-Import und je eine Grenzfall-Datei (Rich-Text, Lautschrift, CDATA, Inline-Text, Fehlerwerte, Präfix-Namensraum, fehlendes Blatt, zweiter Datenabschnitt, kaputte Flächen, negative Indizes).

## Kommandos und Testergebnis
- `dotnet test Trendo.sln -c Release`, erster Lauf: Api 16/16; App 871 bestanden, 4 übersprungen von 875 (38 min); Core 2 Fehler von 1890, Exitcode 1.
  - `SYS-13-P21` (Telemetrie-Wächter): von dieser Änderung verursacht, „Sentry“ im Variablennamen `wsEntry`. Umbenannt.
  - `assimp echt über TrendoOS --assimp-convert`: im Volltest rot, danach einzeln und im kompletten Core-Lauf grün. Vermutlich Last durch den parallelen Lauf, Ursache nicht geklärt.
- Nach der Umbenennung: `dotnet test tests/Trendo.Core.Tests -c Release`: Fehler 0, erfolgreich 1885, übersprungen 5, gesamt 1890. `dotnet test tests/Trendo.Api.Tests -c Release`: Fehler 0, erfolgreich 16, gesamt 16.
- `python -m unittest discover -s tests/claude_handoff`: 9 Tests, OK, Exitcode 0.

## Was NICHT geprüft wurde
- `Trendo.App.Tests` liefen vor der Umbenennung einer lokalen Variable und wurden danach nicht wiederholt (38 min).
- Claude-QA-Review steht aus.
- Keine Messung auf dem Pi 4 oder unter Linux.
- Spitzenwert des Arbeitsspeichers nicht gemessen.
- Echte Excel- oder OBJ-Dateien anderer Programme nur über die vorhandenen Tests, die Vergleichsdateien sind erzeugt.

## Offene Punkte/Risiken
- Reihenfolge der Zellen im internen Wörterbuch ändert sich bei Zellen mit Notiz (Notizen werden jetzt nach den Zeilen gelesen). Der Inhalt bleibt gleich.
- Merge und Release nur nach OK.

**Status:** Umsetzung fertig, Branch gepusht, Merge offen.
