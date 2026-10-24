# T-0207: Fach-Studios – Auswahllisten, Bilder statt nur Text, Versprechen halten

**Ausführung:** Box-Agent (nicht Claude Code). Windows-Nachweise am PC über den Integrator.

**Ziel:** Die 14 Fach-Studios sind bequem zu bedienen und halten, was ihr Name verspricht.
**Branch:** box/T-0207-fachstudios-bedienung
**Dateien/Bereich:** `src/Trendo.App/MainWindow.W6.cs`, `MainWindow.Spec*.cs`, `MainWindow.Core09.cs`, Tests.
**Befund:** Alle Werkzeuge rechnen echt (Klick-Audit: Ergebnis erscheint), aber: feste Auswahl wird als Freitext erwartet („Standard: altbau, teilsaniert …“), Ergebnisse nur als Text, Eingaben gehen beim Schließen teils verloren, Namen versprechen mehr („Live streamen“ ohne Streaming, „Kameras & Roboter steuern“ ohne Live-Kamera, „Gegenstände einscannen“ ohne Aufnahme).
**Fertig, wenn:**
- [ ] `FachTool` unterstützt Auswahlfelder; alle Felder mit fester Wertemenge sind Auswahllisten (Test: kein Hinweistext mit Aufzählung „a, b, c“ mehr).
- [ ] Zahlenfelder prüfen sofort und zeigen Einheit.
- [ ] Wo sinnvoll Grafik: Grundriss-Vorschau (Räume), Diagramm (Messwerte), Karte (Routen), Balkenbild (Statik).
- [ ] Jedes Studio speichert seinen Stand als Projekt und lädt ihn wieder.
- [ ] Liste „Name ↔ tatsächlicher Umfang“ im Bericht mit Vorschlag für ehrliche Untertitel (Entscheidung Manuel).
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** Rechenformeln nicht ändern; Katalognamen nicht selbst ändern.
**Am Ende:** Branch pushen, Bericht. PR über den üblichen Ablauf (kleiner PR, main grün).
