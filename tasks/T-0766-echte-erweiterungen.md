# T-0766: Echte Erweiterungen (X-04 „Trendo erweitern“)

**Ziel:** Erweiterungen können echte Funktionen bringen – sicher in einer Sandbox, mit Rechte-Manifest, eigener Freigabe und Werkstatt zum Selbstbauen. Freigegeben vom Nutzer (Entscheidung „Echte Erweiterungen“ statt Umbenennung; Restliste Rang 37, Audit `docs/audit/2026-11-16-profi-audit/audit-5-entwickeln-system.md` Abschnitt X-04).
**Branch:** claude/T-0766-echte-erweiterungen
**Dateien/Bereich:** `src/Trendo.Core/Extensions.cs`, `ExtensionCatalog.cs`, neu `ExtensionSandbox.cs`, `ExtensionModules.cs`, `Erweiterungen/`; `src/Trendo.App/MainWindow.W5Ext*.cs`, Einhängepunkt in `MainWindow.Projektmappe.cs`; Lizenzlisten; Render-Hilfe.
**Fertig, wenn:**
- [x] Fremder Code läuft nur in einer echten Sandbox (WebAssembly/WASI über Wasmtime, kein Netz, nur freigegebene Ordner, Zeit-/Rechen-/Speichergrenze, Not-Stopp), nie als .NET-Assembly im Trendo-Prozess
- [x] Rechte-Manifest, Anzeige vor der Installation, Freigabe in eigenem Schritt, entziehbar; veränderte ZIP/Modul abgewiesen
- [x] Befehl im Studio „Projekte“, Ergebnis im offenen Projekt, rückgängig
- [x] Werkstatt: Vorlage, Manifest-Editor, Bauen aus Quelle, ZIP-Export, Installation auf leerem Datenordner, Aktualisieren
- [x] Feindliche Tests (Datei außerhalb, Netz, Endlosschleife, Speicher) rot-grün
- [x] Daten-Arten seite/vorhaben/links/text unverändert; KI entwirft weiter nur
**Nicht tun:** kein Volltest, kein Merge, keine systemweite Installation, keine KI-Installation.
**Am Ende:** Branch pushen, Bericht `tasks/T-0766-echte-erweiterungen.bericht.md`.
