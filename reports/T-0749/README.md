# T-0749 Einscannen (SPEC-09): echter Fotoscan mit COLMAP

Stand 17.11.2026, PC des Projekts (Windows 11, RTX 5070). Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt.

## Werkzeug auf diesem PC
- COLMAP 4.2.1 (`colmap-x64-windows-nocuda`, ohne GPU-Unterstützung) im Trendo-Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge\colmap` – von der App über `DependencyManager.ResolvePath` gefunden.
- Meshroom/AliceVision, OpenMVS, OpenMVG, RealityCapture: nicht vorhanden (PATH, Programme, Werkzeugordner, Downloads geprüft). Nichts installiert, nichts geladen.
- Testbilder: Blender 5.2.3 im Hintergrund (`tools/echtlauf/einscannen/t0749_bilder.py`), 48 Ansichten 960 × 720 eines texturierten Testkörpers (Affe auf Sockel) auf zwei Ringen. Bildprüfsummen: `T-0749-testbilder-sha256.txt`, Beispielbild `T-0749-testbild-030.jpg`, Blender-Ausgabe `T-0749-testbilder-blender.log`.

## Vorher (Stand main 0.9.105)
- `T-0749-rot-vorher-xunit.txt`, `T-0749-rot-vorher-echtlauf.log`: derselbe Echt-Test über die Oberfläche bricht nach 0,4 s in Schritt 1 ab – `unrecognised option '--SiftExtraction.use_gpu'`. COLMAP 4 heißt die Optionen `FeatureExtraction.*`/`FeatureMatching.*`. Der Fotoscan konnte mit dem mitgelieferten COLMAP nie laufen.
- `T-0749-colmap-manuell-vorher.log`: alter Ablauf von Hand mit korrigierten Namen: `mapper` braucht einen vorhandenen Ausgabeordner, und `delaunay_mesher` auf dem Ordner „dicht“ stürzt ohne Meldung ab (Exit 127 in Bash, 0xC0000409 in PowerShell), weil ohne CUDA-Stereo keine `fused.ply` entsteht.

## Nachher (Zweig claude/T-0749-einscannen)
- `T-0749-gruen-xunit.txt`: `T0749EinscannenEcht_Ui` 2/2 bestanden (Echtlauf + Stopp).
- `T-0749-echtlauf.log` / `.json`: 48 Fotos → 48 Kameras eingepasst, 3819 3D-Punkte, COLMAP-Netz 2135 Punkte / 4165 Dreiecke, Trendo-Netz 4165 Dreiecke, 15,1 s über die Oberfläche.
- `T-0749-colmap-rohlog.txt`: volle Ausgabe aller vier COLMAP-Aufrufe, so wie die App sie jetzt als `rechnung-protokoll.txt` in den Arbeitsordner schreibt (mit Laufzeit und Exitcode je Schritt).
- `T-0749-stopp.log`: „Stopp“ während der Rechnung – der laufende COLMAP-Prozess ist nach 0,1 s weg, kein Modell, Status „Abgebrochen“.
- `T-0749-scan.glb`: Export der App (GLB). OBJ und STL nur mit Prüfsumme im Log (Größe).
- Fremdprogramm: `T-0749-blender-import.log` – Blender 5.2.3 importiert OBJ, STL und GLB, je 1 Objekt mit 4165 Dreiecken, längste Seite 20 cm (OBJ in cm, STL in mm, GLB in m). `T-0749-blender-ansicht.png`: Blender-Ansicht des GLB – Kopf mit Ohren und Sockelplatte erkennbar, Oberfläche grob.
- `T-0749-einrichten-pruefung.txt`: Einrichten-Weg nur über Metadaten geprüft (nichts geladen): Katalog-Prüfsumme = Hersteller-Digest von `colmap-x64-windows-nocuda.zip` 4.2.1; Vollpaket-Manifest v0.9.15 `colmap.zip` = Asset-Digest des Releases; `latest/download/manifest.json` gibt 404, die App fällt dann auf v0.9.15 zurück.
- `T-0749-core-tests.txt`, `T-0749-app-tests.txt`, `T-0749-ohne-umgebung.txt`: gezielte Testklassen; ohne `TRENDO_T0749_FOTOS` wird der Echt-Test übersprungen.

## Grenzen
- COLMAP ohne CUDA: nur grobe Form aus Merkmalspunkten (Sparse-Delaunay), keine dichte Rekonstruktion, keine Farbe/Textur. Die App sagt das im Ergebnistext.
- Meshroom-Weg und Video-Weg (FFmpeg) nicht echt gelaufen; Rückfall auf COLMAP-3.x-Optionsnamen nur mit Ersatz-Runner geprüft.

Prüfsummen: `SHA256SUMS.txt`.
