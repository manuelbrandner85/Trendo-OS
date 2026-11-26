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
- **Belege `reports/T-0775/`:** je Fall `*-rohlog.jsonl` (stream-json von Claude Code; Benutzername durch `<user>` ersetzt, aus Start- und Befehlszeile nur Technisches und Anzahlen, keine Liste der Erweiterungen der Person), `*-vorher`/`*-nach-claude`, `*-bericht.md`, dazu die Konsolenausgaben und `SHA256.txt`.

## Geänderte Dateien
- `src/Trendo.App/MainWindow.Claude.cs`, `MainWindow.ClaudeLos.cs`, `MainWindow.W2.cs` (Schalter `ClaudeEchtImTestmodus`)
- `src/Trendo.App/MainWindow.KreativStudios.cs` (Auftrag „Ideen mit Claude“)
- `src/Trendo.App/MainWindow.W1Office.cs` (Strg+Z/Strg+Y im Brief)
- `tests/Trendo.App.Tests/ClaudeEchtlaufUiTests.cs` (neu)
- `reports/T-0775/*` (neu), `tasks/T-0775-claude-echtlauf.md`, `tasks/T-0775-claude-echtlauf.bericht.md`

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Echtlauf: `TRENDO_ECHT_CLAUDE=reports/T-0775 Trendo.App.Tests.exe -class Trendo.App.Tests.ClaudeEchtlauf_Ui`: **Total 7, Failed 0, Skipped 0, 122,8 s** (Claude Code 2.1.319, Anmeldung claude.ai, Abo max).
- Ohne Variable: Total 7, Failed 0, **Skipped 5**, 3,9 s.
- Berührte Testklassen ohne Variable: alle 24 Claude*-Klassen, AssistChain_Ui, T0708_HuelleEtappe1_Ui, DesignPaket3_Ui, ChatAttachT0734_Ui, T0688_SoftwareStudio_Ui: **Total 108, Failed 0**, 332 s. KreativStudios/VectorStudios/ProgramHeadDesign (Logo): **Total 16, Failed 0**, 54 s.
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
- Claude-Bereich mit „ohne Klicks“ (MCP-Brücke) nicht echt gelaufen – im Testmodus aus.
- Andere Programme der Hülle ohne eigenen Echtlauf (je Programmart einer).
- Claudes Antworten schwanken; die Tests prüfen das Ergebnis im Dokument, nicht den genauen Text.

## Offene Punkte
- Ein Echtlauf verbraucht Abo-Kontingent (hier 5 Läufe, rund 2 Minuten); nur bewusst mit der Variable starten.
- Rechtsklick im Brief, Kontextmenü im Dateibaum von Code mit KI (Rang 2) gehören nicht zu Q2.
