# Echter Lauf: Grundriss → IFC4 mit IfcOpenShell (Wohnung planen) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Eigene Python-Umgebung (Python 3.12) mit `ifcopenshell==0.9.0.post1` (LGPL-3.0) von PyPI, nur Binärpakete, unter `C:\Users\<user>\AppData\Local\Temp\trendo-tools-b\py-ifc`.

## 1. Einfacher Raum (5 × 4 m, 1 Tür, 1 Fenster)
- Erster Lauf: Wandvolumen zu groß – Öffnungen schnitten nur die halbe Wanddicke aus (Öffnung um eine halbe Dicke versetzt). Ursache: `create_2pt_wall` legt den Wandkörper links der Achse an. Korrigiert in `trendo_ifc.py`.
- Nachmessung (Netz aus `ifcopenshell.geom`, Volumen über Dreiecke):
  - Wand 1 mit Tür: **2,573 m³** = 5 × 0,24 × 2,5 − 0,885 × 2,01 × 0,24 ✓
  - Wand 2 mit Fenster: **2,054 m³** = 4 × 0,24 × 2,5 − 1,2 × 1,2 × 0,24 ✓
  - Raum: **44,744 m³** = 4,76 × 3,76 × 2,5 ✓

## 2. Trendo-Vorlage „2-Zimmer-Wohnung“ (Test `IfcExchangeTests.Real_template_roundtrip`)
- `IfcExchange.ToJson` → `trendo_ifc.py export` → `trendo_ifc.py info`: **IFC4: 14 Wände, 6 Türen, 3 Fenster, 5 Räume, 1 Geschoss**. Alle Anzahlen und Raumnamen stimmen mit dem Trendo-Plan überein.
- Datei `ifc-musterwohnung.ifc` (25316 Bytes, SHA-256 b3c4b63119910236f0bb2e7cec41e7a3d97c411c3581fb0f94508228e9a64353).
- Geometrie: Die Innenseiten der Wände liegen genau an den Raumkanten (z. B. Wohnzimmer bis x = 5,00 m, Wand 2 ab x = 5,00 m). Die Wandvolumen sind um die Öffnungen kleiner (Wand 1: 2,755 m³ statt 3,144 m³ ohne Öffnungen). Räume: Wohnzimmer 52,5 m³, Schlafzimmer 37,8 m³, Küche 21,0 m³, Bad 18,2 m³, Flur 19,32 m³.

## Tests
- `IfcExchangeTests` (Kern): 8 nachgebildete Tests und 1 echter Lauf, grün. Ohne `TRENDO_IFC_PYTHON` wird der echte Lauf übersprungen.
- `IfcUi` (Wohnungsplaner → Exportieren „IFC (BIM)“, Hinweis ohne Einrichtung, Aufräumen der Hilfsdateien): grün.

## Grenzen / nicht geprüft
- Es wird nur das aktive Geschoss exportiert. Möbel, Dachschrägen und Bogenwände (nur als gerade Stücke) sind nicht enthalten.
- Öffnen in Revit, ArchiCAD oder BlenderBIM: NOT RUN.
- Einrichtung über den Trendo-Knopf „BIM → IFC einrichten“ (uv): Die Schritte sind geprüft, aber nicht ausgeführt.
