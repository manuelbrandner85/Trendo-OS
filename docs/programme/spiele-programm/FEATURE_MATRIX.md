# Spiele bauen – Funktionsmatrix

Studio: Spiele bauen (CORE-06) · Bereich: 3D, Spiele & VR · Seite: `spiele-programm` · Stand: 0.9.36 (Stand 20e850a)

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
| SP-24 | Engines ehrlich prüfen (Paket 01 S1-01): Unreal, Unity, Godot, Blender mit Version und Zustand aus Dateien, nichts angenommen; Unity ohne Editor „blockiert“ | Vorhanden | umgesetzt |
| SP-25 | Plattformer als Unity-Projekt (Paket 01 S1-01 + Unity-Freigabe): C#-Spiel, Editor-Build-Skript, EditMode- und PlayMode-Tests, Szene legt Unity selbst an | Vorhanden | umgesetzt, PC-Lauf mit Unity 6.6 (reports/paket01) |
| SP-26 | Plattformer als Godot-4-Projekt: GDScript-Spiel, Level-Prüfung und Spiel-Prüfung ohne Fenster | Vorhanden | umgesetzt, PC-Lauf mit Godot 4.7.2 (reports/paket01) |
| SP-27 | Engine-Tests aus Trendo starten (nur wenn die Engine da ist), Bericht mit Exitcode und Protokoll im Projektordner | Vorhanden | umgesetzt |
| SP-28 | Spielstände (Paket 01 S1-15): ein versioniertes Format für Browser-Spiel, Probespiel, Unity und Godot – Speicherpunkt und Level speichern, Fortsetzen, alte Stände heben, neuere/kaputte/unpassende ablehnen, Spielende löscht | Vorhanden | umgesetzt, PC-Lauf Unity 6.6 und Godot 4.7.2 (reports/paket01) |
| SP-29 | Fähigkeiten der Figur (Paket 01 S1-05): Doppelsprung (neuer Tastendruck in der Luft, einmal) und Sprint (Umschalt, 1,6-fach) – gleich im Probespiel und im Browser-Spiel, Schalter mit Rückgängig | Vorhanden | umgesetzt |
| SP-30 | Aufgaben (Paket 01 S1-05): Regel mit Titel wird Aufgabe mit Fortschritt („Sammler: Münzen 1/3“, „✓ Sammler“) im Probespiel und als Anzeige im Spiel | Vorhanden | umgesetzt |
| SP-31 | Inventar und sprechende Figuren (Paket 01 S1-05): Gegenstand (G, benennbar) kommt ins Inventar; Figur (N) sagt Text, wünscht sich einen Gegenstand und belohnt (Leben, Türen, Punkte, Tempo, Sieg); Regel „Gegenstände gesammelt“; Inventar im Spielstand – Probespiel und Browser-Spiel gleich | Vorhanden | umgesetzt |
| SP-32 | Kampf mit Waffen (Paket 01 S1-05): Schwert (Nahkampf vor der Figur) oder Werfen (Geschoss bis Wand/Gegner), Angriff X/J mit Abklingzeit; Gegner mit 1–9 Trefferpunkten, ab 2 Boss mit Lebensbalken (10 Punkte, „Boss besiegt!“); Draufspringen kostet einen Trefferpunkt – Probespiel und Browser-Spiel gleich | Vorhanden | umgesetzt |
| SP-33 | Fahrzeuge (Paket 01 S1-05): Kachel V, Berühren = einsteigen, E = aussteigen (bleibt stehen, wieder nehmbar); Wagen 1,8-fach schnell und fängt einen Treffer ab; Ufo fliegt mit gehaltener Sprungtaste (10 s Treibstoff); Absturz trifft immer – Probespiel und Browser-Spiel gleich | Vorhanden | umgesetzt |
| SP-34 | Gameplay im Unity- und Godot-Export (Paket 01 S1-05): Fähigkeiten, Regeln/Aufgaben, Inventar und Figuren, Kampf mit Bossen, Fahrzeuge in C# bzw. GDScript; Unity-Tests und Godot-Prüfskript aus Trendo startbar | Vorhanden | umgesetzt, PC-Lauf Unity 6.6 (EditMode 5/5, PlayMode 6/6) und Godot 4.7.2 (4 Prüfungen) |
| SP-35 | Spielmenüs (Paket 01 S1-14): Startmenü (Fortsetzen, Spielen, Einstellungen, Steuerung), Pause mit Esc/P, Einstellungen je Spiel gespeichert (Lautstärke, hoher Kontrast, große Schrift, ruhige Effekte, Karte), Karte mit Figur, Controller (Gamepad), kein Seiten-Scrollen durch Pfeiltasten, Spielfeld passt sich an; im Editor abschaltbar | Vorhanden | umgesetzt, im echten Browser angesehen |
| SP-36 | Spielmenüs im Unity- und Godot-Export (Paket 01 S1-14): Startmenü, Pause, Einstellungen (PlayerPrefs bzw. user://), Karte, Controller; im Menü stehen Eingaben, Gegner und Regeln still | Vorhanden | umgesetzt, PC-Lauf Unity 6.6 (PlayMode 8/8, Build ok) und Godot 4.7.2 (5 Prüfungen) |
| SP-37 | 3D-Spiel in Unreal ohne Fachwissen (Paket 01 S1-01/S1-04/S1-05, Wunsch 07.11.2026): „Bauen“ schreibt ein C++-Spiel (Figur mit Kamera, Steuerung inkl. Controller, Gegner/Boss, Münzen, Gegenstände, Figuren, Fahrzeuge, Regeln/Aufgaben, Spielstand, Menü); Welt als 3D-Level oder Open World; „Kompilieren und prüfen“: Build.bat, Unreal-Tests, Startlauf mit Bild, Bericht | Vorhanden | umgesetzt, PC-Lauf UE 5.8 (beide Welten: kompiliert, 3/3 Tests, Startlauf ok); Aussehen noch Graubox |
| SP-38 | Fotoreale Open World in Unreal (Wunsch 07.11.2026 „fotorealistisch, ohne Fachwissen“): „Kompilieren und prüfen“ lädt freie Fotomaterialien und Pflanzen/Felsen von Poly Haven (CC0, offizielle API, Größenprüfung, Lizenzdatei, Zwischenspeicher im Werkzeugordner), Unreal richtet die Welt per Python ein (Wiesen-/Waldboden mit Fernmischung, Blatt-Material mit Durchleuchtung, Nanite mit Flächenerhalt, eigene Karte), das Spiel verteilt rund 126 000 Pflanzen und Felsen, Sonne und Dunst; Prüfstart ohne echte Eingaben, Controller mit Totzone | Vorhanden | umgesetzt, PC-Lauf UE 5.8 (73 Dateien geladen, Welt eingerichtet, 3/3 Tests, Startlauf ok, Bild); Figur und Spielobjekte noch Grundformen |

## Werkzeuge der Oberfläche
- Spiel prüfen (Spiel)
- Bauen (Spiel)
- Spielen / Öffnen (Spiel)
- Figur (Hinzufügen)
- Region (Hinzufügen)
- Mission (Hinzufügen)
- Spielbibel als PDF (Exportieren)
- Spielbibel (Exportieren)
