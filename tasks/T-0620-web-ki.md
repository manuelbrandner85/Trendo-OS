# T-0620: Webseite bauen, KI-Grundlage (Stufe 1)

**Ziel:** Die KI bedient den visuellen Editor über geprüfte Aktionen statt rohem Code: A1, A7, A3, K4, K1, K2, K5, A6 aus `docs/nachbau/web-ki/anforderungen.md`.
**Branch:** claude/T-0620-web-ki
**Dateien/Bereich:** `src/Trendo.Core/Website/WebDesign*`, `src/Trendo.Core/W2/Skills.cs`, `src/Trendo.Core/Skills/webdesign*.md`, `src/Trendo.App/MainWindow.WebDesign*.cs`, `src/Trendo.App/WebsitePreview.cs`, `src/Trendo.App/Assets/webdesign`, `tools/webdesign-editor`, zugehörige Tests, `docs/nachbau/web-ki/anforderungen.md`.
**Fertig, wenn:**
- [ ] Jede Prüfung der acht Funktionen aus der Liste hat einen bestandenen Test
- [ ] Ungültige oder unbekannte Aktionen werden abgelehnt, nichts wird geschrieben
- [ ] Claude nur über das Abo im installierten Claude Code, sonst lokale KI, nie eine bezahlte API
- [ ] Prüfkreislauf läuft auf dem Export, mit Obergrenze an Runden
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Stufe 2 und 3, GSAP, Onlook oder bolt.diy einbauen, fremde Texte oder Prompts, `requirements.yaml` und `decisions.yaml` ändern, Personennamen im Produkt, veröffentlichen, Merge nach `main`, Release, Dateien der Spiele-Sitzung (`claude/T-0610-spiele-design`).
**Am Ende:** Branch pushen, Bericht `tasks/T-0620-web-ki.bericht.md`.
