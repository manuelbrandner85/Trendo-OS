# Bericht T-0687 B2+B3: Ehrlicher Status und Echtlauf-Station

**Branch:** `claude/T-0687-echtlauf` (von `claude/T-0685-b1-integration`, `bf19f45cf`), eigene Kopie in `%TEMP%`. Kein Merge, kein Release.

## 1. Echtlauf-Station (`tools/echtlauf/`)
- **War:** Echtläufe nur als Einzelbelege (`reports/paket01/…`, `reports/alle-studios/…`) und als Tests, die ohne Werkzeug still oder sichtbar übersprungen werden. Kein Befehl, der den Stand auf einem PC belegt (Audit 6, Abschnitt 3, Maßnahme 9).
- **Jetzt:** `python tools/echtlauf/echtlauf.py [--nur …] [--langsam] [--bauen]`.
  - Suchen über Trendos eigene Wege (C#-Helfer `tools/echtlauf/Trendo.Echtlauf`, nicht in `Trendo.sln`, wie `Trendo.Render`): `DependencyManager.ResolvePath/FindOnSystem`, `EngineTargets.FindGodot/FindUnity`, `GameStudio.FindEngines`, `WebsitePreview.FindBrowser`, `ClaudeCode.Find`, `KiLibraryStore`, `ModelConverter.FindBlender`. Nicht genutzt: der gemerkte Pfad aus `tools.json` des Datenordners (die Station liest den echten Datenordner nicht).
  - Fachläufe: vorhandene Echt-Tests werden über ihren Anzeigenamen gefunden und mit `dotnet test` bzw. dem xUnit-Runner gestartet (TRX ausgewertet); eigene Läufe im Helfer für Godot, Unity, 7-Zip, Ollama, Claude Code, Bild-KI, ACE-Step, Python-Umgebungen.
  - Ergebnis `reports/echtlauf/<datum>/`: Rohlog je Werkzeug, `ERGEBNIS.md`, `ergebnis.json`, `werkzeuge.json`, `SHA256SUMS`; Benutzerpfade → `C:\Users\<user>`, Rechnername → `<pc>`. Ein Test ohne Ergebnis zählt als ROT (nie still).
- **Beleg (echt gelaufen, 16.11.2026):** `reports/echtlauf/2026-11-16/`

| Werkzeug | Ergebnis | Was lief |
|---|---|---|
| FFmpeg 9.0.2 | ECHT-GRÜN | Videoschnitt-Export und Hochformat-Export mit echtem FFmpeg |
| Node 24 | ECHT-GRÜN | alle Browser-Spiele + gebaute Spiele, `node --check` |
| Git | ECHT-GRÜN | Git-Stand anlegen/speichern |
| Python | ECHT-GRÜN | Python-/.NET-/Node-Vorlage testen sich selbst; alle 6 KI-Python-Umgebungen starten und rechnen |
| Chrome 157 | ECHT-GRÜN (teilweise) | Editor-Bündel in echtem Chrome grün; „Seite als PDF“ übersprungen (pdfinfo fehlt) |
| Blender 5.2.3 | ECHT-GRÜN | glTF/OBJ/STL/FBX, Kreatur mit Rig, Rundlauf FBX/GLB |
| Unreal 5.8 | BLOCKIERT | nicht automatisiert (C++-Modul, Kompilieren > 30 min) – gefunden, Version gelesen |
| Unity 6000.6.8f1 | ECHT-GRÜN | Trendo-Projekt angelegt, EditMode 5/5 bestanden (128 s) |
| Godot 4.7.2 | ECHT-GRÜN | Projekt aus `CreateGodotProject`, Import, Level-, Spiel-, Ablauf-, Menü-, Spielstand-Prüfung, Start 300 Bilder, Gegenprobe ohne Boden scheitert wie erwartet. Export nicht geprüft (Exportvorlagen fehlen) |
| LibreOffice 26.2.4 | ECHT-GRÜN | öffnet Trendos DOCX/XLSX/PPTX, PDF/ODT/ODS/ODP, Rücklesen |
| Tesseract 5.4 | ECHT-GRÜN (teilweise) | LETTER-012 liest gerenderten Brief; Scan-PDF übersprungen (pdftoppm fehlt) |
| Bild-KI (sd-cli, SDXL Turbo) | ECHT-GRÜN | 512×512-PNG erzeugt und geprüft |
| ACE-Step | ECHT-GRÜN | Server gestartet wie Trendo, 10-s-Lied erzeugt, mit FFmpeg dekodiert |
| Ollama 0.40.11 | **ROT** | Dienst und Modell antworten (3,6 s), aber `qwen2.5:0.5b` rechnet 17 + 25 = **32** – echter Qualitätsbefund des Mini-Modells |
| Claude Code 2.1.319 | ECHT-GRÜN | angemeldet über Abo (claude.ai) – Antwort „42“; API-Variablen entfernt, nie angemeldet |
| 7-Zip 26.05 | ECHT-GRÜN | 7z.exe packt, Trendos `Archive7z` entpackt byte-gleich |
| weitere (assimp, typst, melt, IfcOpenShell, Open3D, PMTiles, Vektorkarte, glTF-Validator/-Transform, FreeSewing, axe, restic, YARA-X, YOLOX, pytiled, Referenz, Iconify, MarkItDown, mediamtx/streamlink) | ECHT-GRÜN | 22 vorhandene Echt-Tests eingebunden |

## 2. Setup-Rundlauf (`scripts/setup-rundlauf.ps1`)
- **War:** nur der Handlauf von 2026-10-25 (`reports/c4-setup`, `tools/messung/c4-setup-test.ps1`), der die **echte** Installation deinstalliert und neu installiert.
- **Jetzt:** Skript baut zwei Testpakete mit eigener Kennung `TrendoOSRundlauf` (0.0.1/0.0.2, keine Verknüpfungen), installiert mit `--silent --installto <Temp>`, startet immer mit eigenem `--data-root`, Update per `Update.exe apply`, Rückfall, Deinstallation, Restprüfung, Vergleich der echten Installation (nur lesend).
- **NICHT GETESTET (Rundlauf):** Velopack startet die App nach dem Installieren selbst ohne Argumente; Trendo nimmt dann den echten Datenordner (`SHGetKnownFolderPath`, nicht umlenkbar) samt Startwächter und Installations-/Deinstallations-Haken. Das wäre ein Eingriff in die echten Daten → nicht ausgeführt. Ohne `-Ausfuehren` baut das Skript nur die Pakete. Dieser Teil lief: `reports/echtlauf/setup-rundlauf-2026-11-16/` (Setup 122 MB, Update-Paket 115 MB). Ob Velopacks `--installto` so greift, ist erst im Rundlauf geprüft.
- **Anleitung:** Trendo schließen, `%APPDATA%\TrendoOS` sichern, `powershell -ExecutionPolicy Bypass -File scripts/setup-rundlauf.ps1 -Ausfuehren`. Dauerhafte Lösung (Vorschlag, nicht umgesetzt, `MainWindow`/App-Start außerhalb des Auftrags): Datenordner per Umgebungsvariable übersteuerbar machen.

## 3. Testschichten
- `python tools/echtlauf/testschichten.py` → `reports/echtlauf/testschichten.md`: 3268 Tests; intern 2716, **ersetzt 419**, echt 110, nur auf Anforderung 23, fest übersprungen 0; **239 Ersatz-Haken** im Code (Audit nannte 243 auf älterem Stand), 211 davon in Tests gesetzt. Je Programm Zahlen, häufigste Haken, Programme ohne einen echten Test.
- Sechs stille Prüfungen auf ehrliches Überspringen umgestellt (am Ende des Tests, alles andere läuft weiter): `GameProTests` (Sounds_and_hit_state), `GameRulesTests` (Built_game, Built_game_levels), `W4DevTests` (Templates_build: Python/Node/.NET einzeln), `W4GameTests` (Browser_games), `MusikSunoUiTests` (Remix, `Assert.Skip`).
- **Gegenprobe:** ohne Node im PATH → 5 der 5 Core-Tests „übersprungen“ (vorher grün); ohne FFmpeg → `Suno V5: Remix …` übersprungen. Mit Werkzeug: alle grün.

## 4. Doku ≠ Code
- `CapabilityCatalog.cs` berichtigt (je gegen den Code geprüft): Brief-PDF (eigener PDF-Weg ohne LibreOffice), SYS-10 (kein LibreOffice-Nachrechnen, Programme ohne LibreOffice), SYS-09 (Versand per SMTP im Programm, `MainWindow.Pim.cs:131`), POD (Printful/Printify/Stripe/Netlify angeschlossen), SPEC-11 („Rechnung erstellen“ bucht Einnahme), Musik (KI-Aktion schreibt nur Liedplan), SPEC-15 (Stream im Studio nach Zustimmung), SPEC-04 (Bildzeit statt „Raumskizze“, Studio entfernt), SPEC-09 („keine Punktwolke“ → Programm Punktwolke), SPEC-10 (MQTT liest mit), SPEC-13 (OSM-Kacheln), CORE-07 (Netlify), X-06 (git clone im Studio), X-02 (Selbstheilung repariert). Grundsatz: Grenze der **KI-Aktion** von dem trennen, was das **Studio** kann.
- `StudioKnowledge.cs`: POD-Ablauf/Qualität (Anbieter mit Freigabe), SPEC-10 (MQTT), SPEC-11 (kein Kredit-Rechner im Programm; Formate `.html/.csv/.xml` statt `.pdf`). SYS-10-Programmliste (`libreoffice, qpdf, tesseract`) bewusst gelassen: sie steuert die MCP-Werkzeugprüfung `programme_pruefen`, LibreOffice dient dort zum Öffnen/Abgleich.
- Katalog-Erzeuger (`tools/Trendo.Render/Program.cs`, nur Katalog-Teil): Tests je Test statt je Datei (auch über Seitenkonstanten wie `MainWindow.ArtKey`) → keine Programme mehr mit `Tests: []` (vorher 16); Gesamtzahl 2767 → 1919 Zuordnungen, weil Dateien vorher pauschal alle Tests an ein Programm gaben. Programme mit geteilter Quelldatei nennen nur ihre eigenen Exportformate; Einrichtungs-Typen (DependencyCatalog …) zählen nicht als Fach-Engine (3D-Programm ohne `.docx/.mp3/.pdf`, ohne LibreOffice/OBS). Entfernte Studios (SPEC-04, SPEC-16, CORE-09): Status „Studio entfernt … kein gültiger Prüfstand“.
- Neu erzeugt und committet: `docs/programme/katalog.json`, `KATALOG.md`, `docs/improvement-42/studio-capability-matrix.md` (`TRENDO_UPDATE_MATRIX=1`). Übrige erzeugte Programmdokus zurückgesetzt.

## 5. `docs/audit/2026-11-16-profi-audit/requirements-delta.md`
- Erzeugt mit `tools/echtlauf/requirements_delta.py`: 37 Zurückstufungen vorgeschlagen (Beleg älter als Code bzw. Studio entfernt), 16 mit Hinweis (kein Test mit echtem Werkzeug), 2 Hochstufungen denkbar (STUDIO-MUSIK-01, STUDIO-IMAGEAI-01 per Echtlauf-Beleg), IMP-42, CORE-06, Umlaute (Zeilen 538/540/545 auf `claude/T-0674-buero-etappe`, auf dem Integrationszweig nicht vorhanden). `requirements.yaml` nicht geändert.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler; `tools/Trendo.Render`, `tools/echtlauf/Trendo.Echtlauf` → 0 Fehler
- Core gezielt (PromptActionTests, QualityLight, UserLevel, VorbildAnalysen, CatalogPrivacy, KreativKits*, Kuenstler, GamePro, GameRules): 172 bestanden, 0 übersprungen; `W4_Core07_DevStudio`/`W4_Core06_Game`: 5/5; ohne Node: 3 + 2 übersprungen
- App gezielt: MusikSuno_Ui 11, ProgramCatalog_Ui 1, StudiosBueroGeld_Ui 36, StudiosTechnikWissen_Ui 44, StudiosAlle_Ui 98, StudiosKreativ3D_Ui 30, EinfachBedienen_Ui 11, PromptBarTests 6, ReferenzStudioSpiele_Ui 23 – alle 0 Fehler
- Namensprüfung über alle geänderten Dateien: leer

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: NICHT GETESTET (kein Programm-Paket; Station belegt Werkzeugläufe). 2. Fremddatei/Export im Fremdprogramm: TEILWEISE (LibreOffice öffnet Trendos DOCX/XLSX/PPTX, Blender/Godot/Unity echt – Beleg mit SHA-256). 3. Prozessabbruch: nicht Teil des Pakets. 4. Claude führt Fachaufgabe aus: TEILWEISE (echter Abo-Lauf beantwortet Frage; nicht im offenen Dokument). 5. Test vom Klick bis zur Datei: TEILWEISE (eingebundene App-Tests). 6. Bedienung: nicht Teil des Pakets.

## Nicht geprüft
- Setup-Rundlauf selbst (siehe 2), Unreal-Fachlauf, Godot-Export, Chrome-PDF und Tesseract-Scan-PDF (Poppler fehlt), Linux.
- Kein Volltest (macht die Hauptsitzung).

## Offene Punkte
- Ollama: Mini-Modell rechnet falsch – Modellwahl/Qualitätsgrenze entscheiden.
- Datenordner-Übersteuerung beim Start (für einen gefahrlosen Setup-Rundlauf).
- Katalog-Zuordnung von Kern-Tests ohne Studio-Kennung im Namen (z. B. LETTER-012) zu Programmen.
- `.gitattributes`: `reports/echtlauf/** -text` ergänzt, damit SHA256SUMS byte-genau bleiben.

## ENTWICKLUNGSBERICHT
- Projekt: Trendo OS · Sprachen: C# (.NET 10), Python, PowerShell · Rust eingesetzt: NEIN · Rust-Komponenten: keine
- Grund der Sprachwahl: Helfer muss Trendos C#-Code direkt aufrufen; Orchestrierung in Python wie `tools/trendo_ops.py`; Setup-Skript in PowerShell wie `tools/messung`
- Tests: siehe oben · Gemessene Verbesserungen: keine Leistungsmessung · Offene Probleme: siehe oben
