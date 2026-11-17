# Bericht T-0681 B1b: Sicherung und gefundene Fehler

**Branch:** `claude/T-0681-b1b-fehler` (von `origin/main`, Stand `f68974c6d`), eigener Worktree. Nicht gepusht, kein PR, kein Merge.

Alle sechs Befunde wurden zuerst gegen den Code auf `origin/main` geprüft – alle sechs stimmen.

## 1. Sicherung (`src/Trendo.Core/Backup.cs`)
- **War falsch (Zeile 21–29):** Die ZIP wurde direkt unter dem endgültigen Namen geschrieben. Eine gesperrte Datei warf eine `IOException`, die Schleife brach ab, die halbe ZIP blieb liegen und galt als neueste Sicherung (`List().First()`, `Due`).
- **Jetzt:**
  - `CreateChecked` schreibt in `sicherung-<guid>.teil`, liest danach die ganze Datei zurück (Anzahl der Einträge, Größe, SHA-256 je Datei gegen das beim Lesen der Quelle Berechnete) und benennt erst dann in `trendo-sicherung-<zeit>.zip` um. `Create` bleibt als bisheriger Aufruf erhalten.
  - Gesperrte oder nicht lesbare Dateien brechen nichts ab. Sie stehen im Ergebnis (`Result.NotSaved`) und als Vermerk in der ZIP selbst (ZIP-Kommentar, kein zusätzlicher Eintrag – alte und neue Sicherungen bleiben gegenseitig lesbar). Im Protokoll: `backup.incomplete`.
  - Bricht das Lesen mitten in einer Datei ab, wird die ZIP ohne diese Datei neu geschrieben (kein halber Eintrag).
  - Reste (`*.teil`) räumt das nächste Sichern weg.
  - `Latest()`/`Due` zählen nur Sicherungen, die sich öffnen lassen – eine beschädigte Datei aus einer älteren Version blockiert keine neue Sicherung mehr.
  - `Restore` prüft vorher mit `Verify` (alles entpacken, Größe und CRC-32). Beschädigt: `InvalidDataException` mit Meldung „… übersprungen – es wurde nichts geändert“, Protokoll `backup.restore.skipped`, keine „Vorher“-Sicherung, keine Datei angefasst.
  - Seite „Sicherung“: Meldung nach „Jetzt sichern“ nennt „Unvollständig gesichert … nicht gesichert: …“ (gelb); jede Zeile der Liste zeigt „unvollständig: n Datei(en) nicht gesichert“ bzw. „beschädigt – nicht verwendbar“. Systemprüfung zeigt „Hinweis“ statt „Bereit“, wenn die letzte Sicherung unvollständig ist.
- **Tests:** `T0681_Sicherung` (2), `T0681_Sicherung_Pruefung` (3), UI `T0681_Ui.Sicherung_zeigt_unvollstaendig_und_beschaedigt`.
  - Rot vorher (auf altem Code gelaufen): `Gesperrte_Datei_bricht_nicht_ab` (IOException), `Absturz_beim_Sichern_laesst_keine_halbe_Sicherung` („… ist eine halbe Sicherung“).
  - **Echter Prozessabbruch:** `Absturz_beim_Sichern…` startet die Test-DLL als eigenen Kindprozess (`AbsturzKind`), der 24 MB in einer Schleife sichert, und beendet ihn 4-mal hart mitten im Schreiben.

## 2. Abläufe (`src/Trendo.Core/W2/Workflows.cs`)
- **War falsch:** Zeile 86: beschädigte Datei wurde per `catch { _items = []; }` still als leer gelesen – das nächste Speichern überschrieb sie endgültig. Zeile 230: `w.LastRun = t` auch bei Fehlschlag – ein täglicher Ablauf, der scheiterte, lief erst am nächsten Tag wieder. Zeile 261: `File.WriteAllText` direkt auf die Datei.
- **Jetzt:**
  - `LastRun` zählt nur geglückte Läufe. Neu am Ablauf: `FailCount`, `LastError`, `LastAttempt` (werden mitgespeichert; alte Dateien bleiben lesbar).
  - Ein Fehlschlag bleibt fällig und wird Takt für Takt erneut versucht, höchstens `MaxRetries = 3`-mal; danach erst zum nächsten regulären Zeitpunkt (täglich: am nächsten Tag; Intervall: nach dem nächsten Intervall). „Neue Datei“: die Datei gilt wieder als neu (früher wurde sie nie wieder versucht). Ein Ablauf ohne Freigabe zählt als Fehlversuch (früher schrieb er jede Minute einen Eintrag ins Protokoll, unbegrenzt).
  - Ändern oder Freigeben des Ablaufs setzt den Zähler zurück; ein geglückter Lauf löscht den Fehlerstand.
  - Anzeige: roter Text am Ablauf (`wf-fail-<n>`): „Letzter Lauf fehlgeschlagen (n Versuche, zuletzt …): <Meldung> …“.
  - Speichern atomar: eigene vorläufige Datei, `Flush(flushToDisk)`, dann in einem Schritt ersetzen.
  - Beschädigte Datei wird nach `ablaeufe.json.beschaedigt-<zeit>` umbenannt, gemeldet (`LoadProblem`, Seite Abläufe `wf-load-problem`, Protokoll `workflow.datei.beschaedigt`). Ist sie nur gerade nicht lesbar, wird nichts überschrieben.
- **Tests:** `T0681_Ablaeufe` (3), `T0681_Ablaeufe_Fehlerstand` (4), UI `T0681_Ui.Ablaeufe_zeigen_Fehlerstand`.
  - Rot vorher (auf altem Code gelaufen): `Fehlschlag_setzt_LastRun_nicht`, `Beschaedigte_Datei_wird_aufgehoben`, `Absturz_beim_Speichern_laesst_Datei_ganz` („Ablaufdatei nach dem Absturz beschädigt … 2342912 Bytes“).
  - **Echter Prozessabbruch:** Kindprozess speichert eine 3-MB-Ablaufdatei in Dauerschleife und wird 10-mal hart beendet; danach muss die Datei jedes Mal vollständig lesbar sein.

## 3. Podcast (`src/Trendo.App/MainWindow.KreativStudios.cs`)
- **War falsch (Zeile 278–279):** `_podcBusy = true` stand vor der FFmpeg-Prüfung; ohne FFmpeg kam `return` ohne Zurücksetzen → jeder weitere Klick: „wird schon angeglichen“.
- **Jetzt:** erst prüfen, dann Merker setzen, unmittelbar vor dem `try` mit `finally`. Meldung: „FFmpeg fehlt … So richtest du es ein: Einstellungen › Programme › Zusatzprogramme, dort bei FFmpeg auf ‚Einrichten‘ …“.
- **Test:** `T0681_Ui.Podcast_Merker_bleibt_nicht_haengen`. Rot nachgewiesen durch Zurückstellen der Zeile (Gegenprobe: „Die Lautheit wird schon angeglichen“ statt „FFmpeg fehlt“), danach wiederhergestellt. FFmpeg fehlt im Test über `PodcFfmpegPath = ""` (ersetzt, weil FFmpeg auf dem Prüf-PC vorhanden ist).

## 4. App-Center (`src/Trendo.App/MainWindow.AppCenter.cs`)
- **War falsch (Zeile 30–32):** `Plan` und `Approve` im selben Klick – der Text „Vor jeder Änderung fragt Trendo dich“ stimmte nicht.
- **Jetzt:** Vor Installieren, Aktualisieren und Entfernen ein eigenes Fenster (Muster wie „Ordner teilen“: `ApprovalGate` + Dialog, „Abbrechen“ vorbelegt, Esc/Schließen lehnt ab). Inhalt: Programm, Paket, Version, woher (Quelle + Paketverwaltung), Größe („vorab nicht bekannt“ – die Paketverwaltung liefert sie nicht). Nein: `Approvals.Deny` + Protokoll `app.abgelehnt <vorgang> <paket>`, Meldung „abgebrochen – es wurde nichts geändert“.
- **Tests:** `T0681_Ui.AppCenter_fragt_vor_dem_Installieren` (echtes Dialogfenster im Headless-Lauf: offen → nichts installiert; Abbrechen; Schließen; erst „Installieren“ installiert), `T0681_Ui.AppCenter_fragt_vor_dem_Entfernen`. Der bestehende Test `SYS04_Ui.AppCenter_page` setzt jetzt ausdrücklich „Mensch stimmt zu“ (`AppConfirm`), sonst unverändert.

## 5. Code-Editor (`src/Trendo.App/MainWindow.CodeEditor.cs`, `src/Trendo.Core/Cli.cs`)
- **War falsch (Zeile 84–99):** `Cli.Run(…, 120000)` bzw. `DevStudio.RunTests` (180 s) liefen im Klick-Handler auf dem Oberflächen-Thread.
- **Jetzt:** neu `Cli.RunStreamAsync` (Ausgabe zeilenweise, Abbruch beendet den Prozess samt Unterprozessen, Zeitlimit wie bisher). Der Editor startet damit im Hintergrund, schreibt die Ausgabe laufend in das Ausgabefeld, sperrt „Ausführen“/„Testen“ solange und zeigt „■ Abbrechen“. Schließen von Trendo beendet ein laufendes Programm.
- **Tests:** Core `T0681_Cli` (2): echter Kindprozess mit eigenem Unterprozess, nach Abbruch sind beide beendet. UI `T0681_Ui.CodeEditor_laeuft_im_Hintergrund_und_bricht_ab`: echter Kindprozess (300 s), Klick kehrt sofort zurück, Zeile erscheint während des Laufs, andere Knöpfe reagieren, „Abbrechen“ beendet den Prozess (Prozessnummer geprüft).
- **Ehrlich:** Ein Rot-Lauf auf altem Code war hier nicht möglich (im Testmodus startete der Editor nie etwas); der Beleg ist der alte Code selbst. Der Test startet ein ersetztes Kommando (`CodeRunOverride`), nicht Python/dotnet.

## 6. Stille Test-Rückkehr
- 84 Stellen `if (…) return;` umgestellt: Api 1, App 13 (`Assert.Skip`), Core 70 (`Ueberspringen.Weil`). Keine Prüfung entfernt oder abgeschwächt; was vor der Stelle geprüft wird, läuft weiter.
- Core und Api nutzen xUnit 2 ohne `Assert.Skip`. Neu: `tests/*/Ueberspringen.cs` – eine kleine xUnit-Erweiterung (`TestFramework`), die die Ausnahme als „übersprungen“ meldet. (Der erste Versuch über die Kennung `$XunitDynamicSkip$` wirkt in xUnit 2.9.3 nicht: 14 Tests schlugen fehl, deshalb die Erweiterung.)
- **Auf diesem PC** (FFmpeg, Node.js, LibreOffice, Git, Python vorhanden): 69 Stellen laufen ganz durch, **15 Tests sind jetzt sichtbar übersprungen**, die vorher als bestanden zählten:
  - Core 14: CORE-04 Begleiter ×4 und CORE-09 Aufnahme, Standardbrowser Linux, Linux-Wege (nur Linux); T-0223 T1 (TRENDO_LO_ABGLEICH), M4 Cover (TRENDO_COVER_ECHT), S3-13 Wan (TRENDO_GENVIDEO_ECHT), S4-06 Plugin ×2 (Plugin-Umgebung), S3-09 Lippensync ×2 (Modelle fehlen)
  - App 1: CORE-04 UI Begleiter (nur Linux)
- Umgestellte Dateien (Zeile):
  - Api: ApiTests 43
  - App: ClaudeRunFixUiTests 25, 43 · DevicesPciUiTests 21 · MpvWindowTests 17 · MusikImportUiTests 17 · MusikSunoUiTests 294, 349 · PdfF3UiTests 86 · UebersichtVorschlaegeUiTests 179 · VideoEnhanceUiTests 141, 199 · VideoLipSyncUiTests 17 · W7UiTests 35
  - Core: AudioQaTests 68 · CameraRobotTests 82 · ColorScopesTests 54 · CoverStudioTests 95 · CreditsTests 61 · DesktopPlatformTests 24, 45 · DocMarkdownTests 49 · FilmDeliveryTests 71 · GameCombatTests 79 · GameDocumentTests 241 · GameInventoryTests 61 · GameMenusTests 31, 41, 52, 61, 79 · GameSavesTests 74 · GameVehicleTests 64 · GameplayTests 79 · GenVideoTests 64 · GreenscreenTests 39 · LetterF3Tests 61 · LetterQaTests 76, 263 · LipSyncTests 73, 107, 124 · LiveShowTests 70 · MulticamTests 36 · MusicProTests 73 · MusicSoundsTests 36 · MusicToolsTests 20 · PluginHostTests 53 · PluginInspectTests 12, 45 · ProExportTests 47 · SandboxTests 19, 42 · SheetChartPdfTests 91 · SheetExtrasTests 115 · SheetFilesTests 129 · SheetFunctionTests 139 · ShotPlanTests 52 · SlideTests 117 · Sys10Tests 68, 77 · Tests 179 · TextDocTests 147, 210 · TextProTests 173, 193 · TimePitchTests 23 · TimedTextTests 53 · VideoEditPlusTests 30 · VideoEditTests 56 · VideoEnhanceTests 57 · VideoExportTests 63 · VideoProTests 44 · VideoProxyTests 13 · VideoSubtitleTests 45 · W0bLookTests 69 · W4DevTests 23 · W4MediaTests 18 · W4OssTests 23 · W7Tests 85, 292, 317, 373 · WebDesignStufe3QaTests 236
- Bewusst nicht umgestellt: `BambuTests` 56 (Server-Attrappe), `SlideTests` 85 / `TextProTests` 87 (Parameter einer Prüfhilfe), `PdfToolsTests` 25 (Hilfsfunktion), `UebersichtEinfachUiTests` 93/159 (Berichtsmodus per Schalter).

## Kommandos und Ergebnisse (Zahlen aus der Ausgabe)
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build` (ganzes Core-Projekt, weil 53 Core-Testdateien berührt sind) → Exit 0, **gesamt 2611, bestanden 2592, übersprungen 19** (5 davon schon vorher über `NeedsFact`), Fehler 0
- `dotnet test tests/Trendo.Api.Tests -c Release --no-build` → 16 bestanden, 0 übersprungen
- App gezielt (32 Klassen: alle berührten Testdateien, `T0681_Ui`, Sicherung, Abläufe, App-Center, Code-Editor, Kreativ, `NoFixedUserTests`) → Total 87, Failed 0, Skipped 1
- Namensprüfung (`grep` aus dem Auftrag) über alle geänderten Dateien: leer

## Nicht geprüft
- Kein Volltest der App-Tests (macht die Hauptsitzung), keine Paket-Tests.
- Nichts in der laufenden App von Hand geklickt; kein echtes winget/flatpak, kein echtes FFmpeg im Podcast-Test.
- Linux nicht geprüft (`Cli.RunStreamAsync`, atomares Ersetzen laufen dort über dieselben .NET-Aufrufe, ungetestet).
- Stromausfall des Rechners (nur Prozessabbruch).

## Offene Punkte / Risiken
- `tests/Trendo.Core.Tests/Trendo.Core.Tests.csproj`: `GenerateProgramFile=false` und eigener Einstieg `AbsturzKind.Main` für die Kindprozess-Tests. Falls Paket B1a dort ebenfalls einen Einstieg anlegt, müssen beide beim Zusammenführen in einer `Main` landen.
- Software-Seite `MainWindow.W4Dev.cs:56` („Testen“) ruft weiter `DevStudio.RunTests` (bis 180 s) im Oberflächen-Thread auf – derselbe Fehler wie im Code-Editor, nicht Teil dieses Auftrags.
- `MainWindow.Workflows.cs:111` („Freigeben …“ bei Abläufen) erteilt die Freigabe ebenfalls im selben Klick – gleiches Muster wie im App-Center, nicht Teil dieses Auftrags.
- Sechs Tests prüfen Node/FFmpeg nur in einem `if (… vorhanden) { … }`-Block und zeigen ein Fehlen weiter nicht an: `GameProTests` 95, `GameRulesTests` 73/145, `W4DevTests` 42, `W4GameTests` 17, `MusikSunoUiTests` 387.
- Fehlerzähler der Abläufe gilt je Ablauf, nicht je Datei: Bei „Neue Datei“ mit mehreren scheiternden Dateien teilen sich diese die drei Versuche.
- Wiederherstellen liest die Sicherung jetzt zweimal (Prüfen, dann Entpacken) – bei sehr großen Sicherungen entsprechend länger.

**Status:** umgesetzt und gezielt getestet; wartet auf Volltest und Review der Hauptsitzung.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Verwendete Sprachen: C# (.NET 10) · Rust eingesetzt: NEIN · Rust-Komponenten: keine
- Grund der Sprachwahl: Korrekturen in bestehendem C#-Code; kein Engpass, der eine zweite Sprache rechtfertigt
- Durchgeführte Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung (Fehlerbehebung) · Offene Probleme: siehe „Offene Punkte“
