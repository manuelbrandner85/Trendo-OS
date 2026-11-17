# T-0703: Lokale KI – bestes vorhandenes Modell als Standard, Bildverstehens-Modell einrichten

**Ziel:** Trendo nimmt für Chat und Agenten das leistungsfähigste lokal vorhandene Modell, das zum PC passt (mit Ausweichen), und „Bilder erkennen“ beschreibt Fotos lokal mit einem über die KI-Bibliothek geladenen Bildverstehens-Modell.
**Freigaben (17.11.2026):** „Bestes vorhandenes Modell als Standard“ (Echtlauf 16.11.: 0,5B rechnete 17 + 25 = 32) · „Bildverstehens-Modell über die KI-Bibliothek einrichten: Ja“ (Qwen 2.5 VL 7B o. ä., offizielle Quelle, kostenlos, mehrere GB).
**Branch:** claude/T-0703-lokale-modelle (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** `KiModel` (Modellwahl), `IntelligenceCore` (Ausweichen), `KiBackends` (Fortschritt beim Laden, Denk-Lebenszeichen), KI-Bibliothek (`MainWindow.KiLibrary.cs`), KI-Quellen (`MainWindow.X05.cs`), Modell-Seite (`MainWindow.axaml.cs`), Haupt-KI/Agenten-KI (`MainWindow.W2.cs`, `MainWindow.AgentRun.cs`), Echtlauf-Helfer, Tests, `reports/T-0703/`.
**Fertig, wenn:**
- [x] Auswahlregel dokumentiert und mit nachgebildeten Modelllisten getestet (Spezialmodelle ausgeschlossen, Grafik-/Arbeitsspeicher, schwacher PC, eigene Wahl hat Vorrang)
- [x] Ausweichen auf kleinere Modelle bei Ladefehler oder Zeitgrenze (Nachdenken zählt als Lebenszeichen)
- [x] Echter Lauf: „Was ist 17 + 25?“ an das automatisch gewählte Modell → 42
- [x] Bildverstehens-Modell über den vorhandenen Einrichten-Weg wirklich geladen (Speicherplatz geprüft, Fortschritt protokolliert, Audit)
- [x] „Bilder erkennen“ beschreibt ein Testfoto lokal (Beleg mit Beschreibungstext)
- [x] Standard-Modell und Grund in KI-Quellen, KI-Bibliothek und „Modell“ sichtbar
**Nicht tun:** `MainWindow.ProgramShell.cs`, Claude-Anbindung, `SimpleWords.cs`; keine neue Download-Logik; Ollama-Prozess der Person nicht beenden; kein Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0703-lokale-modelle.bericht.md`.
