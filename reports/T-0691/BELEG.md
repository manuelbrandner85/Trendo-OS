# T-0691 – Belege 3D-Studio und CAD (16.11.2026)

Alle Läufe auf dem Entwicklungs-PC, Pfade mit Benutzernamen ersetzt durch `C:\Users\<user>`.

## 1. Absturz mit echtem Prozessabbruch (`core-tests.log`)
Hilfsprogramm `tests/Trendo.CrashProbe` (Modi `szene3d`, `cad`) schreibt die automatische Sicherung genau wie die App
(`Scene3D.SaveTo` bzw. `CadDoc.Save`, beide über `AtomicFile`) und wird von `Studio3DCadCrashTests` 16-mal mit `Process.Kill()` beendet.
Ergebnis je Art: „16 harte Abbrüche, 16 mitten im Schreiben, höchstens 1 Schritt verloren“; die Sicherung war nach jedem Abbruch vollständig lesbar
(Netz mit 40 000 Dreiecken nicht abgeschnitten). Die App sichert alle 20 s (`ProgramAutosaveEvery`) und beim Schließen → Verlust höchstens 20 s.

## 2. Fremddatei → bearbeiten → Export → Fremdprogramm
- Eingang: `tests/fixtures/t0691/affe-blender.{glb,obj,stl}` – von Blender 5.2 (eigener Hintergrundprozess `blender -b`) geschrieben, Suzanne, 968 Dreiecke.
- Trendo: einlesen, als Netz bearbeiten (höchster Punkt +7,5 cm), exportieren → `dateien/affe.{gltf,glb,obj,stl}` (Test `MeshEditTests.Roundtrip_foreign_file`, zusätzlich Wiedereinlesen in Trendo: gleiche Dreieckszahl, gleicher Umfang, verschobener Punkt vorhanden).
- **Blender 5.2.3** (`blender-oeffnen.py`, eigener Prozess `blender -b --factory-startup`, nicht der laufende Blender des Nutzers) öffnet alle vier Dateien: je 1 Objekt, 967 Dreiecke (`blender-oeffnen.log`).
  Ein Dreieck der Vorlage ist entartet (Fläche 0) und fällt beim Verschweißen weg. Die Maße passen zu den Einheiten: glTF/GLB in Metern (2,04 m Höhe = 197 cm + 7,5 cm);
  die OBJ/STL-Vorlagen waren in Blender-Metern geschrieben, der Test liest sie als cm bzw. mm – daher kleinere Maße, Formverhältnis gleich.
- **Khronos glTF-Validator 2.0.0-dev.3.10**: `affe.gltf` und `affe.glb` – 0 Fehler, 0 Warnungen (`validator-affe-*.json`).
  Dabei gefunden und behoben: Trendos GLB-Schreiber gab min/max der Positionen gerundet aus (ACCESSOR_MIN/MAX_MISMATCH) – jetzt exakt.
- DXF: handgeschriebene Fremd-DXF `tests/fixtures/t0691/fremd-halteplatte.dxf` (AC1015, LWPOLYLINE mit Bogen, Kreise, Bogen, Linie, TEXT, MTEXT, Bemaßung, 5 Ebenen, eine davon aus)
  → gelesen, Linie geändert, geschrieben `dateien/halteplatte-trendo.dxf` → **Inkscape 1.4.4** liest sie und schreibt SVG mit allen 6 Ebenen (`inkscape-dxf.log`, `dateien/halteplatte-aus-inkscape.svg`).

## 3. Oberfläche (`app-tests.log`, `3d-bearbeiten.png`)
Echte Maus- und Tastenereignisse (Avalonia headless): Würfel einsetzen → Tab → 3 → Fläche anklicken → E → Strg+Z / Strg+Y → Entf → Tab → „Als glTF“ → Datei wieder gelesen.
Punkt anklicken, Umschalt+Klick, Pfeiltaste; Fläche füllen und Normalen über die Knöpfe; Wiederherstellung nach „Neustart“ für 3D und CAD; DXF einlesen, Ebene als Körper, DXF-Export, Strg+Z in der CAD-Ansicht.

## Nicht belegt / blockiert
- **STEP in FreeCAD**: FreeCAD ist auf dem PC nicht vorhanden → BLOCKIERT (nichts installiert). STEP-Export heißt jetzt ehrlich „facettiert“.
  **Nachtrag T-0776 (26.11.2026):** FreeCAD 1.1.4 ist inzwischen vorhanden (`C:\Program Files\FreeCAD 1.1`). Der Nachlauf fand einen Fehler: FreeCAD las Trendos STEP als leere Schalen ohne Fläche (nackte `FACE`). Behoben (`FACE_SURFACE` mit Ebene); jetzt gültige geschlossene Körper mit richtigem Volumen (`reports/T-0776/freecad-step/`). DXF-Export ebenfalls in FreeCAD gelesen (`reports/T-0776/freecad-cad-dxf/`).
- Echter Absturz der ganzen App (Fenster) wurde nicht getötet; geprüft sind der Schreibweg (Prozessabbruch) und der Neustart-Weg (neues Fenster, gleicher Datenordner) getrennt.

## SHA-256
```
35f19f0b3f26aa1ad07418b6894fbf07830ba853feb9f5724234a266f2d35494  app-tests.log
3f7a40374e73d1baeb248ca7fc95193b6be3189a5377de9e16feeb3878885577  blender-oeffnen.log
3fd3848f0424e9c1dd7631587847be27cdc03f16e0abd31891a86ab2bd2ac45a  core-tests.log
d3182f190d3811e92b743ece0d9b02b2d133ed6fdb6d321098197a6f37042863  inkscape-dxf.log
717196865858fe2b21ca15e4f0cc360e7fce89f777db9ae147804cb1a6be6b4e  validator-affe-glb.json
b33a02e661cbe29aeeb7587cadaba7d348a746566916439678fe10f4ee58db3f  validator-affe-gltf.json
6264d3962fc3ed5a76ec42da18c35b4e017471ef7a39c5a7e5b4ee0018896853  3d-bearbeiten.png
ed867e27fbea7c511584c4c206863381f5740176e15bb661875a10066db70756  blender-oeffnen.py
ddc83e18533439fa28063afa2ed8067ff806025f84c5860f0cbab4b201f278ff  dateien/affe.glb
59755e6b7d2cb6d8818d4a73fd51e736a0a304fb8a626cfc311deaffdb321ccc  dateien/affe.gltf
1bc4f218c14a435572d2785e200407c60d59e2ae9ce1d20f673f9832dbccc5ad  dateien/affe.mtl
2a91bc43c5fad33ba666696feab95b336e32a009c3efd87a44bfdc9f8abab140  dateien/affe.obj
ebfb0932d59cbce080f2a6228f54433569167ef8cdc6bf1508c1b70aa839d306  dateien/affe.stl
721d441df6046cdf9edd889040f0089442e98c76465387de87a01671f28a8b9d  dateien/halteplatte-aus-inkscape.svg
9e1d739492945ccf0760e8323c4bb3eee5fe55ddbd9addf91890db920d215723  dateien/halteplatte-trendo.dxf
1600bf43695a3b4bc592019181a1ae674229d1e48ba8b1f59f5805f91e61bc51  tests/fixtures/t0691/affe-blender.glb
316ddbc3e9be80d8f41c7793b69aa059afd90d2439173c444902b338a7105784  tests/fixtures/t0691/affe-blender.obj
dbb43e67372ce454841097b8ff7af6513e1d91190d8d189a3fb762fa19042c77  tests/fixtures/t0691/affe-blender.stl
b96cc46e7eeaf4a495ce4a1f111bfe1a24670ca904cce5b131d78711a16fb486  tests/fixtures/t0691/fremd-halteplatte.dxf
```
