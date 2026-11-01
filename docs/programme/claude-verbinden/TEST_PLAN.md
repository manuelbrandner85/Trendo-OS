# Mit Claude verbinden – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `claude-verbinden` · Stand: 0.9.32 (Stand 345fe84)

10 Tests sprechen dieses Programm an.

- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
