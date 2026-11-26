# T-0777 Bericht: Stille Tests ehrlich, Katalog aus Belegen, Umzug zweier Studios

**Auftrag:** `tasks/T-0777-ehrlich-katalog.md` (Etappe 0.9.112, freigegeben am 26.11.2026) · **Zweig:** `claude/T-0777-ehrlich-katalog` · **Basis:** main 0.9.111 (`2b1ffa923`)

## 1. Q4 – stille Tests ehrlich

**Vorher (Beleg):** `docs/audit/2026-11-26-neubewertung/BERICHT.md` 3.7 – Echtlauf-Tests kehrten ohne Belegordner mit `return` zurück oder schluckten Netzfehler (`catch (HttpRequestException) { }`) und zählten als „bestanden“.

**Jetzt:** Ohne Voraussetzung melden sie „übersprungen“ mit Grund. Mechanismus wie im Projekt üblich: App-Tests (xUnit v3) `Assert.Skip(...)`, Kern-Tests (xUnit 2) `Ueberspringen.Weil(...)`. Keine Prüfung entfernt oder abgeschwächt; die Prüfungen nach dem Schalter sind unverändert.

- App: `AssistChainUiTests.cs:208`, `W4AssetsUiTests.cs:37` (jetzt `finally { w.Close(); }`), `W4OssUiTests.cs:26`, `CodeAiKiUiTests.cs:171`, `AgentEchtlaufUiTests.cs:59/79` (+ „Claude Code nicht gefunden“ meldet BLOCKIERT-Überspringen), `KiTeamEchtlaufUiTests.cs:24`, `ChatAttachT0734UiTests.cs:265`, `KiChecksumLiveUiTests.cs:20`, `ModelsBatchUiTests.cs:234`, `T0742AblaufAusSatzUiTests.cs:219/298` (+ drei BLOCKIERT-Rückkehrer, vier „keine Aufgabenplanung“-Rückkehrer), `KiModelT0703UiTests.cs:120/158/191`, `KiModelT0712UiTests.cs:51`.
- Kern: `KiModelT0712Tests.cs:124`, `MailRoundtripTests.cs:300`, `MeshEditTests.cs:171`, `RechteKernFix1Tests.cs:79`, `RechteKernFix4Tests.cs:122`, `RechteKernFix5Tests.cs:58`, `T0755Fix3Tests.cs:20`, `T0755Fix4Tests.cs:19`, `T0763SystemAlltagTests.cs:223`.
- Bewusst nicht geändert: `PhoneSendT0767Tests.cs:116` (ein zu großer Upload darf die Verbindung beenden, danach wird geprüft, dass keine Datei entstand), `T0719KartenTagesplanerUiTests.cs:53` und die Hilfen in `MeshEditTests`/`PdfToolsTests` (nur Belegschreiben in Hilfsmethoden, Hauptprüfung läuft immer), `DesignPaket3UiTests.cs:139` (fachlicher Zweig: lokale KI läuft), reine Betriebssystem-Schalter `if (!OperatingSystem.IsWindows()) return;` (auf Windows laufen diese Tests).
- **Neue Prüfung** `tests/Trendo.Core.Tests/T0777StilleTestsTests.cs` (Klasse `T0777StilleTests`) liest die Testquellen: kein Echtlauf-Schalter mit stillem `return` am Testanfang, keine geschluckten Netzfehler. **Rot-Grün belegt:** Auf dem alten Stand der Testdateien 2 Fehler (15 stille Schalter, 2 geschluckte Netzfehler aufgelistet), auf dem neuen grün.
- **Befund dabei:** `T-0691 glTF-Export besteht den Khronos-glTF-Validator …` bestand auf diesem PC bisher still – der Validator ist hier nicht eingerichtet. Jetzt „übersprungen“.

## 2. Q5 – Katalog aus Belegen

**Vorher:** `ProgramCatalog.Status` übernahm `test_status` aus `requirements.yaml` ungeprüft; `katalog.json` zeigte 97× NOT_RUN und 25× PC_VERIFIED (u. a. SYS-04, SYS-07, CORE-04, X-08 trotz ersetztem Kernweg).

**Jetzt** (`src/Trendo.Core/Programs/ProgramCatalog.cs`, `tools/Trendo.Render/Program.cs`):
- Register-Status TESTED/PC_VERIFIED zählt nur, wenn der eingetragene Rohbeleg unter `reports/` da ist und seine SHA-256 stimmt (`Evidence.Check`); sonst NOT_RUN mit Grund („Register …, aber Beleg fehlt oder verändert“).
- Mit TRX-Lauf (`TRENDO_KATALOG_TRX`): je Programm die Ergebnisse genau seiner Tests – ein Fehlschlag → FAIL, mindestens ein bestandener → TESTED (PC_VERIFIED bleibt nur mit gültigem Beleg), übersprungene zählen nie als bestanden; Zählung „Lauf: x bestanden · y übersprungen · z fehlgeschlagen · n nicht im Lauf“ steht im Status.
- `ProgramCatalog.RevokedPcVerified`: PC_VERIFIED für SYS-04, SYS-07, CORE-04, X-08 zurückgenommen (höchstens TESTED, Grund im Status). `requirements.yaml` ist **nicht** geändert (Sperre `Edit(/requirements.yaml)` in `.claude/settings.json`; das Register ändert nur Manuel) – die Fortschritt-Seite der App zeigt dort weiter „auf dem PC geprüft“ (offener Punkt).
- Neue Tests `tests/Trendo.Core.Tests/T0777KatalogBelegTests.cs` (Klasse `T0777KatalogBeleg`, 3 Tests: Beleg-Prüfung, TRX-Auswertung, echtes Register für die vier Module).

**Probelauf (nicht committet, danach zurückgesetzt):** `TRENDO_KATALOG=. dotnet run --no-build --project tools/Trendo.Render -c Release -- <bild>.png` → „Programmkatalog: 123 Programme“, Exit 0, **keine** Meldung zu veralteten Namen in `docs/claude/vorbilder` (T-0771-Prüfung). Ohne TRX: 98 NOT_RUN, 21 PC_VERIFIED, 4 TESTED (die vier zurückgenommenen); Tagesplaner und Recherche mit Quellen mit Bereich „KI & Automatik“. `VorbildAnalysenTests`, `T0771Restpunkte`, `MP131_Catalog` gegen den Probe-Katalog grün (39/39).

**Befehl für die endgültige Erzeugung (Hauptsitzung nach dem Volltest mit TRX):**

```
dotnet test Trendo.sln -c Release --logger trx --results-directory <TRX-Ordner>
TRENDO_RENDER_ROOT=<eigener Temp-Ordner> TRENDO_KATALOG=. TRENDO_KATALOG_TRX=<TRX-Ordner> dotnet run --project tools/Trendo.Render -c Release -- <Temp>/k.png
```

Danach `docs/programme/` committen. Meldet der Erzeuger „ACHTUNG: … Programmname(n) in den Vorbild-Analysen“ (Exit 3), die Namen in `docs/claude/vorbilder` anpassen, sonst wird `VorbildAnalysenTests` rot.

**SHA-Listen neu** (gegen den eingecheckten Inhalt, LF; nur die abweichenden Zeilen geändert, alle anderen stimmten):
- `reports/T-0698/claude-SHA256.txt`: `claude-lauf.md` · `reports/T-0698/ollama-SHA256.txt`: `ollama-lauf.md`
- `reports/T-0703/SHA256SUMS`: `vlm-laden.md`
- `reports/T-0707/SHA256.txt`: `ausserhalb-der-huelle.md`, `echtlauf-lokale-ki.md`, `rohlog-testlauf.txt`
- `reports/T-0723/SHA256SUMS`: `echtlauf-code-mit-ki.md`, `testlauf-roh.txt`
- Ehrlich: Diese Dateien wurden nach dem damaligen Hashen bearbeitet; die neue Liste bestätigt den heutigen Inhalt, nicht den Zustand zum Laufzeitpunkt. Ergebnisdateien (`.tdoc`, Code, Diff, Bilder, `claude-lauf.json`) stimmten unverändert.

## 3. Umzug Tagesplaner und Recherche mit Quellen

- `src/Trendo.App/StudioCatalog.cs` und `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`: Bereich „KI & Automatik“ für `STUDIO-DAYPLAN-01` und `STUDIO-CITE-01`. Seiten, Programme, Symbole (`StudioIcons`), Vorlagen und KI-Aufträge unverändert – keine Funktion entfällt. Matrix `docs/improvement-42/studio-capability-matrix.md` neu erzeugt (`TRENDO_UPDATE_MATRIX=1`).
- Bild: `reports/T-0777/uebersicht-nach-umzug.png` (Trendo.Render, Seite `studios`, 1600×2600, SHA-256 `259c16913ee3202115b00c07b3a7639aebe17359b74a054ac904bd319e4d8985`): Arbeiten & Büro (12), Wissen & Lernen (4), KI & Automatik (12). Nebenbefund (nicht durch T-0777 verursacht): zwei goldene Knöpfe auf der Übersicht („Erstes Projekt anlegen“ und „Freie Projekte auf GitHub finden“).

## 4. Freigabe festgehalten

`docs/audit/2026-11-26-neubewertung/fehlende-studios.md` Abschnitt 5: Umzug Ja, fünf Studios zählen als erledigt, **51 fehlen**, erste Studio-Etappe **Wissen & Lernen**, neue Zahlen je Kategorie.

## Tests (Zahlen aus der Ausgabe)

- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Kern (gezielt, T0777*, MP131_Catalog, EvidenceTests, VorbildAnalysen, T0771Restpunkte, PromptActionTests, DecisionRegister, KiModelT0712, MailRoundtrip, MeshEdit, RechteKernFix1/4/5, T0755Fix3/4): **306 bestanden, 3 übersprungen, 0 Fehler** (übersprungen: glTF-Validator fehlt, T-0712-Echtlauf, T-0689-Beleg – vorher still bestanden).
- App (einzeln je Klasse, alle 0 Fehler): AgentEchtlauf_Ui 2/2 übersprungen · AssistChain_Ui 6 (1 übersprungen) · ChatAttachT0734_Ui 9 (1) · CodeAiKi_Ui 4 (1) · KiChecksumLive_Ui 1 (1) · KiModelT0703_Ui 5 (3) · KiModelT0712_Ui 3 (1) · KiTeamEchtlauf_Ui 1 (1) · ModelsBatch_Ui 4 · ModelsBatchLive_Ui 1 (1) · T0742_Ui 3 · T0742_Gegenpruefung_Ui 5 · T0742_Echtlauf_Ui 1 (1) · W4_X07_Ui 2 (Live mit Internet echt bestanden) · W4_X06_Ui 2 (echt) · PromptBarTests 6 · StudioOrder_Ui 3 · StudioIconTests 4 · Design41RolloutTests 3 · KreativStudios4_Ui 5 · ProgramCatalog_Ui 1 · RechercheQuellen_Ui 7 · T0728_Bedienung_Ui 7 · BueroStudios_Ui 5 · DesignHonesty 5 · StartMenu_Ui 3 · StartMenu_StudioFenster 5 · StudioClickTests 3 · StudioFolds_Ui 3 · StudioHeadingTests 2 · StudioBilder 1 · StudiosAlle_Ui 103 · StudiosBueroGeld_Ui 36 · StudiosTechnikWissen_Ui 39 · T0719_KartenTagesplaner_Ui 4 · UebersichtEinfach_Ui 2 · UebersichtVorschlaege_Ui 13 · DesktopIconsUiTests 4.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien

Das Paket ist Querschnitt (Ehrlichkeit von Tests und Doku), kein Fachstudio.
1. Leere Vorlage → Ergebnis: NICHT BETROFFEN · 2. Fremddatei: NICHT BETROFFEN · 3. Abbruch: NICHT BETROFFEN · 4. KI im Dokument: NICHT BETROFFEN · 5. Test vom Klick bis zur Datei: NICHT BETROFFEN (Echtläufe nun ehrlich „übersprungen“, nicht neu gelaufen) · 6. Bedienung: NICHT BETROFFEN (Umzug ändert nur den Bereich; Übersicht im Bild geprüft).

## Nicht geprüft

- Kein Echtlauf der übersprungenen Tests (Ollama/Claude/Belegordner nicht gesetzt) – sie laufen nur mit den genannten Umgebungsvariablen.
- `T0763SystemAlltagTests` nicht ausgeführt (liest die echte Trendo-Installation; nur kompiliert).
- Katalog mit TRX nicht erzeugt (Aufgabe der Hauptsitzung); TRX-Auswertung nur im Unit-Test belegt.
- Kein Volltest.

## Offene Punkte

- `requirements.yaml` führt SYS-04, SYS-07, CORE-04, X-08 weiter als PC_VERIFIED (Fortschritt-Seite, Studio-Karten). Änderung nur durch Manuel oder ausdrückliche Freigabe für die Register-Datei.
- Viele reine `if (!OperatingSystem.IsWindows()) return;`-Schalter in Kern-Tests bleiben (unter Linux still bestanden).
- „Mods für eigene Spiele“ weiter unklar.
- Übersicht `studios`: zwei goldene Knöpfe (Gold-Regel), bestand schon vorher.

**Status:** fertig zur Zusammenführung.
