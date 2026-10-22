# T-0213: Studionamen und Untertitel ehrlich machen (Vorschlagsliste, kein Code)

**Ausführung:** Box-Agent (nicht Grok Build). Windows-Nachweise am PC über den Integrator.

**Ziel:** Jeder Studioname verspricht nur, was das Studio heute kann – oder das Studio wird so erweitert, dass es stimmt.
**Branch:** box/T-0213-namen-versprechen
**Dateien/Bereich:** nur `tasks/T-0213-namen-versprechen.bericht.md` (Liste), kein Code.
**Befund:** z. B. „Spiele bauen – mit der Unreal Engine“ (meist 3 Browser-Vorlagen), „Live streamen“ (nur Planungsrechner), „Kameras & Roboter steuern“ (Bildauswertung + Rechner, keine Steuerung), „Gegenstände einscannen“ (Punktwolken-Bearbeitung, keine Aufnahme), „Webseite bauen“ (Software-Vorlagen ohne Editor).
**Fertig, wenn:**
- [ ] Tabelle aller 54 Studios: Name, Untertitel, was wirklich geht, Vorschlag A (Name anpassen) / B (Funktion ergänzen).
- [ ] Projektleitung legt die Liste Manuel zur Entscheidung vor.
**Nicht tun:** `StudioCatalog.cs` nicht ändern.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
