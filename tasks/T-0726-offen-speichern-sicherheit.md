# T-0726: Offene Probleme Gruppe A – Speichern, Sicherheit, Freigaben

**Ziel:** Die offenen Punkte A1–A9 aus den Paketberichten T-0713 … T-0719 beheben (Nutzer: „wichtig auch offene probleme beheben“).
**Branch:** claude/T-0726-offen-speichern-sicherheit (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** `Fach.Save` (FachBase), `CodeDrafts` + Code-Editor, `AuditLog`/`Secrets`/`TrendoCore`, `ApprovalGate` (Ablage offen.json), GitHub-Upload im Claude-Chat, OSM-Kacheln der Karte, Lernen + Projekt-Notiz, `MediaStudio.Frames`, Live streamen (Sendeablauf).
**Fertig, wenn:**
- [ ] A1 `Fach.Save` atomar über `AtomicFile` (eine Stelle, alle Fachprogramme), Test mit zwei gleichzeitigen Schreibern + Abbruchtest
- [ ] A2 Code-Entwürfe mit der Fenstersperre (`ProgramLocks`), kein zweites Fenster überschreibt/löscht/bietet fremde Entwürfe an
- [ ] A3 Prüfprotokoll: HMAC mit Schlüssel aus `ISecretStore`, alte Protokolle bleiben lesbar und prüfbar
- [ ] A4 `freigaben/offen.json`: Sperre zwischen Prozessen, Zusammenführen statt Überschreiben
- [ ] A5 GitHub-Upload im Claude-Chat über `ApprovalGate` (wie Netlify)
- [ ] A6 OSM-Kacheln nur nach einmaliger Zustimmung über das Tor (Einstellung, ausschaltbar), offline sauber
- [ ] A7 Lernen: Löschen mit Rückgängig; Projekt-Notiz und Kartenbearbeitung nach der Tipp-Pause gespeichert
- [ ] A8 Vorschaubilder der Zeitleiste atomar
- [ ] A9 Live streamen: Zelle mit Fokus nach der Tipp-Pause gespeichert
- [ ] je Punkt Rot-Grün; Pflichtklassen grün; Sicherheitsprüfung (03-security-permissions) für A3–A6
**Nicht tun:** Punkte der Gruppen B (T-0727) und C (T-0728), übrige Fachstudios (T-0725); kein Volltest, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0726-offen-speichern-sicherheit.bericht.md`.
