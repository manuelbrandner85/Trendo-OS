# Konto, Lizenz & Vollversion – Architektur

Studio: Schrift & Farben anpassen (SYS-12) · Bereich: System & Sicherheit · Seite: `lizenz` · Stand: 0.9.92 (Stand 94252fc)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Lizenz.cs`
- `src/Trendo.App/MainWindow.Sys12.cs`

## Bedienelemente der Arbeitsfläche
- Einstellungen-App (Bereiche, Suche, Seite – wie Windows 11)

## Fach-Engine (Trendo.Core)
- `A11y`
- `DeliveryMode`
- `LicenseVerifier`
- `DependencyCatalog`
- `LicenseKeys`
- `LicenseStore`

## Dateiformate (im Code belegt)
- .txt
- .zip

## Externe Programme und Dienste
- Blender (optional, 3D-Fotos/Render)
- Unreal Engine (optional)
- Ollama – lokale KI (optional)
- FFmpeg
- LibreOffice (optional)
- qpdf
- Krita (optional)
- Inkscape (optional)
- Tesseract OCR
- PDFsharp (MIT)
- Hunspell-Wörterbücher
- Git
- OBS Studio (optional)
- mpv (optional)
- Python (optional)

## Bausteine (Masterprompt 131 / 5)
Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.
