# T-0715: B6 „Programm-Hülle für alle“ – Etappe E3 (Kreativ/Medien)

**Ziel:** Die Kreativ/Medien-Programme außerhalb der Hülle, die ein Dokument im Speicher bearbeiten, hängen an Rückgängig (100 Schritte), atomarer Notsicherung (20-s-Takt, Sperre gegen ein zweites Fenster) und Wiederherstellen-Karte der Hülle; Ergebnisse und Exporte schreiben atomar; eigene JSON-Formate bekommen ein Versionsfeld; die Menüleiste (T-0710) gilt auch für die neu angeschlossenen Seiten.
**Branch:** claude/T-0715-huelle-e3 (vom Integrationszweig `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0715`
**Grundlage:** `tasks/T-0708*`, `tasks/T-0711*`, `tasks/T-0714-huelle-e2.bericht.md`, `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E3). Vom Nutzer freigegeben.
**Dateien/Bereich:** Live streamen (`MainWindow.Spec15.cs`, `LiveStudio.cs`, Core `W6/Broadcast.cs`, `W6/LiveShow.cs`), Trendo Musik Spuren-Mischer (`MusikStemsPlus.cs`, eine Stelle in `MusikStudio.cs`), Medien bearbeiten (`W4Media.cs`, `ImageTools.cs`, Core `MediaStudio.cs`), Freie Bilder & Klänge (`W4Assets.cs`, Core `Assets.cs`); gemeinsam nur additiv: `MainWindow.MenuBar.cs` (zwei Fälle); Tests, CrashProbe, Umzugsliste.

**Fertig, wenn:**
- [x] Medien bearbeiten und Spuren-Mischer über `ProgramLink` (Rückgängig 100, Notsicherung, Sperre, Karte); Live streamen mit Rückgängig 100 (sofort gespeichert – begründet ohne Notsicherung); DAW-Ansicht, TV & Live, Freie Bilder & Klänge begründet ohne Dokument.
- [x] Ergebnisse/Exporte atomar; `ablauf.json`, `szenen.json` mit Versionsfeld, alte Dateien lesbar (Test), neuere nicht überschrieben.
- [x] Menüleiste auf `sendung` und `kreativ-medien` (Datei + Bearbeiten), Spuren-Mischer unter „Bearbeiten“ von Trendo Musik.
- [x] UI-Test je Programm, Absturztest (CrashProbe `sendung`, `link-medien`), Pflichtklassen, Core QA-019, Funktions-Baseline.
- [x] Umzugsliste aktualisiert.

**Nicht tun:** kein Volltest, kein Push auf `main`/Integrationszweig, kein Merge, kein Release; Programme von E4 (T-0716) und E5 (T-0717) nicht anfassen; `ProgramLink.cs`, `LinkSlots.cs`, `ProgramShell.cs` unverändert lassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0715-huelle-e3.bericht.md`, Kopie löschen.
