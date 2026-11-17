# T-0720: Drei Studios löschen – VR & AR (SPEC-04), Auto & Fahrten (SPEC-16), Kameras & Roboter (CORE-09)

**Ziel:** Genau die drei vom Nutzer freigegebenen Studios löschen; kein anderes Studio und keine Funktion eines anderen Studios geht verloren.
**Entscheidung des Nutzers (16.11.2026, wörtlich):** „VR/AR (SPEC-04), Auto & Fahrten (SPEC-16) sowie Kameras & Roboter (CORE-09) kannst ruhig löschen“ – und vorher: „alle studios erhalten bleiben darf keines gelöscht werden den ist wichtig aber müssen funktioniere“.
**Branch:** claude/T-0720-studios-entfernen (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Code, Seiten, KI-Aufträge, Tests und Register der drei Studios; geteilte Teile bleiben (bei Bedarf in neutrale Datei verschoben, gleiche Funktion).
**Fertig, wenn:**
- [ ] Liste aller Stücke der drei Studios mit Entscheidung „gelöscht / bleibt (wer nutzt es)“
- [ ] nur Ungeteiltes gelöscht, Tests der gelöschten Teile entfernt, Tests geteilter Teile grün
- [ ] Katalog, PromptAction-Matrix, Funktions-Baseline, requirements.yaml, huelle-umzug.md, einfach-vorher-ausgeblendet.txt nachgezogen
- [ ] Build 0 Fehler, betroffene Testklassen und Paket-Tests grün
**Nicht tun:** SPEC-07 anfassen; Programme von T-0718 (Web veröffentlichen) und T-0719 (Karten/Tagesplaner) anfassen; Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0720-studios-entfernen.bericht.md`.
