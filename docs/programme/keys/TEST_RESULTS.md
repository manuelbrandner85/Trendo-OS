# Online-Dienste – Testergebnisse

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `keys` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 7 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet | ✓ bestanden |
| CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt | ✓ bestanden |
| CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort | ✓ bestanden |
| CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen | ✓ bestanden |
| CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll | ✓ bestanden |
| Schlüssel: ungültig wird nicht gespeichert, gültig landet im Secret-Store, nie im Audit | ✓ bestanden |
| T-0225 UI: Schlüssel holen öffnet die Anbieterseite, Ersatz-KI erst mit gültigem Schlüssel, springt nur ein, wenn die lokale KI schweigt | ✓ bestanden |
