# T-0760 – Belege

Alle Pfade ohne Benutzernamen (`C:\Users\<user>`, `<pruefer>` = Prüfmaterial der T-0747-Prüfer, `<scratch>` = eigener Arbeitsordner).
Prüfsummen aller Dateien: `SHA256SUMS.txt`.

| Ordner | Inhalt |
|---|---|
| `regression/` | Lesen aller 1710 verschiedenen Prüfer-DXF mit main (24bb4f70e) und dem Zweig: `lesen-je-datei.tsv` (gleich / anders mit Grund), Fräsen und Grundriss der 46 geänderten Dateien (`fraesen-grundriss-geaenderte.tsv`), Prüfsummen der Roh-JSONL (155 MB, lokal) |
| `echte-dateien/` | 14 echte DXF auf dem PC (11 Zeichnungen eines Stahlsockels, geschrieben von Pro/ENGINEER; 3 FreeCAD-Testdateien), nur gelesen |
| `fremdbeleg/` | FreeCAD 1.1.4 (`freecadcmd`): Import der Test-DXF mit Blockverweisen und SPLINE (`vergleich-basis0.txt`, `vergleich-basis10.txt`, `lagen-basis10.txt`), Rundreise FreeCAD-Export → Trendo (`freecad-export.dxf`, `vergleich-rundreise.txt`), Rohlog `freecad-rohlog.txt` |
| `zeittests/` | Zeittests normal, unter künstlicher Last (16 Rechenprozesse; alter Stand `last-lauf-main.log`, neuer `zeit-last16-neu.log`), mit künstlich verlangsamtem Code (`bremse-*.log`), Vergleichsarbeit (`bezug-*.txt`) |
| `tests/` | gezielte Testläufe Core und App, Rot-Grün der neuen Tests gegen main (`rotgruen-main.log`) |
| `werkzeuge/` | Erzeuger, FreeCAD-Skripte, Vergleich, Regressions-Harness, Last- und Bremsskript (Bremse nur vorübergehend, nicht im Code) |
