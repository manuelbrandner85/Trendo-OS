# T-0704: Übersicht „Einfach“ wieder grün

**Ziel:** Die Prüfung `UebersichtEinfach_Ui` (2/2) auf dem Integrationszweig wieder grün – ohne die Prüfung zu lockern (Grenzen 40/8/400 bleiben), nach dem Leitsatz „Einfach entrümpeln, keine Funktion verlieren“.
**Branch:** claude/T-0704-einfach-gruen (Basis claude/T-0685-b1-integration)
**Dateien/Bereich:** Comic-Programm (Zeichnen in „Einfach“, Exporte), 3D-Studio (Ansichten in „Einfach“), Ausnahmeliste `tests/Trendo.App.Tests/Daten/einfach-vorher-ausgeblendet.txt`.
**Fertig, wenn:**
- [x] `Simple_keeps_every_function` grün: Comic-Kernwerkzeuge (Pinsel, Farbe, Strichstärke, Ebenen, CBZ, Bildfelder) in „Einfach“ sichtbar; Profi-Werkzeuge (DATEV, Kredit-Sonderfälle, Steuersätze, Podcast Folge/Jahr, WAV-Zwischenstand) begründet in der Liste mit Ersatzweg
- [x] `Every_view_stays_clear` grün: Seite „3d“ in „Einfach“ ≤ 40 Knöpfe (jetzt 38), alle sechs Ansichten über „Ansicht: … ▾“
- [x] Testklassen der berührten Programme grün, neue Prüfung rot auf altem Code
**Nicht tun:** Grenzen ändern, Tests löschen/schwächen, Volltest.
**Am Ende:** Branch pushen, Bericht `tasks/T-0704-einfach-gruen.bericht.md`.
