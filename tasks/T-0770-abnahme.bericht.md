# Bericht T-0770: Abnahme Installieren, Update, Deinstallieren, Leistung

Basis main 542dc1ce7 (Release 0.9.110) · Zweig `claude/T-0770-abnahme-installation` · PC: Windows 11 (10.0.26340), 16 Kerne, 32 GB.
Belege: `reports/T-0770/rohlogs/lauf3/` (Gesamtlauf nach der Korrekturrunde, eigene Id `TrendoOSAbnahme3`), `reports/T-0770/rohlogs/lauf2/` (Gesamtlauf vor der Korrekturrunde), `reports/T-0770/rohlogs/lauf1-vor-fix/` (erster Lauf, deckte den Sicherungsfehler auf), `reports/T-0770/rohlogs/korrektur/` (Rot-Grün und Proben der Korrekturrunde). Prüfsummen in `reports/T-0770/SHA256SUMS.txt` gelten für die eingecheckten Bytes (`reports/T-0770/** -text`, ohne Zeilenende-Umwandlung). Hinweis: lauf1/lauf2 wurden beim ersten Einchecken noch auf LF normalisiert; maßgeblich sind die Bytes im Repo.
Der Abschnitt „Korrekturrunde“ am Ende geht den Abschnitten davor vor.
Wiederholen: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/abnahme/lebenszyklus.ps1 -Repo <Repo> -Work <eigener leerer Ordner> [-Phase Alles|Bauen,Installieren,Update,Leistung,Gross,Deinstallieren]`.

## Abschottung (harte Grenze)
- Eigene Paket-Id `TrendoOSAbnahme`, Titel „Trendo OS Abnahme“, Installation per `--installto` unter dem eigenen Arbeitsordner in `%TEMP%\trendo-auftrag-T-0770\`.
- `TRENDO_INSTANZ` (neu) legt Daten, Werkzeugordner und Altordner unter den Arbeitsordner – auch in den Velopack-Hooks, die ohne Kommandozeile der App laufen. TEMP/TMP ebenfalls umgelenkt, `TRENDO_TEMP_AUFRAEUMEN=0`.
- Prozesse nur per PID und nur, wenn ihre Datei im Arbeitsordner liegt.
- Vorher/nachher-Vergleich (nur lesend) von `%LOCALAPPDATA%\TrendoOS`, `%APPDATA%\TrendoOS`, `%LOCALAPPDATA%\TrendoWerkzeuge`, den Verknüpfungen „Trendo OS“, dem HKCU-Eintrag `TrendoOS` und den Prozessen der echten Installation: in jeder Phase **unverändert** (`echte-installation-vorher/nachher-Alles.json`).
- Ohne `TRENDO_INSTANZ` hätte die Deinstallation der Abnahme-Instanz den echten Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge` gelöscht (Hook arbeitet mit festen Pfaden, siehe Befund B2).

## Ergebnis je Schritt (Lauf 2, alle Prüfungen PASS, 0 Fehler)
| Schritt | Ergebnis | Beleg |
|---|---|---|
| 1 Pakete bauen | BESTANDEN – 0.9.110-abnahme1 und -abnahme2, Release, self-contained win-x64, Velopack 1.2.161; Feed mit full + delta | `pakete-sha256.txt`, `vpk-*.log`, `feed-releases.win.json` |
| 2 Setup A still | BESTANDEN – `Setup.exe --silent --installto <eigener Ordner>` in 11 s, 424 Dateien/418 MB, `current\TrendoOS.exe`, Version laut `sq.version` 0.9.110-abnahme1, 2 Verknüpfungen (Desktop, Startmenü), HKCU-Uninstall `TrendoOSAbnahme` | `abnahme.log`, `setup-a.log` |
| 2 Start A | BESTANDEN – Hauptfenster „Trendo OS“ nach 2,6 s sichtbar, Fensterbild 87 % nicht schwarz, Ende per PID | `fenster-a.png` |
| 3 Update A→B | BESTANDEN – A startet ohne `--offline`, sucht selbst (Quelle `TRENDO_UPDATE_QUELLE`), lädt das Delta, 30-s-Countdown, Sicherung, `Update.exe apply`, Neustart mit `--nach-update`; installiert 0.9.110-abnahme2, B läuft mit Hauptfenster | `velopack_TrendoOSAbnahme.log`, `fenster-b-nach-update.png` |
| 3 Nutzerdaten | BESTANDEN – Testdatei und Projektdatei im Datenordner byte-gleich (SHA-256 vorher = nachher) | `abnahme.log` |
| 3 Sicherung für Rückkehr (U5) | BESTANDEN nach Fix – `updates\vorherige-version\TrendoOSAbnahme-0.9.110-abnahme1-full.nupkg`, SHA-256 = Paket im Feed (0840797e…) | `abnahme.log`; Lauf 1: fehlte |
| 4 Deinstallieren | BESTANDEN – `Update.exe --uninstall --silent` 5,1 s, Hook lief (Protokoll im eigenen TEMP), Installationsordner entfernt, beide Verknüpfungen weg, HKCU-Eintrag weg, keine weiteren HKCU-Schlüssel mit der Id, kein `%LOCALAPPDATA%\TrendoOSAbnahme`, keine eigenen Prozesse | `abnahme.log`, `hook-trendo-deinstallation.log` |
| 5 Leistung | gemessen (unten) | `leistung.json`, `start-*.txt`, `gross.json` |
| 6 Skript | `tools/abnahme/lebenszyklus.ps1`, ohne Benutzernamen, PowerShell 5.1 | – |

Reste nach dem Deinstallieren und Bewertung:
- Datenordner der Instanz (16 Einträge) – **Absicht**: stille Deinstallation behält Daten (UninstallPrompt).
- Darin `updates\vorherige-version\*.nupkg` (135 MB) – Teil der Daten, bleibt mit ihnen; **Rest** im Sinne von Speicherplatz.
- `%LOCALAPPDATA%\velopack\velopack_TrendoOSAbnahme.log` (Velopack-Protokoll, 37 KB) – **Rest**, Velopack entfernt es nicht. Von mir nach dem Lauf gelöscht.
- Keine Ordner, Verknüpfungen oder HKCU-Schlüssel der eigenen Id übrig.

## Gemessene Zahlen (Release-Build 0.9.110-abnahme2, installiert)
- Kaltstart (neuer, leerer Datenordner; Windows-Dateicache nicht geleert, dafür fehlen Adminrechte): Fenster sichtbar 1311 / 1097 / 1080 ms → **Median 1097 ms**; erstes Bild laut App 1345 / 1103 / 1115 ms → Median 1115 ms.
- Warmstart (gleicher Datenordner nach Vorlauf): Fenster 1589 / 1602 / 1767 ms → **Median 1602 ms**; erstes Bild 1560 / 1580 / 1747 ms → Median 1580 ms. Warm ist langsamer als „kalt“, weil der bestehende Datenordner mehr lädt; echte Kaltstarts sind nicht gemessen.
- Allererster Start nach der allerersten Installation (Lauf 1): 37,6 s bis zum Fenster, Installations-Hook 20,7 s; in Lauf 2 mit neu gebauten Dateien 2,6 s bzw. 3,7 s (Befund B5).
- Speicher: Leerlauf 30 s nach Start **223 MB Working Set** (412 MB privat). Nach 5 Programmen (Text, Tabelle, Präsentation, PDF, Bildbearbeitung – über die Fenster-Wiederherstellung der App geöffnet, im Bild belegt) **272 MB** (521 MB privat, Spitze 275 MB).
- Update A→B: 43 s vom Start von A bis B (davon 30 s Countdown).
- Große Dateien (je 3 Läufe, Release-Build von `tools/Trendo.Render`, Avalonia headless mit Skia – nicht das installierte Fenster):
  - Text, 500 Seiten (Trendo zeigt „Seite 1 von 501 · 121000 Wörter“): bis bearbeitbar 2002 / 1703 / 1505 ms (Median 1703), Fett-Setzen 736–1058 ms, Working Set ~250 MB.
  - Tabelle, 100.000 Zeilen × 6 Spalten (CSV 4,7 MB): bis bearbeitbar 1149 / 1054 / 1088 ms (Median 1088), Zelle ändern 408–459 ms, Working Set 415 MB.
  - Bild 8000 × 8000 (PNG): bis bearbeitbar 4878 / 5018 / 4814 ms (Median 4878), Zauberstab 8,9–11,0 s, **Working Set 1,69 GB, Spitze 1,93 GB, privat 1,62 GB**.
  - Keine Messung über 4 GB oder 30 s.

## Befunde
- **B1 (behoben, Rot-Grün):** Die Sicherung für „Rückkehr zur alten Version“ (U5) entstand nie. Velopack löscht beim Laden des Updates das Paket der laufenden Version (Lauf 1, Velopack-Protokoll: `…abnahme1-full.nupkg deleted` um 03:45:02), `BackupCurrent` lief aber erst beim Installieren (03:45:34) und fand nichts. Fix in `MainWindow.Updates.cs`: Sicherung vor dem Laden, beim Installieren nur noch, falls noch keine da ist. Test `T0770UpdateSicherungUiTests` (rot auf altem Code: `rohlogs/rot-sicherung-alter-code.log`, grün danach); Lauf 2 belegt die Sicherung mit gleicher Prüfsumme wie das Feed-Paket. Restfall: Wurde ein Update in einer früheren Sitzung schon geladen, ist das alte Paket weg und es gibt keine Sicherung.
- **B2 (Restliste):** Installations- und Deinstallations-Hook arbeiten mit festen Ordnern (`%APPDATA%\TrendoOS`, `%LOCALAPPDATA%\TrendoWerkzeuge`) statt mit der Installation, zu der sie gehören. Eine zweite Installation daneben würde beim Deinstallieren die Werkzeuge der echten löschen. Für Endnutzer mit einer Paket-Id gering; für Abnahmen jetzt über `TRENDO_INSTANZ` abgeschottet.
- **B3 (Weg ergänzt):** Die Update-Quelle war fest GitHub, eine lokale Quelle gab es nicht. Neu: `TRENDO_UPDATE_QUELLE=<vorhandener Ordner>` → Velopack `SimpleFileSource`; ohne Variable unverändert GitHub. Test `T0770UpdateQuelleTests`.
- **B4 (Restliste):** Der Neustart nach dem Update bekommt nur `--nach-update`. Wer mit `--data-root` startet, landet nach dem Update im Standard-Datenordner.
- **B5 (Restliste, Ursache nicht bewiesen):** Erster Start nach der allerersten Installation 37,6 s, Installations-Hook 20,7 s von 30 s, die Velopack erlaubt. Ein zweiter Lauf mit neuen Dateien brauchte nur 2,6 s bzw. 3,7 s. Vermutlich prüft der Virenschutz die neuen, unsignierten Dateien. Auf langsamen PCs kann der Hook über 30 s kommen, dann bricht Velopack ihn ab.
- **B6 (Hinweis):** Bild 8000 × 8000 braucht 1,6–1,9 GB, rechnerisch etwa 6 Kopien des 256-MB-Bildes. Der Zauberstab braucht rund 9–11 s. Grenzen nicht überschritten, aber ein Kandidat für Optimierung.
- **B7 (Hinweis):** Velopack trägt im Uninstall-Schlüssel `DisplayVersion=0.9.110` ohne Vorabkennung ein. Beim Schlüssel der echten Installation stand lesend `DisplayVersion=0.9.10`; nicht weiter untersucht.
- **MSIX-Container:** Ein Probeordner unter `%LOCALAPPDATA%` landete aus dieser Sitzung im Container (`Packages\Claude_…\LocalCache\Local`). `%TEMP%` wird nicht umgeleitet, die Installation lag also wirklich im Arbeitsordner. Die Verknüpfungen lagen im echten Desktop- und Startmenü-Ordner, im Container gab es keine Kopie. Das Velopack-Protokoll ging in den echten Ordner `%LOCALAPPDATA%\velopack`, weil der schon existierte. Den HKCU-Uninstall-Schlüssel hat das Setup aus dem Prozessbaum von Claude geschrieben. Ob er in der echten Registry oder nur im Container-Abbild stand, lässt sich von innen nicht unterscheiden. „Einstellungen → Apps“ ist deshalb nicht geprüft.
- Nach dem Update zeigte B den Einrichtungsassistenten, weil die Prüf-Instanz mit dem Entwicklerschalter `--no-first-run` lief und die Einrichtung nie abgeschlossen war. Das ist kein Produktfehler.

## Was NICHT geprüft wurde
- Code-Signing: Pakete unsigniert, SmartScreen-Verhalten offen – **nicht bestanden/nicht geprüft**.
- Echter Fremd-PC, langsame Hardware, zweiter Windows-Benutzer, Setup mit Oberfläche (nicht still), Deinstallation mit Datenfrage (interaktiv).
- Update über die echte GitHub-Quelle (nur lokaler Feed; der GitHub-Weg ist unverändert), Update der echten Paket-Id, Rückkehr zur alten Version tatsächlich ausgeführt (nur Sicherung belegt).
- Echter Kaltstart mit geleertem Dateicache; große Dateien im installierten Fenster (gemessen im Release-Build headless).
- Sicht von außerhalb des MSIX-Containers (Registry, „Apps“-Liste).

## Abnahmekriterien
1. Leere Vorlage → Ergebnis: NICHT GETESTET (nicht Gegenstand des Pakets).
2. Fremddatei importieren/exportieren: TEILWEISE – große DOCX/CSV/PNG geöffnet und bearbeitet, kein Export ins Fremdprogramm.
3. Prozessabbruch ≤ 30 s: TEILWEISE – Abbruch per PID während des Laufs, Daten blieben (Testdatei, Einstellungen); Autosave-Grenze nicht gemessen.
4. KI-Fachaufgabe: NICHT GETESTET.
5. Test vom Klick bis zur echten Datei: VOLLWERTIG für den Lebenszyklus (Setup → Start → Update → Deinstallation mit echten Dateien, Prozessen, Registry, Rohlogs).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: NICHT GETESTET.

## Tests
- Rot auf altem Code: `rohlogs/rot-alter-code.log` (Instanz-Pfade fehlen), `rohlogs/rot-sicherung-alter-code.log` (Sicherung nach dem Laden).
- Grün (erste Runde): Core `T0770AbnahmeInstanzTests` + `DataLocation_And_Model` 8/8; App `T0770UpdateSicherungUiTests` 1, `T0770UpdateQuelleTests` 2, `UpdatesUiTests` 4, `UninstallPromptTests` 8 = 15/15 (`rohlogs/gruen-app-tests.log`). Korrektur M2: Die dort genannte Klasse „RechteKernFix2UiTests“ gibt es nicht (sie heißt `RechteKernFix2Ui`) und lief damals nicht mit; jetzt mitgelaufen, siehe Korrekturrunde.
- Echter Beleg: kompletter Lebenszyklus Lauf 2, 0 Fehler.

## Berührte Dateien
`src/Trendo.Core/DataLocation.cs`, `src/Trendo.App/TrendoUpdater.cs`, `src/Trendo.App/MainWindow.Updates.cs`, `tools/Trendo.Render/Program.cs`, `tools/abnahme/lebenszyklus.ps1`, `tests/Trendo.Core.Tests/T0770AbnahmeInstanzTests.cs`, `tests/Trendo.App.Tests/T0770UpdateQuelleTests.cs`, `tests/Trendo.App.Tests/T0770UpdateSicherungUiTests.cs`, `reports/T-0770/**`, `tasks/T-0770-abnahme.md`, `tasks/T-0770-abnahme.bericht.md`.

## Entwicklungsbericht
- Projekt: Trendo OS · Sprachen: C#, PowerShell, Python (Testdaten-Erzeugung) · Rust eingesetzt: NEIN.
- Grund: Änderungen sind wenige Zeilen in bestehendem C#; Abnahme-Skript braucht Windows-APIs (PowerShell). Rust brächte keinen Vorteil.
- Gemessene Verbesserung: Sicherung für U5 entsteht jetzt (vorher nie).
- Offene Probleme: B2, B4, B5, B6, MSIX-Sicht, Code-Signing.

## Korrekturrunde (Gegenprüfung: 6× MITTEL, 2× NIEDRIG)
| Punkt | Erledigt | Beleg / Test |
|---|---|---|
| M1 Prüfsummen | `reports/T-0770/** -text` in `.gitattributes`. Arbeitsdateien byte-genau aus den eingecheckten Blobs neu geschrieben. `SHA256SUMS.txt` neu erzeugt und jede Zeile gegen `git show HEAD:<datei> \| sha256sum` geprüft (0 Abweichungen). | `reports/T-0770/SHA256SUMS.txt` |
| M2 Testzahl | Die Klasse heißt `RechteKernFix2Ui` (5 Tests) und ist jetzt mitgelaufen. | `korrektur/gruen-app-je-klasse.log` |
| M3 Testquelle | `TRENDO_UPDATE_QUELLE` wirkt nur zusammen mit einer gültigen `TRENDO_INSTANZ`; eine normale Installation ignoriert sie. Netzpfade (`\\server`, `//server`) werden abgelehnt. In „Trendo-Updates“ steht dann „Achtung, Prüfbetrieb: Updates kommen aus einer Testquelle (…)“, der Satz „nur aus den offiziellen …“ entfällt. Jede Suche schreibt `update.quelle.test` ins Protokoll. | `T0770KorrekturUiTests.Local_source_needs_instance`, `.Test_source_visible_and_logged` (rot: `korrektur/rot-app.log`), Bild `korrektur/updates-testquelle.png` |
| M4 Instanz-Pfade | `TRENDO_INSTANZ` wird abgelehnt, wenn der Ordner gleich einem echten Trendo-Ordner (`%LOCALAPPDATA%\TrendoOS`, `%APPDATA%\TrendoOS`, `%LOCALAPPDATA%\TrendoWerkzeuge`) ist, darüber liegt (also auch `%LOCALAPPDATA%`, `%APPDATA%`, Benutzerordner, Laufwerk) oder darunter, und bei Netzpfaden. | `T0770KorrekturTests` – Verhaltens-Rot auf altem Code 13 von 14 (`korrektur/rot-core-m4-verhalten.log`), grün danach |
| M5a atomar | `VelopackUpdater.ReplaceBackup`: Platz prüfen, vollständig in eine Zwischendatei kopieren, Größe prüfen, atomar umbenennen und erst danach ältere Sicherungen löschen. Bei Fehlern bleibt die alte Sicherung und keine Zwischendatei liegen. | `T0770KorrekturUiTests.Replace_backup_atomic` |
| M5b Speicher | Zu wenig frei (Paketgröße + min(512 MB, 2× Paket)) → alte Sicherung bleibt. Die Meldung steht in der Updates-Ansicht und im Protokoll (`update.sicherung.fehlt`). | `.Replace_backup_low_space` |
| M5c gute Sicherung | `BootGuard.MayReplaceBackup`: Ist die laufende Version noch nicht als gut markiert und gibt es schon eine Sicherung, wird nicht ersetzt (weder vor dem Laden noch beim Installieren). | Core `T0770KorrekturTests.Unverified_version_keeps_good_backup`, App `.Unverified_keeps_good_backup` |
| M6 -Work | Abbruch mit Code 90 vor jedem anderen Schritt, wenn `-Work` ein echter Ordner ist, darin liegt oder darüber. Probe mit `%LOCALAPPDATA%`, `%APPDATA%`, Benutzerordner, `C:\`, `…\TrendoOS`, `…\TrendoOS\abnahme`, `…\TrendoWerkzeuge\x`: alle sofort Exit 90, nichts gestartet oder beendet, kein Ordner angelegt. | `korrektur/m6-probe.log` |
| N1 tiefer Vergleich | Rekursive Liste mit relativem Pfad, Größe und Zeitstempel, daraus SHA-256. Die vollen Listen werden gespeichert, abweichende Einträge namentlich ausgegeben. | Lauf 3, siehe unten |
| N2 Kaltstart-Bild | Das Skript wartet auf die Zeile `first.frame`, statt nur auf die Datei. In Lauf 3 stehen alle 3 Werte in `leistung.json`. | `lauf3/leistung.json` |
| Neuerungen | `installer/neuerungen/0.9.111.md`, eigener Abschnitt T-0770 | – |

**Tests:**
- Core `T0770|DataLocation_And_Model|BootGuard|Update`: 32/32 (`korrektur/gruen-core.log`).
- App: `T0770KorrekturUiTests` 5, `T0770UpdateSicherungUiTests` 1, `T0770UpdateQuelleTests` 2, `UpdatesUiTests` 4, `UninstallPromptTests` 8, `RechteKernFix2Ui` 5 = **25/25** (`korrektur/gruen-app.log`, je Klasse `korrektur/gruen-app-je-klasse.log`).
- Kein Volltest.
- `T0770UpdateQuelleTests` habe ich an die neue Regel angepasst: Die Quelle braucht jetzt eine Instanz.

**Lauf 3 (neue Id `TrendoOSAbnahme3`, weil M3/M5 den Update-Weg ändern):**
- 1 Fehler – nur der tiefe Vergleich der echten Ordner (N1) schlug an. Alle anderen Prüfungen bestanden:
  - Setup still, Start A, Render-Beleg
  - Update A→B über den App-Weg mit Testquelle; Sicherung `vorherige-version` vorhanden (SHA-256 dbc90655…)
  - B läuft, Testdateien byte-gleich
  - Deinstallation ohne Reste der eigenen Id
- Der N1-Fehler: In `%LOCALAPPDATA%\TrendoWerkzeuge` haben sich zwei Ordner-Zeitstempel geändert, Anzahl (297.372) und Bytes blieben gleich:
  - `libreoffice\…\program\opencl` (06:17:18)
  - `libreoffice\…\share\uno_packages\cache` (06:17:16)
- Zu dieser Zeit lief nur Trendo.Render (Bildmessung). Dieselben Render-Messungen erneut ausgeführt änderten nichts. Gleichzeitig liefen parallele Sitzungen (T-0771, T-0772). Bewertung: sehr wahrscheinlich fremd, **nicht bewiesen** (`korrektur/n1-werkzeuge-probe.log`). Die anderen echten Ordner, Verknüpfungen, der HKCU-Eintrag und die Prozesse waren unverändert.
- Last: Parallele Sitzungen bauten zur selben Zeit. Ein Release-Build dauerte 583–665 s statt rund 37 s. Die Zahlen aus Lauf 3 sind deshalb unter Last gemessen. Lauf 2 (ohne Fremdlast) bleibt die Referenz.

**Zahlen Lauf 3 (unter Last):**

| Messung | Werte | Median |
|---|---|---|
| Kaltstart, Fenster | 3139 / 3114 / 2814 ms | 3114 ms |
| Kaltstart, erstes Bild | 3097 / 3026 / 2732 ms | 3026 ms |
| Warmstart, Fenster | 2704 / 3016 / 3311 ms | 3016 ms |
| Warmstart, erstes Bild | 2727 / 2925 / 3291 ms | 2925 ms |
| Text 500 Seiten, bis bearbeitbar | 6893 / 2946 / 2191 ms | 2946 ms |
| Tabelle 100.000 Zeilen, bis bearbeitbar | 4772 / 3003 / 3384 ms | 3384 ms |
| Bild 8000×8000, bis bearbeitbar | 11262 / 9714 / 6671 ms | 9714 ms |

- Speicher: Leerlauf 223 MB Working Set (449 MB privat), nach 5 Programmen 266 MB (519 MB privat, Spitze 268 MB).
- Bild 8000×8000: Zauberstab 13,2–17,4 s, Working Set 1,69 GB, Spitze 1,94 GB.
- Keine Messung über 4 GB oder 30 s.
- B5 bestätigt: Installations-Hook 21,1 s (Velopack-Grenze 30 s), erster Start nach der Installation 38,6 s.

**Weiter offen:** B2, B4, B5, B6, Registry-Sicht wegen MSIX-Container, Code-Signing (nicht bestanden/nicht geprüft). Der tiefe Vergleich (N1) dauert auf diesem PC wegen 297.000 Dateien im Werkzeugordner je Seite bis zu 30 Minuten.

**Berührte Dateien (Korrekturrunde):** `.gitattributes`, `src/Trendo.Core/DataLocation.cs`, `src/Trendo.Core/Updates.cs`, `src/Trendo.App/TrendoUpdater.cs`, `src/Trendo.App/MainWindow.Updates.cs`, `tools/abnahme/lebenszyklus.ps1`, `tests/Trendo.Core.Tests/T0770KorrekturTests.cs`, `tests/Trendo.App.Tests/T0770KorrekturUiTests.cs`, `tests/Trendo.App.Tests/T0770UpdateQuelleTests.cs`, `installer/neuerungen/0.9.111.md`, `reports/T-0770/**`, `tasks/T-0770-abnahme.bericht.md`.

## Korrekturrunde 2 (2. Prüfung: 2× MITTEL, 3× NIEDRIG)
| Punkt | Erledigt | Test (Rot → Grün) |
|---|---|---|
| M-A Verknüpfungen | `DataLocation.FinalPath` löst jede vorhandene Verknüpfung (Junction, symbolischer Link) in der Kette auf (`ResolveLinkTarget(returnFinalTarget:true)`). `Instance()` vergleicht aufgelöste Pfade mit den aufgelösten echten Ordnern. `IsNetwork` lehnt UNC und verbundene Netzlaufwerke (`DriveType.Network`) ab, auch in `LocalSourceDir`. `Guard`/`RemovableTools`/`RemoveTools`/`RemoveUserData` lehnen eine Verknüpfung irgendwo in der Elternkette ab. `lebenszyklus.ps1`: `Resolve-Final` und `Test-Network` vor dem Vergleich. | `T0770Korrektur2Tests.Junction_to_real_folders_rejected`, `.Removable_tools_rejects_reparse_parent` – echte Junctions per `mklink /J` im Temp, danach nur die Verknüpfung entfernt. Verhaltens-Rot auf altem Code (`korrektur2/rot-core.log`). Skript-Probe mit echten Junctions: `korrektur2/m-a-skript-probe.log` |
| M-B Sicherung ↔ LastGood | `BootGuard.BackupVersion` (Version aus dem Dateinamen der Sicherung) und `RollbackTarget`: Die Rückkehr geht immer zur tatsächlich gesicherten Version – beim Knopf und bei der automatischen Rückkehr nach drei Fehlstarts (`Program.cs`). Der Knopf zeigt die gesicherte Version. Weicht „zuletzt gut“ ab, erscheint der Hinweis „Als zuletzt gut gilt X, gesichert ist Y …“ (bei der automatischen Rückkehr im Absturzprotokoll). | `T0770Korrektur2UiTests.Rollback_uses_backed_up_version` (v1 gut, v2 frisch vor MarkOk gesichert, v3 läuft). Verhaltens-Rot: `korrektur2/rot-app.log` |
| N-a Zwischendateien | `ReplaceBackup` räumt `*.nupkg.neu-*` vor jedem Sichern weg, auch wenn das Sichern danach scheitert. | `.Leftover_temp_files_cleaned`, Verhaltens-Rot in `rot-app.log` |
| N-b start.json | Entscheidung: Fehlt `start.json` oder ist sie unlesbar, darf die Sicherung erneuert werden. Begründung: Ohne Zustand gibt es kein Wissen, dass die vorhandene Sicherung „gut“ ist. Blockieren hieße, für immer eine beliebig alte Sicherung zu behalten. Sobald es einen Zustand gibt, gilt wieder M5c. | `T0770Korrektur2Tests.Missing_or_broken_state_does_not_block`, Verhaltens-Rot in `rot-core.log` |
| N-c Katalogpfade | `DependencyManager.ExpandPath`: `%LOCALAPPDATA%\TrendoWerkzeuge\…` zeigt in einer Prüf-Instanz auf `DataLocation.ToolsRootIn(instanz)`. Ohne Instanz bleibt es genau `Environment.ExpandEnvironmentVariables`. | `T0770Korrektur2PfadTests` – nur Kompilier-Rot (neue reine Funktion), Verhalten ohne Variable im Test verglichen |

**Mitgefunden:**
- Die Wächterprüfung `RechteKernFix12` (Schreibziele) schlug seit der ersten Korrekturrunde an, weil `TrendoUpdater.ReplaceBackup` Dateien schreibt. Damals lief die Klasse nicht mit.
- Ich habe den Eintrag mit Begründung in die vorgesehene Ausnahmeliste aufgenommen („Sicherung der laufenden Version in Trendos Update-Ordner, Aufrufer BackupCurrent“), so wie `BackupCurrent` und `RollbackTo`.

**Probe-Panne (ehrlich):**
- Die Skript-Probe „Junction auf %LOCALAPPDATA% + `\abnahme`“ ergibt aufgelöst `%LOCALAPPDATA%\abnahme`. Das ist ein Geschwister der echten Ordner und nach der Regel erlaubt.
- Das Skript startete dort einen Build; Setup oder Installation liefen nicht. Ich habe ihn per eigener PID beendet und den Ordner entfernt. Die echten Ordner waren danach unverändert (siehe Probe-Log).

**Tests:**
- Core 390/390, Filter: Dependencies_Core, ToolCheck_Core, QpdfDepTests, Model3DPreviewTests, PhotoScanTests, SPEC15_LiveStream, (No)VollpaketTests, RechteKern*, FreigabenKern, T0713FreigabenNachtrag, Paket44_Fachprogramme, T0203_ExternalApps, ConnectionsTests, HardwareCompatTests, KiModelT0703Tests, T0770*, DataLocation_And_Model, BootGuard, Update* (`korrektur2/gruen-core.log`).
- App: `T0770Korrektur2UiTests` 2, `T0770KorrekturUiTests` 5, `T0770UpdateSicherungUiTests` 1, `T0770UpdateQuelleTests` 2, `UpdatesUiTests` 4, `UninstallPromptTests` 8, `RechteKernFix2Ui` 5 = 27/27 (`korrektur2/gruen-app-je-klasse.log`).
- Kein Volltest. Kein neuer Lebenszyklus-Lauf (laut Vorgabe nicht nötig).

**Nicht geprüft:** Ein echtes verbundenes Netzlaufwerk (keins vorhanden; `IsNetwork` ist nur über die UNC-Fälle getestet).

**Berührte Dateien (Runde 2):**
- `src/Trendo.Core/DataLocation.cs`, `src/Trendo.Core/Updates.cs`, `src/Trendo.Core/Dependencies.cs`
- `src/Trendo.App/TrendoUpdater.cs`, `src/Trendo.App/MainWindow.Updates.cs`, `src/Trendo.App/Program.cs`
- `tools/abnahme/lebenszyklus.ps1`
- `tests/Trendo.Core.Tests/T0770Korrektur2Tests.cs`, `tests/Trendo.Core.Tests/T0770Korrektur2PfadTests.cs`, `tests/Trendo.App.Tests/T0770Korrektur2UiTests.cs`, `tests/Trendo.Core.Tests/RechteKernFix12Tests.cs` (ein Ausnahmeeintrag)
- `installer/neuerungen/0.9.111.md`, `reports/T-0770/**`, `tasks/T-0770-abnahme.bericht.md`
