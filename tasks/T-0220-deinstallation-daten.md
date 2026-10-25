# T-0220: Beim Deinstallieren fragen, ob die eigenen Daten gelöscht werden

**Ziel:** Die Deinstallation fragt, ob auch die Trendo-Daten weg sollen. Standard ist „Behalten“.
**Branch:** claude/T-02xx (Sammelbranch)
**Auftrag (Manuel, Chat 2026-10-25):** „Ja, beim Deinstallieren soll Trendo fragen, ob auch die eigenen Daten gelöscht werden (Standard: behalten).“
**Dateien/Bereich:** `src/Trendo.App/UninstallPrompt.cs` (neu), `src/Trendo.App/Program.cs` (Velopack-Hook), `src/Trendo.Core/DataLocation.cs` (`RemoveUserData`), Tests.
**Fertig, wenn:**
- [ ] Bei einer normalen Deinstallation erscheint eine Ja/Nein-Frage, „Nein“ ist vorausgewählt. Ohne Antwort bleibt nach 25 s alles; Velopack gibt dem Hook höchstens 30 s.
- [ ] Stille Deinstallation (`--silent`): keine Frage, die Daten bleiben.
- [ ] Gelöscht wird nur der Trendo-Datenordner, nie Laufwerks-, Benutzer- oder Systemordner und nie, wenn eigene Projekte darin liegen.
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Projekte oder Dokumente löschen, ohne Rückfrage löschen.
**Am Ende:** Bericht `tasks/T-0220-deinstallation-daten.bericht.md`.
