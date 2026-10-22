# T-0107 Bericht: Fenster, Einstellungen, Bewegung

**Auftrag:** Fenstertiefe, Einstellung mit Bereichen, Bewegung reduzieren, Leistungsprofile.
**Branch:** `cursor/bauplan-t0005-t0012-f36c`

Fenster und Einstellungen liegen auf `main` (PR #237). Bilder: `reports/design-41/vergleich/06_FENSTER.jpg`, `mehrfenster-1366.jpg`, Einstellungen unter `reports/design-41/vergleich/gruppen/settings-*.jpg`. Diese Sitzung hat die Fensterlogik nicht neu geschrieben.

## Geänderte Dateien

Keine an Fenstern oder Einstellungen. Dieser Bericht.

## Testergebnis

App 225 grün, darin Ziehen, Größe, Taskleiste und die Design-41-Einstellungsabschnitte, soweit die Suite sie schon prüft.

## Offene Punkte

- Startzeit am Windows-PC vorher/nachher ist nicht gemessen (siehe T-0100).
- DPI 125 % und 150 % fehlen.
- Logo bleibt Entwurf A.

## Nicht geprüft

- Kein PC-Lauf mit Stoppuhr, kein separates Low-Power-Profil neu erfunden in diesem Branch.
