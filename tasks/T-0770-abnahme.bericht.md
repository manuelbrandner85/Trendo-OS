# Bericht T-0770: Abnahme Installieren, Update, Deinstallieren, Leistung

Basis main 542dc1ce7 (Release 0.9.110) · Zweig `claude/T-0770-abnahme-installation` · PC: Windows 11 (10.0.26340), 16 Kerne, 32 GB.
Belege: `reports/T-0770/rohlogs/lauf2/` (gültiger Gesamtlauf), `reports/T-0770/rohlogs/lauf1-vor-fix/` (erster Lauf, deckte den Sicherungsfehler auf), Prüfsummen in `reports/T-0770/SHA256SUMS.txt`.
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
- Grün: Core `T0770AbnahmeInstanzTests` + `DataLocation_And_Model` 8/8; App `T0770UpdateSicherungUiTests`, `T0770UpdateQuelleTests`, `UpdatesUiTests`, `UninstallPromptTests`, `RechteKernFix2UiTests` 15/15 (`rohlogs/gruen-app-tests.log`). Kein Volltest (macht die Hauptsitzung).
- Echter Beleg: kompletter Lebenszyklus Lauf 2, 0 Fehler.

## Berührte Dateien
`src/Trendo.Core/DataLocation.cs`, `src/Trendo.App/TrendoUpdater.cs`, `src/Trendo.App/MainWindow.Updates.cs`, `tools/Trendo.Render/Program.cs`, `tools/abnahme/lebenszyklus.ps1`, `tests/Trendo.Core.Tests/T0770AbnahmeInstanzTests.cs`, `tests/Trendo.App.Tests/T0770UpdateQuelleTests.cs`, `tests/Trendo.App.Tests/T0770UpdateSicherungUiTests.cs`, `reports/T-0770/**`, `tasks/T-0770-abnahme.md`, `tasks/T-0770-abnahme.bericht.md`.

## Entwicklungsbericht
- Projekt: Trendo OS · Sprachen: C#, PowerShell, Python (Testdaten-Erzeugung) · Rust eingesetzt: NEIN.
- Grund: Änderungen sind wenige Zeilen in bestehendem C#; Abnahme-Skript braucht Windows-APIs (PowerShell). Rust brächte keinen Vorteil.
- Gemessene Verbesserung: Sicherung für U5 entsteht jetzt (vorher nie).
- Offene Probleme: B2, B4, B5, B6, MSIX-Sicht, Code-Signing.
