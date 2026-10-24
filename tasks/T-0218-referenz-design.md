# T-0218: Startseite und „Alle Studios“ im Referenz-Design

**Ziel:** Die Startseite sieht aus wie Referenzbild 1, „Alle Studios“ wie Referenzbild 2 – mit echten Funktionen und Zuständen.
**Branch:** claude/T-02xx (Sammelbranch dieses Blocks)
**Auftrag (Manuel, Chat 2026-10-24):** „Kann man Trendo OS mit Unreal Engine und einer passenden kostenlosen KI optimieren, es soll exakt dieses Design sein“ – Wahl: „Bild 1 als Startseite und Bild 2 als ‚Alle Studios‘“.
**Dateien/Bereich:** `src/Trendo.App/MainWindow.Shell.cs` (neu), `MainWindow.StudiosView.cs` (neu), `MainWindow.axaml.cs` (Aufbau, Messwerte, Dock), `MainWindow.Windows.cs` (Fensterfläche), `MainWindow.Look.cs`/`UserSettings` (Hintergrund „Abendrot“), `Assets/studios/*.jpg`, `Assets/wallpapers/abendrot.jpg`, `tools/design/ki_*.py`, `docs/design/*.json`, betroffene Tests.
**Fertig, wenn:**
- [ ] Startseite: Kopfleiste (Reiter, Suche, Freigaben, Online-Dienste, Benutzer, Uhr), linke Bereiche + Schnellzugriff, 9 Studio-Kacheln, Logo + Schriftzug + 5 Aktionen + Lichtring, rechts KI-Assistent, Systemstatus (Ringe), Aktuelle Projekte, Schnell-Tools, Dock.
- [ ] „Alle Studios“: Eingabezeile, Bereichs-Chips, Bildkarten für alle 54 Studios, Systemmodule, Aktuelle Projekte, rechts KI, Geräte, KI-Team, GitHub.
- [ ] Bilder lokal mit KI erzeugt (ComfyUI + FLUX.2 klein), Herkunft dokumentiert; keine Fremdbilder.
- [ ] Jede sichtbare Taste wirkt (Tote-Tasten-Test), nur echte Zustände.
- [ ] alle drei Testprojekte + Paket-Tests grün (Zahlen in den Bericht)
**Nicht tun:** Logo ändern (Logo A bleibt), Studionamen aus den Bildern übernehmen (Katalognamen bleiben), den Hinweis auf eine Ersatz-KI eines anderen Anbieters aus Bild 2 oder die Namen „AION/JARVIS“ übernehmen, erfundene Zahlen anzeigen, Unreal als Oberfläche einsetzen.
**Decision-Delta:** IMP-41 (Design 41) hatte den Desktop mit Kachelraster, Ringen und Schnell-Tools durch eine ruhige Mitte ersetzt. Manuels Wahl vom 2026-10-24 setzt für Startseite und „Alle Studios“ wieder die Referenzbilder 1 und 2. `decisions.yaml` wird nicht eigenmächtig geändert – Eintrag durch Manuel oder ausdrücklich freigegebenen Auftrag.
**Am Ende:** Branch pushen, Bericht `tasks/T-0218-referenz-design.bericht.md`.
