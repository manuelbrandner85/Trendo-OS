# Funktionsliste: Spiele bauen

Studio CORE-06 · Spiele bauen · Bereich 3D, Spiele & VR

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| SP-01 | Vorlagen mit Umfang, Meilensteinen, Prüfung auf Sackgassen | umgesetzt | GameDesignTests |
| SP-02 | Level im Raster malen, prüfen, bauen, spielen | umgesetzt | GameProgramUiTests |
| SP-03 | Level malen mit der Maus (ziehen, rechts radieren) | umgesetzt | LevelEditor_Ui |
| SP-04 | Objekte auswählen, Eigenschaften je Objekt | umgesetzt | SpielEditor_Ui |
| SP-05 | Probespielen im Fenster | umgesetzt | SpielEditor_Ui |
| SP-06 | Sprungfeder, Speicherpunkt, Schlüssel und Tür | umgesetzt | GameRulesTests |
| SP-07 | Ereignis-Regeln „Wenn … dann …“ | umgesetzt | GameRulesTests |
| SP-08 | Zeitlimit | umgesetzt | GameRulesTests |
| SP-09 | Browser-Spiel mit gültigem JavaScript | umgesetzt | W4_Core06_Game |
| SP-10 | Veröffentlichen: Ziele, Checkliste, Pakete | umgesetzt | GameRelease_Ui |
| SP-11 | Unreal-Projekt mit Spielbibel | umgesetzt | GameDesignTests |
| SP-12 | Mehrere Level hintereinander: Level-Reiter, hinzufügen, umbenennen, früher schieben, löschen; Ziel lädt das nächste Level | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-13 | Animationen je Zustand (stehen, laufen, springen, fallen, getroffen): Bildstreifen, Bilder, Tempo, Blickrichtung | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-14 | Ebenen im Level: Spiel-Ebene und Hintergrund-Ebene (Wolke, Berg, Baum, Busch, Gras), andere Ebene blass sichtbar | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-15 | Claude baut Level im Editor: sieht alle Level, Hintergründe und den Prüfbericht (unfaire Stellen), Plan mit Level-Vorschau, erst „Ausführen“ ändert, ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ stellt zurück | umgesetzt | ClaudeGame_Ui |
| SP-16 | Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release): Kriterien je Phase, nur der Reihe nach, wieder öffnen | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-17 | Team und Rollen (Game Design, Level, Grafik & Animation, Programmierung, Ton & Musik, Test, Produktion) mit Arbeitsplatz je Rolle | umgesetzt | SpielEditor_Ui |
| SP-18 | Testmodus: Spielbarkeits-Prüfung je Level (Ziel erreichbar, knappe Sprünge, Landung neben Stacheln, Gegner am Start, Münzen), „Zeigen“ springt zur Stelle | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-19 | Fehler und Spieler-Notizen aus dem Probespiel mit Level und Stelle, abhaken; offene Fehler sperren die Beta-Abnahme | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-20 | Ton-Spur: Geräusch je Ereignis (Springen, Münze, Treffer, Ziel) und Musik in Schleife, ins Spiel kopiert | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-21 | Freie Assets suchen: Godot Asset Library (offizielle Schnittstelle), Kenney, Quaternius, OpenGameArt, itch.io über Browser und Download-Ordner | umgesetzt (echte Abrufe nicht im Test) | GameAssetsTests, GameAssets_Ui |
| SP-22 | Assets direkt verwenden: Figur-Bild, Animation je Zustand (Bilder aus dem Streifen), Hintergrund, Töne und Musik – ein Rückgängig-Schritt | umgesetzt | GameAssetsTests, GameAssets_Ui |
| SP-23 | Asset-Liste mit Quelle, Lizenz, Urheber; Abspann als CREDITS.txt im gebauten Spiel; Lizenzprüfung vor dem Veröffentlichen | umgesetzt | GameAssetsTests, GameAssets_Ui |
| SP-24 | Engines ehrlich prüfen (Paket 01 S1-01): Unreal, Unity, Godot, Blender mit Version und Zustand aus Dateien, nichts angenommen; Unity ohne Editor „blockiert“ | umgesetzt | EngineTargetsTests, GameEngines_Ui |
| SP-25 | Plattformer als Unity-Projekt (Paket 01 S1-01 + Unity-Freigabe): C#-Spiel, Editor-Build-Skript, EditMode- und PlayMode-Tests, Szene legt Unity selbst an | umgesetzt, PC-Lauf mit Unity 6.6 (reports/paket01) | EngineTargetsTests, GameEngines_Ui |
| SP-26 | Plattformer als Godot-4-Projekt: GDScript-Spiel, Level-Prüfung und Spiel-Prüfung ohne Fenster | umgesetzt, PC-Lauf mit Godot 4.7.2 (reports/paket01) | EngineTargetsTests, GameEngines_Ui |
| SP-27 | Engine-Tests aus Trendo starten (nur wenn die Engine da ist), Bericht mit Exitcode und Protokoll im Projektordner | umgesetzt | GameEngines_Ui |
| SP-28 | Spielstände (Paket 01 S1-15): ein versioniertes Format für Browser-Spiel, Probespiel, Unity und Godot – Speicherpunkt und Level speichern, Fortsetzen, alte Stände heben, neuere/kaputte/unpassende ablehnen, Spielende löscht | umgesetzt, PC-Lauf Unity 6.6 und Godot 4.7.2 (reports/paket01) | GameSavesTests, GameSaves_Ui |
| SP-29 | Fähigkeiten der Figur (Paket 01 S1-05): Doppelsprung (neuer Tastendruck in der Luft, einmal) und Sprint (Umschalt, 1,6-fach) – gleich im Probespiel und im Browser-Spiel, Schalter mit Rückgängig | umgesetzt | GameplayTests, Gameplay_Ui |
| SP-30 | Aufgaben (Paket 01 S1-05): Regel mit Titel wird Aufgabe mit Fortschritt („Sammler: Münzen 1/3“, „✓ Sammler“) im Probespiel und als Anzeige im Spiel | umgesetzt | GameplayTests, Gameplay_Ui |
| SP-31 | Inventar und sprechende Figuren (Paket 01 S1-05): Gegenstand (G, benennbar) kommt ins Inventar; Figur (N) sagt Text, wünscht sich einen Gegenstand und belohnt (Leben, Türen, Punkte, Tempo, Sieg); Regel „Gegenstände gesammelt“; Inventar im Spielstand – Probespiel und Browser-Spiel gleich | umgesetzt | GameInventoryTests, GameInventory_Ui |
| SP-32 | Kampf mit Waffen (Paket 01 S1-05): Schwert (Nahkampf vor der Figur) oder Werfen (Geschoss bis Wand/Gegner), Angriff X/J mit Abklingzeit; Gegner mit 1–9 Trefferpunkten, ab 2 Boss mit Lebensbalken (10 Punkte, „Boss besiegt!“); Draufspringen kostet einen Trefferpunkt – Probespiel und Browser-Spiel gleich | umgesetzt | GameCombatTests, GameCombat_Ui |
| SP-33 | Fahrzeuge (Paket 01 S1-05): Kachel V, Berühren = einsteigen, E = aussteigen (bleibt stehen, wieder nehmbar); Wagen 1,8-fach schnell und fängt einen Treffer ab; Ufo fliegt mit gehaltener Sprungtaste (10 s Treibstoff); Absturz trifft immer – Probespiel und Browser-Spiel gleich | umgesetzt | GameVehicleTests, GameVehicle_Ui |
| SP-34 | Gameplay im Unity- und Godot-Export (Paket 01 S1-05): Fähigkeiten, Regeln/Aufgaben, Inventar und Figuren, Kampf mit Bossen, Fahrzeuge in C# bzw. GDScript; Unity-Tests und Godot-Prüfskript aus Trendo startbar | umgesetzt, PC-Lauf Unity 6.6 (EditMode 5/5, PlayMode 6/6) und Godot 4.7.2 (4 Prüfungen) | EngineTargetsTests, GameEngines_Ui |
| SP-35 | Spielmenüs (Paket 01 S1-14): Startmenü (Fortsetzen, Spielen, Einstellungen, Steuerung), Pause mit Esc/P, Einstellungen je Spiel gespeichert (Lautstärke, hoher Kontrast, große Schrift, ruhige Effekte, Karte), Karte mit Figur, Controller (Gamepad), kein Seiten-Scrollen durch Pfeiltasten, Spielfeld passt sich an; im Editor abschaltbar | umgesetzt, im echten Browser angesehen | GameMenusTests, GameMenus_Ui |
| SP-36 | Spielmenüs im Unity- und Godot-Export (Paket 01 S1-14): Startmenü, Pause, Einstellungen (PlayerPrefs bzw. user://), Karte, Controller; im Menü stehen Eingaben, Gegner und Regeln still | umgesetzt, PC-Lauf Unity 6.6 (PlayMode 8/8, Build ok) und Godot 4.7.2 (5 Prüfungen) | EngineTargetsTests, GameEngines_Ui |
| SP-37 | 3D-Spiel in Unreal ohne Fachwissen (Paket 01 S1-01/S1-04/S1-05, Wunsch 07.11.2026): „Bauen“ schreibt ein C++-Spiel (Figur mit Kamera, Steuerung inkl. Controller, Gegner/Boss, Münzen, Gegenstände, Figuren, Fahrzeuge, Regeln/Aufgaben, Spielstand, Menü); Welt als 3D-Level oder Open World; „Kompilieren und prüfen“: Build.bat, Unreal-Tests, Startlauf mit Bild, Bericht | umgesetzt, PC-Lauf UE 5.8 (beide Welten: kompiliert, 3/3 Tests, Startlauf ok); Aussehen noch Graubox | UnrealGameTests, GameUnreal_Ui |
| SP-38 | Fotoreale Open World in Unreal (Wunsch 07.11.2026 „fotorealistisch, ohne Fachwissen“): „Kompilieren und prüfen“ lädt freie Fotomaterialien und Pflanzen/Felsen von Poly Haven (CC0, offizielle API, Größenprüfung, Lizenzdatei, Zwischenspeicher im Werkzeugordner), Unreal richtet die Welt per Python ein (Wiesen-/Waldboden mit Fernmischung, Blatt-Material mit Durchleuchtung, Nanite mit Flächenerhalt, eigene Karte), das Spiel verteilt rund 126 000 Pflanzen und Felsen, Sonne und Dunst; Prüfstart ohne echte Eingaben, Controller mit Totzone | umgesetzt, PC-Lauf UE 5.8 (73 Dateien geladen, Welt eingerichtet, 3/3 Tests, Startlauf ok, Bild); Figur und Spielobjekte noch Grundformen | UnrealNatureTests, GameUnreal_Ui |
| SP-01 | Vorlagen mit Umfang, Meilensteinen, Prüfung auf Sackgassen | umgesetzt | GameDesignTests |
| SP-02 | Level im Raster malen, prüfen, bauen, spielen | umgesetzt | GameProgramUiTests |
| SP-03 | Level malen mit der Maus (ziehen, rechts radieren) | umgesetzt | LevelEditor_Ui |
| SP-04 | Objekte auswählen, Eigenschaften je Objekt | umgesetzt | SpielEditor_Ui |
| SP-05 | Probespielen im Fenster | umgesetzt | SpielEditor_Ui |
| SP-06 | Sprungfeder, Speicherpunkt, Schlüssel und Tür | umgesetzt | GameRulesTests |
| SP-07 | Ereignis-Regeln „Wenn … dann …“ | umgesetzt | GameRulesTests |
| SP-08 | Zeitlimit | umgesetzt | GameRulesTests |
| SP-09 | Browser-Spiel mit gültigem JavaScript | umgesetzt | W4_Core06_Game |
| SP-10 | Veröffentlichen: Ziele, Checkliste, Pakete | umgesetzt | GameRelease_Ui |
| SP-11 | Unreal-Projekt mit Spielbibel | umgesetzt | GameDesignTests |
| SP-12 | Mehrere Level hintereinander: Level-Reiter, hinzufügen, umbenennen, früher schieben, löschen; Ziel lädt das nächste Level | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-13 | Animationen je Zustand (stehen, laufen, springen, fallen, getroffen): Bildstreifen, Bilder, Tempo, Blickrichtung | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-14 | Ebenen im Level: Spiel-Ebene und Hintergrund-Ebene (Wolke, Berg, Baum, Busch, Gras), andere Ebene blass sichtbar | umgesetzt | GameRulesTests, SpielEditor_Ui |
| SP-15 | Claude baut Level im Editor: sieht alle Level, Hintergründe und den Prüfbericht (unfaire Stellen), Plan mit Level-Vorschau, erst „Ausführen“ ändert, ungültige Vorschläge fallen mit Grund weg, „Verwerfen“ stellt zurück | umgesetzt | ClaudeGame_Ui |
| SP-16 | Phasen mit Abnahme (Konzept → Prototyp → Vertical Slice → Alpha → Beta → Release): Kriterien je Phase, nur der Reihe nach, wieder öffnen | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-17 | Team und Rollen (Game Design, Level, Grafik & Animation, Programmierung, Ton & Musik, Test, Produktion) mit Arbeitsplatz je Rolle | umgesetzt | SpielEditor_Ui |
| SP-18 | Testmodus: Spielbarkeits-Prüfung je Level (Ziel erreichbar, knappe Sprünge, Landung neben Stacheln, Gegner am Start, Münzen), „Zeigen“ springt zur Stelle | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-19 | Fehler und Spieler-Notizen aus dem Probespiel mit Level und Stelle, abhaken; offene Fehler sperren die Beta-Abnahme | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-20 | Ton-Spur: Geräusch je Ereignis (Springen, Münze, Treffer, Ziel) und Musik in Schleife, ins Spiel kopiert | umgesetzt | GameProTests, SpielEditor_Ui |
| SP-21 | Freie Assets suchen: Godot Asset Library (offizielle Schnittstelle), Kenney, Quaternius, OpenGameArt, itch.io über Browser und Download-Ordner | umgesetzt (echte Abrufe nicht im Test) | GameAssetsTests, GameAssets_Ui |
| SP-22 | Assets direkt verwenden: Figur-Bild, Animation je Zustand (Bilder aus dem Streifen), Hintergrund, Töne und Musik – ein Rückgängig-Schritt | umgesetzt | GameAssetsTests, GameAssets_Ui |
| SP-23 | Asset-Liste mit Quelle, Lizenz, Urheber; Abspann als CREDITS.txt im gebauten Spiel; Lizenzprüfung vor dem Veröffentlichen | umgesetzt | GameAssetsTests, GameAssets_Ui |
| SP-24 | Engines ehrlich prüfen (Paket 01 S1-01): Unreal, Unity, Godot, Blender mit Version und Zustand aus Dateien, nichts angenommen; Unity ohne Editor „blockiert“ | umgesetzt | EngineTargetsTests, GameEngines_Ui |
| SP-25 | Plattformer als Unity-Projekt (Paket 01 S1-01 + Unity-Freigabe): C#-Spiel, Editor-Build-Skript, EditMode- und PlayMode-Tests, Szene legt Unity selbst an | umgesetzt, PC-Lauf mit Unity 6.6 (reports/paket01) | EngineTargetsTests, GameEngines_Ui |
| SP-26 | Plattformer als Godot-4-Projekt: GDScript-Spiel, Level-Prüfung und Spiel-Prüfung ohne Fenster | umgesetzt, PC-Lauf mit Godot 4.7.2 (reports/paket01) | EngineTargetsTests, GameEngines_Ui |
| SP-27 | Engine-Tests aus Trendo starten (nur wenn die Engine da ist), Bericht mit Exitcode und Protokoll im Projektordner | umgesetzt | GameEngines_Ui |
| SP-28 | Spielstände (Paket 01 S1-15): ein versioniertes Format für Browser-Spiel, Probespiel, Unity und Godot – Speicherpunkt und Level speichern, Fortsetzen, alte Stände heben, neuere/kaputte/unpassende ablehnen, Spielende löscht | umgesetzt, PC-Lauf Unity 6.6 und Godot 4.7.2 (reports/paket01) | GameSavesTests, GameSaves_Ui |
| SP-29 | Fähigkeiten der Figur (Paket 01 S1-05): Doppelsprung (neuer Tastendruck in der Luft, einmal) und Sprint (Umschalt, 1,6-fach) – gleich im Probespiel und im Browser-Spiel, Schalter mit Rückgängig | umgesetzt | GameplayTests, Gameplay_Ui |
| SP-30 | Aufgaben (Paket 01 S1-05): Regel mit Titel wird Aufgabe mit Fortschritt („Sammler: Münzen 1/3“, „✓ Sammler“) im Probespiel und als Anzeige im Spiel | umgesetzt | GameplayTests, Gameplay_Ui |
| SP-31 | Inventar und sprechende Figuren (Paket 01 S1-05): Gegenstand (G, benennbar) kommt ins Inventar; Figur (N) sagt Text, wünscht sich einen Gegenstand und belohnt (Leben, Türen, Punkte, Tempo, Sieg); Regel „Gegenstände gesammelt“; Inventar im Spielstand – Probespiel und Browser-Spiel gleich | umgesetzt | GameInventoryTests, GameInventory_Ui |
| SP-32 | Kampf mit Waffen (Paket 01 S1-05): Schwert (Nahkampf vor der Figur) oder Werfen (Geschoss bis Wand/Gegner), Angriff X/J mit Abklingzeit; Gegner mit 1–9 Trefferpunkten, ab 2 Boss mit Lebensbalken (10 Punkte, „Boss besiegt!“); Draufspringen kostet einen Trefferpunkt – Probespiel und Browser-Spiel gleich | umgesetzt | GameCombatTests, GameCombat_Ui |
| SP-33 | Fahrzeuge (Paket 01 S1-05): Kachel V, Berühren = einsteigen, E = aussteigen (bleibt stehen, wieder nehmbar); Wagen 1,8-fach schnell und fängt einen Treffer ab; Ufo fliegt mit gehaltener Sprungtaste (10 s Treibstoff); Absturz trifft immer – Probespiel und Browser-Spiel gleich | umgesetzt | GameVehicleTests, GameVehicle_Ui |
| SP-34 | Gameplay im Unity- und Godot-Export (Paket 01 S1-05): Fähigkeiten, Regeln/Aufgaben, Inventar und Figuren, Kampf mit Bossen, Fahrzeuge in C# bzw. GDScript; Unity-Tests und Godot-Prüfskript aus Trendo startbar | umgesetzt, PC-Lauf Unity 6.6 (EditMode 5/5, PlayMode 6/6) und Godot 4.7.2 (4 Prüfungen) | EngineTargetsTests, GameEngines_Ui |
| SP-35 | Spielmenüs (Paket 01 S1-14): Startmenü (Fortsetzen, Spielen, Einstellungen, Steuerung), Pause mit Esc/P, Einstellungen je Spiel gespeichert (Lautstärke, hoher Kontrast, große Schrift, ruhige Effekte, Karte), Karte mit Figur, Controller (Gamepad), kein Seiten-Scrollen durch Pfeiltasten, Spielfeld passt sich an; im Editor abschaltbar | umgesetzt, im echten Browser angesehen | GameMenusTests, GameMenus_Ui |
| SP-36 | Spielmenüs im Unity- und Godot-Export (Paket 01 S1-14): Startmenü, Pause, Einstellungen (PlayerPrefs bzw. user://), Karte, Controller; im Menü stehen Eingaben, Gegner und Regeln still | umgesetzt, PC-Lauf Unity 6.6 (PlayMode 8/8, Build ok) und Godot 4.7.2 (5 Prüfungen) | EngineTargetsTests, GameEngines_Ui |
| SP-37 | 3D-Spiel in Unreal ohne Fachwissen (Paket 01 S1-01/S1-04/S1-05, Wunsch 07.11.2026): „Bauen“ schreibt ein C++-Spiel (Figur mit Kamera, Steuerung inkl. Controller, Gegner/Boss, Münzen, Gegenstände, Figuren, Fahrzeuge, Regeln/Aufgaben, Spielstand, Menü); Welt als 3D-Level oder Open World; „Kompilieren und prüfen“: Build.bat, Unreal-Tests, Startlauf mit Bild, Bericht | umgesetzt, PC-Lauf UE 5.8 (beide Welten: kompiliert, 3/3 Tests, Startlauf ok); Aussehen noch Graubox | UnrealGameTests, GameUnreal_Ui |
| SP-38 | Fotoreale Open World in Unreal (Wunsch 07.11.2026 „fotorealistisch, ohne Fachwissen“): „Kompilieren und prüfen“ lädt freie Fotomaterialien und Pflanzen/Felsen von Poly Haven (CC0, offizielle API, Größenprüfung, Lizenzdatei, Zwischenspeicher im Werkzeugordner), Unreal richtet die Welt per Python ein (Wiesen-/Waldboden mit Fernmischung, Blatt-Material mit Durchleuchtung, Nanite mit Flächenerhalt, eigene Karte), das Spiel verteilt rund 126 000 Pflanzen und Felsen, Sonne und Dunst; Prüfstart ohne echte Eingaben, Controller mit Totzone | umgesetzt, PC-Lauf UE 5.8 (73 Dateien geladen, Welt eingerichtet, 3/3 Tests, Startlauf ok, Bild); Figur und Spielobjekte noch Grundformen | UnrealNatureTests, GameUnreal_Ui |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Engine-Projekt zurück nach Trendo übernehmen
- ✗ Fotorealer Look im Unreal-Spiel (Gelände-Material, Vegetation, Figur aus Blender), gepacktes Spiel (BuildCookRun)

## Teilweise vorhanden

- ◐ Szenenbaum: Objekte im Raster statt freier Szene
