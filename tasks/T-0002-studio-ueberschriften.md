# T-0002: Studio-Seitenüberschriften auf die zentralen StudioCatalog-Namen umstellen

**Ziel:** Jede Studio-Seite zeigt als Überschrift genau den Namen aus `src/Trendo.App/StudioCatalog.cs` (seit PR #194 die zentrale Namensquelle), damit Kachel, Karte, Startmenü und Seitentitel gleich heißen.
**Branch:** grok/T-0002-studio-ueberschriften
**Dateien/Bereich:** nur `src/Trendo.App/MainWindow*.cs` (Überschriften-Aufrufe wie `Page("…")`, `KreativPage("…")`, `BusinessPage("…")`, `FachPage("…")`, `W45Page(…)`), ggf. `src/Trendo.App/StudioCatalog.cs` (nur eine Hilfsmethode, keine Namen ändern) und Tests unter `tests/Trendo.App.Tests/`.

**Vorgehen:**
1. Finde alle Seiten, die zu einem Studio gehören (Zuordnung Seite → Modul: `StudioPages`, `PageModules`, `RegisterTab`). Liste sie im Bericht mit alter und neuer Überschrift.
2. Ersetze fest eingetragene Überschriften durch den Katalognamen (z. B. über eine kleine Hilfsmethode `StudioCatalog.NameOf(id, fallback)`), statt die Texte doppelt zu pflegen. Registerkarten-Beschriftungen (Tabs) und Seiten ohne Studio bleiben unverändert.
3. Ergänze einen Test: Für jede Studio-Seite mit Katalogeintrag ist die angezeigte Überschrift gleich `StudioCatalog.ById[id].Name`.

**Fertig, wenn:**
- [ ] keine Studio-Seite zeigt mehr einen abweichenden Namen (z. B. „3D-Studio“ statt „3D-Modelle erstellen“)
- [ ] neuer Test vorhanden und grün
- [ ] `Trendo.Core.Tests`, `Trendo.App.Tests`, `Trendo.Api.Tests` grün (Zahlen in den Bericht)

**Nicht tun:** keine Katalognamen ändern, keine Navigation umbauen, SPEC-07 nicht anfassen, nichts außerhalb des Bereichs.
**Am Ende:** Commit(s) auf dem Branch, `git push -u origin grok/T-0002-studio-ueberschriften`, Bericht `tasks/T-0002-studio-ueberschriften.bericht.md` mitcommitten. Keinen PR öffnen (macht Grok Bot).
