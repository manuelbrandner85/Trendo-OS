# T-0725: Übrige Fachstudios nach den Lücken der Teilberichte (Audit-Paket D7)

**Ziel:** Für die übrigen Fachstudios (nicht von T-0680…T-0723 abgedeckt) eine Restliste anlegen und je Programm die wichtigste Lücke schließen – alle Studios bleiben, müssen aber funktionieren (Freigabe des Nutzers).
**Branch:** claude/T-0725-fachstudios-rest (Basis claude/T-0685-b1-integration)
**Dateien/Bereich:** Briefe, Bücher, Fotos verwalten, Texte zusammenfassen, Neues Projekt (Vorhaben), Wissen merken, Karteikarten, Messwerte; Restliste `docs/audit/2026-11-16-profi-audit/d7-restliste.md`; Belege `reports/T-0725/`.
**Fertig, wenn:**
- [x] Restliste mit Lücke je Eintrag, bearbeitete Programme und Folgeetappen in Reihenfolge
- [x] je bearbeitetem Programm: UI-Test mit echten Maus-/Tastenereignissen bis zur echten Datei
- [x] Fremdwerkzeug-Beleg (LibreOffice, qpdf, Ical.Net) unter `reports/T-0725/` mit Rohlog und SHA256SUMS
- [x] Pflichtklassen und berührte Klassen grün, Core QA-019, Funktions-Baseline
**Nicht tun:** T-0722 (3D-Modelle ansehen), T-0723 (Code mit KI), T-0724 (Abnahme-Läufe) anfassen; Volltest; zweite Engine; Funktionen entfernen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0725-fachstudios-rest.bericht.md`.
