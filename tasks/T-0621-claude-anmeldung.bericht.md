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
- Volltest: zusammen mit Release 0.9.96 (siehe reports/volltest-T-0621/).
