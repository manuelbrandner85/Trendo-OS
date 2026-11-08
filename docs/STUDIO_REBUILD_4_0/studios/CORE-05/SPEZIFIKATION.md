# Technische Spezifikation: 3D-Modelle erstellen

Studio CORE-05 · 3D-Modelle erstellen · Bereich 3D, Spiele & VR

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

## Ist-Stand

3D-Modelle bauen und ansehen – Blender optional.

- Spezifikation: APPROVED_SPEC
- Umsetzung: IMPLEMENTED
- Test-Status: PC_VERIFIED
- Nachweis: reports/pc-0.9.2/tests-pc-4e6b81d.txt
- Abgenommen: nein, solange kein Abnahmeprotokoll 4.0 mit Testartefakten vorliegt

## Programme und Arbeitsflächen

- **3D** (`3d`): CAD-/3D-Zeichenfläche
- **Trendo 3D-Druck** (`3d-druck`): CAD-/3D-Zeichenfläche
- **Trendo CAD** (`cad`): CAD-/3D-Zeichenfläche

## Quellcode

- `src/Trendo.App/MainWindow.Cad.cs`
- `src/Trendo.App/MainWindow.Print3D.cs`
- `src/Trendo.App/MainWindow.W4Studio3D.cs`

## Externe Programme

- Blender (optional, 3D-Fotos/Render)
- FFmpeg
- FreeCAD (optional)
- Git
- Hunspell-Wörterbücher
- Inkscape (optional)
- Krita (optional)
- LibreOffice (optional)
- Node.js (optional)
- OBS Studio (optional)
- Ollama – lokale KI (optional)
- PDFsharp (MIT)
- Python (optional)
- Tesseract OCR
- Unreal Engine (optional)
- mpv (optional)
- qpdf

## Dateiformate

`.docx`, `.fbx`, `.gcode`, `.glb`, `.gltf`, `.html`, `.jpg`, `.json`, `.mp3`, `.mp4`, `.obj`, `.odt`, `.pdf`, `.png`, `.stl`, `.txt`, `.xlsx`, `.xml`, `.zip`

Die Formatliste ist aus dem Quelltext geschätzt und kann Formate enthalten, die nur nebenbei vorkommen.

## Stand der GitHub-Recherche

- Recherche und Bewertung: erledigt (Runde 1, 07.11.2026).
- **f3d-Vorschaubilder: eingebaut.**
  - Kern: `src/Trendo.Core/Model3DPreview.cs` (Formatprüfung, Pfad- und Größenprüfung, Argumente als Liste, Zeitlimit).
  - Katalog: Eintrag „f3d“ mit offizieller Zip v4.0.1 und SHA-256 7c36b7ab…56f8; Einrichtung pro Person ohne Admin, nur nach Zustimmung.
  - Oberfläche: Karte „Vorschaubilder (f3d)“ im 3D-Studio (`MainWindow.Studio3DF3d.cs`).
  - Kommandozeile geprüft gegen `doc/user/03-OPTIONS.md` von f3d v4.0.1.
  - Tests: 10 Kerntests (5 positiv, 5 negativ) und 1 Oberflächentest grün – f3d dabei nachgebildet.
- **NOT RUN:** echter f3d-Lauf auf diesem PC. Das Ausführen der heruntergeladenen Programmdatei wurde vom Schutz für fremden Code abgelehnt; es braucht die Freigabe der Person.
- assimp-Import: geplant, NOT RUN.
