# T-0104: UI41-3 Startmenü und KI-Bereich nach Referenz 02/05

**Ziel:** Startmenü visuell modernisieren (angeheftet, Empfohlen/Kürzlich, Kategorien, Nutzer/Power); KI als Chat-Layout (Verlauf links, Eingabe unten, Modell & Status rechts).
**Branch:** claude/T-0104-ui41-startmenue-ki
**Dateien/Bereich:** `MainWindow.StartMenu.cs`, KI-Ansicht in `MainWindow.axaml.cs`/KI-Dateien, Tests.
**Vorgehen:** Suche, Win/Esc, Anheften unverändert lassen. KI-Status nur echt: Nicht verbunden / Verbindung wird geprüft / Bereit / Antwort läuft / Fehler / Offline. Vorschlagskarten dürfen nur echte Aktionen auslösen.
**Fertig, wenn:**
- [ ] Startmenü-Tests (Win/Esc/Suche/Pins) grün
- [ ] KI-Test im Zustand „Nicht verbunden“ und (falls Ollama am PC) „Bereit“ mit echter Antwort
- [ ] Screenshots neben Ref 02 und 05 im Bericht
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** kein „GPT-4o“ o. ä. anzeigen, keine erfundenen Unterhaltungen, Berechtigungen nicht ändern.
**Am Ende:** Branch pushen, Bericht. Keinen PR öffnen.
