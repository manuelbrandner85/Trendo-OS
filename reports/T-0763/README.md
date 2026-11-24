# T-0763 – Belege

Alle Pfade ohne Benutzernamen (`C:\Users\<user>`). Prüfsummen: `SHA256SUMS.txt`.

| Datei | Inhalt |
|---|---|
| `lastprobe.txt` | X-09 Lastprobe (echt, dieser PC, 16 Kerne): Prozessorlast je Prozess aus Trendo (`TaskManager.Rows`) gegen die Windows-Leistungsindikatoren `\Process(*)\% Processor Time` (pdh.dll, ohne Administratorrechte). Last 1: PowerShell-Endlosschleife (1 Kern), Last 2: Testprozess mit 8 Rechenfäden. Abweichung 0,00 bzw. 0,18 Prozentpunkte. Hinweis: Vergleich mit den Leistungsindikatoren, nicht mit dem Bild des Windows-Task-Managers (der zeigt „Processor Utility“, das bei Turbo-Takt abweichen kann). |
| `installation-echt.txt` | X-02 echte Installation dieses PCs (Velopack, Trendo 0.9.108): nur lesend gegen das Paket `packages\TrendoOS-0.9.108-full.nupkg` geprüft (418 Dateien, 0 Abweichungen, 1,1 s). Auf einer Kopie (Programmordner + Paket) `Trendo.Core.dll` gelöscht und ein Byte in `TrendoOS.deps.json` geändert → beide erkannt und aus dem Paket ersetzt → 0 Abweichungen. Erster Lauf fand „Squirrel.exe“ und „Trendo OS_ExecutionStub.exe“ als fehlend – die legt Velopack bewusst nicht in den Programmordner; seitdem ausgenommen. |
| `core-tests-rohlog.txt` | Rohlog der 9 neuen Core-Tests (`T0763SystemAlltagTests`) mit `TRENDO_BELEG_DIR` |
| `app-tests-lauf1-rohlog.txt` | 62 App-Testklassen, die die geänderten Seiten berühren (323 Tests, 1 Fehler: T-0736 erwartete „Leistung messen“ als Startseite von SYS-13 – angepasst, siehe Bericht) |
| `app-tests-lauf2-rohlog.txt` | Wiederholung der betroffenen Klassen nach der Anpassung (59 Tests, 0 Fehler) |
| `bilder/sys13.png` | „Alles prüfen“ mit Ergebnis (Render, eigener Datenordner) |
| `bilder/heal.png` | „Einrichten & Reparieren“ mit den neuen Karten |
| `bilder/modus.png` | Betriebsmodus mit Angebotsaussage |

## Nachtrag Fix 1
| Datei | Inhalt |
|---|---|
| `core-tests-rohlog.txt` | neu erzeugt: 16 Core-Tests (9 bisherige + 7 Fix-1-Tests `T0763Fix1Tests`, davon M2/N4/N5 mit echten Junctions per `mklink /J`) |
| `installation-echt.txt`, `lastprobe.txt` | neu erzeugt; die echte Installation wird weiter nur gelesen, jetzt mit genauer Paketzuordnung (Kennung + Version + .nuspec) und ohne Dateien „nicht aus dem Paket“ |
| `app-tests-fix1-rohlog.txt` | 64 App-Testklassen nach Fix 1: 325 Tests, 0 Fehler, 2 übersprungen |
| `app-tests-fix1b-rohlog.txt` | T0763SystemAlltagUi, W7_Ui, X02_Ui nach letzter Textänderung: 15 Tests, 0 Fehler |
| `bilder/*.png` | neu gerendert (Betriebsmodus ohne goldenen Stick-Knopf, Heilung mit Grenze und Prüfung beim Öffnen, Alles prüfen) |

## Nachtrag Fix 2
| Datei | Inhalt |
|---|---|
| `fix2-rot-vorher-rohlog.txt` | Rot: die 6 neuen Tests `T0763Fix2Tests` gegen den Stand 1f0fcab8d – alle 6 schlagen fehl (u. a. „ORIGINAL“ draußen überschrieben, Junction „logs“ beschrieben) |
| `core-tests-rohlog.txt` | Grün: 22 Core-Tests `T0763*` mit `TRENDO_BELEG_DIR` (Hardlinks per `CreateHardLinkW`, Junctions per `mklink /J`) |
| `app-tests-fix2-rohlog.txt` | 9 App-Testklassen (T0763, X02, Sys13, W7, TaskManager, SetupFeedback, T0736, Hardware, DesignPaket1): 59 Tests, 0 Fehler |
| `installation-echt.txt`, `lastprobe.txt` | neu erzeugt; echte Installation nur lesend: 0 Abweichungen, keine Fremddateien, keine Verknüpfungen |

## Nachtrag Fix 3
| Datei | Inhalt |
|---|---|
| `fix3-rot-vorher-rohlog.txt` | Rot: `T0763Fix3Tests.H1` gegen 5ea97928f – die fremde Datei hinter dem Hardlink `tools.json` wurde zu `{}` überschrieben |
| `core-tests-rohlog.txt`, `lastprobe.txt`, `installation-echt.txt` | Grün: 23 Core-Tests `T0763*`; Lastprobe ordnet die Leistungsindikator-Instanz jetzt über die Kennung aus derselben Messung zu |
