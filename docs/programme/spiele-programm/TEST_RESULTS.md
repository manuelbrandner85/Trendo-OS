# Spiele bauen – Testergebnisse

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele-programm` · Stand: 0.9.36 (Stand 5b38700)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 24 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen | ✓ bestanden |
| CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check) | ✓ bestanden |
| CORE-06: Claude baut Level – sieht alle Level voll, Plan zeigt das neue Level als Vorschau, erst „Ausführen“ legt Level 2 und den Hintergrund an, falsches Zeichen fällt weg, „Verwerfen“ stellt alles zurück | ✓ bestanden |
| CORE-06: Gebautes Spiel mit 2 Leveln, Hintergrund und Lauf-Animation – config.js trägt alles, Bildstreifen liegt im Spielordner, game.js ist gültiges JavaScript (Node) | ✓ bestanden |
| CORE-06: Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node) | ✓ bestanden |
| CORE-06: Mehrere Level – Ziel lädt das nächste Level (+10, Hinweis „Level 2 von 2“), Münzen und Eigenschaften je Level („2:x,y:value“), erst das letzte Ziel gewinnt; Nochmal beginnt bei Level 1 | ✓ bestanden |
| CORE-06: Phasen mit Abnahme – nur der Reihe nach und nur mit erfüllten Kriterien; Zurücknehmen öffnet auch spätere Phasen; Beta braucht Test-Notizen ohne offene Fehler | ✓ bestanden |
| CORE-06: Profi-Ablauf – Phasen-Leiste mit Abnahme (gesperrt, bis alles erfüllt ist), Rollen mit Arbeitsplatz, Test-Bereich prüft jedes Level und zeigt die Stelle, Notiz und Fehler aus dem Probespiel, Ton-Spur; Claude bekommt den Prüfbericht | ✓ bestanden |
| CORE-06: Prüfung – Hintergrund nur Deko-Zeichen (w, b, t, m, g), Level 2 ff. nur gültige Kacheln, höchstens 20 Level, Animation nur für bekannte Zustände, Bilder 1–32 und 1–30 je Sekunde | ✓ bestanden |
| CORE-06: Regeln: „Münzen ≥ 2 → +1 Leben“ feuert genau einmal, „Punkte ≥ 3 → Gewonnen“ beendet das Spiel | ✓ bestanden |
| CORE-06: Schlüssel öffnet die Tür: mit Schlüssel geschafft, ohne Schlüssel bleibt die Tür zu; Regel „Sekunden ≥ 1 → Türen öffnen“ öffnet sie auch | ✓ bestanden |
| CORE-06: Speicherpunkt: nach einem Treffer geht es dort weiter statt am Start | ✓ bestanden |
| CORE-06: Spiel-Editor mit mehreren Leveln – Level hinzufügen, umbenennen, wechseln, früher schieben, löschen (Eigenschaften rücken nach); Probespiel läuft durch alle Level | ✓ bestanden |
| CORE-06: Spiel-Editor – Animation je Zustand: Bildstreifen wählen, Bilder und Tempo setzen (begrenzt), Probespiel zeigt Laufen-Bilder nacheinander, Entfernen nimmt sie raus; Bauen kopiert den Streifen ins Spiel | ✓ bestanden |
| CORE-06: Spiel-Editor – Hintergrund-Ebene nur mit Deko (Wolke, Baum …), Spiel-Ebene bleibt unverändert, andere Ebene blass sichtbar; Bauen übernimmt den Hintergrund | ✓ bestanden |
| CORE-06: Spielbarkeits-Prüfung meldet unfaire Stellen – Gegner am Start, Landung neben Stacheln, unerreichbare Münze, Schlüssel öffnen Türen; Sprungfeder hilft über hohe Wände | ✓ bestanden |
| CORE-06: Spielbarkeits-Prüfung – Lücke von 3 schafft die Figur (wie im Probespiel), Lücke von 7 nicht; Ziel hinter zu hoher Wand nicht erreichbar | ✓ bestanden |
| CORE-06: Sprungfeder schleudert viel höher als ein normaler Sprung | ✓ bestanden |
| CORE-06: Ton-Spur und Treffer-Animation – Geräusch je Ereignis und Musik landen im Spielordner, config.js nennt sie, game.js spielt sie (sonst Piepton); nach einem Treffer ist der Zustand „treffen“ | ✓ bestanden |
| CORE-06: Unreal-Engines aus Launcher-Liste erkannt, neueste zuerst; .uproject ist gültig | ✓ bestanden |
| CORE-06: Zeitlimit: nach Ablauf verloren, Restzeit zählt herunter; ohne Limit −1 | ✓ bestanden |
| CORE-06: Zustände der Figur – stehen auf dem Boden, laufen mit Tempo, springen nach oben, fallen nach unten; Blickrichtung folgt der Laufrichtung | ✓ bestanden |
| Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen | ✓ bestanden |
| Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an | ✓ bestanden |
