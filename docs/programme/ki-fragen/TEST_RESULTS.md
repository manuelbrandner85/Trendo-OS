# KI fragen – Testergebnisse

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `ki-fragen` · Stand: 0.9.30 (Stand 234e066)

Lauf: `volltest-0.9.30-api.trx, volltest-0.9.30-app.trx, volltest-0.9.30-core.trx`

Bestanden 17 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet | ✓ bestanden |
| CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt | ✓ bestanden |
| CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort | ✓ bestanden |
| CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen | ✓ bestanden |
| CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll | ✓ bestanden |
| CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe | ✓ bestanden |
| CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch | ✓ bestanden |
| CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen | ✓ bestanden |
| Chatfenster: Enter sendet, Antwort als Sprechblase mit Tabelle und Code (Kopieren), Gespräch gespeichert und wieder zu öffnen, umbenennen, löschen | ✓ bestanden |
| SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf | ✓ bestanden |
| T-0316: Abo-Token – API-Schlüssel wird abgelehnt (nur Abo), Abo-Token landet im sicheren Speicher und lässt sich entfernen | ✓ bestanden |
| T-0316: Claude als Haupt-KI – Frage läuft eingegrenzt über Claude (kein Datei-Ändern), Antwort mit Quelle „Claude (dein Abo)“; Haupt-KI aus → Claude wird nicht gefragt | ✓ bestanden |
| T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“ | ✓ bestanden |
| T-0316: Ohne Anthropic-Freigabe – kein Abo im Hintergrund (keine Haupt-KI, Läufe gesperrt), Hinweis sichtbar; „Claude Code öffnen“ startet das echte Claude Code im Projektordner | ✓ bestanden |
| T-0316: Studio – „Claude Code“ immer in der Auftragsleiste; „Im eigenen Fenster“ öffnet Claude Code mit der Rolle des Studios | ✓ bestanden |
| T-0316: Testmodus – Knöpfe „Claude Code einrichten“, „Mit Claude verbinden“, „Token erzeugen“, „Claude Code öffnen“ starten nie ein echtes Programm | ✓ bestanden |
| X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette | ✓ bestanden |
