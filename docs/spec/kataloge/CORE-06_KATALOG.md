# CORE-06 – Spiele bauen: Funktionskatalog

Funktionen nach den Vorbildern (Godot, GDevelop, Construct, GameMaker, Unreal Engine – nur Funktionen, Clean-Room), Stand 05.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| SP-01 | Vorlagen mit Umfang, Meilensteinen, Prüfung auf Sackgassen | GameMaker | umgesetzt | GameDesignTests |
| SP-02 | Level im Raster malen, prüfen, bauen, spielen | GDevelop | umgesetzt | GameProgramUiTests |
| SP-03 | Level malen mit der Maus (ziehen, rechts radieren) | Construct | umgesetzt | LevelEditor_Ui |
| SP-04 | Objekte auswählen, Eigenschaften je Objekt | Godot Inspektor | umgesetzt | SpielEditor_Ui |
| SP-05 | Probespielen im Fenster | Godot „Szene starten“ | umgesetzt | SpielEditor_Ui |
| SP-06 | Sprungfeder, Speicherpunkt, Schlüssel und Tür | GDevelop Verhalten | umgesetzt | GameRulesTests |
| SP-07 | Ereignis-Regeln „Wenn … dann …“ | GDevelop/Construct Ereignisblatt | umgesetzt | GameRulesTests |
| SP-08 | Zeitlimit | Construct | umgesetzt | GameRulesTests |
| SP-09 | Browser-Spiel mit gültigem JavaScript | GDevelop HTML5-Export | umgesetzt | W4_Core06_Game |
| SP-10 | Veröffentlichen: Ziele, Checkliste, Pakete | GameMaker, itch.io | umgesetzt | GameRelease_Ui |
| SP-11 | Unreal-Projekt mit Spielbibel | Unreal Engine | umgesetzt | GameDesignTests |
| SP-12 | Mehrere Level hintereinander: Level-Reiter, hinzufügen, umbenennen, früher schieben, löschen; Ziel lädt das nächste Level | GDevelop Szenen, Godot Szenenwechsel | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-13 | Animationen je Zustand (stehen, laufen, springen, fallen, getroffen): Bildstreifen, Bilder, Tempo, Blickrichtung | Godot AnimatedSprite, GDevelop Objekt-Animationen | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-14 | Ebenen im Level: Spiel-Ebene und Hintergrund-Ebene (Wolke, Berg, Baum, Busch, Gras), andere Ebene blass sichtbar | Godot TileMap-Ebenen, GDevelop Ebenen | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-15 | Claude baut Level im Editor: sieht alle Level, Hintergründe und den Prüfbericht (unfaire Stellen), Plan mit Level-Vorschau, erst „Ausführen“ ändert, ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ stellt zurück | Godot-KI-Helfer, GDevelop-KI (nur Funktionsidee) | umgesetzt | ClaudeGame_Ui |
| SP-16 | Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release): Kriterien je Phase, nur der Reihe nach, wieder öffnen | Produktionsablauf in Spielestudios | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-17 | Team und Rollen (Game Design, Level, Grafik & Animation, Programmierung, Ton & Musik, Test, Produktion) mit Arbeitsplatz je Rolle | Studio-Rollen | umgesetzt | SpielEditor_Ui |
| SP-18 | Testmodus: Spielbarkeits-Prüfung je Level (Ziel erreichbar, knappe Sprünge, Landung neben Stacheln, Gegner am Start, Münzen), „Zeigen“ springt zur Stelle | Playtest-Protokolle | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-19 | Fehler und Spieler-Notizen aus dem Probespiel mit Level und Stelle, abhaken; offene Fehler sperren die Beta-Abnahme | Bug-Tracker im Studio | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-20 | Ton-Spur: Geräusch je Ereignis (Springen, Münze, Treffer, Ziel) und Musik in Schleife, ins Spiel kopiert | GDevelop/Godot Audio | umgesetzt | GameProTests, SpielEditor_Ui |
