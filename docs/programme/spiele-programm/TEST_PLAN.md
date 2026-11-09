# Spiele bauen – Testplan

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele-programm` · Stand: 0.9.36 (Stand 20e850a)

33 Tests sprechen dieses Programm an.

- 3D-Modelle, Einfach: Grundfunktionen sichtbar, Export und Fachwerkzeuge nach einem Klick; Erweitert zeigt alles
- CORE-06 (UI): Spiel anlegen öffnet es im Browser; ohne Unreal ehrlicher Hinweis, mit Unreal Projekt anlegen
- CORE-06 G1: Godot Asset Library – Suche mit Titel, Autor, Lizenz; Laden nur von GitHub/GitLab, ZIP sicher entpackt; fremde Download-Adresse abgelehnt
- CORE-06 G1–G3: Assets – Godot-Suche, neue Downloads übernehmen, Bildstreifen als Lauf-Animation (4 Bilder), Ton als Sprung-Geräusch, Asset-Liste mit Abspann, NC-Lizenz warnt beim Veröffentlichen
- CORE-06 G2/G3: Asset-Liste – Kopie im Projekt, Verwendung passt zur Datei, Bildstreifen-Bilder aus dem Seitenverhältnis, Abspann nach Quellen, Prüfung meldet NC-Lizenz, fehlenden Urheber und fehlende Lizenz
- CORE-06 G2/G3: Gebautes Spiel bekommt Figur- und Hintergrundbild aus der Asset-Liste und den Abspann als CREDITS.txt
- CORE-06: Alle Browser-Spiele werden angelegt, JavaScript ist gültig (node --check)
- CORE-06: Claude baut Level – sieht alle Level voll, Plan zeigt das neue Level als Vorschau, erst „Ausführen“ legt Level 2 und den Hintergrund an, falsches Zeichen fällt weg, „Verwerfen“ stellt alles zurück
- CORE-06: Gebautes Spiel mit 2 Leveln, Hintergrund und Lauf-Animation – config.js trägt alles, Bildstreifen liegt im Spielordner, game.js ist gültiges JavaScript (Node)
- CORE-06: Gebautes Spiel: config.js trägt Zeitlimit und Regeln, neue Kacheln sind erlaubt, game.js ist gültiges JavaScript (Node)
- CORE-06: Mehrere Level – Ziel lädt das nächste Level (+10, Hinweis „Level 2 von 2“), Münzen und Eigenschaften je Level („2:x,y:value“), erst das letzte Ziel gewinnt; Nochmal beginnt bei Level 1
- CORE-06: Phasen mit Abnahme – nur der Reihe nach und nur mit erfüllten Kriterien; Zurücknehmen öffnet auch spätere Phasen; Beta braucht Test-Notizen ohne offene Fehler
- CORE-06: Profi-Ablauf – Phasen-Leiste mit Abnahme (gesperrt, bis alles erfüllt ist), Rollen mit Arbeitsplatz, Test-Bereich prüft jedes Level und zeigt die Stelle, Notiz und Fehler aus dem Probespiel, Ton-Spur; Claude bekommt den Prüfbericht
- CORE-06: Prüfung – Hintergrund nur Deko-Zeichen (w, b, t, m, g), Level 2 ff. nur gültige Kacheln, höchstens 20 Level, Animation nur für bekannte Zustände, Bilder 1–32 und 1–30 je Sekunde
- CORE-06: Regeln: „Münzen ≥ 2 → +1 Leben“ feuert genau einmal, „Punkte ≥ 3 → Gewonnen“ beendet das Spiel
- CORE-06: Schlüssel öffnet die Tür: mit Schlüssel geschafft, ohne Schlüssel bleibt die Tür zu; Regel „Sekunden ≥ 1 → Türen öffnen“ öffnet sie auch
- CORE-06: Speicherpunkt: nach einem Treffer geht es dort weiter statt am Start
- CORE-06: Spiel-Editor mit mehreren Leveln – Level hinzufügen, umbenennen, wechseln, früher schieben, löschen (Eigenschaften rücken nach); Probespiel läuft durch alle Level
- CORE-06: Spiel-Editor – Animation je Zustand: Bildstreifen wählen, Bilder und Tempo setzen (begrenzt), Probespiel zeigt Laufen-Bilder nacheinander, Entfernen nimmt sie raus; Bauen kopiert den Streifen ins Spiel
- CORE-06: Spiel-Editor – Hintergrund-Ebene nur mit Deko (Wolke, Baum …), Spiel-Ebene bleibt unverändert, andere Ebene blass sichtbar; Bauen übernimmt den Hintergrund
- CORE-06: Spielbarkeits-Prüfung meldet unfaire Stellen – Gegner am Start, Landung neben Stacheln, unerreichbare Münze, Schlüssel öffnen Türen; Sprungfeder hilft über hohe Wände
- CORE-06: Spielbarkeits-Prüfung – Lücke von 3 schafft die Figur (wie im Probespiel), Lücke von 7 nicht; Ziel hinter zu hoher Wand nicht erreichbar
- CORE-06: Sprungfeder schleudert viel höher als ein normaler Sprung
- CORE-06: Ton-Spur und Treffer-Animation – Geräusch je Ereignis und Musik landen im Spielordner, config.js nennt sie, game.js spielt sie (sonst Piepton); nach einem Treffer ist der Zustand „treffen“
- CORE-06: Unreal-Engines aus Launcher-Liste erkannt, neueste zuerst; .uproject ist gültig
- CORE-06: Zeitlimit: nach Ablauf verloren, Restzeit zählt herunter; ohne Limit −1
- CORE-06: Zustände der Figur – stehen auf dem Boden, laufen mit Tempo, springen nach oben, fallen nach unten; Blickrichtung folgt der Laufrichtung
- Einfach: In jedem Programm bleibt jeder Befehl der Stufe erreichbar, direkt oder unter „Mehr“; direkt sichtbar sind höchstens vier Programmbefehle
- Spiele bauen, Einfach: Auftrag und nächster Schritt; „Alle Bereiche“ zeigt alles; Erweitert unverändert
- Spiele bauen: Nächster Schritt nennt den schwächsten Bereich in Alltagssprache; Alltagswörter nur in Einfach
- Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen
- Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an
- Übrige Seiten, Einfach: Rest hinter „Mehr anzeigen“ und nach einem Klick wieder da; Erweitert zeigt alles sofort
