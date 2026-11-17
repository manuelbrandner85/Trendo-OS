# Bericht T-0683 Claude nur über das eigene Abo

- Zweig: `claude/T-0683-kein-claude-api`, Basis `origin/main` (f68974c6d)
- Geändert:
  - `src/Trendo.Core/W2/KiSources.cs`: Eintrag „Claude (Anthropic)“ aus `KiCatalog.Cloud` entfernt; Name des alten Schlüssels als `KiCatalog.ClaudeApiSecret`.
  - `src/Trendo.App/KiBackends.cs`: Sonderbehandlung für `api.anthropic.com` entfernt. Die Adresse ist ohnehin nicht mehr zulässig, weil nur Dienste der Liste erlaubt sind.
  - `src/Trendo.App/MainWindow.Claude.cs`: `ClaudeForgetApiKey()` löscht einen früher gespeicherten API-Schlüssel beim Start und schreibt einen Protokolleintrag.
  - Tests: `KiCloudTests.Cloud_catalog` (kein Dienst führt zu Anthropic), `Claude_Ui.No_claude_api` (Schlüssel wird gelöscht, alte Einstellung bringt den Dienst nicht zurück, Seite „KI-Quellen“ zeigt ihn nicht).
- Kommandos und Ergebnis:
  - `dotnet build Trendo.sln -c Release`: 0 Fehler.
  - Core `KiCloud|KiSource`: 3 bestanden.
  - App `Claude_Ui`: 7 bestanden.
  - App `T0225_KiUi`, `Runde12_Ui`: 7 bestanden.
- Nicht geprüft: Volltest. Der Schlüssel-Löschweg ist nur mit dem Test-Geheimnisspeicher geprüft, nicht mit dem Windows-Anmeldeinformationsspeicher.
- Offen: OpenAI bleibt als kostenpflichtiger, ausdrücklich gekennzeichneter Dienst in der Liste – die Entscheidung betraf nur Claude. Die eingebettete claude.ai-Anmeldung ist unverändert.
- Rust: nein; drei kleine C#-Änderungen.
- Status: fertig, wartet auf Freigabe zum Merge.
