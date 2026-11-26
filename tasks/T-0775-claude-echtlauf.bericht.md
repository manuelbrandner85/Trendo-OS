# Bericht T-0775: Q2 – ein echter Claude-Abo-Lauf je Programmart

**Branch:** `claude/T-0775-claude-echtlauf` (Basis main 0.9.111 = 2b1ffa923), eigene Kopie in `%TEMP%\trendo-auftrag-T-0775`.

## Was war
- Alle Claude-Tests der Programme ersetzten Claude (`ClaudeRunOverride`/`ClaudeStatusOverride`); der Testmodus (`Isolated`) sperrte das echte Claude Code an vier Stellen (`ClaudeCheck`, `ClaudeRun`, `ClaudeTakesStudioPrompts`, `DefaultKiBackends`). Echte Claude-Läufe gab es nur in T-0698/T-0742, dort über einen Ersatz-Haken, der `claude.exe` startete.
- „Ideen mit Claude“ (Logos & Marken) ließ Claude nur Ideen beschreiben; was im Dokument landete, war offen (`logo-claude` stand in keinem Test).
- „Briefe schreiben“ hatte Rückgängig nur als Knopf, kein Strg+Z.

## Was ist jetzt
- **Testschicht „echt“:** `MainWindow.ClaudeEchtImTestmodus` (Standard aus) hebt nur die Sperre des Testmodus auf (`ClaudeRealAllowed`). Anmeldestand und Läufe gehen dann durch denselben Code wie im Produkt: `claude auth status --json`, `ClaudeRun` → `ClaudeEnv` (API-Variablen und `CLAUDE_CODE_OAUTH_TOKEN` entfernt) → `ClaudeRunProcess`. Kein Override, keine Ersatz-KI. Trendo meldet nie an, speichert keine Tokens.
- **Testklasse `ClaudeEchtlauf_Ui`** (`tests/Trendo.App.Tests/ClaudeEchtlaufUiTests.cs`), aktiv nur mit `TRENDO_ECHT_CLAUDE=<Belegordner>`:
  - Kreativ `Logo_recolor_real`: Klick „Ideen mit Claude“ → Weiche „Dokument“ → Claude (Abo) plant `dokument.setzen` → Vorschau (Original unverändert) → „Ja“ → ein Rückgängig-Schritt, Datei auf der Platte → Strg+Z = Ausgangsstand. Farben vorher #7A2E1F/#E0A458, nachher #8C2F1B/#B5541C; Quelle „KI (Claude (dein Abo))“ geprüft.
  - Büro `Letter_shorten_real`: Claude-Bereich im Brief-Studio, Claude ändert `Brief.json`, Trendo übernimmt live in den offenen Brief (754 → 205 Zeichen, „Lindenweg“ bleibt), Strg+Z = alter Text.
  - Technik `Floorplan_move_wall_real`: gespeicherter Plan „Badezimmer“, Claude verschiebt Wand um +50 cm (272 → 322), Speichern wie Strg+S prüft die Datei, Strg+Z = alte Lage.
  - Zusatz `CodeAi_change_real`: „Mit Claude ändern“ in Code mit KI, `rechner.py` bekommt den ValueError, „Zurücknehmen“ stellt die Datei her.
  - Zusatz `Ask_with_attachment_real`: KI fragen mit `lieferschein.txt`, Antwort von Claude (Status nennt Claude) mit „12“ und „Donnerstag“; im Rohlog steht der Dateitext.
  - Ohne Variable: „übersprungen“ (5 von 7, Beleg `testlauf-ohne-variable.txt`). Fehlt Claude oder die Anmeldung: „übersprungen“ mit „BLOCKIERT – …“ und dem Schritt (`claude` starten, `/login` mit dem eigenen Abo) plus Datei `<fall>-BLOCKIERT.txt`.
  - Immer aktiv: `Switch_off_never_starts_claude` (ohne Schalter: „nicht installiert“, Lauf abgelehnt, Claude nicht Haupt-KI) und `Letter_ctrl_z` (Strg+Z/Strg+Y im Brief).
- **Produkt:** „Ideen mit Claude“ setzt jetzt die beste Idee gleich im offenen Logo um (Farben der Formen, primary/accent; Strg+Z nimmt sie zurück). „Briefe schreiben“ hat Strg+Z / Strg+Y / Strg+Umschalt+Z (außerhalb von Eingabefeldern, die nehmen selbst zurück), Tooltips nennen die Kürzel.
- **Belege `reports/T-0775/`:** je Fall `*-rohlog.jsonl` (stream-json von Claude Code; Benutzername durch `<user>` ersetzt, aus Start- und Befehlszeile nur Technisches und Anzahlen, keine Liste der Erweiterungen der Person), `*-vorher`/`*-nach-claude`, `*-bericht.md`, dazu die Konsolenausgaben, `README.md` und `SHA256.txt`.

## Welcher Weg ist echt getestet – ehrlich
Die Echtläufe laufen im Testmodus. Dort ist „Claude ohne Klicks“ (`ClaudeNoClicks`) aus. Getestet sind deshalb diese Wege:
- **Logo:** die Kette ins offene Dokument (Weiche „Dokument“: Plan → **Vorschau → „Ja“** → ein Rückgängig-Schritt). Die Vorschau mit „Ja“ gibt es **nur auf diesem Weg**.
- **Brief und Grundriss:** der Claude-Bereich ohne Brücke: Claude schreibt die Arbeitsdatei (`Brief.json` bzw. `.tdoc`) im Projektordner, Trendo übernimmt sie live als einen Rückgängig-Schritt, **ohne Vorschau**.
- **Nicht echt getestet ist der Produkt-Standard „Claude ohne Klicks“** (freigegeben 09.11.2026, `trendo-claude-handsfree`): Ist Claude verbunden und „ohne Klicks“ an, geht „Ideen mit Claude“ in den Claude-Bereich, und die MCP-Brücke schreibt `dokument_setzen` direkt ins Dokument – ein Rückgängig-Schritt für den ganzen Lauf (`ClaudeRunSquash`), aber **ohne Vorschau und ohne den Hinweis-Text** aus Punkt 7. Diesen Brückenweg prüfen bisher nur Tests mit ersetztem Claude (`ClaudeBridge_Ui`, `T0728Bedienung`), kein Echtlauf.

## Nachbesserung nach der Gegenprüfung (zweiter Commit, ohne neuen Echtlauf)
1. **Schalter pro Fenster:** `ClaudeEchtImTestmodus` ist jetzt eine Eigenschaft des Fensters, nicht mehr statisch. `OpenReal` setzt ihn erst nach `Ui.Open` am eigenen Fenster in try/catch, `Close` setzt ihn zurück. Test `Switch_is_per_window`: Fenster A an, Fenster B und ein später geöffnetes Fenster C bleiben aus, B meldet „nicht installiert“, `ClaudeRun` lehnt ab, Claude ist dort nicht Haupt-KI.
2. **Bericht:** Abschnitt „Welcher Weg ist echt getestet“ (oben); auch in der Taskdatei berichtigt.
3. **Kontodaten:** `CopyRawLog` lässt `rate_limit_event`-Zeilen weg und entfernt `credential_expires_at`, `total_cost_usd`, `costBasis` und `costUSD` (rekursiv, `StripAccount`). Die Angabe des Abo-Typs steht nicht mehr im Bericht-Kopf. Die vorhandenen Rohlogs sind nachträglich nur um diese Daten gekürzt (je Datei 1 Zeile entfernt, 1 Zeile gekürzt), `SHA256.txt` ist neu erzeugt, Vermerk in `reports/T-0775/README.md`. Test: `Raw_log_filter_strips_account_data`.
4. **Rohlog aus diesem Lauf:** `CopyRawLog` prüft, dass die Mitschrift nach dem Start des Laufs geschrieben wurde (Zeitstempel) und dass Start- und Ergebniszeile dieselbe Sitzungs-Id haben. Mangels neuem Echtlauf ist das noch nicht mit echtem Claude gelaufen.
5. **Brief von Claude übernehmen:** `ClaudeSyncLetter` sichert vor `_letter = fresh` mit `LetterCommit` das gerade Getippte als eigenen Schritt. Foto und Unterschrift von Claude gelten nur, wenn die Datei im Claude-Projektordner liegt (auch `..\` wird abgewiesen). Der schon gewählte eigene Pfad der Person bleibt erlaubt, sonst bleibt der bisherige Stand. Test: `Claude_letter_sync_keeps_typing_and_limits_files`.
6. **Strg+Z im Eingabefeld:** Test `Letter_ctrl_z_in_field_stays_in_field`: Strg+Z im Feld lässt den Brief-Schritt stehen, außerhalb nimmt es ihn zurück.
7. **Ideen sichtbar:** Die KI darf zum Plan einen kurzen Text im Feld `hinweis` schreiben (`AssistPlan.Note`). Die Vorschau zeigt ihn (`assist-ki-note`), und „Ideen mit Claude“ bittet darin um die drei Ideen mit Begründung. Test mit ersetzter KI: `Logo_ideas_visible_in_preview`. Ob echtes Claude das Feld füllt, ist ohne neuen Echtlauf **nicht geprüft**. Auf dem Brückenweg (oben) erscheint der Text nicht.

## Geänderte Dateien
- `src/Trendo.App/MainWindow.Claude.cs`, `MainWindow.ClaudeLos.cs`, `MainWindow.W2.cs` (Schalter `ClaudeEchtImTestmodus`)
- `src/Trendo.App/MainWindow.KreativStudios.cs` (Auftrag „Ideen mit Claude“)
- `src/Trendo.App/MainWindow.W1Office.cs` (Strg+Z/Strg+Y im Brief)
- `tests/Trendo.App.Tests/ClaudeEchtlaufUiTests.cs` (neu)
- `reports/T-0775/*` (neu), `tasks/T-0775-claude-echtlauf.md`, `tasks/T-0775-claude-echtlauf.bericht.md`, `.gitattributes` (`reports/T-0775/** -text`)
- Nachbesserung: `src/Trendo.App/MainWindow.Claude.cs` (Schalter pro Fenster), `src/Trendo.App/MainWindow.ClaudeLetter.cs` (LetterCommit, Pfade nur aus dem Projektordner, Test-Zugang), `src/Trendo.Core/Programs/Assist.cs` (`AssistPlan.Note` aus „hinweis“), `src/Trendo.App/MainWindow.Assist.cs` (Hinweis in der Vorschau), `src/Trendo.App/MainWindow.KreativStudios.cs` (Auftrag bittet um „hinweis“), `tests/Trendo.App.Tests/ClaudeEchtlaufUiTests.cs`, `reports/T-0775/*-rohlog.jsonl`, `*-bericht.md`, `README.md`, `SHA256.txt`

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Echtlauf: `TRENDO_ECHT_CLAUDE=reports/T-0775 Trendo.App.Tests.exe -class Trendo.App.Tests.ClaudeEchtlauf_Ui`: **Total 7, Failed 0, Skipped 0, 122,8 s** (Claude Code 2.1.319, Anmeldung claude.ai über das Abo).
- Ohne Variable: Total 7, Failed 0, **Skipped 5**, 3,9 s.
- Berührte Testklassen ohne Variable: alle 24 Claude*-Klassen, AssistChain_Ui, T0708_HuelleEtappe1_Ui, DesignPaket3_Ui, ChatAttachT0734_Ui, T0688_SoftwareStudio_Ui: **Total 108, Failed 0**, 332 s. KreativStudios/VectorStudios/ProgramHeadDesign (Logo): **Total 16, Failed 0**, 54 s.
- Nachbesserung (ohne Variable, kein neuer Echtlauf): ClaudeEchtlauf_Ui, alle Claude*-Klassen, AssistChain, T0708, DesignPaket3, ChatAttachT0734, T0688, VectorStudios, KreativStudios: **Total 130, Failed 0, Skipped 5** (die 5 Echt-Tests), 367 s; Core `~Assist`: 17/17 bestanden.
- Kein Volltest (macht die Hauptsitzung).
- Core: `NoFixedUser`: 1/1 bestanden.

## Abnahmekriterien (nur für dieses Paket)
1. Leere Vorlage → Ergebnis: nicht Teil des Pakets (NICHT GETESTET hier).
2. Fremddatei: nicht Teil des Pakets (NICHT GETESTET hier).
3. Absturz ≤ 30 s: nicht Teil des Pakets (NICHT GETESTET hier).
4. **KI im offenen Dokument, rückgängig, echt:** VOLLWERTIG für Logos & Marken, Briefe schreiben, Grundriss & Wohnen (echter Claude-Lauf, Strg+Z, Rohlog+SHA-256); Code mit KI: VOLLWERTIG für „Mit Claude ändern“ (Zurücknehmen statt Strg+Z); KI fragen mit Anhang: echte Antwort belegt (kein Dokument).
5. Klick bis Datei mit echtem Werkzeug: TEILWEISE – Logo (Klick → Datei) und Grundriss (Speichern) mit echtem Claude; Brief-Export nicht erneut geprüft.
6. Kürzel: TEILWEISE – Strg+Z/Strg+Y im Brief neu; Rechtsklick im Brief weiter offen.

## Nicht geprüft / ehrlich ersetzt
- Fensterdarstellung headless (Avalonia Headless). Code mit KI: Ordnerauswahl ersetzt (`FolderPickOverride`), Claude selbst nicht.
- Produkt-Standard „Claude ohne Klicks“ (MCP-Brücke, `dokument_setzen` direkt, ohne Vorschau) nicht echt gelaufen – im Testmodus aus; siehe „Welcher Weg ist echt getestet“.
- Nachbesserung (Punkte 4 und 7) ohne neuen Echtlauf: Herkunftsprüfung des Rohlogs und das Feld `hinweis` sind nur mit ersetzter KI bzw. ohne Claude geprüft.
- Andere Programme der Hülle ohne eigenen Echtlauf (je Programmart einer).
- Claudes Antworten schwanken; die Tests prüfen das Ergebnis im Dokument, nicht den genauen Text.

## Offene Punkte
- Ein Echtlauf verbraucht Abo-Kontingent (hier 5 Läufe, rund 2 Minuten); nur bewusst mit der Variable starten.
- Rechtsklick im Brief, Kontextmenü im Dateibaum von Code mit KI (Rang 2) gehören nicht zu Q2.
