# T-0739: KI-Modelle vergleichen (Fragenreihe) und Modelldateien mit Prüfsumme

**Ziel:** STUDIO-MODELS-01 bekommt eine Fragenreihe, die in einem Durchgang gegen mehrere Modelle läuft (Tabelle, Fortschritt, Abbrechen, CSV/Markdown); X-05 prüft jede Modelldatei gegen eine feste SHA-256 aus offizieller Quelle.
**Branch:** claude/T-0739-modelle-vergleichen-pruefsumme
**Quelle:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md` Rang 12 (STUDIO-MODELS-01) und 16 (X-05); Lücken laut `audit-4-ki-wissen.md`.
**Dateien/Bereich:**
- `src/Trendo.Core/Kreativ/ModelBatch.cs` (neu), `src/Trendo.App/MainWindow.ModelsBatch.cs` (neu), `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Abschnitt „KI-Modelle vergleichen“)
- `src/Trendo.Core/W2/KiLibrary.cs` (KiFile mit SHA-256/Herkunft, Katalog), `src/Trendo.Core/W2/KiLibraryStore.cs` (Prüfung), `src/Trendo.App/MainWindow.KiLibrary.cs` (Überspringen nur bei passender Summe, Anzeige je Modell)
- Tests: `tests/Trendo.Core.Tests/ModelBatchTests.cs` (neu), `tests/Trendo.Core.Tests/KiLibraryTests.cs`, `tests/Trendo.App.Tests/ModelsBatchUiTests.cs` (neu), `tests/Trendo.App.Tests/KiChecksumLiveUiTests.cs` (neu), `tests/Trendo.App.Tests/KiLibraryUiTests.cs`
- Belege `reports/T-0739/`, `.gitattributes` (eine Zeile `reports/T-0739/** -text`)

**Fertig, wenn:**
- [x] Fragenreihe aus Liste oder Datei (CSV/TXT) läuft in einem Durchgang gegen alle gewählten Modelle, nur über `IntelligenceCore.AskAsync` (kein zweiter KI-Kern)
- [x] Tabelle je Frage und Modell: Antwort, Zeit, Zeichen, Fehler; Übersicht je Modell
- [x] Fortschritt je Modell und Frage, „Abbrechen“ (Erhaltenes bleibt), „Fortsetzen“
- [x] Export CSV und Markdown
- [x] Ohne erreichbares Modell: Meldung „BLOCKIERT“, nichts wird gefragt
- [x] Echtlauf gegen lokale Ollama-Modelle mit Rohlog und SHA-256 unter `reports/T-0739/`
- [x] Jede Hugging-Face-Modelldatei im Katalog hat eine feste SHA-256 mit Herkunft; übrige ehrlich „ohne Prüfsumme“
- [x] Nach dem Download: falsche Summe → Datei verworfen, klare Meldung, nichts installiert
- [x] Test mit echter kleiner Datei und manipulierten Bytes
- [ ] alle drei Testprojekte grün – **kein Volltest in diesem Paket** (macht die Hauptsitzung), nur die betroffenen Testklassen

**Nicht tun:** kein zweiter KI-Kern, keine Claude-API (Claude nur über das Abo als wählbare Quelle wie bisher), kein Modell laden, kein Volltest, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0739-modelle-vergleichen-pruefsumme.bericht.md`.
