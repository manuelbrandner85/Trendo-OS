# Native Integration
- `MainWindow.WebDesign.cs`: Knöpfe Editor öffnen/schließen, Export, Export im Browser; NativeWebView wird wiederverwendet (aus altem Rahmen gelöst – Absturz „Control already has a parent“ im Test gefunden und behoben).
- Höhe des Editors nach Fensterhöhe; das Mausrad über dem Editor gehört dem Editor.
- Bündel als Dateien neben dem Programm (`<None CopyToOutputDirectory>`), nicht als Avalonia-Ressource.
- Testmodus (`Isolated`): Editor wird nicht geladen.
