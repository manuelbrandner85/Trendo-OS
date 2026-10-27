# T-0231: Studios als vollwertige Programme (V1–V12)

**Ziel:** Jedes Studio ist ein echtes, hochwertiges Programm statt einer Formularseite – und innerhalb von Trendo OS installierbar.
**Freigabe:** Nutzer im Chat, 27.10.2026: „Alle Vorschläge V1–V12 ja. Setz sie in der empfohlenen Reihenfolge um.“
**Branch:** claude/T-02xx (Sammelzweig)
**Dateien/Bereich:** `src/Trendo.Core` (Programmdokument, Studio-Pakete), `src/Trendo.App` (Programmrahmen, Studio-Seiten), Tests.

**Vorschläge:**
- V1 Studios als installierbare Programme (Programme-Seite: installieren, aktualisieren, entfernen; offizielle Quellen, Zustimmung, Audit-Log)
- V2 Gleicher Programmaufbau: Startseite Neu · Vorlagen · Zuletzt, Menüband, Arbeitsfläche, Eigenschaften rechts, Statusleiste
- V3 Echte Dateien je Studio: Projektdatei, Öffnen/Speichern/Speichern unter/Zuletzt, Rückgängig/Wiederholen, automatisches Sichern
- V4 Passende Arbeitsfläche statt Feldern (3D-Ansicht, Zeitleiste, Leinwand, Raster, Code, Schaltplan)
- V5 Freie Profi-Programme per Klick dazu (Blender, Kdenlive, Krita, Inkscape, Audacity, FreeCAD, LibreOffice) mit Dateiübergabe
- V6 Edles Erscheinungsbild: Farbwelt und Symbol je Studio, Tiefe, Startbildschirm, Schrift ab 12 pt
- V7 Vorlagen mit echten Vorschaubildern
- V8 KI-Seitenleiste in jedem Programm, Änderungen rückgängig machbar
- V9 Mehrere Dokumente in Reitern, Fenster nebeneinander
- V10 Optionen und Tastenkürzel je Programm
- V11 Kurze Einführung beim ersten Start
- V12 Programm-Abnahme als Test je Studio (Neu, Öffnen, Speichern, Export, Rückgängig, Drucken, Hilfe)

**Reihenfolge:** V2/V3/V4/V6 (zuerst Kreativ, 3D, Geld, Wissen) → V1/V5 → V7/V8/V9 → V10/V11/V12.

**Fertig, wenn:**
- [ ] Programmrahmen und Dokumentmodell im Kern, getestet
- [ ] alle Studios nutzen den Rahmen (Abnahmetest V12 grün je Studio)
- [ ] alle drei Testprojekte und Paket-Tests grün (Zahlen in den Bericht)

**Nicht tun:** Fremdprogramme ohne Zustimmung installieren; Code oder Logos fremder Programme übernehmen; Trendo-Logo oder Programmnamen in Nutzerdateien.
**Am Ende:** Bericht `tasks/T-0231-studios-als-programme.bericht.md`.
