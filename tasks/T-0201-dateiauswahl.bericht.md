# T-0201 Bericht – Dateiauswahl und Drag & Drop

- Gemeinsame Hilfe `MainWindow.Files.cs` + Erkennung `Trendo.Core/Shell/PathFields.cs`: Jedes Datei-/Ordnerfeld (erkannt an Name `…-file/-folder/-path/…` bzw. Platzhalter „Datei/Ordner/Pfad“; Adressen, Muster, Parameter ausgenommen) bekommt **„Datei wählen …“** bzw. **„Ordner wählen …“** (Avalonia `StorageProvider`, Filter aus dem Platzhalter, z. B. CSV/XLSX, GPX, Bilder) und **🕘 Zuletzt** (zuletzt benutzte Dateien der Seite, in `settings.json`).
- **Drag & Drop:** Datei aufs Feld → dieses Feld; aufs Studio-Fenster → erstes leeres passendes Feld. Datei auf Ordnerfeld → ihr Ordner. Mehrfachfelder (PDF zusammenfügen) hängen an.
- Eintippen bleibt möglich; Berechnungslogik unverändert. Felder, die erst nach einem Klick entstehen (Rezepte), werden danach ergänzt.
- Abdeckung: **79 Felder** auf allen Seiten (Audit nannte 36). 3D-Studio hat keine Pfadfelder → unberührt.
- Tests: `T0201_PathFields` (Core, 13), `T0201_Dateiauswahl_Ui` (4: jedes Feld hat Knopf, Mausklick füllt + Zuletzt, Ordner-Dialog, Drop).
- Offen: echter Explorer-Drop und System-Dialog auf dem Windows-PC (Integrator).
