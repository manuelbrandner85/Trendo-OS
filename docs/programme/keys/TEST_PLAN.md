# Online-Dienste – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `keys` · Stand: 0.9.113 (Stand 1978f1a)

7 Tests sprechen dieses Programm an.

- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit
- T-0225 UI: Schlüssel holen öffnet die Anbieterseite, Ersatz-KI erst mit gültigem Schlüssel, springt nur ein, wenn die lokale KI schweigt
