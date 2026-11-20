# Bericht T-0749: Einscannen (Fotogrammetrie) – echter Lauf

**Branch:** `claude/T-0749-einscannen` (Kopie von main 0.9.105, Ordner `%TEMP%\trendo-auftrag-T-0749`)
**Status:** Echtlauf mit COLMAP 4.2.1 über die Oberfläche belegt (grobe Form, ohne GPU). Nicht BLOCKIERT.

## Werkzeugsuche
- COLMAP 4.2.1 (nocuda) liegt im Trendo-Werkzeugordner `%LOCALAPPDATA%\TrendoWerkzeuge\colmap\bin\colmap.exe` (aus dem Vollpaket); die App findet es über `DependencyManager.ResolvePath`.
- Meshroom/AliceVision, OpenMVS, OpenMVG, RealityCapture: nicht vorhanden (PATH, `Program Files`, `C:\tools`, `%LOCALAPPDATA%`, Desktop, Downloads). pycolmap/trimesh/open3d nicht installiert.
- Blender 5.2.3 vorhanden → Testbildsatz selbst gerendert (`tools/echtlauf/einscannen/t0749_bilder.py`).

## Gefundene Fehler (vorher → nachher)
1. **Schritt 1 brach immer ab.** Vorher: `--SiftExtraction.use_gpu` – COLMAP 4 meldet „unrecognised option“, der Scan lief mit dem mitgelieferten COLMAP nie (Beleg `reports/T-0749/T-0749-rot-vorher-*.{txt,log}`, derselbe Echt-Test über die Oberfläche, rot). Nachher: `FeatureExtraction.*`/`FeatureMatching.*`; meldet eine ältere Fassung (3.x) „unrecognised option“, wiederholt Trendo den Schritt einmal mit den alten Namen und bleibt dabei (`PhotoScan.Legacy`). Test: `PhotoScanTests.Plan_colmap_und_video` (Optionen), `Alte_fassung_und_absturz` (Rückfall – mit Ersatz-Runner, kein echtes 3.x vorhanden), echt: `T0749EinscannenEcht_Ui.Fotos_zu_netz`.
2. **mapper ohne Ausgabeordner.** Vorher: „`E… sfm.cc:363] output_path is not a directory`“ im ersten Echtlauf nach Fix 1 (Ausgabe nicht gesondert abgelegt; im Hand-Lauf `T-0749-colmap-manuell-vorher.log` wurde der Ordner deshalb vorher angelegt). Nachher: `sparse` wird vor der Rechnung angelegt. Test: Fake in `Lauf_und_fehler` prüft den Ordner vor `mapper`; echt grün.
3. **Oberfläche stürzte ab.** Vorher: `image_undistorter` + `delaunay_mesher` auf „dicht“ – ohne CUDA-Stereo keine `fused.ply`, COLMAP beendet sich mit 0xC0000409 ohne Meldung (`T-0749-colmap-manuell-vorher.log`). Nachher: `delaunay_mesher --input_type sparse` direkt auf dem Kameramodell, `--DelaunayMeshing.max_side_length_factor 2` (sonst ragte ein Keil zu einem Ausreißerpunkt aus dem Modell, im Blender-Bild gesehen). Entzerren entfällt (wurde nur für den dichten Weg gebraucht).
4. **Fehlertexte/Protokoll.** Vorher: nur die letzten 3 Zeilen, Absturz als „fehlgeschlagen.“ ohne Code, keine Datei mit der vollen Ausgabe. Nachher: `rechnung-protokoll.txt` im Arbeitsordner (Befehl, volle Ausgabe, Exitcode, Laufzeit je Schritt); Absturz als „(abgestürzt, Code 0xC0000409)“; Meldung nennt das Protokoll. Ergebnistext sagt ehrlich „grob, feine Details fehlen“.
5. **Abbruch:** war schon richtig (Prozessbaum wird beendet) – jetzt echt belegt (`T-0749-stopp.log`: Prozess nach 0,1 s weg, kein Modell).

## Echtlauf (reports/T-0749)
48 Fotos → 48 Kameras, 3819 3D-Punkte, COLMAP-Netz 2135 Punkte / 4165 Dreiecke, Trendo-Netz 4165 Dreiecke, 15,1 s über die Oberfläche (Merkmale 3,2 s, Abgleich 5,9 s, Kameras 5,1 s, Oberfläche 0,3 s). Export OBJ/STL/GLB über die Knöpfe; Blender 5.2.3 importiert alle drei (je 4165 Dreiecke, längste Seite 20 cm in der jeweiligen Einheit). Einrichten-Weg: Katalog-SHA-256 = Hersteller-Digest (GitHub-API), Vollpaket-Manifest v0.9.15 = Release-Asset-Digest; nichts geladen.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "FullyQualifiedName~PhotoScanTests|…ScanXrProTests|…ScanCoverageTests|…PhotoSetTests"` → Bestanden 14/14
- `Trendo.App.Tests.exe -class Trendo.App.Tests.PhotoScanUiTests` → 4/4; `ScanCoverage_Ui` → 1/1; `ScanXrPro_Ui` → 2/2
- `Trendo.App.Tests.exe -class Trendo.App.Tests.T0749EinscannenEcht_Ui` mit `TRENDO_T0749_FOTOS` → 2/2 (echt); ohne → 2 übersprungen
- Volltest nicht ausgeführt (Vorgabe).

## Was ersetzt blieb / nicht geprüft
- Die Suche nach der Programmdatei: Das Testfenster ist isoliert, der Pfad kommt aus der App-Erkennung ohne Isolierung (`ScanToolOverride`). Prozess, Ausgabe, Dateien echt.
- Rückfall auf COLMAP-3.x-Namen nur mit Ersatz-Runner; Meshroom- und Video-Weg (FFmpeg) nicht echt gelaufen; CUDA-Fassung (dichte Rekonstruktion) nicht vorhanden und nicht eingebaut; echte Handyfotos nicht getestet; Oberfläche nicht im Bild geprüft (nur Status-Text geändert, Render-Werkzeug zeigt nur den Leerzustand).

## Offene Punkte
- Dichte, texturierte Rekonstruktion braucht die CUDA-Fassung von COLMAP (patch_match_stereo + stereo_fusion) – eigener Auftrag mit Freigabe für den Download (~415 MB).
- `latest/download/manifest.json` gibt 404; die App fällt auf v0.9.15 zurück – funktioniert, ist aber ein Umweg.
- Ausrichtung: COLMAP-Koordinaten sind beliebig gedreht (Modell kann kopfstehen); Ausrichten am Boden fehlt.
