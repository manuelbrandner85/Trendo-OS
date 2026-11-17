# T-0722: VR-Teile entfernen, Seitengruppe neutral „3D-Modelle ansehen“

**Ziel:** Die VR-eigenen Teile unter SPEC-04 entfernen und die Seitengruppe neutral benennen. 3D-Bibliothek und Modell-Prüfer bleiben für 3D-Druck (CORE-05) und Architektur (SPEC-01).
**Entscheidung des Nutzers (16.11.2026):** Auf die Frage „VR-Teile aus dem Modell-Prüfer entfernen und den Reiter neutral benennen (z. B. ‚3D-Modelle ansehen‘); Bibliothek und Prüfer bleiben für 3D-Druck und Architektur?“ – „ja“.
**Branch:** claude/T-0722-3d-modelle-ansehen (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Modell-Prüfer (`MainWindow.Spec04*.cs`, `W6/Xr*.cs`, `MeshView`), SPEC-04-Einträge in Katalog, KI-Auftrag, Team, Ideen, Rolle, Fachwissen, Symbol, Abhängigkeitstext; Tests dieser Teile; Kataloge und Register.
**Fertig, wenn:**
- [ ] Zielbrillen, Brillen-Ansicht, „Für die Brille reparieren“, Komfort-Rechner und Bildzeit (KI-Auftrag) sind weg, jedes Stück mit Grund im Bericht
- [ ] 3D-Bibliothek, Modell-Prüfer (Maße, Dreiecke, Fehler im Modell), glTF-Werkzeuge bleiben; Wege aus 3D-Druck und Architektur gehen weiter; Suche findet „3D-Modelle ansehen“
- [ ] Katalog (nur Eintrag xr), PromptAction-Matrix, requirements.yaml-Notiz nachgezogen
- [ ] `SPEC04_Ui.Heavy_model_and_calcs`: Ursache geklärt und behoben
- [ ] Build 0 Fehler, Pflicht-Testklassen und Paket-Tests grün, Bild in Einfach und Profi geprüft
**Nicht tun:** Studio SPEC-04 wieder in Listen aufnehmen; fremde Katalog-Messwerte neu schreiben; Bereich „3D, Spiele & VR“ umbenennen (gehört mehreren Studios); Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0722-3d-modelle-ansehen.bericht.md`.
