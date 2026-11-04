# Verbindungen – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `verbindungen` · Stand: 0.9.36 (Stand dfd134a)

12 Tests sprechen dieses Programm an.

- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- CORE-03 UI: Rolle wählen, starten, Team-Ablauf, Speichern über Freigabe
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- CORE-12 UI: Vorlage ausfüllen und senden, neue Vorlage speichern, Masterprompt bauen
- Claude-Chat: „⬆ GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „🌐 Netlify“ nur im Web-Studio, veröffentlicht nach Ja
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert
- X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette
