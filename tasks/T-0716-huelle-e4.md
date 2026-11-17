# T-0716: B6 „Programm-Hülle für alle“ – Etappe E4 (3D/Spiele/Technik/Entwickeln)

**Ziel:** Die Programme der Gruppe 3D/Spiele/Technik/Entwickeln außerhalb der Hülle hängen über `ProgramLink` an Rückgängig (bis 100), atomarer Notsicherung im 20-s-Takt mit Sperre gegen ein zweites Fenster und Wiederherstellen-Karte; eigene Formate bleiben lesbar, Speichern atomar, Versionsfeld wo ein eigenes JSON-Format besteht; Menüleiste (T-0710) auf den neu angeschlossenen Seiten. CAD/3D-Rückgängig als Delta nur bei messbarem Vorteil.
**Branch:** claude/T-0716-huelle-e4 (vom Integrationszweig `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0716`
**Grundlage:** `tasks/T-0708*`, `tasks/T-0711*`, `tasks/T-0714-huelle-e2.bericht.md`, `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E4). Vom Nutzer freigegeben.
**Parallel:** T-0715 (E3 Kreativ/Medien), T-0717 (E5 KI & Wissen) – nur Programme dieser Etappe; gemeinsame Dateien nur minimal und additiv.
**Dateien/Bereich:** neu `src/Trendo.App/MainWindow.LinkLive.cs`, `MainWindow.WebDesignLink.cs`, `src/Trendo.Core/Scene3DHistory.cs`; Baukasten (`W4Builder.cs`, `PageBuilder.cs`), Fahrzeugbuch (`Spec16.cs`), Anlagen-Monitor (`Spec10.cs`), Web Design Studio (`WebDesign.cs`, eine Zeile), 3D (`W4Studio3D.cs`, `Studio3DMeshEdit.cs`, `Scene3D.cs`), CAD (`Cad.cs`, `CadDoc.cs`, `CadDrawing.cs`), 3D-Druck (`Print3D.cs`, `Print3DPro.cs`); gemeinsam additiv: `LinkSlots.cs`, `ProgramLinks.cs`; Tests, CrashProbe, Umzugsliste.

**Fertig, wenn:**
- [x] Baukasten: Rückgängig der Hülle (100) statt Stapel ohne Grenze, Wiederholen, Notsicherung/Karte/Sperre, `seite.json` atomar mit `Version` (alte Datei lesbar, neuere abgelehnt).
- [x] Fahrzeugbuch und Anlagen-Monitor angeschlossen (bisher ohne Rückgängig); festgeschriebene Fahrten nie zurücknehmbar; Messwerte des Monitors begründet abgegrenzt.
- [x] Web Design Studio: ungespeicherter Editor-Entwurf an der Notsicherung der Hülle, Karte.
- [x] 3D und CAD: Delta gemessen (Speicher/Zeit bei großer Szene) und umgesetzt, weil messbar besser.
- [x] Übrige Programme der Gruppe geprüft und eingeordnet (3D-Druck, Aus Fotos, VR/AR, Open Source, Erweiterungen, Code-Editor, Werkstatt, Roboter, Nachbau).
- [x] Menüleiste auf Baukasten, Fahrzeugbuch, Anlagen-Monitor mit „Bearbeiten › Rückgängig/Wiederholen“.
- [x] UI-Test je Programm (ändern → Absturz/Schließen → neues Fenster bietet Karte → Wiederherstellen bringt den Stand; mehrere Schritte zurück; zweites Fenster bietet nichts an), Absturztest über `Trendo.CrashProbe` (Baukasten), Pflichtklassen, Core QA-019, Funktions-Baseline.
- [x] Umzugsliste aktualisiert.

**Nicht tun:** kein Volltest (macht die Hauptsitzung), kein Push auf `main`/Integrationszweig, kein Merge, kein Release; Programme anderer Etappen nicht anfassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0716-huelle-e4.bericht.md`, Kopie löschen.
