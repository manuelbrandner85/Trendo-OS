# Spiele bauen – Testergebnisse

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele-programm` · Stand: 0.9.36 (Stand 208ed9e)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 11 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen | ✓ bestanden |
| CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check) | ✓ bestanden |
| CORE-06: Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node) | ✓ bestanden |
| CORE-06: Regeln: „Münzen ≥ 2 → +1 Leben“ feuert genau einmal, „Punkte ≥ 3 → Gewonnen“ beendet das Spiel | ✓ bestanden |
| CORE-06: Schlüssel öffnet die Tür: mit Schlüssel geschafft, ohne Schlüssel bleibt die Tür zu; Regel „Sekunden ≥ 1 → Türen öffnen“ öffnet sie auch | ✓ bestanden |
| CORE-06: Speicherpunkt: nach einem Treffer geht es dort weiter statt am Start | ✓ bestanden |
| CORE-06: Sprungfeder schleudert viel höher als ein normaler Sprung | ✓ bestanden |
| CORE-06: Unreal-Engines aus Launcher-Liste erkannt, neueste zuerst; .uproject ist gültig | ✓ bestanden |
| CORE-06: Zeitlimit: nach Ablauf verloren, Restzeit zählt herunter; ohne Limit −1 | ✓ bestanden |
| Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen | ✓ bestanden |
| Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an | ✓ bestanden |
