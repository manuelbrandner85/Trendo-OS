# Hinweis zum Designbild
`TRENDO_OS_DESKTOP_KONZEPT.png` ist ein **KI-generiertes Konzeptbild**, kein Screenshot einer realen Trendo-OS-Installation.
- Alle Inhalte (Uhrzeit, CPU/RAM/GPU-Werte, Projekte, „Online“-Status) sind **Demodaten**.
- Das V-/T-Logo im Konzeptbild ist veraltet; gültig ist Logo A (`src/Trendo.App/Assets/logo/trendo-logo-a.svg`, von Manuel am 22.10.2026 gewählt).
- Verbindlich sind nur die Gestaltungsziele in `DESIGN_BRIEF.md`.

## Umsetzung in der App (UI v0.3)
Die Avalonia-Oberfläche bildet Layout und Stil des Konzepts nach (Topbar, Seitenleiste + Schnellzugriff, 3×3-Studio-Kacheln, Hero mit Logo/Slogan/Aktionen, Dock, rechte Spalte mit KI, Systemstatus, Projekten, Schnell-Tools). Abweichungen aus Ehrlichkeitsgründen:
- Systemstatus zeigt **echte Messwerte** (CPU/RAM/SSD; GPU via nvidia-smi falls vorhanden, sonst „n. v.“).
- „Aktuelle Projekte“ zeigt **echte Projekte**, sonst Leerzustand.
- KI-Assistent: **„Nicht verbunden“**, bis ein Modell eingerichtet ist.
- Studio-Kacheln tragen **echte Status-Badges** (aktuell „Noch nicht gebaut“/„Nicht verbunden“/„Blockiert“).
- Nicht umgesetzte Schnell-Tools (Aufnahme, Notiz, Übersetzen) sind deaktiviert; „Übersetzen“ berührt nicht das abgelehnte Language-Studio 2.0.
- Die Vorschau-Leiste am unteren Bildrand des Konzepts (Marketing-Thumbnails) ist kein Desktop-Element und wurde nicht übernommen.
- **Logo:** Logo A „Obsidian-Kachel“ (App-Symbol, Fenster, Taskleiste, Startknopf, Desktop).
- **Hintergrund:** `src/Trendo.App/Assets/wallpaper.jpg`, KI-generiert mit dem früheren Entwicklungswerkzeug (21.10.2026) eigens für Trendo OS; keine Fremdfotos. Wird durch eine mit Claude erstellte Fassung ersetzt (`docs/claude/FERTIGSTELLUNGSPLAN.md`, D2).
