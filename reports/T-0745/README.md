# T-0745 – Belege: Schnittteile frei bearbeiten (SPEC-08)

Alle Dateien stammen aus einem Lauf am 17.11.2026 auf dem Entwicklungs-PC (Windows 11, Blender 5.2.3 LTS, Python 3.13, xpdf pdftotext 4.06). Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt; Zeilenenden einheitlich LF (`reports/T-0745/** -text`). Prüfsummen: `SHA256SUMS.txt`.

## 1. Klick bis Datei (echte Oberfläche, Avalonia headless)
`Trendo.App.Tests.exe -method Trendo.App.Tests.GarmentEdit_Ui.Click_to_file` mit `TRENDO_T0745_OUT=<Ordner>`:
T-Shirt-Vorlage öffnen → Vorderteil anklicken → linke und rechte Saumecke anklicken und je 5 × Umschalt+Pfeil runter (= 5 cm) → Export über `GarmentExport` (derselbe Weg wie die Knöpfe „Schnitt als PDF/DXF/SVG“) → Anprobe-Auftrag (Blender-Lauf im Test ersetzt).

| Datei | Inhalt |
|---|---|
| `schnitt-bearbeitet.dxf` | DXF R12 (mm), Ebenen SCHNITT/NAHT/FADENLAUF – mit bearbeitetem Vorderteil |
| `schnitt-bearbeitet.svg` | SVG 1:1 (Einheit mm) – neu in T-0745 |
| `schnitt-bearbeitet.pdf` | Schnittbogen A4 1:1, 4 × 14 Blätter |
| `anprobe-auftrag.json` | Auftrag an Blender, so wie Trendo ihn schreibt (bearbeitete Kanten) |
| `dokument-felder.json` | das Dokumentfeld `gEdits` (Punkte mit Bézier-Griffen) |
| `erwartet.json` | Werte, die Trendo selbst für jedes Teil rechnet (Fläche, Umfang, Schnittmaß) |

## 2. Fremdwerkzeuge lesen die Dateien
- **DXF → Python** (`python -I dxf_lesen_T-0745.py schnitt-bearbeitet.dxf erwartet.json`, eigener Leser nur mit Standardbibliothek, unabhängig vom C#-Code): `dxf-python-T-0745.log` – alle 5 Teile OK, Vorderteil gegenüber Vorlage +5,00 cm Schnitthöhe, +10,00 cm Umfang. Einschränkung: der Leser ist selbst geschrieben (kein CAD-Programm vorhanden; LibreOffice ist nur unvollständig installiert, ezdxf nicht installiert – nichts nachinstalliert).
- **SVG → Blender** (`blender -b --factory-startup --python svg_blender_T-0745.py -- schnitt-bearbeitet.svg erwartet.json`, Blender-eigener SVG-Import `io_curve_svg`): `svg-blender-T-0745.log` – alle 5 Schnittkanten maßgleich (Abweichung ≤ 0,01 cm).
- **PDF → pdftotext 4.06**: `pdf-text-T-0745.txt` – 56 Seiten, alle 5 Teilbeschriftungen, Kontrollquadrat.

## 3. Echte 3D-Anprobe mit dem bearbeiteten Teil
`anprobe-auftrag.json` mit eigenem Ausgabeordner und `samples` 48 statt 200 (sonst unverändert, 110 Bilder Stoffsimulation) an `trendo_anprobe.py`: `blender -b --factory-startup --python trendo_anprobe.py -- auftrag.json` → `blender-anprobe-T-0745.log` (TRENDO_FERTIG, 2 min 39 s, 4 Bilder), verkleinert `anprobe-bearbeitet-vorn.png`. Trendo selbst startet Blender ohne `-b`; ein Vergleichsbild mit dem unbearbeiteten Schnitt wurde nicht gerechnet.

## 4. Oberfläche
`oberflaeche-profi-bearbeitet.png` (Profi, bearbeitetes Dokument über `TRENDO_PROGDATA`), `oberflaeche-einfach.png` (Einfach, ohne Leiste) – `tools/Trendo.Render`.

## 5. Testprotokolle
`core-tests-T-0745.log`, `app-tests-T-0745.log` (Rohlogs der gezielten Testklassen).
