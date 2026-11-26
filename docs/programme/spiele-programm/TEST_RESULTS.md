# Spiele bauen – Testergebnisse

Studio: Spiele bauen (CORE-06) · Bereich: 3D & Spiele · Seite: `spiele-programm` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 106 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Automatik: Claude erhält im Studio die Anweisung, sein Team ohne Rückfrage einzusetzen | ✓ bestanden |
| Automatik: Fachleute stehen ab Start auf „Automatisch“, die KI bekommt die Engine-Anweisung, Übernehmen hält die Wahl fest und zeigt sie | ✓ bestanden |
| Automatik: Ohne Angabe der KI speichert Trendo keine Engine und entscheidet nach dem, was installiert ist | ✓ bestanden |
| Automatik: Ordner besteht schon, das Spiel wird trotzdem gebaut und der Grund genannt | ✓ bestanden |
| Automatik: gewählte Godot-Engine beim Plattformer ergibt beim Bauen ohne weiteren Klick das Godot-Projekt | ✓ bestanden |
| Board: Stories aus der Spielbibel, Kriterien abhaken, erst dann fertig; Blockiert braucht einen Grund | ✓ bestanden |
| Browser ECHT (ohne Ersatz): Spiel von der Seite in Chrome – laufen, Münze, Wand, mit der Leertaste darüber springen, Ziel = gewonnen; zweites Spiel: Stacheln mit einem Leben = verloren, Leertaste startet neu; eigenes Bild wird geladen | ✓ bestanden |
| Browser ECHT (ohne Ersatz): Spiel von oben über die Oberfläche bauen und in Chrome mit echten Tasten durchspielen – Menü starten, laufen, Münze sammeln, Wand hält auf, Regel zählt, Ziel = gewonnen; „Im echten Browser prüfen“ meldet ✓ | ✓ bestanden |
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
| Chat: Antwort der KI lässt getippten Text und Fokus stehen und holt niemanden aus einem anderen Programm | ✓ bestanden |
| Chat: Tab-Wechsel während die KI arbeitet – Antwort landet nur beim gefragten Spiel | ✓ bestanden |
| Chat: Vorschlag bleibt beim Tab-Wechsel an seinem Spiel und erscheint im anderen Spiel nicht | ✓ bestanden |
| Chat: Vorschlag ändert erst nach Übernehmen und nur den Abschnitt; Verwerfen lässt alles; Rückgängig ein Schritt; Verlauf ohne Rückgängig | ✓ bestanden |
| Chat: ohne KI erscheint ein Hinweis im Verlauf, das Dokument bleibt unverändert | ✓ bestanden |
| D5: Ähnliche Spiele zeigt nur belegte Titel mit Quelle; ohne Beleg „keine Referenz gefunden“ | ✓ bestanden |
| Dokument: Abschnitte verschieben und ergänzen, Bild einfügen und in die Ablage legen, Text ändern | ✓ bestanden |
| Dokument: Netzwerkpfad aus fremder Datei wird nicht geöffnet, die Spielbibel zeigt einen Hinweis | ✓ bestanden |
| Dokument: kaputte doc-Felder aus KI oder fremder Datei stürzen nicht ab | ✓ bestanden |
| E1: Ohne Engine genau ein Schritt zur offiziellen Seite; mit Engine und bestandenem Bericht „Alles geprüft“ | ✓ bestanden |
| E5 + E6: Testlauf mit Startwert, schwarzes Bild wird gemeldet, zweiter Lauf meldet die Verschlechterung | ✓ bestanden |
| E6: „Was möchtest du machen?“ startet die Aufgabe – fehlt eine Angabe, fragt Trendo kurz und startet dann mit der Antwort | ✓ bestanden |
| E7: Dienste-Auswahl je Art; Erzeugtes landet mit Herkunft und Lizenz in der Asset-Liste | ✓ bestanden |
| Export: PDF ist der erste Export (Stufe Einfach zeigt nur ihn) und schreibt eine Datei | ✓ bestanden |
| Fahrzeug Editor: Werkzeug da; Art Ufo wählen und rückgängig; Probespiel: einsteigen, Status zeigt Ufo mit Treibstoff, E steigt aus | ✓ bestanden |
| Gameplay: Doppelsprung einschalten und rückgängig; Aufgabe „Sammler“ anlegen; Probespiel zeigt Fortschritt; gebautes Spiel enthält Fähigkeit und Aufgabe | ✓ bestanden |
| Godot ECHT über die Oberfläche (ohne Ersatz): Spiel von oben bauen → „Als Godot-Projekt“ → „Godot prüfen“ startet die echte Engine; Bericht nennt jede Prüfung mit ✓ und die Durchspiel-Prüfung | ✓ bestanden |
| Ideen: Einstellungen gehen an die KI, Ablehnung beim nächsten Lauf; ohne Bild-KI nur ein Hinweis | ✓ bestanden |
| Ideen: Konzeptbild erscheint auf der Karte und landet im Abschnitt Idee | ✓ bestanden |
| Ideen: geführter Weg vom leeren Studio bis zum Spiel ohne Texteingabe; ohne KI aus dem Baukasten | ✓ bestanden |
| Ideen: „Neue Ideen“ zeigt ohne KI andere Themen; Abschnitt entfernen löscht seinen Verlauf | ✓ bestanden |
| Inventar Editor: Werkzeuge Gegenstand/Figur; Gegenstand „Apfel“ benennen; Figur sagt Text, wünscht Apfel, gibt +1 Leben; Rückgängig; Probespiel zeigt Inventar | ✓ bestanden |
| Kachelspiel frei: Vorlage „Abenteuer von oben“ → Blick umschalten (Rückgängig), eigenes Level malen, Gegner verfolgt, eigenes Bild, Regel „alle Münzen“; Spielen im Fenster läuft in vier Richtungen bis zum Sieg; Bauen schreibt alles ins Spiel | ✓ bestanden |
| Kampf Editor: Schwert wählen und rückgängig; Gegner 3 Trefferpunkte; Probespiel: Schwertschlag trifft, Boss verliert einen Punkt; gebautes Spiel hat die Waffe | ✓ bestanden |
| Kette: Das Bildprogramm exportiert eine Figur, Spiele bauen übernimmt sie mit Projektbezug und baut sie ins Spiel | ✓ bestanden |
| Kette: Der Ablauf „Spielfigur → fertiges Spiel“ läuft nur nach Freigabe und liefert Bild, Dokument und spielbares Spiel | ✓ bestanden |
| Kette: Falsche Verwendung, falsches Studio und doppelte Annahme werden abgelehnt, nichts wird verbraucht | ✓ bestanden |
| Kette: Ohne Namen hält der Ablauf mit klarer Meldung an und läuft nicht weiter | ✓ bestanden |
| Level-Editor: Münzen ziehend malen, Start versetzen (nur einer), rechts radieren, Rückgängig je Strich, Prüfung meldet fehlendes Ziel, Testen baut das Spiel | ✓ bestanden |
| M1–M5: Mixamo öffnen, neue Downloads übernehmen (ohne Doppelte), Aktionen zuordnen, Blender aufbereiten, nach Godot | ✓ bestanden |
| Plattformer-Prüfung: kein Start, zwei Starts, kein Ziel, kein Boden unter dem Start; erlaubte Kacheln | ✓ bestanden |
| Produktion: Prüfstufe wählen, Urteil sehen, Bedenken nur mit Risiko abnehmen; Team-Aufbau sichtbar | ✓ bestanden |
| Referenz (Messung): Studio öffnet unter 1,5 Sekunden | ✓ bestanden |
| Referenz (Messung): Vom Start zur ersten sinnvollen Aktion genügt ein Klick | ✓ bestanden |
| Referenz: Alle Befehle sind in jeder Stufe erreichbar, in Einfach über „Mehr“ und über Claude | ✓ bestanden |
| Referenz: Auftrag in Alltagssprache führt zur passenden Aufgabe | ✓ bestanden |
| Referenz: Bauen erzeugt echte Spieldateien mit Titel und Level des Dokuments | ✓ bestanden |
| Referenz: Bei Not-Stopp arbeitet Claude nicht weiter und bekommt den Grund genannt | ✓ bestanden |
| Referenz: Claude bekommt das passende Fachwissen zum Auftrag; unbekanntes Studio wird abgelehnt | ✓ bestanden |
| Referenz: Claude kennt Projekt, Dokument und frühere Entscheidungen | ✓ bestanden |
| Referenz: Dateien entstehen nur im Projektordner, kein Ausbruch nach außen | ✓ bestanden |
| Referenz: Fehlende Programme werden gemeldet, nichts gilt als verbunden | ✓ bestanden |
| Referenz: Fehler werden verständlich gemeldet, das Dokument bleibt unverändert | ✓ bestanden |
| Referenz: Gedächtnis nimmt nur erlaubte Arten und braucht ein Projekt | ✓ bestanden |
| Referenz: Helfer-Studios und Programme der Beschreibung gibt es wirklich | ✓ bestanden |
| Referenz: Installiert heißt nicht startbar, Unreal bleibt ungeprüft, MCP gilt erst nach echtem Lauf | ✓ bestanden |
| Referenz: Ohne ausführliche Beschreibung bleibt das Studio ehrlich als nicht ausführlich markiert | ✓ bestanden |
| Referenz: Speichern und Laden geben dasselbe Spiel zurück | ✓ bestanden |
| Referenz: Startseite fragt „Was möchtest du machen?“ und zeigt fünf Aufgaben statt Fachbegriffe | ✓ bestanden |
| Referenz: Umschalten zwischen Einfach, Erweitert und Profi verändert keine Projektdaten | ✓ bestanden |
| Referenz: Veröffentlichen läuft nie ohne Zustimmung; Ablehnen ändert nichts | ✓ bestanden |
| Spiel aus einem Satz: Übernehmen legt ein sofort spielbares, im Level-Editor änderbares Spiel an (Blick von oben) – Spielen läuft, Rückgängig nimmt den Auftrag zurück | ✓ bestanden |
| Spiel-Editor wie GDevelop/Construct: neue Kacheln malen (Feder, Speicherpunkt, Schlüssel, Tür), Regel „Wenn … dann …“ anlegen und löschen, Zeitlimit; Probespiel zeigt Schlüssel und Restzeit; Bauen schreibt Regeln | ✓ bestanden |
| Spiel-Editor: Gegner auswählen und Tempo setzen (Rückgängig), Münzwert, Spielregeln; Probespiel im Fenster läuft mit Tasten, Nochmal, Zurück; Bauen übernimmt die Eigenschaften | ✓ bestanden |
| Spiele Engines: ohne Unity „blockiert“ und Unity-Tests verweigert; Plattformer bauen → Unity- und Godot-Projekt; mit Engines laufen EditMode/PlayMode bzw. die Godot-Prüfung mit Bericht | ✓ bestanden |
| Spiele bauen, Einfach: Auftrag und nächster Schritt; „Alle Bereiche“ zeigt alles; Erweitert unverändert | ✓ bestanden |
| Spiele-Dashboard: Vorschau-Karte, Aufgabe anlegen, weiter/zurück schieben, Hinweise übernehmen, rückgängig | ✓ bestanden |
| Spiele-Prompt: Satz → Vorschau mit KI-Name → Übernehmen legt das Spiel an (Strg+Z-fähig); Rückfrage-Knöpfe; Ändern zeigt nur die Änderung; Team-Vorschläge mit Ja | ✓ bestanden |
| Spiele: Retro-Vorlage, Level im Raster bearbeiten, Figur hinzufügen, prüfen, bauen und spielen | ✓ bestanden |
| Spiele: Umfang AAA+ wählen setzt AAA-Meilensteine; Bauen legt ein Unreal-Projekt mit Spielbibel an | ✓ bestanden |
| Spielmenü Editor: Schalter an als Vorgabe; aus → gebautes Spiel ohne Menü; rückgängig → wieder an; Spiel enthält Menü-Code | ✓ bestanden |
| Spielstand Probespiel: Fortsetzen erst nach Speichern; Speichern hält Punkte/Leben/Level fest; nach „Nochmal“ setzt „Fortsetzen“ fort; kaputte Datei → Meldung; gebautes Spiel speichert | ✓ bestanden |
| T-0764 B: Kind – Godot- und Unity-Prüfung starten keine Engine (Rolle zuerst); Erwachsene: Engine-Lauf wie bisher | ✓ bestanden |
| T-0764 B: Kind – Spiele-Bibliothek startet weder Godot noch den Emulator; Kittl über Claude fragt und öffnet nichts | ✓ bestanden |
| T-0764 B: Kind – Unreal kompilieren/prüfen und „Spielen (Unreal)“ starten nichts (Build.bat, UnrealEditor-Cmd, Editor) | ✓ bestanden |
| Team: KI-Antwort nur mit Fragen ändert nichts, bis eine Antwort gewählt ist | ✓ bestanden |
| Tiled-Ausgabe: Plattform-Spiel mit 2 Leveln → 2 .tmj + Kachelbild + Welt; Level 1 zurückgelesen gleich; Münzwert aus Level 2 steht am Objekt | ✓ bestanden |
| U1/U2: Rat-Modus umschalten, „Am Auftrag“ zeigt Fachleute, die Team-Anweisung enthält sie | ✓ bestanden |
| U3: Team-Lauf mit Ablaufplan – Gegenprüfung, Abnahme, Protokoll im Spielordner | ✓ bestanden |
| U4/U5/U6: /sprite erzeugt ein Spritesheet über das Bild-Werkzeug, die Prüfschleife bessert nach; Modus /musik-start … -ende | ✓ bestanden |
| Unreal-Spiel veröffentlichen: Paketierer (Shipping) → Windows-ZIP; Fehlschlag nennt den Grund | ✓ bestanden |
| Unreal-Spiel: Open World wählen (Rückgängig), Bauen schreibt C++-Spiel; ohne Unreal klare Meldung; mit Unreal kompilieren, Tests 3/3, Startlauf → Bericht im Projektordner; Fehler beim Kompilieren wird verständlich gemeldet | ✓ bestanden |
