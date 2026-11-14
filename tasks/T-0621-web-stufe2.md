# T-0621: Webseite bauen, Stufe 2 (Design und Mechanik)

**Ziel:** Stufe 2 aus `docs/nachbau/web-ki/anforderungen.md` und `anforderungen-ergaenzung.md`: A5, W1 mit Z4, W6, W5 mit Z1, W4 mit Z2, A2, A8, Z6, Z7.
**Branch:** claude/T-0621-web-stufe2
**Teile:**
- Teil 1: A5, W1 mit Z4, W6
- Teil 2: W5 mit Z1, W4 mit Z2
- Teil 3: A2, A8, Z6, Z7
Jeder Teil bekommt Tests, QA-Prüfung und einen Abschnitt im Bericht.
**Dateien/Bereich:** `src/Trendo.Core/Website/WebDesign*`, `src/Trendo.App/MainWindow.WebDesign*.cs`, `src/Trendo.App/WebDesignAudit.cs`, `src/Trendo.App/Assets/webdesign`, `tools/webdesign-editor`, zugehörige Tests, `docs/nachbau/web-ki/`.
**Fertig, wenn:**
- [ ] Jede Prüfung der Liste für diese Funktionen hat einen bestandenen Test oder der Bericht sagt, warum nicht
- [ ] Neue Editor-Funktionen sind auch als geprüfte Aktionen für die KI da (A1)
- [ ] QA-Prüfung bestanden
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Stufe 3, GSAP, Theatre-Studio (AGPL), fremde Texte oder Prompts, `requirements.yaml` und `decisions.yaml` ändern, Personennamen im Produkt, veröffentlichen, Merge nach `main` oder Release ohne eigenes OK des Nutzers für diesen Stand.
**Am Ende:** Branch pushen, Bericht `tasks/T-0621-web-stufe2.bericht.md`.
