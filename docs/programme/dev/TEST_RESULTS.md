# Webseite bauen – Testergebnisse

Studio: Webseite bauen (CORE-07) · Bereich: Entwickeln · Seite: `dev` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 67 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| A4: Mod prüfen zeigt Beschreibung, Rechte und Lizenz; erst „Ja“ installiert; Vorlage einfügen | ✓ bestanden |
| A6: Eingebauter Fehler wird gemeldet und gezielt nachgebessert; die Obergrenze gilt; Bildbewertung ist nur ein Hinweis | ✓ bestanden |
| A6: „Seite prüfen“ meldet Fehler der gespeicherten Seite, ohne etwas zu ändern | ✓ bestanden |
| A7: Ohne Claude läuft die Textaufgabe lokal und die Oberfläche nennt die KI; ohne jede KI ein klarer Hinweis | ✓ bestanden |
| A8: Ohne Wahl wird nichts gebaut; abgelehnte Richtungen werden gemeldet; die gewählte Richtung prägt die Seite | ✓ bestanden |
| Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio | ✓ bestanden |
| CORE-07 (UI): Seite aus Beschreibung, Note, Stil, Paket erst nach Bestätigung | ✓ bestanden |
| CORE-07 (UI): Software-Studio legt Projekt an und zeigt es mit Git-Stand | ✓ bestanden |
| CORE-07 (UI): Suche findet das Software-Studio | ✓ bestanden |
| CORE-07: Git-Stand anlegen, Änderung erkennen, speichern (echtes git) | ✓ bestanden |
| CORE-07: Python- und .NET-Vorlage bestehen ihren eigenen Test (echte Werkzeuge) | ✓ bestanden |
| CORE-07: Vorlagen legen echte Projekte an, vorhandener Ordner wird nie überschrieben | ✓ bestanden |
| Claude im Programm: Studio ohne eigenes Programm (Webseiten) bleibt auf seiner Seite, kein Dokument | ✓ bestanden |
| Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet | ✓ bestanden |
| Claude im Studio: Startideen, Rückfrage als Knöpfe, Anhang landet im Projekt, Abo-Verbrauch je Antwort | ✓ bestanden |
| Claude im Studio: Trendo prüft das Ergebnis und gibt Fehler automatisch an Claude zurück, bis es läuft – höchstens drei Durchgänge | ✓ bestanden |
| Claude schlägt vor: Vorschläge als Knöpfe, Klick schickt sie ab, auch in der Befehlsleiste; abschaltbar | ✓ bestanden |
| Claude-Abo aufgebraucht: Ersatz-KI übernimmt dieselbe Nachricht (sichtbar „Aktive KI“), bis zur Freigabe ohne Claude, danach automatisch wieder Claude | ✓ bestanden |
| Claude-App-Knopf: installiert (auch App-Paket über claude://) → öffnen; sonst die offizielle Download-Seite | ✓ bestanden |
| Claude-Chat: „GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „Netlify“ nur im Web-Studio, veröffentlicht nach Ja | ✓ bestanden |
| Claude: während der Arbeit weiterschreiben – Nachricht wartet sichtbar und kommt gleich danach dran; „Stopp & jetzt“ nimmt sie sofort | ✓ bestanden |
| Echt testen bis es läuft: Skriptfehler gehen zurück an Claude; ohne Fehler sieht Claude das Bildschirmfoto an; abschaltbar | ✓ bestanden |
| Etappen: Abo mitten im Auftrag leer – Ersatz-KI übernimmt, Trendo merkt sich das Projekt; ist Claude wieder frei, macht Claude dort weiter | ✓ bestanden |
| Freihändig sprechen: zuhören, abschicken, Antwort vorlesen, bis man es ausschaltet | ✓ bestanden |
| Gutachter: zweite Meinung mit Note, ändert nichts; „Verbessern lassen“ gibt die Punkte an Claude | ✓ bestanden |
| K3: Skizze aus dem Editor kommt an; ohne bildfähige KI ein Hinweis statt Fehler; mit bildfähiger KI enthält der Auftrag das Bild (Seite plus Striche) | ✓ bestanden |
| K4 und A7: Bild ohne bildfähige KI ergibt einen Hinweis statt eines Fehlers, nichts wird gefragt; mit bildfähiger KI fließt das Bild in den Auftrag | ✓ bestanden |
| K4: Satz → Vorschau ohne Schreiben → Übernehmen als ein Schritt → Rückgängig; Editor-Daten statt Dateien | ✓ bestanden |
| K4: Ungültige KI-Antwort wird gemeldet, nichts gelangt in die Seite | ✓ bestanden |
| K6: Code zeigen, Klick auf einen Baustein springt zur Zeile, Änderung im Code landet nach „Code übernehmen“ in der Seite (vorher ein Stand) | ✓ bestanden |
| K8/T-0718: ohne Ja im Freigabe-Tor kein Hochladen; nach Ja genau einmal, mit ehrlichem Bericht der Dateien; die Freigabe gilt nicht für den nächsten Lauf | ✓ bestanden |
| Kein Datenverlust: Ungespeicherter Entwurf landet vor Wiederherstellen und Fassungswechsel in einem Stand; benannte Stände bleiben sichtbar | ✓ bestanden |
| Projekt-Gedächtnis: Claude merkt sich „Merken:“-Zeilen, sie gehen bei jeder Nachricht mit; ansehen, ergänzen, vergessen | ✓ bestanden |
| Schöne Antworten: Überschrift, Tabelle als Raster, Code mit Farben und „Kopieren“; Glas-Design im Chat | ✓ bestanden |
| Seiten: anlegen über die Felder, wechseln, löschen erst nach Nachfrage; Prüfung zeigt Befunde | ✓ bestanden |
| Selbstlernend in allen Studios: Claudes „Lernregel“ wird Vorschlag; nach Ja gilt sie für alle Projekte des Studios; Nein und Zurücknehmen; Gutachten lehrt mit | ✓ bestanden |
| T-0203: „Einrichten“ fragt, „Ja“ lädt nichts, solange die Prüfung nicht besteht | ✓ bestanden |
| T-0203: „In VS Code öffnen“ startet den Ordner, wenn --version schnell antwortet | ✓ bestanden |
| T-0206 (UI): Dateiliste, Editor mit Zeilennummern, Speichern; Änderung erst als Vorschau, dann übernehmen | ✓ bestanden |
| T-0662: Abgelehnter Plan bekommt genau eine Nachfrage mit der Meldung; die berichtigte Antwort wird zur Vorschau | ✓ bestanden |
| T-0688 Software-Seite: „Testen“ blockiert nicht mehr – öffnet den Code-Editor, Test läuft im Hintergrund; neue Vorlage „Programm mit Fenster“ | ✓ bestanden |
| T-0716 Web Design Studio: ungespeicherter Entwurf des Editors – Notsicherung der Hülle, Sperre, Karte, Zurückholen in den Editor; „Speichern“ räumt weg | ✓ bestanden |
| T-0718 UI Baukasten: „Veröffentlichen …“ fragt im echten Dialog; nach Ja liegt die Seite im Zielordner, Prüfung grün | ✓ bestanden |
| T-0718 UI Kinderprofil: keine Rückfrage, nichts veröffentlicht, Hinweis „Erwachsener“, im Protokoll abgelehnt | ✓ bestanden |
| T-0718 UI Ordner: echter Dialog nennt Wirkung, Ziel, Dateien, Größe; Abbrechen = nichts im Ziel; Ja = genau eine Ausführung, Rückmeldung mit Ort, Dateien und Prüfung | ✓ bestanden |
| T-0718 UI Testmodus: Server-Weg lädt nie ins Internet; Netlify ohne eigenes Konto: BLOCKIERT mit „Was tun“, keine Anfrage im Tor | ✓ bestanden |
| T-0718 UI ZIP: nur nach Ja im Tor; Paket enthält genau die gezeigten Dateien; Rückmeldung nennt „nichts hochgeladen“ | ✓ bestanden |
| T-0718 UI eigener Server: echter Upload per HTTP an WebDAV auf 127.0.0.1 nach Ja – Dateien liegen byte-gleich auf dem Server, Nachprüfung, Anmeldung nur im geschützten Speicher; falsches Passwort → Meldung mit „Was tun“ | ✓ bestanden |
| T-0718 echter Chrome: Ordner und WebDAV-Server auf 127.0.0.1 – veröffentlichte Seite lädt ohne Fehler, alle lokalen Links/Dateien per HTTP erreichbar, Bildschirmfoto (übersprungen ohne Chrome/Edge) | ✓ bestanden |
| T-0726 A5: „GitHub“ im Claude-Chat fragt im Freigabe-Tor (Repo, Sichtbarkeit, Dateien) – Nein lädt nichts hoch, Ja genau einmal; ändert sich vorher eine Datei, blockiert der Kern | ✓ bestanden |
| Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen | ✓ bestanden |
| Unterlagen: nur Freigegebenes geht an Claude (als Datei im Projekt); passende Unterlagen werden vorgeschlagen – erst nach Ja | ✓ bestanden |
| V1: Webseite bauen in „Einfach“ – geführter Weg offen, Fachbereiche im Klappbereich „Mehr Werkzeuge“ (weiter im Baum) | ✓ bestanden |
| Veröffentlichen: erst nach Ja; Projekt als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien – hochgeladen wird nichts | ✓ bestanden |
| Visueller Editor: Knöpfe da, im Testmodus kein WebView; „speichern“ vom Editor schreibt die Seite, Export legt die fertige Seite an | ✓ bestanden |
| W1/Z4: Ansicht mit eigener Breite anlegen, Breite ändern und entfernen, ungültige Breite wird gemeldet | ✓ bestanden |
| W2: Sammlung anlegen, Eintrag über das Formular, Pflichtfeld leer wird gemeldet, Vorlage-Seite mit Platzhaltern | ✓ bestanden |
| W5: Auswahl zur Komponente machen, einfügen, als Vorlage speichern und in ein anderes Projekt holen | ✓ bestanden |
| W6: Kontrast wird schon beim Wählen gemeldet; Übernehmen gilt fürs Projekt, eine bestehende Variante bleibt unverändert | ✓ bestanden |
| Wiederholungen automatisieren: Auftrag als Ablauf merken (Prompt-Bibliothek), mit einem Klick wiederholen; Wiederholung wird erkannt | ✓ bestanden |
| Z5: Sprache hinzufügen, KI-Vorschlag ändert erst nach „Übernehmen“, fehlende Übersetzung steht in der Liste | ✓ bestanden |
| Z6: Prüflauf vergleicht die lokalen Modelle, speichert das Ergebnis und empfiehlt nur das bestandene | ✓ bestanden |
| Z7: Medien ins Projekt holen – Bilder werden verkleinert, Lizenz fehlt wird angezeigt, Suche, Schrift von Fontsource mit Herkunft und Lizenz | ✓ bestanden |
| Z8: Internet-Adresse abgelehnt, ohne dass etwas gelesen wird; eigene Datei wird zur neuen Seite mit Meldeliste | ✓ bestanden |
| Zeitreise: jede Antwort mit Änderungen wird ein Versionspunkt; zurück auf einen älteren Stand und wieder vor | ✓ bestanden |
| Ziehen in Claude: Text kommt als Zitat ins Eingabefeld, Dateien werden angehängt | ✓ bestanden |
| axe-Oberfläche: ohne Einrichtung Einrichten-Knopf; mit axe → Befunde auf Deutsch (✗ Fehler, • Hinweis) mit Version und Zahl der bestandenen Regeln | ✓ bestanden |
