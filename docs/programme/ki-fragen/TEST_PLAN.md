# KI fragen – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `ki-fragen` · Stand: 0.9.28 (Stand 83bf3c3)

17 Tests sprechen dieses Programm an.

- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Chatfenster: Enter sendet, Antwort als Sprechblase mit Tabelle und Code (Kopieren), Gespräch gespeichert und wieder zu öffnen, umbenennen, löschen
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen
- T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0316: Code-Studio – „In Claude Code öffnen“ in der Auftragsleiste nimmt Auftrag und Studio-Rolle mit; ohne Claude Code kein Knopf
- T-0316: Ohne Anthropic-Freigabe – kein Abo im Hintergrund (keine Haupt-KI, Läufe gesperrt), Hinweis sichtbar; „Claude Code öffnen“ startet das echte Claude Code im Projektordner
- T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Token erzeugen“, „Claude Code öffnen“ starten nie ein echtes Programm
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
