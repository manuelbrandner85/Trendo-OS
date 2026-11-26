# T-0775 ECHT: Briefe schreiben – Claude kürzt den offenen Brief

Datum: 2026-11-26 10:55 · Claude Code: installiert=True, angemeldet=True, Anmeldung=claude.ai, Abo=max
Nichts ersetzt außer der Fensterdarstellung (headless): kein ClaudeRunOverride, kein ClaudeStatusOverride, kein Ersatz-KI-Dienst. API-Variablen und Token entfernt (ClaudeEnv).

## Ablauf

- Brief im Programm „Briefe schreiben“ (Betreff, Empfänger, langer Text), Claude-Bereich des Studios geöffnet, Auftrag „kürzen“ gesendet.
- Claude arbeitete im Projektordner in Brief.json (32 s); Trendo übernahm die Änderung live in den offenen Brief.
- Länge vorher 754 Zeichen, nachher 205 Zeichen.
- Text nachher: ich ziehe zum Ende des kommenden Monats in den Lindenweg 7, 12345 Musterstadt. Bitte senden Sie alle Schreiben und Rechnungen ab dann an diese neue Anschrift. Meine Kundennummer 55-1234 bleibt unverändert.
- Strg+Z: Brief wieder wie vorher = True
- Rohlog: brief-rohlog.jsonl · SHA-256 557735121abbad5b2123bc2f41cea2c7dc0ea585b883b1b60ac3df7c3f36b0cf

Ergebnis: BESTANDEN.
