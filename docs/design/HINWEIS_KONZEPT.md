# Hinweis zum Designbild
`TRENDO_OS_DESKTOP_KONZEPT.png` ist die Design-41-Referenz „Desktop“ (mit Claude Code gebaut, Hintergrund lokal mit KI), kein Screenshot einer realen Trendo-OS-Installation.
- Alle Inhalte (Uhrzeit, CPU/RAM/GPU-Werte, Projekte, „Online“-Status) sind **Demodaten**.
- Das V-/T-Logo im Konzeptbild ist veraltet; gültig ist Logo B „Saphir-Kristall“ (`src/Trendo.App/Assets/logo/trendo-logo-b.svg`, von Manuel am 26.10.2026 gewählt; vorher Logo A).
- Verbindlich sind nur die Gestaltungsziele in `DESIGN_BRIEF.md`.

## Umsetzung in der App (UI v0.3)
Die Avalonia-Oberfläche bildet Layout und Stil des Konzepts nach (Topbar, Seitenleiste + Schnellzugriff, 3×3-Studio-Kacheln, Hero mit Logo/Slogan/Aktionen, Dock, rechte Spalte mit KI, Systemstatus, Projekten, Schnell-Tools). Abweichungen aus Ehrlichkeitsgründen:
- Systemstatus zeigt **echte Messwerte** (CPU/RAM/SSD; GPU via nvidia-smi falls vorhanden, sonst „n. v.“).
- „Aktuelle Projekte“ zeigt **echte Projekte**, sonst Leerzustand.
- KI-Assistent: **„Nicht verbunden“**, bis ein Modell eingerichtet ist.
- Studio-Kacheln tragen **echte Status-Badges** (aktuell „Noch nicht gebaut“/„Nicht verbunden“/„Blockiert“).
- Nicht umgesetzte Schnell-Tools (Aufnahme, Notiz, Übersetzen) sind deaktiviert; „Übersetzen“ berührt nicht das abgelehnte Language-Studio 2.0.
- Die Vorschau-Leiste am unteren Bildrand des Konzepts (Marketing-Thumbnails) ist kein Desktop-Element und wurde nicht übernommen.
- **Logo:** Logo B „Saphir-Kristall“ (App-Symbol, Fenster, Taskleiste, Startknopf, Desktop).
- **Hintergrund:** `src/Trendo.App/Assets/wallpaper.jpg`, lokal mit KI erzeugt (FLUX.2 klein, 24.10.2026) eigens für Trendo OS; keine Fremdfotos (Herkunft: `docs/design/HINTERGRUENDE.json`).
