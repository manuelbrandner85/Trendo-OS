# T-0709: Design-Reste (Bild-Audit V-01…V-22, offene Punkte aus T-0684, T-0686, T-0700, T-0705)

**Ziel:** Die nach Design-Paket 1–3 offenen Punkte des Bild-Audits schließen, soweit sie nicht in parallel laufenden Paketen liegen: V-14 in Tabelle, Präsentation und Mail; Verkaufen mit einer Reiterzeile (V-15); kein sichtbares Pfadfeld in „Einfach“ (V-07); Podcast-Kapitel als Liste (V-12); wichtigste Exporte der Büro-Studios in „Einfach“ sichtbar (Rest aus T-0705); eine goldene Hauptaktion auf „Webseite bauen“ (V-22); Abgleich-Tabelle V-01…V-22.
**Freigabe:** Nutzer am 16.11.2026, alle 22 Vorschläge V-01…V-22 mit Ja (Audit `docs/audit/2026-11-16-profi-audit/audit-7-anordnung-design.md`).
**Branch:** claude/T-0709-design-reste (vom Integrationszweig `claude/T-0685-b1-integration`)
**Dateien/Bereich:** `MainWindow.DocActions.cs`, `MainWindow.Tabelle.cs`, `MainWindow.Praesentation.cs`, `MainWindow.MailProgramView.cs`, `MainWindow.Kasse.cs`, `MainWindow.W5Sell.cs`, `MainWindow.SimpleMode.cs`, `MainWindow.PodcastStudio.cs`, `MainWindow.WebDesign.cs`, `MainWindow.WebDesignKi.cs`, `MainWindow.W4Dev.cs`, `MainWindow.Pim.cs`, `MainWindow.AddressBook.cs`, `MainWindow.Workflows.cs`, `tools/Trendo.Render/Program.cs` (Schreibfenster rendern), neuer Test `DesignPaket4UiTests.cs`.
**Fertig, wenn:**
- [x] jede Änderung hat einen UI-Test (`DesignPaket4_Ui`)
- [x] Pflichtklassen grün: `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket3_Ui`, `W0b_Look`, `PathPickAuditTests`, Funktions-Baseline (`python -m unittest discover -s tests/claude_handoff`) und die Testklassen der berührten Programme (Zahlen im Bericht)
- [x] vorher/nachher im Bild geprüft (Einfach und Profi)
- [ ] Volltest (macht die Hauptsitzung)
**Nicht tun:** `MainWindow.ProgramShell.cs`, Programm-Hülle, ApprovalGate/Freigaben, Claude/KI-Assistenzweg, `MainWindow.PromptAction.cs`, CAD/3D/Code-Editor/Briefe (parallel T-0706, T-0707, T-0708).
**Am Ende:** nur diesen Branch pushen, Bericht `tasks/T-0709-design-reste.bericht.md`, Arbeitsordner löschen.
