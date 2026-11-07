# Puck (puckeditor/puck, main d05542f, 2026-11-06; npm @puckeditor/core 0.24.0) – gewählter Editor-Kern
- Lizenz MIT. React 18/19 (peer), Ziehen über dnd-kit 0.4, Rich-Text über tiptap, Popover über Radix.
- Kern `packages/core`: `components/` (Puck, DropZone, LayerTree, ViewportControls, AutoField …), `lib/` (Daten, dnd, Felder), `reducer/` (Aktionen insert/move/duplicate/remove …), `store/`, `plugins/`.
- Datenmodell: JSON `{root, content[], zones}`; Bausteine über `config.components` mit Feldern (text, textarea, select, radio, number, slot …).
- Übersetzbar: `dictionary`-Prop mit allen Oberflächentexten (`lib/dictionary.ts`) → deutsche Oberfläche `tools/webdesign-editor/src/de.js`.
- Overrides: `headerActions` (Vorschau-Knopf), `drawerItem` (Doppelklick/Eingabetaste fügt ein).
- Befund im Test: Mit iframe-Leinwand unter `file://` bleibt die Leinwand leer → unter `file://` ohne iframe; Container-Abfragen für Handy/Tablet.
