# Spiele bauen – Funktionsmatrix

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele-programm` · Stand: 0.9.36 (Stand df8e056)

Quelle: `docs/spec/kataloge/CORE-06_KATALOG.md`

| Nr | Funktion | Einordnung | Stand laut Katalog |
|---|---|---|---|
| SP-01 | Vorlagen mit Umfang, Meilensteinen, Prüfung auf Sackgassen | Vorhanden | umgesetzt |
| SP-02 | Level im Raster malen, prüfen, bauen, spielen | Vorhanden | umgesetzt |
| SP-03 | Level malen mit der Maus (ziehen, rechts radieren) | Vorhanden | umgesetzt |
| SP-04 | Objekte auswählen, Eigenschaften je Objekt | Vorhanden | umgesetzt |
| SP-05 | Probespielen im Fenster | Vorhanden | umgesetzt |
| SP-06 | Sprungfeder, Speicherpunkt, Schlüssel und Tür | Vorhanden | umgesetzt |
| SP-07 | Ereignis-Regeln „Wenn … dann …“ | Vorhanden | umgesetzt |
| SP-08 | Zeitlimit | Vorhanden | umgesetzt |
| SP-09 | Browser-Spiel mit gültigem JavaScript | Vorhanden | umgesetzt |
| SP-10 | Veröffentlichen: Ziele, Checkliste, Pakete | Vorhanden | umgesetzt |
| SP-11 | Unreal-Projekt mit Spielbibel | Vorhanden | umgesetzt |
| SP-12 | Mehrere Level hintereinander: Level-Reiter, hinzufügen, umbenennen, früher schieben, löschen; Ziel lädt das nächste Level | Vorhanden | umgesetzt |
| SP-13 | Animationen je Zustand (stehen, laufen, springen, fallen, getroffen): Bildstreifen, Bilder, Tempo, Blickrichtung | Vorhanden | umgesetzt |
| SP-14 | Ebenen im Level: Spiel-Ebene und Hintergrund-Ebene (Wolke, Berg, Baum, Busch, Gras), andere Ebene blass sichtbar | Vorhanden | umgesetzt |
| SP-15 | Claude baut Level im Editor: sieht alle Level, Hintergründe und den Prüfbericht (unfaire Stellen), Plan mit Level-Vorschau, erst „Ausführen“ ändert, ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ stellt zurück | Vorhanden | umgesetzt |
| SP-16 | Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release): Kriterien je Phase, nur der Reihe nach, wieder öffnen | Vorhanden | umgesetzt |
| SP-17 | Team und Rollen (Game Design, Level, Grafik & Animation, Programmierung, Ton & Musik, Test, Produktion) mit Arbeitsplatz je Rolle | Vorhanden | umgesetzt |
| SP-18 | Testmodus: Spielbarkeits-Prüfung je Level (Ziel erreichbar, knappe Sprünge, Landung neben Stacheln, Gegner am Start, Münzen), „Zeigen“ springt zur Stelle | Vorhanden | umgesetzt |
| SP-19 | Fehler und Spieler-Notizen aus dem Probespiel mit Level und Stelle, abhaken; offene Fehler sperren die Beta-Abnahme | Vorhanden | umgesetzt |
| SP-20 | Ton-Spur: Geräusch je Ereignis (Springen, Münze, Treffer, Ziel) und Musik in Schleife, ins Spiel kopiert | Vorhanden | umgesetzt |
| SP-21 | Freie Assets suchen: Godot Asset Library (offizielle Schnittstelle), Kenney, Quaternius, OpenGameArt, itch.io über Browser und Download-Ordner | Vorhanden | umgesetzt (echte Abrufe nicht im Test) |
| SP-22 | Assets direkt verwenden: Figur-Bild, Animation je Zustand (Bilder aus dem Streifen), Hintergrund, Töne und Musik – ein Rückgängig-Schritt | Vorhanden | umgesetzt |
| SP-23 | Asset-Liste mit Quelle, Lizenz, Urheber; Abspann als CREDITS.txt im gebauten Spiel; Lizenzprüfung vor dem Veröffentlichen | Vorhanden | umgesetzt |

## Werkzeuge der Oberfläche
- Spiel prüfen (Spiel)
- Bauen (Spiel)
- Spielen / Öffnen (Spiel)
- Figur (Hinzufügen)
- Region (Hinzufügen)
- Mission (Hinzufügen)
- Spielbibel (Exportieren)
