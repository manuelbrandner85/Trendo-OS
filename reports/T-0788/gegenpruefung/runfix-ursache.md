# T-0788, 5. Prüfrunde – Ursache der ClaudeRunFix_Ui-Regression (Bisect, 28.11.2026)

Fehlerbild: `ClaudeRunFix_Ui.Back_into_program` scheitert in `Ui.Open` (`w.Show()`) mit „The calling thread cannot access this object because a different thread owns it.“, danach „Cannot re-show a closed window.“ (FensterAufraeumen). Einzeln grün.

## Messungen (dieser PC, Release-Build, Trendo.App.Tests.exe)
Paar `Silence_watchdog` + `Back_into_program` (zuerst ein [Fact] außerhalb der Oberfläche, dann der Oberflächen-Test):
| Stand | Ergebnis |
|---|---|
| main 9bc135aaa | 3 von 3 rot |
| f42783151 | 2 von 2 rot |
| 95e567750 | 2 von 2 rot |
| 7bb702189 | 2 von 2 rot |
| 1dbc638b6 | 2 von 2 rot |
| 97340c9c1 | 2 von 2 rot (auch mit `Result_ends_run_even_if_process_stays` statt `Silence_watchdog`: 2 von 2 rot) |
| neuer Stand (Pinsel unveränderlich) | 2 von 2 grün |

Ganze Klasse `-class Trendo.App.Tests.ClaudeRunFix_Ui`:
| Stand | Ergebnis |
|---|---|
| main 9bc135aaa | 1 von 5 Läufen rot |
| 56396d26d | 2 von 2 rot |
| 97340c9c1 | 6 von 6 rot (davor), im Rot-Lauf dieser Runde 2 von 3 rot |
| neuer Stand | 4 von 4 grün |

## Ursache
Die beiden [Fact]-Tests rufen `MainWindow.ClaudeRunProcess` (statisch) auf einem Thread außerhalb der Oberfläche auf. Das löst den statischen Aufbau von `MainWindow` dort aus – und damit auch statische, **veränderliche** Pinsel (`new SolidColorBrush(...)` in `MainWindow.Look.cs` `OnGold`/`OnGoldMuted`, `MainWindow.Shell.cs` `NavyGlass`/`NavyEdge`, `MainWindow.Windows.cs` `GoldEdge`). Diese Avalonia-Objekte gehören danach dem fremden Thread; das erste Fenster des Oberflächen-Tests greift darauf zu → „different thread owns it“. Ob die Klasse rot wird, hängt von der Reihenfolge ab, in der der Läufer die Tests ausführt (läuft der Oberflächen-Test zuerst, entstehen die Pinsel im richtigen Thread). Der Fehler steckt schon in main; meine Commits haben nur die Reihenfolge bzw. das Zeitverhalten verschoben. `ClaudeRoots()` ist nicht die Ursache – das Paar ist auch auf main und f42783151 (vor `ClaudeRoots`) rot.

## Behebung
Die fünf statischen Pinsel von `MainWindow` sind jetzt unveränderlich (`Brush.Parse` → `ImmutableSolidColorBrush`, ohne Thread-Bindung). Keine Wartezeiten im Test. Zusätzlich (Auftrag): `ClaudeRoots()` liest nur noch die vorhandenen Projektlisten (`ClaudeChats.AllFolders`) und wird nur bei Programmen mit `KiLocalOnlyDoc` gefragt – andere Programme zahlen beim Speichern nichts dafür.
