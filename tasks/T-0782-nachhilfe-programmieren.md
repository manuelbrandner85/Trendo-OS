# T-0782: Wissen & Lernen – „Nachhilfe mit KI“ und „Programmieren lernen“ (Etappe 0.9.113)

**Ziel:** Zwei neue Studios der Kategorie Wissen & Lernen mit echtem Fachkern, freigegeben vom Nutzer am 26.11.2026.
**Branch:** claude/T-0782-nachhilfe-programmieren (Basis main 184fcbbfc = 0.9.112)
**Dateien/Bereich:** neue Dateien `src/Trendo.Core/Lernen/*`, `src/Trendo.App/MainWindow.Nachhilfe.cs`, `src/Trendo.App/MainWindow.CodeLernen.cs`, `src/Trendo.Core/PromptAction/LernenPromptAdapters.cs`; kleine Einträge am Listenende in StudioCatalog, StudioIcons, CapabilityCatalog, StudioProfiles, StudioTeams, StudioKnowledge, PromptChips, IntelligencePromptModel, W6 (Init), `tools/studio_bilder.py`, Bilder; Programm-Hülle/KI-Kette nur um `ProgramKiAction.ApplyNote` und `ProgramSpec.KiLocalOnly` erweitert.
**Fertig, wenn:**
- [x] Nachhilfe: Lernpfad je Fach und Klasse, Übungen mit Prüfung, gestufte Hinweise, sokratischer Modus (Lösungsweg erst nach Versuch), Beherrschung je Thema, Wiederholungsplan nach Abständen (FSRS)
- [x] Nachhilfe kinderprofil-tauglich: keine Links, keine Sendewege, nur lokale KI (auch in der Auftragszeile), sokratisch im Kinderprofil nicht abschaltbar
- [x] Programmieren lernen: Python-Aufgaben mit echten Tests, isoliert (AppContainer ohne Netz, nur Temp-Ordner, Zeitgrenze, ein Prozess), Lernpfad, Hinweise, Fortschritt
- [x] KI-Fachaktion je Studio ins offene Dokument (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt) über `ProgramSpec.KiActions`; Bewertung von Code nur durch die Tests
- [x] Echtlauf mit lokaler KI (qwen3:14b) mit Rohlog unter `reports/T-0782/`
- [x] Fremdformat: ODT/DOCX/CSV in LibreOffice (Kopie), .py im System-Python
- [x] gezielte Testklassen grün (Zahlen im Bericht); Volltest macht die Hauptsitzung
**Nicht tun:** `requirements.yaml` nicht ändern (Vorschlag im Bericht), kein Merge, kein Release, kein Push nach main, keine Internet-KI im Nachhilfe-Studio.
**Am Ende:** Branch pushen, Bericht `tasks/T-0782-nachhilfe-programmieren.bericht.md`.
