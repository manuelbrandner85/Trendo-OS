# Status CORE-05 – 3D-Modelle erstellen

- Recherche und Bewertung: erledigt (Runde 1, 07.11.2026).
- **f3d-Vorschaubilder: eingebaut.**
  - Kern: `src/Trendo.Core/Model3DPreview.cs` (Formatprüfung, Pfad- und Größenprüfung, Argumente als Liste, Zeitlimit).
  - Katalog: Eintrag „f3d“ mit offizieller Zip v4.0.1 und SHA-256 7c36b7ab…56f8; Einrichtung pro Person ohne Admin, nur nach Zustimmung.
  - Oberfläche: Karte „Vorschaubilder (f3d)“ im 3D-Studio (`MainWindow.Studio3DF3d.cs`).
  - Kommandozeile geprüft gegen `doc/user/03-OPTIONS.md` von f3d v4.0.1.
  - Tests: 10 Kerntests (5 positiv, 5 negativ) und 1 Oberflächentest grün – f3d dabei nachgebildet.
- **NOT RUN:** echter f3d-Lauf auf diesem PC. Das Ausführen der heruntergeladenen Programmdatei wurde vom Schutz für fremden Code abgelehnt; es braucht die Freigabe der Person.
- assimp-Import: geplant, NOT RUN.
