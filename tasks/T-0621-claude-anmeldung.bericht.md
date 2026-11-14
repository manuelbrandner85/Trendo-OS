# Bericht T-0621: Abgelaufene Claude-Anmeldung

**Anlass (Nutzer, 14.11.2026):** In Trendo Musik kam beim Liedtext-Erzeugen „Failed to authenticate: this login reached its time limit and cannot be renewed …“. Einmal angebunden, soll Claude nicht „herausfallen“.

**Ursache:** Claude Code meldet die abgelaufene Anmeldung als normale Antwort; Trendo hat sie als Ergebnis (Liedtext) übernommen.

**Grenze:** Eine abgelaufene Anmeldung kann Trendo nicht selbst verlängern – angemeldet wird nur über Anthropics eigenen Ablauf, Trendo speichert keine Tokens (Projektentscheidung). Daher: nie als Ergebnis zeigen, Ersatz-KI übernehmen lassen, Neu-Anmelden mit einem Klick.

## Inhalt
- `ClaudeCode.IsAuthExpired` erkennt abgelaufene/fehlende Anmeldung (getestet gegen normale Antworten und Kontingent-Meldungen).
- `ClaudeKiBackend`: wirft statt die Meldung zurückzugeben → der KI-Kern fragt die nächste Quelle (Ersatz-KI); Claude gilt bis zur neuen Anmeldung als nicht bereit; Kontingent-Meldung ebenfalls nie als Ergebnis.
- Claude-Chat im Studio: Ersatz-KI übernimmt mit Hinweis im Verlauf.
- Hinweis mit Knopf „Claude neu anmelden“ (höchstens alle 10 Minuten), danach ist Claude wieder Haupt-KI; Protokolleintrag.

## Tests
- Core: EinfachBedienenTests.Claude_auth_expired. App: ClaudeAuth_Ui (Claude nachgebildet: kein Fehlertext als Liedtext, Ersatz-KI antwortet, Claude wird danach nicht erneut gefragt); ClaudeFallback_Ui, ClaudeChat_Ui, Claude_Ui, MusikStudio_Ui grün.
- Volltest (Stand 700d428c5): Api 16, App 1285, Paket-Tests OK; Core 3 Fehler – zwei „echt“-Tests mit Python-Werkzeugen (Startfehler von Python unter Last, einzeln grün), der neue BibTeX-Lasttest (10,5 s unter Last).
- BibTeX-Suche hört jetzt an einem neuen Eintrag (Zeilenanfang mit @) auf – lineare Laufzeit, 17 Tests in 0,6 s. Danach Core vollständig: 2490 bestanden, 5 übersprungen, 0 Fehler; KreativStudios4_Ui 5 bestanden. Belege in reports/volltest-T-0621/.
