# T-0755 Bericht: Programme installieren, Downloads & Links, System auf Gefahren prüfen

**Auftrag:** Restliste `d7-restliste.md`, Rang 33–35 (Freigabe des Nutzers 23.11.2026).
**Branch:** `claude/T-0755-installieren-surfen-gefahren` (Basis `main`, Stand 0.9.107), eigene Kopie in `%TEMP%\trendo-auftrag-T-0755`.

## (1) SYS-04 Programme installieren
**Vorher:** Rückfrage-Dialog gab es seit T-0681. Nach „Ja“ lief winget unsichtbar; Erfolg = Exitcode 0, keine Nachprüfung, kein Protokoll je Lauf, kein Abbruch (audit-5, SYS-04 Nr. 12/14).
**Jetzt:**
- Die Rückfrage nennt den genauen Befehl (`winget install --id … -e --scope user …`). Der Knopf der Rückfrage („Installieren“/„Aktualisieren“/„Entfernen“) ist der Start durch den Nutzer – ohne ihn passiert nichts (bestehende T-0681-Tests).
- Lauf-Karte auf der Seite: Befehl, Fortschrittsbalken (aus „x MB / y MB“ bzw. „n %“), Ausgabe live, „Abbrechen“ (beendet Prozess samt Kindprozessen), danach „Protokoll öffnen“.
- Protokoll je Lauf unter `<Datenordner>\protokolle\programme\<Zeit>-<Vorgang>-<Paket>.log`, Zeile für Zeile geschrieben (bleibt nach Absturz lesbar): Befehl, Ausgabe, Rückgabewert (bekannte winget-Codes in Alltagssprache), Nachprüfung, „Ziel erreicht“.
- Nachprüfung danach: `winget list --id <ID> -e` (nur lesend) – Meldung richtet sich nach der Nachprüfung, nicht nur nach dem Exitcode. Protokolleinträge `app.pruefung`, `app.abgebrochen`.
- Dateien: `src/Trendo.Core/W3/Packages.cs`, `src/Trendo.App/MainWindow.AppCenter.cs`.
**Belege (ERSETZT – kein echter winget):** Tests nutzen ein eigenes Testprogramm `winget-ersatz.cmd`, das winget-Ausgaben nachahmt und nur eine Zustandsdatei im eigenen Temp-Ordner schreibt.
- Core `T0755_Programme`: Ausgaben lesen (nachgebildete Ausgabe); Installieren/Entfernen mit Protokoll + Nachprüfung; Fehlercode 0x8A150014; Abbrechen beendet den Lauf (Testprogramm wartet 90 s, Lauf endet in Sekunden).
- App `T0755_Ui.AppCenter_run_visible`: Klick → Rückfrage mit Befehl → Lauf mit Ausgabe und Fortschritt (25 %, 100 %) → „nachgeprüft“ → Protokoll öffnen → zweiter Lauf → „Abbrechen“ → „abgebrochen, nicht installiert“.
- **Echter winget-Lauf: NICHT GETESTET – braucht Lauf durch den Nutzer.** Ich habe winget, msiexec und choco nicht gestartet, auch nicht lesend.

## (2) SYS-03 → „Downloads & Links“
**Vorher:** Name „Surfen & Herunterladen“, kein eigener Browser (audit-5).
**Jetzt (Entscheidung des Nutzers: ehrlicher Name):**
- Neuer Name in StudioCatalog (Fenstertitel, Übersicht), CapabilityCatalog, StudioKnowledge, StudioProfiles, StudioTeams („Download-Team“), Dock-Hinweis (Wort unter dem Symbol: „Downloads“), Leisten-Reihenfolge, Einstellungs-Navigation, Apps-Navigation, Suche (Begriffe „link“, „prüfsumme“, „surfen“ finden die Seite), Seitenhilfe, Reiter „Browser“ → „Links & Webseiten“, „Privat surfen“ → „Privates Fenster in deinem Browser“. Alte IDs (`SYS-03`, Seite `downloads`, `netz-*`, Dock-ID `browser`) und Dateien (`downloads/warteschlange.json`, `netz/lesezeichen.json`) bleiben.
- SHA-256 wird für jeden fertigen Download berechnet und angezeigt (markierbar) und mit der angegebenen Prüfsumme verglichen (✓/✗).
- Verlauf: `downloads/verlauf.json` (neueste zuerst, höchstens 500, atomar geschrieben), Reiter „Verlauf“, bleibt nach „Fertige aus der Liste nehmen“, „Erneut laden“, „Verlauf leeren“.
- Anhalten/Fortsetzen über die vorhandene `.part`-Fortsetzung (bestand schon, jetzt im UI-Test mit echtem HTTP geprüft).
- „Webseite öffnen“: Hinweis „Trendo hat keinen eigenen Browser“, Knöpfe „Im Standard-Browser öffnen“ / „In <Browser>“.
- „Links sammeln“: Text einfügen → alle http(s)-Links ohne Doppelte, gespeichert als Lesezeichen-Ordner „Gesammelt“ (Export wie bisher), je Link „Im Browser öffnen“, „Herunterladen“, Entfernen.
- Eine goldene Hauptaktion: „Herunterladen“ (Standard-Browser-Knopf und Reiter nicht mehr gold, Fortschrittsbalken gold statt blau).
**Belege:** App `T0755_Ui.Downloads_and_links` – echter HTTP-Download (WebTools.DownloadAsync gegen lokalen Testserver, kein Internet) mit richtiger und falscher Prüfsumme, Anhalten/Fortsetzen mit gleicher Prüfsumme, Verlauf, Links sammeln/öffnen/entfernen. Core `T0755_DownloadsLinks`. Bild der Seite geprüft (Render).
**Nicht geprüft:** Download aus dem echten Internet; Docs in `docs/spec/kataloge/SYS-03_KATALOG.md` und `MODULE_REGISTER.csv` tragen noch den alten Namen (Spezifikation ändert nur der Nutzer).

## (3) SPEC-12 System auf Gefahren prüfen
**Vorher:** keine Systemprüfung (audit-5).
**Jetzt:** Karte „System auf Gefahren prüfen“ mit goldener Hauptaktion auf der Seite `forensik`:
- Liest NUR: Autostart-Ordner (Benutzer + alle; `.lnk` wird direkt aus der Datei gelesen), Run/RunOnce-Schlüssel (HKCU, HKLM, WOW6432Node – schreibgeschützt geöffnet), geplante Aufgaben (Aufgabenplanung, nur lesen), installierte Programme (Deinstallations-Einträge).
- Je Eintrag: Art, Ort, Befehl, Datei, Herausgeber, Authenticode-Prüfung (WinVerifyTrust, eingebettet und Windows-Katalog, ohne Netz), Ampel, Erklärung in Alltagssprache, „Was du tun kannst“. Grün = gültig signiert von bekannter Firma (ganzes Wort), Gelb = unsigniert/unbekannt/Skript-Programm/nicht prüfbar, Rot = Datei fehlt, Signatur ungültig, verdächtiger Ort (%TEMP%, Downloads, Papierkorb, Öffentlich) oder versteckter PowerShell-Befehl.
- Filter Rot/Gelb/Grün/Alle, „Ordner zeigen“, Abbrechen, Bericht als HTML und CSV (atomar, Projektordner `sicherheit`). Keine automatischen Änderungen.
- Dateien: `src/Trendo.Core/Sys/Authenticode.cs`, `src/Trendo.Core/Sys/Gefahren.cs`, `src/Trendo.App/MainWindow.Gefahren.cs`, `MainWindow.Forensik.cs`.
**Belege (echt, nichts ersetzt):** Core `T0755_Gefahren` – kernel32.dll/explorer.exe gültig (Microsoft), coreclr.dll eingebettete Signatur gültig, Kopie einer unsignierten DLL = unsigniert, um ein Byte veränderte coreclr.dll = ungültig („verändert“); Autostart-Ordner mit echten Windows-Verknüpfungen (Explorer grün, fehlendes Ziel rot, cmd gelb) und unsignierter Temp-Datei (rot, verdächtiger Ort); echte Lesung dieses PCs. App `T0755_Ui.System_scan_page`. Echtlauf-Zählung: `reports/T-0755/spec12-systempruefung-echt.txt` (174 Einträge, 3 s, nur Zahlen). Der Echtlauf fand zwei Fehlalarme, beide behoben und mit Test belegt: rundll32 mit Schalter `/d`, Store-App-Verknüpfungen unter WindowsApps (jetzt „nicht prüfbar“ statt „ungültig“).
**Grenzen:** Ein gültig signierter Herausgeber mit bekanntem Namensbestandteil gilt als bekannt (Liste in `SystemScan.KnownPublishers`); laufende Prozesse, Dienste, Netzverbindungen und Ordner-Prüfung mit yara sind nicht Teil dieses Pakets.

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `T0755*`, `SYS04*`, `DownloadQueue*`, `NoFixedUser*` → 14/14 bestanden; dazu ConnectedPrompt/Sys03/PromptAction/W3/Studio-Filter 219 bestanden, 1 übersprungen.
- App: 82 Testklassen, die die geänderten Dateien berühren → Total 641, Failed 0, Skipped 3 (Echtläufe ohne Werkzeug); nach den letzten Korrekturen erneut `T0755_Ui`, `SPEC12_Ui`, `SYS04_Ui`, `T0681_Ui`, `Sys03_Netz_Ui`, `W1_Web`, `RoundSix_Ui` → 16/16.
- Rot-Grün: die neuen Tests kompilieren gegen den alten Stand nicht (RunAsync, Authenticode, SystemScan, History, Bookmarks.Collect fehlten).
- Belege mit SHA-256: `reports/T-0755/SHA256SUMS.txt`.
- Testanpassung: `FakePkg` (W3UiTests) merkt sich jetzt, was installiert ist – nötig, weil Trendo nach jedem Lauf nachprüft. Kein Test gelöscht oder geschwächt.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: TEILWEISE – Download mit Prüfsumme, Systembericht (HTML/CSV) entstehen über die Oberfläche; Installation nur ersetzt.
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: NICHT GETESTET – Bericht ist HTML/CSV, Öffnen im Browser/Tabellenprogramm nicht geprüft.
3. Prozessabbruch ≤ 30 s: TEILWEISE – Protokoll je Lauf zeilenweise, Verlauf und Bericht atomar; Neustart-Fortsetzen der Downloads bestand schon.
4. KI-Fachaufgabe: FEHLT (nicht Teil des Auftrags).
5. Test vom Klick bis zur echten Datei: VOLLWERTIG für Downloads (lokaler Server) und Systemprüfung; ERSETZT für Installieren.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: FEHLT/nicht geändert.

## Was NICHT geprüft wurde
- Echter winget-Lauf (installieren, erkennen, entfernen): **NICHT GETESTET – braucht Lauf durch den Nutzer.**
- Download aus dem Internet; Seiten-Darstellung der Systemprüfung mit Ergebnissen im Bild (nur leerer Zustand gerendert, Ergebnisse per UI-Test).
- Linux: Systemprüfung und Signaturprüfung sind nur für Windows gebaut (Linux meldet „nicht prüfbar“).

## Offene Punkte
- Nutzer: einen kleinen Echtlauf über „Programme verwalten“ machen (z. B. ein kleines freies Programm installieren und wieder entfernen) und das Protokoll aus `protokolle\programme` beilegen.
- Spezifikation/Katalog-Dokumente auf „Downloads & Links“ umbenennen (Freigabe des Nutzers nötig).
- Studio-Symbol (Globus) und Dock-Bild von SYS-03 sind unverändert.

## Geänderte Dateien
`src/Trendo.Core/W3/Packages.cs`, `src/Trendo.Core/Sys/Authenticode.cs` (neu), `src/Trendo.Core/Sys/Gefahren.cs` (neu), `src/Trendo.Core/Sys/DownloadQueue.cs`, `src/Trendo.Core/Sys/Sys03.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `src/Trendo.Core/PromptAction/StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `src/Trendo.Core/W2/StudioTeams.cs`, `src/Trendo.App/MainWindow.AppCenter.cs`, `MainWindow.DownloadManager.cs`, `MainWindow.W1Web.cs`, `MainWindow.Sys03.cs`, `MainWindow.Forensik.cs`, `MainWindow.Gefahren.cs` (neu), `MainWindow.DockLook.cs`, `MainWindow.Einstellungen.cs`, `MainWindow.Look.cs`, `MainWindow.W1.cs`, `MainWindow.W1Apps.cs`, `MainWindow.axaml.cs`, `StudioCatalog.cs`, `tests/Trendo.Core.Tests/T0755Tests.cs` (neu), `tests/Trendo.App.Tests/T0755UiTests.cs` (neu), `tests/Trendo.App.Tests/W3UiTests.cs`, `reports/T-0755/*`, `tasks/T-0755-*`.

## Gegenprüfung behoben (Korrektur auf 8fe0c0a79)

| Befund | Lösung | Test |
|---|---|---|
| HOCH 1: Persistenz-Tricks grün (regsvr32 /i:http, msiexec http, forfiles, certutil -urlcache, rundll32 shell32 ShellExec C:\Users\Public\x.exe, python/node/javaw) | `SystemScan.Assess` bewertet jetzt Startprogramm UND Argumente. Liste der Windows-Werkzeuge und Interpreter (regsvr32, msiexec, rundll32, forfiles, certutil, bitsadmin, schtasks, msbuild, installutil, regasm, regsvcs, cmstp, wmic, mshta, explorer mit Argument, curl, wget, python, pythonw, py, node, java, javaw, wscript, cscript, powershell, pwsh, cmd) → höchstens gelb. Ziele in den Argumenten werden aufgelöst (`ArgTargets`, auch rundll32-Ziel). URL (http/https/ftp) oder `\\UNC` → rot „Lädt aus dem Netz“; Ziel im Benutzer-, Public-, Temp- oder Downloads-Ordner → rot „Verdächtiges Ziel“. Je Fall eine Erklärung. Nur-Lösch-Befehle (`cmd /c del/rmdir` ohne Verkettung) prüfen kein Ziel. | `T0755_Gegenpruefung.Lolbins_never_green` |
| MITTEL 2a: Aktualisieren trotz Fehler „nachgeprüft“ | `RunAsync(…, expectVersion)`; `Verified` beim Aktualisieren nur, wenn die installierte Version danach der angekündigten (`Available`) entspricht; ohne Zielversion nie bestätigt. Protokoll „Version danach … · erwartet …“, eigene Meldung „Aktualisierung nicht bestätigt“. | `Verify_upgrade_and_broken_list` (ERSETZT: Testprogramm) |
| MITTEL 2b: Scheiternde Liste = „entfernt“ | `WingetBackend.IsInstalledAsync`: Exit 0 → Liste lesen, 0x8A150014 → false, jeder andere Code → null („nicht prüfbar“, „nicht bestätigt“). Listen werden nur bei `Verified` angepasst. | `Verify_upgrade_and_broken_list` (ERSETZT) |
| MITTEL 3: Umbenennung unvollständig, `Matrix_deckt_den_Katalog` rot | Matrix mit `TRENDO_UPDATE_MATRIX=1` neu geschrieben; Name nur in `docs/programme/katalog.json`, `KATALOG.md`, `docs/programme/downloads/*.md`, `docs/ALL_STUDIOS_GITHUB_GODMODE/{studio_registry.json, CURRENT_STUDIO_REGISTRY.md, studios/SYS-03/STUDIO_ANALYSIS.md}` ersetzt; Anzeigename von `W1_Web` angepasst. | Core PromptAction/Studio/Katalog/Vorbild/W3/NoFixedUser: 220 bestanden, 1 übersprungen |
| MITTEL 4: CSV ohne Formelschutz | `ReportCsv.C()` stellt Feldern mit `= + - @ Tab CR` ein `'` voran. | `Csv_formula_safe` |
| NIEDRIG 5: KnownPublisher vertraut Teilwörtern; Registry-Herausgeber | Genaue Organisationsnamen (vereinheitlicht: Groß/klein, Satzzeichen); grün nur mit dem Namen aus der Signatur, `DeclaredPublisher` nie. | `Known_publishers_exact` |
| NIEDRIG 6: SafeId mit führendem „-“ | Erstes Zeichen muss Buchstabe/Ziffer sein. | `SafeId_no_switch` |
| NIEDRIG 7: SHA-256 im UI-Thread, doppelt | `DownloadQueue.CompletedAsync` rechnet im Hintergrund und nutzt den Wert für den Vergleich (SHA-256 nur einmal gelesen). | `Download_hash_off_thread`, `T0755_Ui.Downloads_and_links` |
| NIEDRIG 8: Links ohne Schema-Prüfung geöffnet | Vor `Launch` http(s) prüfen, sonst Hinweis. | `T0755_Ui.Downloads_and_links` |

Weitere Fehlalarme aus dem Echtlauf behoben: `forfiles /p … /m notepad.exe` wurde bis „.exe“ als Programmpfad gelesen (jetzt Programm ohne Ordner über Windows-Ordner/PATH); relative Skriptnamen ohne bekannten Arbeitsordner gelten nicht als verdächtiger Ort.
Echtlauf danach (`reports/T-0755/spec12-systempruefung-echt.txt`): 174 Einträge, Rot 11, Gelb 63, Grün 100 (vorher Rot 1, Gelb 59, Grün 114). Rot sind jetzt u. a. versteckt startende PowerShell-Skripte aus dem Benutzerordner – genau das Muster, das die Gegenprüfung rot haben will.
Tests: Core `T0755*` 14/14 (davon 6 neu), Core-Kataloge/Matrizen 220 bestanden/1 übersprungen; App W3UiTests-Klassen, `T0755_Ui`, `W1_Web`, `Sys03_Netz_Ui`, `T0681_Ui`, `RoundSix_Ui`, `ForensikYara_Ui`, `StudiosAlle_Ui`: 131/131. Rot-Grün: die neuen Tests prüfen Verhalten, das 8fe0c0a79 nicht hatte (grüne LOLBins, Teilwort-Herausgeber, rohe CSV, „--override“), bzw. kompilieren dort nicht (`expectVersion`, `VersionAfter`, `CompletedAsync`, `ArgTargets`); ein getrennter Lauf gegen 8fe0c0a79 wurde nicht gemacht. Bild der Seite „System auf Gefahren prüfen“ geprüft.
Echter winget-Lauf weiterhin **NICHT GETESTET – braucht Lauf durch den Nutzer.**

## Zweitprüfung behoben (Fix2 auf d810ffd3b)

| Befund | Lösung | Test |
|---|---|---|
| HOCH 1: umbenannte Kopie von regsvr32.exe unter `%LOCALAPPDATA%\Programs\…\helper.exe` mit `/i:http://…` grün (Microsoft, Katalog) | Werkzeuge werden über `FileVersionInfo.OriginalFilename` bzw. `InternalName` erkannt (`SystemScan.ToolName`, deckt Umbenennen und 8.3-Kurznamen ab). Eine Microsoft-signierte Kopie eines Windows-Werkzeugs außerhalb von `%SystemRoot%` oder unter anderem Namen ist rot („Umbenannte Windows-Datei“). Netz- und Zielprüfung laufen für jeden Eintrag. | `T0755_Fix2.Renamed_regsvr32_copy_is_red` – echte Kopie von regsvr32.exe, echte Signaturprüfung, Kopie danach gelöscht. Den Run-Schlüssel habe ich nicht geschrieben (Registry-Schreiben verboten); der Eintrag wird im Test direkt gebaut. |
| MITTEL 2: odbcconf, pcalua, SyncAppvPublishingServer, hh grün | Liste ergänzt (odbcconf, pcalua, SyncAppvPublishingServer, hh, mavinject, msxsl, presentationhost, ieexec, dnscmd, diskshadow, esentutl, expand, extrac32, replace, ftp, finger). Für alle Einträge: Adresse (http/https/ftp/file:/UNC) im Befehl → mindestens gelb („Adresse im Befehl“); Programm- oder Skriptdatei in den Argumenten im Benutzer-, Public-, Temp- oder Downloads-Ordner → rot (außer im eigenen Programmordner). Klammern `{}`/`()` werden beim Lesen der Ziele entfernt. Windows-Einträge wie CompatTelRunner, defrag, sc bleiben grün. | `T0755_Fix2.More_tools_and_any_url` |
| NIEDRIG: `file:`, `/enc`, Gedankenstriche, Versionen mit `< >`/Unknown, Herausgeber-Varianten | `file:` in der Netzprüfung; versteckter Befehl erkennt `-`, `/`, `–`, `—` und Kurzformen von `-EncodedCommand`; `SameVersion` lehnt `<`, `>`, „Unknown“ ab; genaue Varianten (Zoom Communications, Inc.; Google Inc; Advanced Micro Devices; Dell Technologies Inc.; Lenovo (Beijing) Limited; UBISOFT ENTERTAINMENT INC.; Microsoft Windows Hardware Compatibility Publisher). Name in `docs/programme/netz-pruefen/*.md` nachgezogen. | `T0755_Fix2.Small_fixes` |

**Rot-Grün:** Die drei neuen Tests liefen zuerst gegen d810ffd3b und schlugen fehl (HOCH: „Gruen“ statt „Rot“), Beleg `reports/T-0755/core-tests-fix2-rot-auf-d810ffd3b.txt`; danach grün.
**Tests:** Core T0755*, W3, PromptActionTests, DownloadQueue, NoFixedUser, SYS04: 113/113. App `T0755_Ui`, `SPEC12_Ui`, `SYS04_Ui`, `W1_Web`, `Sys03_Netz_Ui`, `ForensikYara_Ui`, `StudiosAlle_Ui`: 112/112.
**Echtlauf** (`reports/T-0755/spec12-systempruefung-echt.txt`, nur Zählungen): 174 Einträge, Rot 13, Gelb 60, Grün 101. Keine Windows-Aufgabe ist rot, kein Treffer „Umbenannte Windows-Datei“.

**Hinweis für den Nutzer:** Eigene versteckt startende Hilfsaufgaben, deren Skript im Benutzerordner liegt (z. B. PowerShell mit `-WindowStyle Hidden -ExecutionPolicy Bypass -File C:\Users\…\skript.ps1` oder Blender mit `-P C:\Users\…\skript.py`), erscheinen **absichtlich rot** – genau so tarnen sich Schadprogramme. Die Erklärung im Eintrag sagt das; wenn du die Aufgabe selbst angelegt hast, ist alles in Ordnung. Trendo ändert daran nichts.

**Status:** Umsetzung fertig und gezielt getestet; SYS-04-Echtlauf offen.
