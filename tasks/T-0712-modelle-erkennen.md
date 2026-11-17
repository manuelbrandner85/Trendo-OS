# T-0712: Vorhandene lokale Modelle sicher erkennen (Fehlerbehebung zu T-0703)

**Ziel:** Die automatische Modellwahl aus T-0703 kennt immer die wirklich vorhandenen Ollama-Modelle – auf dem Prüf-PC wird qwen3:14b Standard, nicht qwen2.5:0.5b.
**Branch:** claude/T-0712-modelle-erkennen (vom Integrationszweig claude/T-0685-b1-integration)
**Freigabe:** Nutzer „bestes vorhandenes lokales Modell als Standard: Ja“ (T-0703).
**Dateien/Bereich:** `src/Trendo.Core/KiModel.cs` (Modellliste), `src/Trendo.App/KiBackends.cs` (`OllamaBackend.InstalledAsync`), `src/Trendo.App/MainWindow.axaml.cs` (nur `InstalledModels`/`RefreshKi`), neue Tests, `reports/T-0712/`.
**Fertig, wenn:**
- [x] Ursache gefunden (nur gelesen, nichts umkonfiguriert, Ollama nicht beendet)
- [x] Läuft Ollama: Liste über GET 127.0.0.1:11434/api/tags; sonst Manifeste an allen bekannten Orten (OLLAMA_MODELS Prozess + Benutzer, ~/.ollama/models, Trendos Werkzeugordner/model), Ergebnis zwischengespeichert
- [x] Core-Tests für die Quellen-Reihenfolge, echter Beleg dieses PCs unter `reports/T-0712/` (qwen3:14b)
- [x] KiModelT0703 (Core + KiModelT0703_Ui), KiLibraryUiTests, AssistChain_Ui grün
**Nicht tun:** kein zweiter KI-Kern, keine neue Download-Logik, keine Umkonfiguration von Ollama, nichts an der Programm-Hülle (T-0710/T-0711), kein Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0712-modelle-erkennen.bericht.md`.
