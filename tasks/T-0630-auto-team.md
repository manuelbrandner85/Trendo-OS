# T-0630: Team und Engine automatisch

**Ziel:** Das KI-Team arbeitet in den Studios ohne Auswahl mit, und beim Spielebau entscheiden Claude oder die lokale KI selbst über Engine und Werkzeuge.
**Branch:** claude/T-0630-auto-team
**Dateien/Bereich:** `src/Trendo.Core/Programs/GameTools.cs`, `GameDirector.cs`, `GameExperts.cs`, `src/Trendo.Core/W2/StudioTeams.cs`, `src/Trendo.App/MainWindow.{Claude,Assist,ProgGame,ProgGameUgs,ProgGamePrompt,ProgGameDesign}.cs`, zugehörige Tests.
**Fertig, wenn:**
- [ ] Fachleute stehen standardmäßig auf „Automatisch“, ihre Zahl richtet sich nach dem Auftrag
- [ ] Claude setzt das Studio-Team ohne Rückfrage ein, die lokale KI bekommt passende Rollen zum Auftrag
- [ ] KI wählt Engine und Blender mit Begründung; ohne brauchbare Angabe entscheiden Regeln; gewählt wird nur Installiertes
- [ ] Die Wahl ist sichtbar, der Bau folgt ihr ohne weiteren Klick, wo Trendo das kann, und sagt sonst ehrlich, was geschieht
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Engines herunterladen, Merge oder Release ohne OK, `requirements.yaml` und `decisions.yaml` ändern.
**Am Ende:** Branch pushen, Bericht `tasks/T-0630-auto-team.bericht.md`.
