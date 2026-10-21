# Design-Diff-Checkliste: Konzept vs. App (v0.5.1)

Referenz: docs/design/TRENDO_OS_DESKTOP_KONZEPT.png. Iterationen: 5 Headless-Renders (r1–r5) und 2 PC-Builds (v0.5.0, dessen Screenshot fehlerhaft war, und v0.5.1).
Belege: side-by-side-v051.png (Konzept | PC), before-after-v051.png (v0.3 | v0.5.1), screenshot-pc-v051*.png. Die Screenshots wurden vom echten PC-Build mit --screenshot aufgenommen, in logischer Auflösung 1365×649.

| # | Bereich | Konzept | Status |
|---|---|---|---|
| 1 | Topbar | Marke + Ultimate, Tabs mit Icons, Suche, Glocke, Cloud, User, Uhr | umgesetzt |
| 2 | Sidebar | farbige Chips, Schnellzugriff | umgesetzt |
| 3 | Studio-Kacheln 3×3 | Verlaufs-Icons mit Glow | umgesetzt; Badges zeigen echten Status |
| 4 | Hero | Logo mit Glow, Verlaufs-Schrift, Tagline, Aktionen, Ring | umgesetzt |
| 5 | Dock | freistehende Icons plus Papierkorb | umgesetzt |
| 6 | KI-Panel | Chat, Eingabe, Senden | umgesetzt; echte lokale Antwort |
| 7 | System Status | Ringe | umgesetzt; echte Werte |
| 8 | Projekte | Liste | Leerzustand, da keine echten Projekte vorhanden sind |
| 9 | Schnell-Tools | 4 Buttons | umgesetzt |

## Verbleibende, nicht vermeidbare Unterschiede
- Kein echter Backdrop-Blur (Avalonia), nur halbtransparente Panels.
- Daten sind echt: Badges, Projekte (leer), KI-Text und Systemwerte.
- Nicht gebaute Tools sind deaktiviert.
- Das Wallpaper ist ein anderes Bild (eigenes, KI-generiertes) und dunkler.
- Der Marketing-Streifen unten im Konzept ist kein Desktop-Element.
- Pixelidentität ist bei anderer Auflösung und anderem Font-Rendering nicht möglich.
- Logo: Konzept, nicht freigegeben.
