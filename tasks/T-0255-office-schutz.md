# T-0255 – Schutz beim Öffnen von Office-Dateien (AP-100-2)

- Freigabe: Manuel im Chat am 31.10.2026 („ok mach“ – nächster Schritt Office 100)
- Branch: `claude/T-0255-office-format`
- Befund vorab: Trendo hat kein eigenes Office-Format (nur docx/odt/txt, xlsx/ods/csv, pptx/odp) – eine eigene Formatversion/Migration entfällt.
- Umgesetzt: `OfficeZip` (höchstens 10.000 Einträge, 256 MB je Eintrag, 512 MB gesamt, Packverhältnis > 200 ab 10 MB abgelehnt, nie mehr lesen als angegeben, DTD verboten, kein Nachladen) in DocxFormat, OdtFormat, SheetFiles, SlideRead.
- Tests: OfficeZipTests (XXE, Billion Laughs, ZIP-Bombe, zu viele Einträge, normale Rundreise). Core 868 bestanden, 3 übersprungen, 0 Fehler.
- Nicht geprüft: App-Volltest; Makro-Prüfung (docm/xlsm werden ohnehin nicht geöffnet).
