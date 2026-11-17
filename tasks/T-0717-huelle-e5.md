# T-0717: B6 „Programm-Hülle für alle“ – Etappe E5 KI & Wissen

**Ziel:** Die Programme der Gruppe KI & Wissen (und die drei kleinen Rückgängig-Stapel aus E2) an Rückgängig und Notsicherung der Hülle anschließen bzw. atomar speichern – ohne zweites System, ohne Funktionsverlust.
**Branch:** claude/T-0717-huelle-e5 (vom Integrationszweig claude/T-0685-b1-integration)
**Grundlage:** T-0708, T-0711, T-0714 (Berichte), `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E5); vom Nutzer freigegeben.
**Dateien/Bereich:** Abläufe, Produktion, Lernen, KI fragen, KI-Rollen, Recherche (Markierungen), Forensik (nur atomar), Projekte (Projekt-JSON); Aufgaben, Kalender, Vorhaben (Rückgängig). Gemeinsame Dateien (ProgramLink.cs, LinkSlots.cs, MenuBar.cs, ProgramShell.cs) nur minimal/additiv.
**Fertig, wenn:**
- [x] Entwürfe ohne Sicherung (neuer Ablauf, Produktions-Angaben, neue Lernkarte, KI-Auftrag, neue KI-Rolle, KI-Nachricht) hängen an der Notsicherung der Hülle (20 s, atomar, Sperre, Karte) mit Rückgängig bis 100
- [x] Aufgaben, Kalender, Vorhaben, Abläufe-Bausteine, Produktion-Tafel: Rückgängig/Wiederholen der Hülle (100) statt eigener Stapel
- [x] Recherche-Markierungen, Forensik-Fälle, Projekt-JSON, Produktion (Bauplan/Protokoll/Tafel) atomar; alte Dateien lesbar (Test mit alter Datei)
- [x] Forensik: keine Notsicherung, 120-Stunden-Regel unverändert (Test)
- [x] Menüleiste „Bearbeiten“ auf allen neu angeschlossenen Seiten
- [x] UI-Test je Programm, Absturztest über tests/Trendo.CrashProbe (link-ablauf, tafel)
- [x] Pflichtklassen grün, Core QA-019, Funktions-Baseline; Umzugsliste aktualisiert
**Nicht tun:** ApprovalGate/Abläufe-Freigabe ändern, Forensik-Grenzen umgehen, Programme anderer Etappen (E3/E4) anfassen, Volltest, Merge, Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0717-huelle-e5.bericht.md`.
