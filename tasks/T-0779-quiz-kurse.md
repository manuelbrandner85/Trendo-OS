# T-0779: Quiz erstellen und Kurse erstellen (Etappe 0.9.113 „Wissen & Lernen“)

**Ziel:** Zwei neue Studios in „Wissen & Lernen“ mit echtem Fachkern in der Programm-Hülle: „Quiz erstellen“ (STUDIO-QUIZ-01) und „Kurse erstellen“ (STUDIO-COURSE-01). Freigabe des Nutzers am 26.11.2026.
**Branch:** claude/T-0779-quiz-kurse (Basis main 184fcbbfc, 0.9.112)
**Dateien/Bereich:** neue Dateien `src/Trendo.Core/Wissen/*`, `src/Trendo.App/MainWindow.QuizKurse.cs`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`; Einträge am Ende der gemeinsamen Listen (StudioCatalog, Symbole, CapabilityCatalog, Profile, Teams, Fachwissen, Startideen, Bildbeschreibung); kleiner Schalter `ProgramKiAction.MakesFile`.
**Fertig, wenn:**
- [x] Quiz: Auswahl, Mehrfachauswahl, Wahr/Falsch, Lückentext, Zuordnen; Punkte, Zeit, Mischen, Spielmodus mit Auswertung
- [x] Quiz-Ausgabe: eigenständiges HTML-Quiz, PDF mit Lösungen, GIFT für Moodle (Recherche: GIFT statt QTI)
- [x] Kurs: Kapitel, Lektionen, Bausteine, Quiz aus „Quiz erstellen“, Kursansicht, Fortschritt je Person; Ausgabe Webseite, SCORM 1.2, CSV
- [x] KI-Fachaktion je Studio über `ProgramSpec.KiActions` (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt), Echtlauf mit lokaler KI
- [x] Vorbilder recherchiert, Anforderungen in `docs/nachbau/quiz|kurse/anforderungen.md`
- [x] gezielte Tests aller berührten Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** `requirements.yaml` ändern (gesperrt – Vorschlag im Bericht), Claude-Echtlauf, Merge, Release, Push nach main.
**Am Ende:** Branch pushen, Bericht `tasks/T-0779-quiz-kurse.bericht.md`.
