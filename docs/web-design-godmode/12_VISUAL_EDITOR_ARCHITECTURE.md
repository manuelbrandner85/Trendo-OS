# Visueller Editor
- Quellen `tools/webdesign-editor/src/main.jsx` (Bausteine, App, Anzeigemodus), `de.js`, `trendo.css`; Bau mit Vite als **klassisches Skript (IIFE)**, weil Modul-Skripte unter `file://` gesperrt sind.
- Bausteine: Kopfbereich (mit Bewegung), Überschrift, Text, Bild (Alt-Text-Feld), Knopf, Bereich (Slot), Spalten (2 Slots), 3D-Szene.
- Einmalige „bereit“-Meldung (im Test gefundene Endlosschleife Laden→bereit→Laden mit über 4000 Neuzeichnungen behoben).
