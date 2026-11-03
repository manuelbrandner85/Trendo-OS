# Software – Testplan

Studio: Webseite bauen (CORE-07) · Bereich: Entwickeln · Seite: `dev` · Stand: 0.9.36 (Stand d735c2c)

51 Tests sprechen dieses Programm an.

- Annehmen/Verwerfen im Programm: Claude ändert das Studio-Dokument – Vorher/Nachher vergleichen, verwerfen stellt alles her, behalten übernimmt
- Befehlsleiste Strg+K und Projekt-Fortsetzung überall: Claude fragen, weitermachen – auch aus einem anderen Studio
- CORE-07 (UI): Seite aus Beschreibung, Note, Stil, Paket erst nach Bestätigung
- CORE-07 (UI): Software-Studio legt Projekt an und zeigt es mit Git-Stand
- CORE-07 (UI): Suche findet das Software-Studio
- CORE-07: Git-Stand anlegen, Änderung erkennen, speichern (echtes git)
- CORE-07: Python- und .NET-Vorlage bestehen ihren eigenen Test (echte Werkzeuge)
- CORE-07: Vorlagen legen echte Projekte an, vorhandener Ordner wird nie überschrieben
- Claude im Programm: Studio ohne eigenes Programm (Webseiten) bleibt auf seiner Seite, kein Dokument
- Claude im Programm: neues Projekt im Spiele-Studio legt ein Dokument in „Spiele bauen“ an, zeigt das Programm, Claude arbeitet darin; Fortsetzen öffnet es wieder
- Claude im Studio: Auswahl in Trendo Tabelle geht als Zustand mit; Internet und Befehle bei Bedarf immer an, je Projekt auf „sicher“ stellbar; Vorgabe abschaltbar
- Claude im Studio: Ergebnis erscheint links im passenden Programm – Webseite im Webseiten-Studio, Spiel im Spiele-Studio; Studio wird daneben eingeblendet
- Claude im Studio: Startideen, Rückfrage als Knöpfe, Anhang landet im Projekt, Abo-Verbrauch je Antwort
- Claude im Studio: Trendo prüft das Ergebnis und gibt Fehler automatisch an Claude zurück, bis es läuft – höchstens drei Durchgänge
- Claude schlägt vor: Vorschläge als Knöpfe, Klick schickt sie ab, auch in der Befehlsleiste; abschaltbar
- Claude wie im Original: Skills, Plugins, Connectoren und Agenten aus dem eigenen Claude sichtbar; genutzter Skill/Connector unter der Antwort
- Claude-Abo aufgebraucht: Ersatz-KI übernimmt dieselbe Nachricht (sichtbar „Aktive KI“), bis zur Freigabe ohne Claude, danach automatisch wieder Claude
- Claude-App-Knopf: installiert (auch App-Paket über claude://) → öffnen; sonst die offizielle Download-Seite
- Claude-Chat: „⬆ GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „🌐 Netlify“ nur im Web-Studio, veröffentlicht nach Ja
- Claude: während der Arbeit weiterschreiben – Nachricht wartet sichtbar und kommt gleich danach dran; „Stopp & jetzt“ nimmt sie sofort
- Echt testen bis es läuft: Skriptfehler gehen zurück an Claude; ohne Fehler sieht Claude das Bildschirmfoto an; abschaltbar
- Etappen: Abo mitten im Auftrag leer – Ersatz-KI übernimmt, Trendo merkt sich das Projekt; ist Claude wieder frei, macht Claude dort weiter
- Freihändig sprechen: zuhören, abschicken, Antwort vorlesen, bis man es ausschaltet
- Gutachter: zweite Meinung mit Note, ändert nichts; „Verbessern lassen“ gibt die Punkte an Claude
- Kostenlose KI-Werkzeuge: Claude bestellt ein Bild – erst als Entwurf zum Ansehen, ins Projekt erst nach „Übernehmen“; neu erzeugen, verwerfen; Knöpfe; Diktieren
- Markieren & Claude fragen: Zellbereich in Trendo Tabelle geht mit ins Eingabefeld; Knopf auf der Seite; Strg+Umschalt+A
- Projekt-Gedächtnis: Claude merkt sich „Merken:“-Zeilen, sie gehen bei jeder Nachricht mit; ansehen, ergänzen, vergessen
- Schöne Antworten: Überschrift, Tabelle als Raster, Code mit Farben und „Kopieren“; Glas-Design im Chat
- Selbstlernend in allen Studios: Claudes „Lernregel“ wird Vorschlag; nach Ja gilt sie für alle Projekte des Studios; Nein und Zurücknehmen; Gutachten lehrt mit
- Spiele-Studio: eingebaute Spielregeln; mit GAMEFORGE dessen Regeln, Skills und Agenten (im sicheren Modus als Projekt-Skills); Regeln-Feld zeigt den Stand
- Spiele: Unreal öffnen und die Herstellerseite bleiben der alte Weg
- T-0203: Abbrechen installiert nichts
- T-0203: LibreOffice bleibt zu, wenn die Versionsabfrage zu lange dauert
- T-0203: OBS startet nicht, wenn die Dateiversion fehlt
- T-0203: OBS unter Windows liest die Dateiversion und öffnet ohne Prozess in der Prüfung
- T-0203: „Einrichten“ fragt, „Ja“ lädt nichts, solange die Prüfung nicht besteht
- T-0203: „In LibreOffice öffnen“ startet die Datei, wenn --version schnell antwortet
- T-0203: „In OBS öffnen“ startet die Sammlung, wenn --version schnell antwortet
- T-0203: „In VS Code öffnen“ startet den Ordner, wenn --version schnell antwortet
- T-0203: „Ja, einrichten“ öffnet keine Herstellerseite, solange die Prüfung nicht besteht
- T-0203: „Starten“ öffnet LibreOffice und Blender erst nach bestandener Prüfung und Freigabe
- T-0203: „Starten“ öffnet nicht, wenn die Prüfung scheitert
- T-0206 (UI): Dateiliste, Editor mit Zeilennummern, Speichern; Änderung erst als Vorschau, dann übernehmen
- Teams im Claude-Chat: vor dem Lauf liegt das Studio-Team als .claude/agents/trendo-*.md im Projektordner – Claude kann die Fachrollen hinzuziehen
- Teams: jedes Studio (außer dem abgelehnten SPEC-07) hat ein Profi-Team; „👥 Team“ im 3D-Studio öffnet „KI einsetzen“ mit dem 3D-Team; eigene Rolle nur mit Studio
- Unterlagen: nur Freigegebenes geht an Claude (als Datei im Projekt); passende Unterlagen werden vorgeschlagen – erst nach Ja
- Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert
- Veröffentlichen: erst nach Ja; Projekt als ZIP im Ordner „Veröffentlicht“, ohne Hilfsdateien – hochgeladen wird nichts
- Wiederholungen automatisieren: Auftrag als Ablauf merken (Prompt-Bibliothek), mit einem Klick wiederholen; Wiederholung wird erkannt
- Zeitreise: jede Antwort mit Änderungen wird ein Versionspunkt; zurück auf einen älteren Stand und wieder vor
- Ziehen in Claude: Text kommt als Zitat ins Eingabefeld, Dateien werden angehängt
