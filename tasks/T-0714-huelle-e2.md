# T-0714: B6 „Programm-Hülle für alle“ – Etappe E2 (Büro: eine Notsicherung)

**Ziel:** Text, Tabelle, Präsentation, CAD und 3D haben nur noch EINE Notsicherung – die der Hülle (`ProgramLink`); PDF-Formularwerte hängen an Rückgängig und Notsicherung; Büro-Speicherorte schreiben atomar; `.tcad` und Szene-JSON haben ein Versionsfeld.
**Branch:** claude/T-0714-huelle-e2 (vom Integrationszweig `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0714`
**Grundlage:** `tasks/T-0708-huelle-etappe-1.bericht.md`, `tasks/T-0711-huelle-etappe-1b.bericht.md`, `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E2). Vom Nutzer freigegeben.
**Dateien/Bereich:** `src/Trendo.Core/Programs/ProgramLink.cs`, `ProgramDocument.cs`, `Cad/CadDoc.cs`, `Scene3D.cs`, Büro-Speicherorte (LetterCase, BusinessStudio, BusinessMarket, FinanceExtra, CreationSuite/Mappe, W3/Mail, W3/AddressBook, Sys/Sys02);
App `MainWindow.OfficeRecovery.cs` (neu geschrieben), `LinkSlots.cs`, `ProgramLinks.cs`, `ProgramShell.cs` (eine Zeile), `Pdf.cs`, Speichern-Stellen in Text/Tabelle/Präsentation/CAD/3D, kleine Atomar-Stellen; Tests; Umzugsliste; `tools/Trendo.Render` (Bild-Hilfe).

**Fertig, wenn:**
- [x] `OfficeAutosave` und der Ordner `wiederherstellen/` sind entfernt; die fünf Programme sichern über `ProgramLink` (atomar, 20-s-Takt, Sperre gegen zweites Fenster, Wiederherstellen-Karte); keine Funktion entfällt.
- [x] Alte `wiederherstellen/`-Dateien werden beim ersten Start angeboten (Test mit alten Sicherungsdateien).
- [x] Eigene Rückgängig-Systeme: nur ersetzt, wo verlustfrei – sonst begründet (Bericht).
- [x] PDF-Formularwerte an `ProgramLink`.
- [x] Briefköpfe, Verkaufen, Vorhaben, Adressbuch und weitere als „nicht atomar“ markierte E2-Speicherorte schreiben über `AtomicFile`.
- [x] Versionsfeld in `.tcad` und Szene-JSON mit Migrationsweg; alte Dateien bleiben lesbar (Test).
- [x] Absturztest (CrashProbe, Familie `link`) für Text und Tabelle; UI-Tests je Programm; Pflichtklassen grün; Funktions-Baseline; Core QA-019.
- [x] Umzugsliste aktualisiert, Etappen-Reihenfolge E3 → E4 → E5.

**Nicht tun:** kein Volltest (macht die Hauptsitzung), kein Push auf `main`/Integrationszweig, kein Merge, kein Release; ApprovalGate (B4), Claude-Brücke (B5) und Menüleiste (T-0710) nicht umbauen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0714-huelle-e2.bericht.md`, Kopie löschen.
