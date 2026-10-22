# T-0101 Bericht: Design-Tokens

**Auftrag:** Zentrale Avalonia-Ressourcen und Bausteine statt im Code erzeugter Brushes.
**Branch:** `cursor/bauplan-t0005-t0012-f36c`

Nicht neu gebaut. Design 41 ist mit PR #237 auf `main` (`2eef442`). Die Farben und Abstände stecken weiter in `MainWindow.Look.cs` und den Seiten, nicht in einem eigenen `Themes/`- oder `Controls/`-Satz. Diesen Umbau wiederholt diese Sitzung nicht.

## Geänderte Dateien

Keine für Tokens. Dieser Bericht.

## Testergebnis

Die bestehende Suite bleibt die aus T-0005: Api 15, Core 538 plus 10 übersprungen, App 225. Ein eigener Fokusring-Test für neue Token-Bausteine gibt es nicht, weil die Bausteine nicht angelegt wurden.

## Offene Punkte

- Theme-Wechsel läuft über die vorhandene Hell/Dunkel/Kontrast-Wahl. Ein kompletter Token-Katalog nach §4 ist nicht als eigene Datei nachgewiesen.
- SPEC-07 bleibt abgelehnt. Logo bleibt Entwurf A.

## Nicht geprüft

- Kein neuer Komponenten-Katalog, kein Windows-PC, keine DPI 125/150.
