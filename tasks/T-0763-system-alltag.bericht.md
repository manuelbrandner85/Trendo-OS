# T-0763 – Bericht: System im Alltag (SYS-13, X-02, X-09, SYS-01)

**Zweig:** `claude/T-0763-system-alltag` (Basis main d8df585a5, Release 0.9.109) · freigegeben vom Nutzer (Restliste Rang 41 Teil 1)

## Geänderte Dateien
- `src/Trendo.Core/Sys/Sys13.cs` – neu `Checkup` (Ampel je Bereich, Befund, nächster Schritt, Seite; Gesamtsatz); `Quality.TelemetryHitsInstalled`/`DepsFiles` (Telemetrie-Prüfung der installierten Version über *.deps.json).
- `src/Trendo.Core/W3/SelfHealing.cs` – Quarantäne mit Herkunftsliste (`logs/quarantaene-herkunft.json`), `Quarantined`, `Restore` (alte Datei bleibt als `.vor-rueckholung-….bak`, zurückgeholte Datei wird nicht erneut beiseitegelegt, solange unverändert); neu `InstallIntegrity` (Programmordner gegen Velopack-Paket: Größe + CRC-32 aus dem ZIP-Verzeichnis, Ersetzen aus dem Paket, Schutz gegen Pfade außerhalb des Programmordners).
- `src/Trendo.Core/Sys/TaskManager.cs` – neu `PdhArray` (Leistungsindikatoren mit Platzhalter, pdh.dll, ohne Admin), `GpuLoad` (Grafik je Prozess aus „GPU Engine“, Netz-Hinweis).
- `src/Trendo.App/MainWindow.Sys13.cs` – Reiter „Alles prüfen“ (Startseite von SYS-13) mit goldenem Knopf, Ampel-Karten, „Hingehen“, Entwicklerteil nur in „Professionell“; Lizenz-Karte ohne Liste ehrlich; Telemetrie-Karte mit echtem Ergebnis.
- `src/Trendo.App/MainWindow.Heal.cs` – Karten Programmdateien (prüfen / nach Rückfrage ersetzen / „Setup erneut ausführen“), Zusatzprogramme (prüfen, „Nachholen“ über vorhandenen Weg), Beiseitegelegte Dateien (Zurückholen mit Rückfrage).
- `src/Trendo.App/MainWindow.Setup.cs` – Reparatur eines Zusatzprogramms als `RepairComponentAsync` herausgelöst (Systemcheck und Heilung nutzen denselben Weg).
- `src/Trendo.App/MainWindow.TaskManager.cs` – Spalte „Grafik“ (sortierbar), Hinweis Netzlast, Beenden nur genau dieser Prozess (ohne Unterprozesse) mit erneuter Prüfung von Kennung + Pfad, Trendo-Zeilen golden statt blau.
- `src/Trendo.App/MainWindow.W7.cs` – Betriebsmodus: Angebotsaussage, Stick-Hinweis.
- `src/Trendo.App/MainWindow.Qualitaet.cs` (Entwickler-Hinweis), `MainWindow.axaml.cs` (Link „Alles prüfen“ auf der Systemseite), `MainWindow.Einstellungen.cs` („Alles prüfen“ in „Zustand & Leistung“).
- `tools/Trendo.Render/Program.cs` – `TRENDO_CHECKUP=1` zeigt „Alles prüfen“ mit Ergebnis.
- Tests: neu `tests/Trendo.Core.Tests/T0763SystemAlltagTests.cs` (9), neu `tests/Trendo.App.Tests/T0763SystemAlltagUiTests.cs` (8); angepasst `T0736SeitennamenUiTests.cs` (Startseite von SYS-13 ist jetzt „Alles prüfen“ statt „Leistung messen“ – gleiche Prüfungen an der neuen Startseite).
- `.gitattributes` (`reports/T-0763/** -text`), `reports/T-0763/**`, `tasks/T-0763-*`.

## Je Punkt
| Punkt | vorher (Beleg Audit) | jetzt | Nachweis |
|---|---|---|---|
| SYS-13 Alles prüfen | Entwickler-Prüfwerkzeuge, kein Endnutzer-Knopf (`MainWindow.Sys13.cs:73-93`) | Studio öffnet „Alles prüfen“: ein goldener Knopf, 7 Bereiche (Programmdateien, Zusatzprogramme, Daten, Protokoll, Sicherung, Speicherplatz, Stabilität) je mit Ampel, Befund, nächstem Schritt und „Hingehen“; prüft im Hintergrund, verändert nichts, Protokolleintrag `pruefung.alles` | `T0763SystemAlltagUi.Check_everything` (echte Prüfung im isolierten Fenster), `Checkup_lights`, Bild `bilder/sys13.png` |
| SYS-13 Trennung Nutzer/Entwickler | Karten brauchen Quelltext/`project.assets.json` | Entwicklerprüfungen (Qualitäts-Ampel, Bau-Fortschritt, Seiten-Tempo) nur in „Professionell“, beschriftet „Nur für Entwickler“; Qualitäts-Ampel mit Hinweis; Telemetrie-Karte prüft jetzt die Paketliste der Installation; Lizenz-Karte ohne Liste mit nächstem Schritt statt „0 Bibliotheken“. Keine Karte braucht mehr Quelltext oder `project.assets.json` | `Developer_split`, `Telemetry_in_installed_folder` |
| SYS-13 veränderte Protokollzeile | „Kette prüfen“ meldet „Ab Eintrag N“ | bleibt; zusätzlich Bereich „Protokoll“ rot mit „Eintrag N wurde verändert oder gelöscht“ | `Audit_line_changed_reported_with_position` (verändert → Eintrag 3, gelöscht → Eintrag 2), UI: `Check_everything` (Eintrag 2) |
| X-02 Installation | nur Datenordner | Programmdateien gegen das Velopack-Paket (Größe + CRC-32), Ersetzen aus dem Paket nach Rückfrage; belegte Dateien → „Setup erneut ausführen“; ohne Paket ehrlich „Setup erneut ausführen“, ohne Installation „nur für Entwickler“ | `Install_check_and_repair`, `Install_honest_limits`, UI `Repair_installation`; **echt:** `Real_installation_copy` gegen das installierte Trendo 0.9.108 (`installation-echt.txt`) |
| X-02 Zusatzprogramme | nicht an dieser Stelle | Karte prüft beim Öffnen (startet die Programme mit `--version`), „Nachholen“ je fehlendem Programm über `SammelFreigabeAsync` → `InstallFreigegebenAsync` (vorhandener Weg, eigene Rückfrage, kein winget/msiexec); „Nein“ ändert nichts | `Fetch_missing_program` (Reihenfolge Frage → Einrichtung) – **Installation ersetzt** (`InstallOverride`), kein echter Download |
| X-02 Quarantäne | nur beiseitelegen | Liste mit „Zurückholen“ (Rückfrage), alte Datei bleibt als `.bak`, alte Quarantäne-Ordner ohne Herkunft ehrlich „Ort unbekannt“ | `Quarantine_restore`, UI `Restore_quarantine` |
| X-09 hängendes Werkzeug | Beenden nur mit ersetztem Kill getestet | echter Prozess (kopiertes `PING.EXE` im Trendo-Werkzeugordner) nach Rückfrage beendet, per Kennung, ohne Unterprozesse, Pfad vorher erneut geprüft; fremdes `PING.EXE` aus System32: kein Knopf, `TaskKill` lehnt ab, läuft weiter | `Kill_hanging_own_tool` (echt) |
| X-09 Grafik/Netz je Programm | nicht geprüft | Grafik je Programm: Leistungsindikator „GPU Engine“ über pdh.dll, ohne Admin, wie Windows-Task-Manager (höchste Grafik-Einheit); Netz je Programm: von Windows ohne Admin nicht lieferbar → ehrlich als nicht vorhanden beschriftet | `Gpu_per_process`, `Gpu_column` (Werte ersetzt); echte GPU-Instanzen auf diesem PC mit `typeperf -qx "GPU Engine"` gesehen, **ein Abgleich der GPU-Werte unter Last fehlt** |
| X-09 Lastprobe | – | Trendo vs. `\Process(*)\% Processor Time`: 0,00 und 0,18 Prozentpunkte | `Load_probe_against_performance_counters` (echt), `lastprobe.txt` |
| SYS-01 | „läuft … von einem Linux-USB-Stick oder auf dem Raspberry Pi“ | „Desktop Edition für Windows 11 … Linux und Raspberry Pi derzeit nicht im Angebot“; Stick-Karte als Vorschau ohne Unterstützung, Start auf echter Hardware nicht nachgewiesen; Stick-Schreiber unverändert | `Mode_offer`; Stick-Schreiber-Tests in `AppsAndSetupResumeTests` grün (Lauf 1) |

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `T0763SystemAlltagTests` 9/9; `Sys13|TaskManagerTests|W3|X02|SelfHeal|NoFixedUser` 13 + 15 bestanden.
- App Lauf 1: 62 Klassen (u. a. PageSweep, Einfachheit, DesignPaket1/3/4, DeadButton, AppsAndSetupResume, Sys13, TaskManager, W7, X02, SetupFeedback): 323 Tests, 1 Fehler (T-0736, siehe oben), 2 übersprungen.
- App Lauf 2 nach Anpassung: T0736, T0763, X02, Sys13, DesignPaket1, Hardware, Lizenz, SetupFeedback, TaskManager, W7: 59 Tests, 0 Fehler, 1 übersprungen (Linux-only).
- Rot-Grün: die neuen Tests nutzen neue Schnittstellen (`Checkup`, `InstallIntegrity`, `Quarantined/Restore`, `GpuLoad`, neue Seitenelemente) und schlagen auf main schon beim Übersetzen fehl; der echte Installationstest fand im ersten Lauf zwei Fehlmeldungen (Squirrel.exe, ExecutionStub) und wurde erst nach der Korrektur grün.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **NICHT ANWENDBAR** (Systemseiten, kein Dokument) – Ersatz: „Alles prüfen“ liefert im leeren Datenordner ein echtes Prüfergebnis (VOLLWERTIG für das Paket).
2. Fremddatei/Fremdprogramm: **TEILWEISE** – echtes Velopack-Paket und echte Installation geprüft und repariert (Kopie), Lastprobe gegen Windows-Leistungsindikatoren; kein Vergleich mit dem Bild des Windows-Task-Managers.
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – Herkunfts- und Rückhollisten mit `AtomicFile`; Ersetzen von Programmdateien über Zwischendatei + Verschieben; sonst nichts zu verlieren (Prüfungen verändern nichts).
4. Claude/KI-Fachaufgabe: **FEHLT** – nicht Teil des Auftrags; „reparatur.zeigen“ bleibt laut Katalog gesperrt (Verbesserung 35 offen).
5. Klick bis echte Datei: **VOLLWERTIG** für Installation (Klick → Datei aus Paket ersetzt) und Quarantäne (Klick → Datei zurück), **TEILWEISE** für Zusatzprogramme (Installation ersetzt).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Einfach/Profi-Trennung umgesetzt; keine neuen Tastenkürzel/Rechtsklick/Ziehen (für diese Seiten nicht sinnvoll).

## Nicht geprüft
- Echtes Nachholen eines Zusatzprogramms (Download/Installation) aus der Heilungsseite – nur mit ersetzter Installation; der Weg selbst ist der bestehende, anderweitig getestete.
- Ersetzen einer gerade benutzten Programmdatei der laufenden Installation (Windows-Sperre) – nur als Fehlerpfad im Code, nicht im Test.
- GPU-Werte unter Grafiklast gegen den Windows-Task-Manager.
- Start von einem geschriebenen Stick auf echter Hardware (unverändert offen).
- Volltest (macht die Hauptsitzung).

## Offene Punkte
- CRC-32 erkennt Beschädigung, schützt nicht vor gezielter Manipulation (fehlende Codesignatur, QA-017 bleibt offen).
- Hängendes Werkzeug wird nicht automatisch als „reagiert nicht“ erkannt (Windows-Abfrage kann je Fenster bis 5 s blockieren); Beenden ist jederzeit möglich.
- `CapabilityCatalog.cs:99` („Es wird nichts repariert“) beschreibt nur den KI-Weg – nicht angefasst.
- Studio-Untertitel von SYS-01 („Erkennt das Gerät und startet Trendo passend“) kommt aus `StudioCatalog.cs` – nicht angefasst.

## Nachtrag Fix 1 (unabhängige Gegenprüfung, Befunde M1–M4, N1–N6)
| Befund | Änderung | Test |
|---|---|---|
| M1 fremdes Paket gleicher Version | Kennung + Version aus `sq.version`, nur genau `{Kennung}-{Version}-full.nupkg`, dessen `.nuspec` beides bestätigt; sonst „Kein passendes Installationspaket (…)“ | `T0763Fix1Tests.M1_exact_package` (fremder Name, falsche Kennung in .nuspec, falsche Version) |
| M2 Schreiben über Junction | neu `PathGuard.LinkOnWay`: jeder Ordner zwischen Programmordner und Ziel sowie die Datei selbst auf Reparse-Point/LinkTarget geprüft; Reparatur lehnt ab | `M2_no_write_through_junction` (echte Junction per `mklink /J`, draußen bleibt leer) |
| M3 ehrliche Grenze, Fremddateien | Grenze „Erkennt Beschädigung, schützt nicht vor gezielter Manipulation – Trendo ist nicht code-signiert“ in Karte und grünem Befund; Dateien im Programmordner, die nicht im Paket stehen → gelber Hinweis (Ausnahme `sq.version`, Reparatur-Zwischendatei) | `M3_limit_and_extra_files`; echte Installation (nur lesend): `Extra` leer, Ampel grün (`Real_installation_copy`); UI `Repair_installation` |
| M4 SYS-01 Widerspruch | Text „kein unterstütztes Setup; Stick-Schreiber ist eine Vorschau“, Knopf „Stick oder SSD schreiben (Vorschau) …“ nicht golden, Pi-Zeile „Nicht verfügbar – nur auf dem Raspberry Pi, der derzeit nicht im Angebot ist“, Untertitel in `StudioCatalog` und KI-Wissen (`StudioKnowledge`) ohne „startet Trendo passend“ | UI `Mode_offer` |
| N1 „verändert nichts“ | Prüflauf ohne Reparatur schreibt `selbstheilung-letzte.json` nicht mehr; Text „ändert nichts an deinen Dateien und schreibt nur einen Protokolleintrag“ | `N1_check_only_writes_no_files` |
| N2 Dauergelb | zurückgeholte Datei = Hinweis (grün) mit Schritt „mit einem Texteditor öffnen oder löschen“ | `Quarantine_restore` (angepasst an das gewünschte Verhalten) |
| N3 .bak überschrieben | eindeutiger Name (`-2`, `-3` …), `File.Move` ohne Überschreiben | `N3_unique_backup_names` (gleiches `now`) |
| N4 Restore über Junction | `PathGuard` auch auf dem Weg im Datenordner | `N4_restore_not_through_junction` (echte Junction) |
| N5 PID-Wiederverwendung / Junction | Startzeit in Zeile und Messung, `TaskManager.SameProcess` vor dem Beenden; `IsOwn` falsch, wenn eine Verknüpfung auf dem Weg liegt | `N5_same_process_and_junction` (echte Junction auf System32, nur die Verknüpfung wird wieder entfernt); UI `Kill_hanging_own_tool` |
| N6 Leerzustand, Bezeichnung, „ein Klick“ | Karte „Programmdateien“ prüft beim Öffnen selbst (Leerzustand „wird geprüft …“); „mit einem Klick“ nur noch, wo Trendo selbst einrichten darf. **Nicht umgesetzt:** „Alles prüfen“ als Leistenname – die Leiste zeigt für Studio-Startseiten den Studio-Namen (Nutzerentscheidung T-0736 „Studio-Name überall“, 17.11.2026); der Reiter heißt „Alles prüfen“ | UI `Repair_installation` (Ergebnis ohne Klick), `T0736Seitennamen_Ui` |

Hinweis zum ersten Lauf dieses Nachtrags: Ein Fehler beim Einsetzen in `MainWindow.TaskManager.cs` verhinderte das Übersetzen der App; die erste Filterausgabe zeigte das nicht deutlich. Entdeckt, weil die UI-Tests am alten Stand scheiterten; behoben und neu gebaut, danach grün.

## Nachtrag Fix 2 (zweite Prüfrunde, F1–F4)
Rot-Grün: die neuen Tests `T0763Fix2Tests` (6) liefen zuerst gegen 1f0fcab8d – alle 6 rot (`reports/T-0763/fix2-rot-vorher-rohlog.txt`), danach grün.
| Befund | Änderung | Test |
|---|---|---|
| F1 Hardlink-Zwischendatei | `Repair` schreibt nie in eine vorhandene Datei: alte `*.trendo-reparatur` wird gelöscht (nur der Verweis) und im Protokoll genannt, neue Zwischendatei mit zufälligem Namen per `FileMode.CreateNew`/`FileShare.None`, Inhalt per `Open().CopyTo`, `LinkOnWay` auch für die Zwischendatei, Ziel per Verschieben ersetzt (zweiter Hardlink der alten Datei bleibt unverändert). Liegengebliebene Zwischendateien werden nicht mehr ausgeblendet, sondern als „nicht aus dem Paket“ gemeldet | `F1_hardlink_temp_and_target` (echte Hardlinks per `CreateHardLinkW`), `F1_stale_temp_is_reported` |
| F2 `logs`/`quarantaene`/`tmp` als Verknüpfung | `Run(true)` legt nichts in eine verknüpfte Quarantäne, schreibt nicht über verknüpftes `logs`, löscht nichts über verknüpftes `tmp`, sucht kaputte JSON nicht hinter Verknüpfungen, schreibt `tools.json` nicht über eine Verknüpfung; `Restore` prüft Herkunfts-/Rückholliste und die Quarantäne-Datei | `F2_data_folders_as_junction`, `F2_restore_logs_junction` (echte Junctions) |
| F3 XML mit DTD | `sq.version` und `.nuspec` über `XmlReader` mit `DtdProcessing.Prohibit`, `XmlResolver = null`; unlesbare `sq.version` ehrlich „nicht lesbar … Setup erneut ausführen“ statt „ohne Installation“ | `F3_no_dtd` (externe und interne Entität) |
| F4 Verknüpfungen im Programmordner | `Report.Links`; gelber Hinweis in „Alles prüfen“ und eigene Zeile `heal-install-links` in der Karte; echte Installation: keine | `F4_links_in_app_folder`, UI `Links_in_app_folder`, `Real_installation_copy` (`Links` leer) |
| Test-Kleinigkeit | `Quarantine_restore`: `Assert.Contains("modell.json", data.Finding)` | – |
| TOCTOU (niedrig) | **offen**: Zwischen Prüfung (`LinkOnWay`) und Schreiben könnte ein anderer Prozess desselben Benutzers eine Verknüpfung anlegen. Ein Schutz per offen gehaltenen Ordner-Handles / `FILE_FLAG_OPEN_REPARSE_POINT` braucht eigenes P/Invoke (`CreateFileW`, `GetFinalPathNameByHandleW`) für jeden Schritt und ist nicht klein; die Zwischendatei ist durch `CreateNew` + zufälligen Namen bereits gegen das vorab gelegte Ziel geschützt | – |
