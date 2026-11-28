# System & Sicherheit – Testergebnisse

Studio: Qualität prüfen (SYS-13) · Bereich: System & Sicherheit · Seite: `system` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 67 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Am Programm vorbei gearbeitet (nur eigene Dateien, Studio-Dokument unverändert) – Trendo schickt Claude zurück ins Programm-Dokument; neue Projekte übernehmen keine bloß geöffnete Vorlage | ✓ bestanden |
| Annehmen/Verwerfen im Programm: Claude ändert das Studio-Dokument – Vorher/Nachher vergleichen, verwerfen stellt alles her, behalten übernimmt | ✓ bestanden |
| Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio | ✓ bestanden |
| C1/C2/C4: Ist Claude verbunden, geht „Los“ der Studio-Zeile an Claude (Studio bleibt sichtbar, volle Rechte mit Rückfrage bei Wirkung nach außen); der zweite Auftrag läuft in derselben Sitzung; ohne Claude plant Trendo selbst | ✓ bestanden |
| CORE-05: Claude baut ein 3D-Modell – sieht Szene, Formen und Materialien, Plan zeigt das Modell mit Gesamtmaß, erst „Ausführen“ baut Teile, Licht und Kamera; ungültiges Material fällt weg; Rückgängig stellt zurück | ✓ bestanden |
| CORE-06: Claude baut Level – sieht alle Level voll, Plan zeigt das neue Level als Vorschau, erst „Ausführen“ legt Level 2 und den Hintergrund an, falsches Zeichen fällt weg, „Verwerfen“ stellt alles zurück | ✓ bestanden |
| CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen | ✓ bestanden |
| Claude auch in der Studio-Übersicht, in der Suche und im Fokusmodus | ✓ bestanden |
| Claude im Brief-Studio: arbeitet im offenen Brief (Brief.json) – Text und Design (z. B. Bewerbung als ZFA, zeitgemäß in Blau) erscheinen sofort im Brief-Programm | ✓ bestanden |
| Claude im Menüband jedes Fachprogramms und Rechtsklick „Claude fragen“ auf Elemente der Seite | ✓ bestanden |
| Claude im Programm: Studio ohne eigenes Programm (Webseiten) bleibt auf seiner Seite, kein Dokument | ✓ bestanden |
| Claude im Programm: neues Projekt im Spiele-Studio legt ein Dokument in „Spiele bauen“ an, zeigt das Programm, Claude arbeitet darin; Fortsetzen öffnet es wieder | ✓ bestanden |
| Claude im Studio: Auswahl in Trendo Tabelle geht als Zustand mit; Internet und Befehle bei Bedarf immer an, je Projekt auf „sicher“ stellbar; Vorgabe abschaltbar | ✓ bestanden |
| Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet | ✓ bestanden |
| Claude im Studio: Startideen, Rückfrage als Knöpfe, Anhang landet im Projekt, Abo-Verbrauch je Antwort | ✓ bestanden |
| Claude im Studio: Trendo prüft das Ergebnis und gibt Fehler automatisch an Claude zurück, bis es läuft – höchstens drei Durchgänge | ✓ bestanden |
| Claude kennt das Programm: offene Seite mit Feldern und Knöpfen geht mit; Claude bedient es erst nach „Ausführen“ – unbekannte Ziele fallen weg | ✓ bestanden |
| Claude live: Webseite erscheint in der Vorschau, beim Ändern vorher/nachher – Behalten oder Zurücknehmen; Lieferformat index.html | ✓ bestanden |
| Claude live: neuer Text öffnet gleich in Trendo Text – aber nie über einen ungespeicherten Text | ✓ bestanden |
| Claude live: offenes Studio-Dokument (.tdoc) – Claude bearbeitet es, das Studio übernimmt die Änderung sofort, Rückgängig im Studio | ✓ bestanden |
| Claude schlägt vor: Vorschläge als Knöpfe, Klick schickt sie ab, auch in der Befehlsleiste; abschaltbar | ✓ bestanden |
| Claude zeigt, wo es arbeitet; Felder in Aufklapp-Menüs kann Claude lesen und ausfüllen | ✓ bestanden |
| Claude-Chat: ohne Claude Code „einrichten“, ohne Anmeldung „Mit Claude anmelden“ – im Testmodus startet nie ein echtes Claude | ✓ bestanden |
| Claude-Chat: wie in Claude – erste Nachricht legt das Projekt an, Antwort mit Arbeitsschritten, Fortsetzen über die Sitzung, Zurücknehmen, Löschen in den Papierkorb | ✓ bestanden |
| Echt testen bis es läuft: Skriptfehler gehen zurück an Claude; ohne Fehler sieht Claude das Bildschirmfoto an; abschaltbar | ✓ bestanden |
| Etappen: Abo mitten im Auftrag leer – Ersatz-KI übernimmt, Trendo merkt sich das Projekt; ist Claude wieder frei, macht Claude dort weiter | ✓ bestanden |
| Freie Bilder & Klänge Profi: ähnliche Bilder, Merken in neue Liste, Merklisten-Reiter mit Namensnennungen, Schriftvorschau mit eigenem Text; Claude sucht, prüft Lizenzen, merkt und schreibt die Namensnennung | ✓ bestanden |
| Freihändig sprechen: zuhören, abschicken, Antwort vorlesen, bis man es ausschaltet | ✓ bestanden |
| Genauere Leser (Kalender, Programm-Dokument), Design „Wie Windows“, Hilfe „?“ auf Seiten ohne eigenen Hilfetext, Hinweis statt stummer Knöpfe | ✓ bestanden |
| Gutachter: zweite Meinung mit Note, ändert nichts; „Verbessern lassen“ gibt die Punkte an Claude | ✓ bestanden |
| H1b Plan-Zeilen: Claude-Bereich aus einem anderen Programm, dann Wechsel in die Nachhilfe – Chat sendet nichts, ein offener Plan wird nicht ausgeführt, die Übung bleibt unverändert | ✓ bestanden |
| Markieren & Claude fragen: Zellbereich in Trendo Tabelle geht mit ins Eingabefeld; Knopf auf der Seite; Strg+Umschalt+A | ✓ bestanden |
| Not-Stopp: Bricht laufende Claude-Arbeit ab und lässt keinen neuen Durchgang starten | ✓ bestanden |
| Projekt-Gedächtnis: Claude merkt sich „Merken:“-Zeilen, sie gehen bei jeder Nachricht mit; ansehen, ergänzen, vergessen | ✓ bestanden |
| SPEC-15: Claude als Regie – Briefing mit Szenen, Ton und Ton-Wache, Plan zeigt neue Szenen, „Ausführen“ legt Szenen mit Bauchbinde an und setzt den Sendeplan; ungültige Quelle fällt weg | ✓ bestanden |
| STUDIO-MUSIK-01: Claude als Produzent – Briefing mit Silben und Reimen, Plan mit Rhythmus vorher/nachher, Ausführen füllt den Entwurf, Erstellen hängt die Fassung ans Original, Mastering-Ziel „laut“ | ✓ bestanden |
| SYS-13 (T-0763): Ampel je Bereich mit nächstem Schritt; Gesamtsatz; rot vor gelb vor grün | ✓ bestanden |
| SYS-13 (T-0763): Entwickler-Prüfungen nur in „Professionell“ und ehrlich beschriftet; Telemetrie-Karte mit echtem Ergebnis | ✓ bestanden |
| SYS-13 (T-0763): Studio öffnet „Alles prüfen“ – goldener Knopf, Ampel je Bereich mit nächstem Schritt; veränderte Protokollzeile rot mit Stelle | ✓ bestanden |
| SYS-13 (T-0763): Telemetrie-Prüfung der installierten Version über die Paketlisten – findet Analyse-Bibliotheken, Trendo selbst ist frei | ✓ bestanden |
| SYS-13 (T-0763): veränderte Protokollzeile wird mit Stelle gemeldet – Ampel rot im Bereich Protokoll | ✓ bestanden |
| SYS-13 (UI): Rückfall-Sicherung und vorige Version; Protokoll-Prüfung | ✓ bestanden |
| SYS-13-P09: Hash-Kette erkennt Ändern, Löschen und Vertauschen samt Stelle | ✓ bestanden |
| SYS-13-P13: Jede Seite gehört zu einem Modul aus requirements.yaml | ✓ bestanden |
| SYS-13-P14: Fehler in einer Seite zeigt freundliche Fehlerseite statt Absturz | ✓ bestanden |
| SYS-13-P15/P16: Median-Budget und Leck-Grenze 50 MB | ✓ bestanden |
| SYS-13-P15/P16: Start < 3 s, Seitenwechsel im Median < 300 ms, 100 Wechsel ohne > 50 MB Zuwachs | ✓ bestanden |
| SYS-13-P20: Alte Einstellungsdatei (v1) wird gelesen und angehoben | ✓ bestanden |
| SYS-13-P21: Keine Telemetrie-Bibliothek oder -Adresse im Quellcode | ✓ bestanden |
| SYS-13-P22: NuGet-Lizenzliste ist vollständig (alle Pakete aus src/) | ✓ bestanden |
| SYS-13-P23: Ausgabe von dotnet list package --vulnerable wird erkannt | ✓ bestanden |
| SYS-13-P25: Rückfall – vorige Version, Sicherung, Anleitung vorhanden | ✓ bestanden |
| SYS-13: Absturzprotokoll schwärzt Benutzerpfade und behält höchstens 20 Dateien | ✓ bestanden |
| SYS-13: Release-Skript baut nur von sauberem main | ✓ bestanden |
| Schöne Antworten: Überschrift, Tabelle als Raster, Code mit Farben und „Kopieren“; Glas-Design im Chat | ✓ bestanden |
| Selbstlernend in allen Studios: Claudes „Lernregel“ wird Vorschlag; nach Ja gilt sie für alle Projekte des Studios; Nein und Zurücknehmen; Gutachten lehrt mit | ✓ bestanden |
| Spiele-Studio: eingebaute Spielregeln; mit GAMEFORGE dessen Regeln, Skills und Agenten (im sicheren Modus als Projekt-Skills); Regeln-Feld zeigt den Stand | ✓ bestanden |
| T-0621: Abgelaufene Claude-Anmeldung wird kein Liedtext – Ersatz-KI antwortet, Claude gilt bis zur neuen Anmeldung als nicht bereit | ✓ bestanden |
| T-0726 A6: Kartenkacheln aus dem Internet nur nach Zustimmung über das Freigabe-Tor – Nein lädt nichts, Ja gilt als Einstellung (im Protokoll), ausschaltbar; offline kein Angebot | ✓ bestanden |
| T-0780 H1 (2. Gegenprüfung): Claude-Brücke – „befehl“, „klicken“ und „feld_setzen“ kommen an Quellen-Sperre, Zitatprüfung und Ordner-Sperre nicht vorbei; Schritt wird zurückgerollt und Claude bekommt den Fehler | ✓ bestanden |
| Unterlagen: nur Freigegebenes geht an Claude (als Datei im Projekt); passende Unterlagen werden vorgeschlagen – erst nach Ja | ✓ bestanden |
| Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert | ✓ bestanden |
| Veröffentlichen: erst nach Ja; Projekt als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien – hochgeladen wird nichts | ✓ bestanden |
| Wiederholungen automatisieren: Auftrag als Ablauf merken (Prompt-Bibliothek), mit einem Klick wiederholen; Wiederholung wird erkannt | ✓ bestanden |
| Zeitleiste mit Dauer und Tönen: fertig, Rückfrage, abschaltbar | ✓ bestanden |
| Zeitreise: jede Antwort mit Änderungen wird ein Versionspunkt; zurück auf einen älteren Stand und wieder vor | ✓ bestanden |
| Ziehen in Claude: Text kommt als Zitat ins Eingabefeld, Dateien werden angehängt | ✓ bestanden |
