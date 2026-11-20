# Bericht T-0750: Geräte überwachen – weitere Quellen und Alarmregeln

**Auftrag:** `tasks/T-0750-geraete-ueberwachen.md` (Restliste Rang 27, SPEC-10, Urteil TEILWEISE)
**Branch:** `claude/T-0750-geraete-ueberwachen` (eigene Kopie von `main` 0.9.105 in `%TEMP%\trendo-auftrag-T-0750`)
**Sprache:** C# (.NET 10, Avalonia 12). Rust geprüft und nicht eingesetzt: Lesen von Leistungsindikatoren, Dateien und HTTP sowie das Auswerten der Regeln sind I/O-gebunden und klein; eine zweite Sprache brächte nur Mehraufwand und eine zusätzliche Abhängigkeit.

## Vorher → nachher

| Punkt | Vorher (Beleg) | Nachher | Nachweis |
|---|---|---|---|
| Quellen | „Dieser PC“ nur Festplatte belegt und Arbeitsspeicher der Trendo-App (`MainWindow.Spec10.cs` `ReadLivePc`), CSV-Import einmalig, MQTT nur lesend (`TwinMqtt.cs`) | zusätzlich **Leistungsindikatoren von Windows** ohne Admin: Prozessor (PDH `\Processor(_Total)\% Processor Time`), Datenträger aktiv (PDH `\PhysicalDisk(_Total)\% Idle Time`), Arbeitsspeicher des Systems (`GlobalMemoryStatusEx`), Netz empfangen/gesendet (alle aktiven Adapter); unter Linux CPU/RAM aus `/proc` (`PcCounters` in `Core/W6/TwinFeeds.cs`) | echt: `TwinAlarmTests.Real_counters`, `TwinAlarm_Ui.Real_cpu_load_alarm`, `reports/T-0750/T-0750-cpu-last-alarm.log` (61 CPU-Werte über eine Minute) |
| Wachsende Datei | – | Quelle „Datei“: liest nur Neues ab dem gemerkten Stand, eine angefangene letzte Zeile wartet, neu begonnene (kürzere) Datei wird von vorn gelesen; Zeilen: Zahl, `Zeit;Wert`, `Zeit;Anlage;Sensor;Wert`, JSON-Zeile mit Feld und Zeit (`TwinFeed.PollFile/ParseLine`) | echt (Dateisystem): `TwinAlarmTests.Growing_file`, `Parse_lines`; UI: `TwinAlarm_Ui.File_rule_alarm`; `reports/T-0750/T-0750-datei-*.csv` |
| HTTP im eigenen Netz | – | Quelle „HTTP“: nur `http(s)` an localhost, `*.local/*.lan/*.home.arpa/*.fritz.box` oder privaten IPs; nur GET, höchstens 64 KB, 5 s, keine Weiterleitung, kein Proxy; **ohne Häkchen „Ich erlaube Trendo, diese Adresse regelmäßig zu lesen“ wird sie nicht angelegt**, Freigabe steht im Prüfprotokoll (`zwilling.quelle.http.freigabe`) | echt: `TwinAlarmTests.Http_real_server` (echter HTTP/1.1-Server über TCP auf 127.0.0.1, Tasmota-artiges JSON), `Local_only` (12 Fälle); UI: Ablehnung ohne Freigabe und außerhalb des eigenen Netzes in `File_rule_alarm` |
| Serielle Schnittstelle | – | **nicht gebaut – BLOCKIERT**: auf diesem PC gibt es nur `COM1` ohne angeschlossenes, sendendes Gerät; ein Leser ohne prüfbare Gegenstelle wäre ungeprüfter Code. Voraussetzung: Gerät mit serieller Ausgabe (z. B. Arduino/USB-Seriell) | – |
| Alarmregeln | nur Ampel-Grenzen je Sensor (Warnung/Alarm über/unter), keine Dauer, keine Hysterese, keine Meldung, kein Protokoll | `TwinRule`: über/unter, Schwelle, Mindestdauer (s), Hysterese, an/aus; Zustandsautomat über die Messwerte seit Anlage der Regel (`TwinRules.Evaluate`); jeder Wechsel (ausgelöst/aufgehoben) **genau einmal** ins Alarmprotokoll (`TwinModel.Alarms`, max. 2000) und ins Prüfprotokoll (`zwilling.alarm`, `zwilling.alarm.ende`), Benachrichtigung über den vorhandenen Weg `Toast` (höchstens drei auf einmal), Protokoll als CSV | `TwinAlarmTests.Rule_above_duration_hysteresis`, `Rule_below_and_created`, `Check_logs_once`; UI-Klickweg `File_rule_alarm`; echt mit Last `Real_cpu_load_alarm` |
| Regeln im Dokument, rückgängig, Datei | – | Regeln und Quellen stehen im Modell (`zwilling/modell`, nach den Messwerten – alte Dateien bleiben lesbar); der Rückgängig-Stand der Hülle (T-0716) umfasst jetzt Anlagen, Regeln und Quellen (ohne Lesestand), alte Stände (nur Anlagenliste) bleiben verwendbar; „Regeln speichern“ → `alarmregeln.json` (Format `trendo-alarmregeln`, Version 1), „Datei wählen …“ / Ziehen aufs Feld → „Regeln einlesen“ (prüft Format, Version, jede Regel; keine Doppelten) | `Rules_file_roundtrip`; UI: Speichern, Entfernen, Einlesen, Rückgängig/Wiederholen inkl. Datei auf der Platte in `File_rule_alarm`; bestehende Hülle `T0716_HuelleE4_Ui` (6/6) |
| Aktoren | MQTT nur abonnieren | unverändert: keine Quelle sendet etwas an ein Gerät, Hinweis „schaltet aber nie ein Gerät“ im Quellen-Feld | Code-Durchsicht; HTTP nur GET |

## Oberfläche
Linke Spalte: neues Klappfeld „Weitere Quellen“ (Anlage, Sensor, JSON-Feld, „Datei wählen …“ für die wachsende Datei, HTTP-Adresse mit Freigabe-Häkchen, Liste der Quellen mit Fehlerhinweis und „Entfernen“, „Quellen jetzt abfragen“). Rechte Spalte unter den Grenzen: „Alarmregeln“ (Liste mit an/aus und Zustand ● ausgelöst / ○ ruhig, Regel anlegen, Regeln speichern/einlesen, Alarmprotokoll mit CSV). „Live (alle 5 s)“ fragt jetzt auch die Quellen ab; MQTT prüft die Regeln höchstens alle 2 s. Keine neue goldene Hauptaktion. Bild: `reports/T-0750/T-0750-anlagen-monitor-alarmregeln.png` (Render mit Beispielanlagen, angesehen).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → erfolgreich (0 Fehler).
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…TwinAlarmTests|…TwinMqttTests|…TwinChartTests|…W6Tests|…PromptActionTests"` → **Bestanden, 114/114** (`reports/T-0750/T-0750-core-tests.log`); davon TwinAlarmTests 20/20.
- App, je `Trendo.App.Tests.exe -class …`: `TwinAlarm_Ui` 2/2 (mit `TRENDO_BELEG_T0750`, 60-s-Lauf), `SPEC10_Ui` 2/2, `TwinMqttUi` 1/1, `T0716_HuelleE4_Ui` 6/6, `StudiosTechnikWissen_Ui` 39/39, `UebersichtEinfach_Ui` 2/2, `DeadButtonTests` 83/83 – Rohlogs unter `reports/T-0750/`.
- Rot-Grün: Die neuen Tests verwenden `TwinRule`, `TwinFeed`, `PcCounters`, `TwinModel.Rules` und die neuen Bedienelemente – auf dem alten Stand kompilieren sie nicht (rot).
- Kein Volltest (Vorgabe).

## Echtbeleg (`reports/T-0750/`)
- `T-0750-cpu-last-alarm.log`: echte Werte dieses PCs (CPU, RAM, Datenträger, Netz) einmal je Sekunde über 60 s; Regel „Prozessor (CPU) über 64 % für 3 s, Hysterese 10“ (Grundlast 34,3 %); künstliche Last mit 16 Rechen-Threads ab Sekunde 20 → **ALARM AUSGELÖST bei 98,3 %**, nach Lastende **AUFGEHOBEN bei 49,6 %**; je eine Benachrichtigung; 61 CPU-Werte (min 28,4 %, max 99,4 %).
- `T-0750-datei-messwerte.csv` (die gewachsene Testdatei), `T-0750-datei-alarmprotokoll.csv` (ausgelöst 58, aufgehoben 44), `T-0750-datei-benachrichtigungen.txt` (Benachrichtigungen + Prüfprotokoll-Einträge), `T-0750-alarmregeln.json` (exportierte Regel-Datei).
- Benutzer- und Rechnernamen ersetzt (`C:\Users\<user>`, `<pc>`, auch URL-kodiert geprüft); SHA-256 siehe `reports/T-0750/SHA256SUMS.txt`, gegen die Commit-Blobs geprüft.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **TEILWEISE** – leerer Monitor → „Diesen PC jetzt messen“ liefert echte Leistungswerte, Datei-Quelle und Regel per Klick, echtes Alarmprotokoll/Regel-Datei; ein „Geräte-Dashboard“ als eigenes Dokument (Datei öffnen/speichern unter) gibt es weiterhin nicht – das Modell im Datenordner ist das Dokument.
2. Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm: **TEILWEISE** – wachsende CSV wird gelesen, Regeln/Protokoll als JSON/CSV ausgegeben; Öffnen im Fremdprogramm (z. B. Tabellenprogramm) **NICHT GETESTET**.
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – Regeln/Quellen werden sofort atomar gespeichert (`Fach.Save`), getippte, noch nicht angelegte Regel-Felder werden bewusst **nicht** notgesichert; Absturz-Test nicht neu gefahren (Hülle aus T-0716 unverändert grün).
4. Claude/KI-Fachaufgabe im Dokument: **FEHLT** – nicht Teil dieses Pakets.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `TwinAlarm_Ui.File_rule_alarm` (Klick → Regel → echte Datei wächst → Alarm → CSV/JSON-Dateien auf der Platte → Rückgängig auf der Platte).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Pfadfelder nutzen den gemeinsamen Weg (Datei wählen, Ziehen aufs Feld, in „Einfach/Erweitert“ liest der Folgeknopf sofort), Rückgängig/Wiederholen der Hülle; kein eigenes Kontextmenü und keine eigenen Tastenkürzel. Ziehen einer Datei auf die freie Seite landet im ersten leeren Pfadfeld (CSV einlesen) – das ist das gemeinsame Verhalten (`MainWindow.Files.cs`, nicht geändert).

## Nicht geprüft
- Echtes Gerät im Netz (Tasmota/Shelly) über HTTP – nur echter lokaler HTTP-Server im Test.
- Serielle Schnittstelle (BLOCKIERT, s. o.).
- Linux-Zweig von `PcCounters` (nur Windows gelaufen).
- Bild der aufgeklappten „Weitere Quellen“ (Render zeigt die zugeklappte Leiste und die Regel-Spalte).
- `DeadButtonTests`, `UebersichtEinfach_Ui`, `StudiosTechnikWissen_Ui`, `TwinMqttUi`, `T0716_HuelleE4_Ui` liefen vor der letzten reinen Layout-Änderung der Regel-Felder (Zeilen statt Fließtext, keine neuen Knöpfe); `TwinAlarm_Ui` und `SPEC10_Ui` liefen danach.
- Dauerlauf (24 h) mit einem echten Sensor.

## Offene Punkte / Risiken
- Regeln werten nur Messwerte ab ihrer Anlage aus (eingelesene Regeln ab dem Einlesen) – nachträglich importierte alte CSV-Werte lösen nichts aus (gewollt gegen Meldungsfluten).
- Auswertung läuft über alle Messwerte eines Sensors seit Anlage der Regel; bei sehr vielen Werten (bis 200 000) und schneller MQTT-Rate ist das durch die 2-s-Drossel begrenzt, aber nicht gemessen.
- Keine Benachrichtigung außerhalb von Trendo (Windows-Mitteilung, E-Mail) – nur der vorhandene Weg in Trendo.

## Geänderte Dateien
- `src/Trendo.Core/W6/DigitalTwin.cs` (TwinModel: Rules, Sources, Alarms)
- `src/Trendo.Core/W6/TwinFeeds.cs` (neu: TwinSource, TwinRule, TwinAlarm, TwinRules, TwinFeed, PcCounters)
- `src/Trendo.App/MainWindow.Spec10.cs` (Leistungsindikatoren beim Messen, Live fragt Quellen ab, Rückgängig-Stand mit Regeln/Quellen, Panels eingehängt)
- `src/Trendo.App/MainWindow.Spec10Mqtt.cs` (eine Zeile: Regeln prüfen)
- `src/Trendo.App/MainWindow.Spec10Alarm.cs` (neu: Quellen, Regeln, Benachrichtigung, Oberfläche)
- `tests/Trendo.Core.Tests/TwinAlarmTests.cs`, `tests/Trendo.App.Tests/TwinAlarmUiTests.cs` (neu)
- `reports/T-0750/*`, `.gitattributes` (eine Zeile), `tasks/T-0750-geraete-ueberwachen*.md`

**Status:** umgesetzt und gezielt getestet; nicht gemergt, kein Release.

## Gegenprüfung behoben (T-0750-fix, 18.11.2026)

Neue Commits auf demselben Zweig `claude/T-0750-geraete-ueberwachen` (eigene Kopie `%TEMP%\trendo-auftrag-T-0750-fix`). Protokolle: `reports/T-0750/T-0750-fix-*.log`.

| Befund | Änderung | Test | Rot-Probe (alter Stand) |
|---|---|---|---|
| 1 HOCH – QA-019 rot (HttpClient im Kern) | HTTP-Abruf nach `src/Trendo.App/TwinHttp.cs` verlegt (wie `AceStepClient`). Im Kern bleiben `IsLocalUrl`, `IsLocalAddress`, `ParseHttpBody` und `FetchHttpAsync(quelle, abrufFunktion)` mit Freigabe- und Adressprüfung. Der Test mit echtem HTTP-Server zog nach `Trendo.App.Tests.TwinHttp_Tests` um (nicht gelöscht, Inhalt gleich). | `QA019_Offline` grün (Test unverändert); Kern: `TwinAlarmTests.Http_fetch_injected`; App: `TwinHttp_Tests.Http_real_server` (echter Server auf 127.0.0.1) | `QA019_Offline` rot: „Found: System.Net.Http“ (`T-0750-fix-rot-core.log`) |
| 2 MITTEL – DNS-Rebinding | `TwinHttp.Create`: `SocketsHttpHandler.ConnectCallback` löst den Namen selbst auf, lehnt ab, sobald eine Adresse nicht privat/Loopback/Link-local ist, verbindet sich genau mit der geprüften IP und prüft die verbundene Gegenstelle nach. „.fritz.box“, „.local“ usw. gelten nur noch, wenn die IP lokal ist. | `TwinHttp_Tests.Dns_rebinding_refused` (ersetzte Namensauflösung, kein echtes DNS: „sensor.fritz.box“ → 93.184.216.34 abgelehnt, gemischte Antwort abgelehnt, unbekannter Name abgelehnt, „kessel.local“ → 127.0.0.1 liest echt); Kern: `Local_address` (15 Fälle) | nicht als Test möglich (alter Code hatte keine Auflöse-Schnittstelle); Codebeleg: alter `IsLocalUrl` prüfte bei Namen nur die Endung |
| 3 MITTEL – `ImportJson` | `"Regeln":[null]` → „Eintrag 1 der Regel-Datei ist leer.“; Version < 1 oder fehlend, > 500 Regeln, Datei > 1 MB (auch vor dem Lesen in der App), Anlage/Sensor > 200 und Name > 300 Zeichen werden mit deutscher Meldung abgelehnt; Meldung nennt die Nummer der Regel. | `TwinAlarmTests.Rules_file_limits`; UI: `TwinAlarm_Ui.Rules_file_null_entry` (Klickweg „Datei wählen …“, Meldung statt Absturz) | rot: NullReferenceException in Kern und Oberfläche, Version 0 und 501 Regeln angenommen |
| 4 MITTEL – UI-Faden | `TwinPollSources` liest Dateien in `Task.Run` (`TwinFeed.ReadFile` ändert nichts am Zwilling), übernimmt per `Dispatcher.UIThread.Post` nur, wenn die Quelle unverändert ist; `MeasureCounters` fragt PDH in `Task.Run` ab. Je ein Merker verhindert den Doppelstart („Die Quellen werden gerade schon gelesen …“). Nach dem Schließen des Fensters fasst kein Nachzügler mehr etwas an. | `TwinAlarm_Ui.Background_reads_off_ui_thread` (Lesen hängt an einem Riegel, Klicks kehren zurück, beide Lesevorgänge laufen auf anderen Fäden, zweiter Klick startet nichts, Werte kommen nach Freigabe an) | nicht rot-prüfbar (Haken für den Faden ist neu); Codebeleg: alter Klick rief `TwinFeed.PollFile` und `PcCounters.Read` direkt im Klick auf |
| 5 NIEDRIG – wachsende Datei | Zeile > 1 MB ohne Zeilenende: übersprungen, Hinweis an der Quelle, Rest bis zum nächsten Zeilenende verworfen (`TwinSource.Resync`); nach dem Sprung auf das letzte MB wird die angeschnittene erste Zeile verworfen; UTF-16 LE/BE und UTF-8 mit BOM; Austausch durch gleich lange Datei über die Dateikennung (Windows, `GetFileInformationByHandle`; sonst nicht erkannt); Netzwerkpfade (`\\Rechner\…`, `//…`) abgelehnt, bevor auf sie zugegriffen wird. | `Growing_file_overlong_line`, `Growing_file_big_old_file_drops_cut_line`, `Growing_file_utf16`, `Growing_file_rotation_same_length`, `Growing_file_unc_refused`; UI: `TwinAlarm_Ui.Unc_file_refused`; bestehendes `Growing_file` grün | alle fünf Kern-Tests und der UI-Test rot |
| 6 NIEDRIG – genau eine Meldung | Zustand je Regel (`TwinRule.State`: ausgelöst, verletzt seit, zuletzt ausgewertet, zuletzt gemeldet) im Modell gespeichert; `Check` wertet nur neuere Messwerte aus; Kürzen von Verlauf/Protokoll ändert nichts; `SetEnabled` – wieder eingeschaltet zählt erst ab jetzt; `IsActive(regel)` liest nur den Zustand (Neuzeichnen rechnet nichts nach). Zustand gehört nicht in Regel-Datei und Rückgängig-Stand (`WithoutState`); `Adopt` übernimmt ihn bei Rückgängig. | `Check_no_duplicates_after_trim`, `State_and_reenable`, `Adopt_keeps_state`; UI: `TwinAlarm_Ui.Reenabled_rule_silent` (an/aus per Klick); bestehende Regel-Tests grün | `Check_no_duplicates_after_trim` rot (Doppelmeldung nach Kürzen), `Reenabled_rule_silent` rot (Alarm aus der Aus-Zeit) |
| 7 NIEDRIG – `_pcc` | Beim Schließen des Fensters: `PcCounters.Dispose()` und HTTP-Client frei; beim Verlassen der Seite im Live-Betrieb ebenfalls. `PcCounters` ist gegen gleichzeitiges `Read`/`Dispose` gesperrt, nach `Dispose` liefert `Read` nichts. | `TwinAlarm_Ui.Counters_disposed_on_close` | nicht rot-prüfbar (`IsDisposed` neu) |

**Tests (gezielt, kein Volltest):** `dotnet build Trendo.sln -c Release` 0 Fehler. Kern: `TwinAlarmTests` (44), `QA019_Offline` (1), `TwinMqttTests` (7), `T0265_TwinChart`, alle Klassen aus `W6Tests.cs`, `PromptActionTests` → **188/188** (`T-0750-fix-core.log`). App: `TwinAlarm_Ui` 7/7, `TwinHttp_Tests` 2/2, `SPEC10_Ui` 2/2, `TwinMqttUi` 1/1, `T0716_HuelleE4_Ui` 6/6, `StudiosTechnikWissen_Ui` 39/39, `UebersichtEinfach_Ui` 2/2, `DeadButtonTests` 83/83, `QA019_OfflineUi` 1/1, `Sys12_Bedienung_Ui` 5/5. Hinweis: Klassen namens `TwinChartTests`/`W6Tests` gibt es nicht – gemeint sind die Dateien; gelaufen sind deren Klassen.
**Rot-Probe:** die neuen Tests mit alter Schnittstelle in einer Wegwerf-Arbeitskopie gegen den Stand `b0bf5f62b` – Kern 8/8 rot, Oberfläche 3/3 rot (`T-0750-fix-rot-*.log`).
**Bild:** Anlagen-Monitor mit Beispielanlagen neu gerendert und angesehen – Aufbau unverändert (nur Meldungstexte neu).

**Weiterhin ersetzt/offen:** DNS-Rebinding nur mit ersetzter Namensauflösung geprüft (kein echtes DNS, kein echtes Gerät). Austausch einer gleich langen Datei wird außerhalb von Windows nicht erkannt. Netzlaufwerke mit Laufwerksbuchstaben (z. B. `Z:`) werden nicht abgelehnt – sie blockieren aber nicht mehr die Oberfläche. Schnell aufeinanderfolgende Messwerte mit gleicher oder älterer Zeit als der zuletzt ausgewertete werden von den Regeln übergangen (Folge des gespeicherten Zustands). `Twin.Save()` (bis 200 000 Messwerte) läuft weiterhin auf dem UI-Faden – nicht Teil der Befunde.

**Zusätzlich geändert:** `src/Trendo.App/TwinHttp.cs` (neu), `src/Trendo.App/MainWindow.Spec10.cs`, `src/Trendo.App/MainWindow.Spec10Alarm.cs`, `src/Trendo.Core/W6/TwinFeeds.cs`, `tests/Trendo.Core.Tests/TwinAlarmTests.cs`, `tests/Trendo.App.Tests/TwinAlarmUiTests.cs`, `tests/Trendo.App.Tests/TwinHttpTests.cs` (neu), `reports/T-0750/T-0750-fix-*.log`, `reports/T-0750/SHA256SUMS.txt`, dieser Bericht.
