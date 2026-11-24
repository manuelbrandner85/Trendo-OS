# Bericht T-0766: Echte Erweiterungen (X-04)

**Branch:** `claude/T-0766-echte-erweiterungen` (Basis `main` = Release 0.9.109), Kopie je Auftrag in `%TEMP%\trendo-auftrag-T-0766`.

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis (echt/ersetzt) |
|---|---|---|---|
| 1 Sandbox | Erweiterungen nur Daten (`Extensions.cs:8,15-17` alt), keine Funktionen | Art `modul`: WebAssembly-Modul (WASI preview 1) in **Wasmtime 50.0.1** (NuGet, Apache-2.0 WITH LLVM-exception, Bytecode Alliance, Release 06.11.2026). Importe nur aus fester WASI-Liste (kein `sock_*`, kein `poll_oneoff`, keine Links), keine Umgebung, keine geerbten Ein-/Ausgaben; Ordner nur `/daten` (eigener Datenordner, wenn Recht) und `/projekt` (nur lesen, wenn Recht); Zeitgrenze per Epoche 15 s, Rechengrenze per Fuel, Speicher 64 MB, Ein-/Ausgabe je 16 MB; Not-Stopp bricht über `ApprovalGate.HaltToken` ab. Kein .NET-Code der Erweiterung im Prozess | `T0766_ExtensionSandbox` (5 Tests, echte Wasmtime-Laufzeit, nichts ersetzt) |
| 2 Rechte-Manifest | Rechte `dateien-lesen`, `internet-links` ohne Wirkung | Rechte `befehl`, `eingabe-datei`, `projekt-schreiben`, `projekt-lesen`, `daten-ordner`; „netz“ wird abgewiesen. Anzeige verständlich vor dem Installieren (Rückfrage, auch beim Katalog). Freigabe **eigener Schritt** (eigene Rückfrage, eigene ApprovalGate-Aktion `ext.grant`, Ziel `Kennung@Prüfsumme`) – die Installations-Freigabe gilt dafür nicht. Entziehen jederzeit. Nach Update andere Prüfsumme → Rechte erlöschen. `ModuleSha256` im Manifest; getauschtes Modul in der ZIP wird abgewiesen; Freigabe an die Quell-Prüfsumme wie bisher | `T0766_ExtensionModules.Separate_approvals_and_halt`, `.Tampered_rejected`, UI-Test |
| 3 Einbindung | – | Knopf je freigegebenem Befehl im Studio „Projekte“ (rechts „Mit Erweiterung“, Dateiauswahl nach Endung); Ergebnis als neue Datei im offenen Projekt, nie überschreiben („kunden (2).md“); „Rückgängig“ entfernt nur die unveränderte Ergebnisdatei (Prüfsumme) | `Install_grant_run_undo`, UI-Test vom Klick bis zur Datei |
| 4 Selbst erstellen | kein Editor, kein Export, kein Update (Audit X-04 „Was fehlt“ 2.–3.) | Werkstatt (Expander „Eigene Erweiterung bauen“): Vorlage, Manifest-Editor (Name, Version, Beschreibung, Knopfname, Endungen, Rechte), Quelltext-Editor (WAT) mit Sichern nach Tipp-Pause, „Bauen und prüfen“ mit dem WAT-Übersetzer **aus der Wasmtime-Bibliothek** (kein weiteres Werkzeug), „Als ZIP ausgeben“, „Diese ZIP installieren …“. Aktualisieren: höhere Version ersetzt atomar, ältere wird abgewiesen. Rust: `rustup`/`cargo` auf diesem PC **nicht vorhanden** → nicht genutzt, nichts installiert. Beispielmodul „CSV als Tabelle“ als Quelle (`src/Trendo.Core/Erweiterungen/csv-tabelle.wat`) + Binär (`csv-tabelle.wasm`, 1081 Byte) + `csv-tabelle.wasm.sha256` | `Workshop_roundtrip_and_update`, `Sample_reproducible_and_converts` (Quelle baut byte-gleich zur Binärdatei), UI-Test, `reports/T-0766/export-zip.log` |
| 5 Feindliche Tests | – | Datei außerhalb (`../geheim.txt`, absoluter Pfad, ohne Ordner) → verweigert, mit Gegenprobe im eigenen Ordner; Netz (`sock_accept`, `sock_send`, `env.http_get`, `poll_oneoff`) → bei Prüfung und Installation abgewiesen; Endlosschleife → Zeitgrenze (≤ 8 s), Rechengrenze, Not-Stopp; Speicher → 125-MB-Startspeicher nicht gestartet, Wachsen endet bei 1009 Seiten (< 64 MB), Ausgabe-Deckel; danach läuft das Beispielmodul normal | `T0766_ExtensionSandbox.Hostile_*` |
| 6 Katalogtext/KI | „Neue Funktionen für Trendo hinzufügen“ stimmte nicht (Audit X-04 Nr. 12) | Stimmt jetzt; Leerzustand erklärt Funktionen + Sandbox. KI-Aktion `erweiterung.entwerfen` unverändert nur Entwurf, `erweiterung.installieren` bleibt gesperrt | unverändert `PromptActionTests` |

Bestehende Daten-Arten `seite`, `vorhaben`, `links`, `text` bleiben gültig und nutzbar (`Data_kinds_unchanged`, `W5_X04_Extensions`, `W5_X04_Ui`); der mitgelieferte Katalog hat jetzt 4 statt 3 Einträge (Tests von 3 auf 4 angehoben, nicht abgeschwächt).

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `--filter T0766|W5_X04|SystemPages|Sys13|NoFixedUser` → **36 bestanden, 0 Fehler** (`reports/T-0766/core-tests.log`).
- App: `T0766ErweiterungenUi` 2/2, `W5_X04_Ui` 2/2, `W5_X04_CatalogUi` 1/1, `StyleAndBrand_Ui` 2/2, `Projektmappe_Ui` 1/1, `T0726Offen_Ui` 4/4 → alle grün (`reports/T-0766/app-tests.log`).
- Rot-Grün: auf `main` kompilieren die neuen Tests nicht (Typen `ExtensionSandbox`, `ExtensionModuleSample`, `ExtensionWorkshop`, `ExtensionResultUndo` fehlen), Katalog-Test erwartet 4 statt 3 Einträge.
- Bildprüfung: `reports/T-0766/erweiterungen-modul-werkstatt.png`, `projekte-knopf.png` (Render mit neuer Hilfe `TRENDO_EXT_DEMO=offen|werkstatt|projekt`).
- Belege mit SHA-256: `reports/T-0766/SHA256SUMS.txt`. Export-ZIP mit Windows `tar.exe` (bsdtar) gelesen und entpackt, Modul-Prüfsumme = Manifest.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **VOLLWERTIG** (Werkstatt → Modul → Tabelle im Projekt, UI-Test).
2. Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm: **TEILWEISE** – echte CSV wird umgewandelt, Export-ZIP mit `tar.exe` gelesen; die erzeugte `.md` wurde nicht in einem Markdown-Programm geöffnet.
3. Absturz ≤ 30 s: **TEILWEISE** – Werkstatt-Felder und Quelltext werden nach der Tipp-Pause gesichert (`SaveOnPause`), Manifest atomar; nicht mit echtem Prozessabbruch geprüft.
4. KI-Fachaufgabe im Dokument, rückgängig: **FEHLT (bewusst)** – Vorgabe: KI entwirft nur, Installation bleibt beim Nutzer.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (`T0766ErweiterungenUi`, echte Sandbox).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **NICHT GETESTET / FEHLT** – keine neuen Kürzel oder Rechtsklick-Menüs.

## Nicht geprüft
- Linux-/Pi-Edition (Wasmtime liefert `linux-x64`/`linux-arm64`, nicht gestartet); Setup-Größe (+~21 MB `wasmtime.dll` für win-x64) nicht im echten Setup gemessen.
- Ein echtes Rust→`wasm32-wasip1`-Modul (Werkzeug fehlt auf dem PC); die Sandbox ist dafür ausgelegt (WASI-Befehlsmodul mit `_start`).
- Katalog-Laden aus dem Netz mit Modul (Download weiterhin nur in Tests nachgebildet, wie vorher).

## Offene Punkte / Risiken
- Recht „Werkzeug für die KI“ ist bewusst **nicht** angeboten (keine Scheinfunktion); wäre ein Folgeauftrag.
- Ansicht „Erweiterungen“ hat wie vorher zwei goldene Knöpfe (Installieren + Hauptaktion der gewählten Erweiterung) – Vorbestand, nicht verändert.
- Rechte- und Datenordner liegen außerhalb des Erweiterungsordners (`erweiterungen-rechte/`, `erweiterungen-daten/`), damit die Prüfsumme gleich bleibt.

## Berührte Dateien
`src/Trendo.Core/Extensions.cs`, `src/Trendo.Core/ExtensionCatalog.cs`, `src/Trendo.Core/ExtensionSandbox.cs` (neu), `src/Trendo.Core/ExtensionModules.cs` (neu), `src/Trendo.Core/Erweiterungen/csv-tabelle.wat|.wasm|.wasm.sha256` (neu), `src/Trendo.Core/Trendo.Core.csproj`, `src/Trendo.App/MainWindow.W5Ext.cs`, `src/Trendo.App/MainWindow.W5ExtCat.cs`, `src/Trendo.App/MainWindow.W5ExtMod.cs` (neu), `src/Trendo.App/MainWindow.Projektmappe.cs` (1 Zeile), `tools/Trendo.Render/Program.cs` (Render-Hilfe), `tests/Trendo.Core.Tests/ExtensionSandboxTests.cs` (neu), `tests/Trendo.Core.Tests/ExtCatalogTests.cs`, `tests/Trendo.App.Tests/T0766ErweiterungenUiTests.cs` (neu), `tests/Trendo.App.Tests/ExtCatalogUiTests.cs`, `THIRD_PARTY.md`, `THIRD_PARTY_NUGET.md`, `docs/commercial/THIRD_PARTY_LICENSES.md`, `reports/T-0766/*`, `tasks/T-0766-*`.

## Neue Abhängigkeit
Wasmtime 50.0.1 (NuGet `Wasmtime`, Quelle github.com/bytecodealliance/wasmtime-dotnet) – Lizenz Apache-2.0 WITH LLVM-exception; gepflegt (letzte Fassung 06.11.2026); bringt native Laufzeit für win-x64/arm64, linux-x64/arm64, osx mit. Nichts systemweit installiert.

## Sprachwahl
Rust: **NEIN** – keine Rust-Toolchain auf dem PC; die Sandbox selbst ist Wasmtime (in Rust geschrieben, als fertige NuGet-Bibliothek eingebunden). Eigener Code C#, Beispielmodul in WebAssembly-Text.

**Status:** umgesetzt und gezielt getestet; Volltest macht die Hauptsitzung.
