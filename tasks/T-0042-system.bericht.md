# T-0042 System

**Ziel:** Die restlichen Katalogstudios haben einen Adapter. Lokale Dateien nur dort, wo eine vorhandene Funktion nichts nach außen tut. Alles andere bleibt blockiert.
**Branch:** cursor/v42-system-fa7f
**Dateien:** `SystemPromptAdapter.cs`, Katalog, Registrierung, Matrix, Render-Aufruf, Tests.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 576 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Die Baukasten-Seite selbst zeigt weiter „Meine Seite“. Die HTML-Datei liegt im Projekt, die Leiste steht auf „QA bestanden“.
- Veröffentlichen, Klonen, Installieren, Ablauf starten, Scan, Stream, Zahlung, Versand, Blender, Unreal, Kamera, Roboter, Download, Theme, Desktop, Forensik, Reparatur und der Verbund bleiben aus.
- Es gibt keine lokale KI-Antwort und keine Netzrecherche. `ready` steht bei keinem Studio. Verbesserung 42 ist nicht vollständig.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019. Kein COLMAP, kein Meshroom, kein LibreOffice, kein Blender, kein Unreal, kein SMTP, kein GitHub-Klon.
