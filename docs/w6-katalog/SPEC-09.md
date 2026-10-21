# SPEC-09 3D Scanning & Reconstruction 2.0 – Funktionskatalog

Katalog-Status: **PROPOSED** (nicht APPROVED). Anzahl: 20 (laut Modulregister).

| Nr | Funktion | Beschreibung | Stand | Test |
|---|---|---|---|---|
| F01 | PLY ASCII | lesen | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F02 | PLY binär | Little Endian lesen | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F03 | XYZ/PTS/CSV | Textformate | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F04 | OBJ-Punkte | v-Zeilen | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F05 | Überblick | Anzahl, Größe, Mitte | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F06 | Farben | RGB übernehmen | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F07 | Störpunkte entfernen | Radius-Ausreißer | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F08 | Ausdünnen | Voxel-Gitter | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F09 | PLY speichern | ASCII mit Farbe | gebaut | `SPEC-09: Punktwolke aus PLY (ASCII/binär), XYZ, OBJ; ausdünnen, Ausreißer, PLY speichern` |
| F10 | Skalieren | Einheit umrechnen (mm ↔ m) | gebaut | `SPEC-09 F10: Skalieren` |
| F11 | Zentrieren | Mitte auf 0, Boden auf z = 0 | gebaut | `SPEC-09 F11: Zentrieren` |
| F12 | Zuschneiden | nur Punkte in einer Box | gebaut | `SPEC-09 F12: Zuschneiden` |
| F13 | Boden finden | Ebene per RANSAC, Neigung | gebaut | `SPEC-09 F13: Boden finden` |
| F14 | Boden entfernen | Punkte nahe der Ebene weg | gebaut | `SPEC-09 F14: Boden entfernen` |
| F15 | Scans vergleichen | mittlerer und größter Abstand | gebaut | `SPEC-09 F15: Scans vergleichen` |
| F16 | Volumen | Höhenfeld über dem Boden | gebaut | `SPEC-09 F16: Volumen` |
| F17 | XYZ speichern | Textexport | gebaut | `SPEC-09 F17: XYZ speichern` |
| F18 | Punktdichte | mittlerer Nachbarabstand | gebaut | `SPEC-09 F18: Punktdichte` |
| F19 | Drehen | um die Hochachse | gebaut | `SPEC-09 F19: Drehen` |
| F20 | Zusammenführen | zwei Wolken vereinen | gebaut | `SPEC-09 F20: Zusammenführen` |
