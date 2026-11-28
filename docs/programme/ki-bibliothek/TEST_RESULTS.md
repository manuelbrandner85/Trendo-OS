# KI-Bibliothek – Testergebnisse

Studio: Kostenlose KI finden (X-05) · Bereich: KI & Automatik · Seite: `ki-bibliothek` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 8 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| KI-Bibliothek (UI): alle Arten als Auswahl, jede Art zeigt ihre Modelle mit Lizenz und Größe | ✓ bestanden |
| T-0739 ECHT: echte Modelldatei laden und per Katalog-SHA-256 prüfen; veränderte Bytes werden verworfen (nur mit TRENDO_ECHT_PRUEFSUMME) | übersprungen |
| T-0739 X-05: feste Katalog-Summe entscheidet – richtige Datei wird installiert, veränderte Bytes werden verworfen, Widerspruch Server/Katalog ebenso | ✓ bestanden |
| T-0739 X-05: jede Hugging-Face-Datei hat eine feste SHA-256 mit Herkunft; ohne Summe steht ehrlich „ohne Prüfsumme“ | ✓ bestanden |
| X-05 UI: Suche findet laufende Quelle, Fragen nutzt die Rückfallkette | ✓ bestanden |
| X-05: Kostenlose Internet-KIs – nur Modelle ohne jeden Preis (auch Bild-Preis), „(free)“ aus dem Namen, Beschreibung gekürzt, größter Kontext zuerst; Liste speichern, nach 24 h veraltet | ✓ bestanden |
| X-05: Nur freie Quellen auf diesem Gerät, Modelle passend zur Hardware | ✓ bestanden |
| X-05: Rückfallkette lässt sich sortieren, abschalten und bleibt gespeichert | ✓ bestanden |
| X-05: Zähler „ins Internet“ – zählt nur den heutigen Tag, am nächsten Tag wieder bei 0, keine Inhalte gespeichert | ✓ bestanden |
