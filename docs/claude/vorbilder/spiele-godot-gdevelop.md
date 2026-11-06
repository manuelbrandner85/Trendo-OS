# Vorbild-Analyse Spiele: Godot, GDevelop, Construct, Unreal Engine (Clean-Room)

- Programme: „Spiele bauen“, „Werkstatt“
- Vorbilder: Godot Engine, GDevelop, Unreal Engine (frei bzw. kostenlos), Construct 3, GameMaker, RPG Maker (kostenpflichtig) – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Level-Baukasten mit Kacheln: Boden, Münze, Gegner, Stacheln, Start, Ziel
- ✓ Neue Kacheln wie in GDevelop/Construct: Sprungfeder, Speicherpunkt, Schlüssel und Tür (öffnet, wenn alle Schlüssel gesammelt sind)
- ✓ Objekte auswählen, Eigenschaften je Objekt (Gegner-Tempo, Münzwert), entfernen
- ✓ Ereignis-Regeln „Wenn … dann …“ wie die Ereignisblätter in GDevelop/Construct: Wenn Münzen, Punkte, besiegte Gegner oder Sekunden ≥ Zahl, dann +1 Leben, Türen öffnen, schneller, +10 Punkte oder gewonnen
- ✓ Zeitlimit mit Restzeit-Anzeige
- ✓ Probespielen im Trendo-Fenster mit denselben Regeln wie das gebaute Spiel
- ✓ Bauen als Browser-Spiel (geprüftes JavaScript), Veröffentlichen-Pakete (Web, Windows, Mac, Handy, Steam), itch.io erst nach Bestätigung
- ✓ Spielbibel: Figuren, Regionen, Missionen mit Sackgassen-Prüfung
- ✓ Unreal-Projekt anlegen (AAA+ mit Lumen und virtuellen Schatten)
- ✓ Geräusche, Musik, 3D aus Bild über freie KIs
- ◐ Szenenbaum: Objekte im Raster statt freier Szene
- ✓ Mehrere Level hintereinander wie die Szenen in GDevelop: Level-Reiter über der Fläche, hinzufügen, umbenennen, früher schieben, löschen (Eigenschaften rücken mit); das Ziel lädt das nächste Level, erst das letzte gewinnt – im Probespiel und im gebauten Spiel
- ✓ Ebenen wie die TileMap-Ebenen in Godot: Spiel-Ebene (fest, mit Wirkung) und Hintergrund-Ebene nur mit Deko (Wolke, Berg, Baum, Busch, Gras); die andere Ebene ist beim Malen blass zu sehen
- ✓ Animationen je Zustand wie AnimatedSprite in Godot: stehen, laufen, springen, fallen je ein Bildstreifen mit Anzahl Bilder und Tempo, Figur dreht sich in Laufrichtung; das Bauen kopiert die Streifen ins Spiel
- ✓ Claude baut Level im Editor (mit dem eigenen Claude-Abo): Claude sieht alle Level und Hintergründe als Zeilen, der Plan zeigt das vorgeschlagene Level als kleine Vorschau, erst „Ausführen“ ändert es; Behalten oder Verwerfen wie in allen Programmen
- ✓ Profi-Ablauf wie im Spielestudio: Phasen-Leiste Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release mit Abnahme-Kriterien je Phase (nur der Reihe nach), Team und Rollen mit Arbeitsplatz je Rolle
- ✓ Testmodus: Trendo spielt jedes Level mit den Sprungweiten des Spiels als Prüfer durch (fairster Weg, Kopf an der Decke, Sprungfedern, Schlüssel und Türen) und meldet unerreichbare Ziele, knappe Sprünge, Landungen neben Stacheln und Gegner am Start; Fehler und Spieler-Notizen aus dem Probespiel mit Stelle; Claude bekommt den Bericht
- ✓ Ton-Spur: eigene Geräusche je Ereignis und Musik, sonst Piepton; Figur-Zustand „getroffen“ mit eigener Animation
- ✓ Export für Browser und PC (Windows-ZIP mit „Spielen.cmd“ unter „Veröffentlichen“)
- ✓ Freie Assets: Godot Asset Library direkt, Kenney/Quaternius/OpenGameArt/itch.io über Browser und Download-Ordner; mit einem Klick als Figur, Animation (Bilder aus dem Streifen erkannt), Hintergrund oder Ton im Spiel; Asset-Liste mit Lizenzen, Abspann CREDITS.txt, Lizenzprüfung vor dem Veröffentlichen

## Bedienkonzept (Profi-Bauplan A1–A3)
Level in der Mitte, Kachel-Werkzeuge oben, rechts Spielregeln, Regeln und Objekt-Eigenschaften – Bauplan erfüllt.

## Nächste Schritte
1. Freie Szene statt Raster (Objekte pixelgenau setzen)
2. Animationen auch für Gegner
