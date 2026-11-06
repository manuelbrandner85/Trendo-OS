# Werkstatt – Umsetzungsstand

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele` · Stand: 0.9.36 (Stand 5b38700)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| SP-01 | Vorlagen mit Umfang, Meilensteinen, Prüfung auf Sackgassen | Implementiert | `GameDesignTests` |
| SP-02 | Level im Raster malen, prüfen, bauen, spielen | Implementiert | `GameProgramUiTests` |
| SP-03 | Level malen mit der Maus (ziehen, rechts radieren) | Implementiert | `LevelEditor_Ui` |
| SP-04 | Objekte auswählen, Eigenschaften je Objekt | Implementiert | `SpielEditor_Ui` |
| SP-05 | Probespielen im Fenster | Implementiert | `SpielEditor_Ui` |
| SP-06 | Sprungfeder, Speicherpunkt, Schlüssel und Tür | Implementiert | `GameRulesTests` |
| SP-07 | Ereignis-Regeln „Wenn … dann …“ | Implementiert | `GameRulesTests` |
| SP-08 | Zeitlimit | Implementiert | `GameRulesTests` |
| SP-09 | Browser-Spiel mit gültigem JavaScript | Implementiert | `W4_Core06_Game` |
| SP-10 | Veröffentlichen: Ziele, Checkliste, Pakete | Implementiert | `GameRelease_Ui` |
| SP-11 | Unreal-Projekt mit Spielbibel | Implementiert | `GameDesignTests` |
| SP-12 | Mehrere Level hintereinander: Level-Reiter, hinzufügen, umbenennen, früher schieben, löschen; Ziel lädt das nächste Level | Implementiert | `GameRulesTests, SpielEditor_Ui` |
| SP-13 | Animationen je Zustand (stehen, laufen, springen, fallen, getroffen): Bildstreifen, Bilder, Tempo, Blickrichtung | Implementiert | `GameRulesTests, SpielEditor_Ui` |
| SP-14 | Ebenen im Level: Spiel-Ebene und Hintergrund-Ebene (Wolke, Berg, Baum, Busch, Gras), andere Ebene blass sichtbar | Implementiert | `GameRulesTests, SpielEditor_Ui` |
| SP-15 | Claude baut Level im Editor: sieht alle Level, Hintergründe und den Prüfbericht (unfaire Stellen), Plan mit Level-Vorschau, erst „Ausführen“ ändert, ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ stellt zurück | Implementiert | `ClaudeGame_Ui` |
| SP-16 | Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release): Kriterien je Phase, nur der Reihe nach, wieder öffnen | Implementiert | `GameProTests, SpielEditor_Ui` |
| SP-17 | Team und Rollen (Game Design, Level, Grafik & Animation, Programmierung, Ton & Musik, Test, Produktion) mit Arbeitsplatz je Rolle | Implementiert | `SpielEditor_Ui` |
| SP-18 | Testmodus: Spielbarkeits-Prüfung je Level (Ziel erreichbar, knappe Sprünge, Landung neben Stacheln, Gegner am Start, Münzen), „Zeigen“ springt zur Stelle | Implementiert | `GameProTests, SpielEditor_Ui` |
| SP-19 | Fehler und Spieler-Notizen aus dem Probespiel mit Level und Stelle, abhaken; offene Fehler sperren die Beta-Abnahme | Implementiert | `GameProTests, SpielEditor_Ui` |
| SP-20 | Ton-Spur: Geräusch je Ereignis (Springen, Münze, Treffer, Ziel) und Musik in Schleife, ins Spiel kopiert | Implementiert | `GameProTests, SpielEditor_Ui` |

0 von 20 Funktionen getestet.
