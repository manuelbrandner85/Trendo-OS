# T-0740: Eigene KI trainieren nutzbar, KI-Anweisungen als Datei

**Ziel:** Das trainierte Modell aus „Eigene KI trainieren“ (STUDIO-TRAIN-01) wird im Stapel auf CSV-Dateien und Ordner angewandt; Vorlagen aus „KI-Anweisungen“ (CORE-12) lassen sich als Datei speichern und einlesen und haben einen echten Texteditor.
**Herkunft:** `docs/audit/2026-11-16-profi-audit/d7-restliste.md`, Rang 13 und 15 (Befunde aus `audit-4-ki-wissen.md`).
**Branch:** claude/T-0740-training-anweisungen
**Dateien/Bereich:**
- STUDIO-TRAIN-01: `src/Trendo.Core/Kreativ/ClassifierBatch.cs` (neu), `src/Trendo.App/MainWindow.TrainBatch.cs` (neu), Einhängen in `src/Trendo.App/MainWindow.KreativStudios3.cs` (nur Abschnitt „Eigene KI trainieren“)
- CORE-12: `src/Trendo.Core/W2/PromptFiles.cs` (neu, Teil von `PromptLibrary`), `src/Trendo.App/MainWindow.PromptFiles.cs` (neu), `src/Trendo.App/MainWindow.Prompts.cs`
- Tests: `tests/Trendo.Core.Tests/T0740Tests.cs`, `tests/Trendo.App.Tests/T0740UiTests.cs`; angepasst (Feld ist jetzt ein Texteditor): `T0711HuelleEtappe1bUiTests.cs`, `W2UiTests.cs`
- Beleg: `reports/T-0740/`, `.gitattributes` (eine Zeile `reports/T-0740/** -text`)

**Fertig, wenn:**
- [ ] Modell auf CSV (je Zeile) und Ordner (je Datei) anwendbar, Ergebnis mit Vorhersage und Sicherheit, Fortschritt, „Abbrechen“, Export als CSV-Datei
- [ ] Echter Lauf: Training aus den Beispieldaten im Repo, dann Anwendung auf Dateien unter `reports/T-0740/daten/`; Rohlog und SHA-256 unter `reports/T-0740/`
- [ ] Vorlagen als `.md` mit Kopfzeilen und `.json` speichern und einlesen; gleicher Name fragt „Ersetzen / Als Kopie / Überspringen“
- [ ] Anweisungstext in einem Texteditor (AvaloniaEdit) mit Rückgängig im Feld und hervorgehobenen Lücken
- [ ] bestehende `prompts/vorlagen.json` bleibt lesbar
- [ ] gezielte Testklassen grün (kein Volltest – macht die Hauptsitzung)

**Nicht tun:** kein zweites Lernsystem, kein neues Vorlagen-Speicherformat, kein Merge nach `main`, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0740-training-anweisungen.bericht.md`.
