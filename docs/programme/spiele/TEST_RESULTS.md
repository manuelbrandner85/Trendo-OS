# Werkstatt – Testergebnisse

Studio: Spiele bauen (CORE-06) · Bereich: 3D & Spiele · Seite: `spiele` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 36 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen | ✓ bestanden |
| CORE-06 G1: Godot Asset Library – Suche mit Titel, Autor, Lizenz; Laden nur von GitHub/GitLab, ZIP sicher entpackt; fremde Download-Adresse abgelehnt | ✓ bestanden |
| CORE-06 G1–G3: Assets – Godot-Suche, neue Downloads übernehmen, Bildstreifen als Lauf-Animation (4 Bilder), Ton als Sprung-Geräusch, Asset-Liste mit Abspann, NC-Lizenz warnt beim Veröffentlichen | ✓ bestanden |
| CORE-06 G2/G3: Asset-Liste – Kopie im Projekt, Verwendung passt zur Datei, Bildstreifen-Bilder aus dem Seitenverhältnis, Abspann nach Quellen, Prüfung meldet NC-Lizenz, fehlenden Urheber und fehlende Lizenz | ✓ bestanden |
| CORE-06 G2/G3: Gebautes Spiel bekommt Figur- und Hintergrundbild aus der Asset-Liste und den Abspann als CREDITS.txt | ✓ bestanden |
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
| Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet | ✓ bestanden |
| Claude wie im Original: Skills, Plugins, Connectoren und Agenten aus dem eigenen Claude sichtbar; genutzter Skill/Connector unter der Antwort | ✓ bestanden |
| E6: „Was möchtest du machen?“ startet die Aufgabe – fehlt eine Angabe, fragt Trendo kurz und startet dann mit der Antwort | ✓ bestanden |
| Kostenlose KI-Werkzeuge: Claude bestellt ein Bild – erst als Entwurf zum Ansehen, ins Projekt erst nach „Übernehmen“; neu erzeugen, verwerfen; Knöpfe; Diktieren | ✓ bestanden |
| Spiele bauen, Einfach: Auftrag und nächster Schritt; „Alle Bereiche“ zeigt alles; Erweitert unverändert | ✓ bestanden |
| Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen | ✓ bestanden |
| Spiele: Unreal öffnen und die Herstellerseite bleiben der alte Weg | ✓ bestanden |
| T-0204 (UI): Anlegen → Editor; Tempo und Level ändern, speichern, als ZIP exportieren | ✓ bestanden |
| T-0204 (UI): Unreal – vorhandenes Projekt öffnen; ohne Unreal klarer Hinweis | ✓ bestanden |
| T-0711 Spiele-Werkstatt: Tempo und Level-Raster – Rückgängig, Notsicherung, Karte öffnet das Spiel mit dem Entwurf | ✓ bestanden |
