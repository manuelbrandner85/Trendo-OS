# KI fragen – Testergebnisse

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `ki-fragen` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 16 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 1

| Test | Ergebnis |
|---|---|
| CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet | ✓ bestanden |
| CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt | ✓ bestanden |
| CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort | ✓ bestanden |
| CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen | ✓ bestanden |
| CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll | ✓ bestanden |
| CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch | ✓ bestanden |
| Chatfenster: Enter sendet, Antwort als Sprechblase mit Tabelle und Code (Kopieren), Gespräch gespeichert und wieder zu öffnen, umbenennen, löschen | ✓ bestanden |
| Gedächtnis: aus, bis man es einschaltet; danach merkt der Chat Aussagen, Einträge sichtbar und löschbar; alles vergessen fragt nach | ✓ bestanden |
| T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“ | ✓ bestanden |
| T-0717 KI fragen: noch nicht gesendete Nachricht – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen | ✓ bestanden |
| T-0734 ECHT: Ollama liest eine angehängte Datei, Antwort wächst sichtbar, Folgefrage kennt die Datei; Bildmodell erkennt die Farbe eines Bildes (nur mit TRENDO_ECHT_KIFRAGEN) | übersprungen |
| T-0734 Gegenprüfung: Anhang beim Senden nicht lesbar (leer, gelöscht) oder Bild ohne Bildmodell → Frage bleibt im Eingabefeld, Anhang bleibt, Meldung nennt die Datei | ✓ bestanden |
| T-0734 Gegenprüfung: BMP wird vor dem Senden mit SkiaSharp in PNG umgewandelt – das Bildmodell bekommt ein echtes PNG | ✓ bestanden |
| T-0734 Gegenprüfung: Datei lesen (hier ein langsames pdftotext) läuft neben der Oberfläche; „Stopp“ bricht das Lesen in unter 3 s ab, Frage und Anhang bleiben | ✓ bestanden |
| T-0734 Gegenprüfung: während einer laufenden Antwort abgelegte Datei bleibt für die nächste Frage; nur die gesendeten Anhänge verschwinden | ✓ bestanden |
| T-0734: Antwort wächst sichtbar in der Sprechblase; Stopp beendet in unter 2 s und das Geschriebene bleibt mit „(abgebrochen)“ im Gespräch | ✓ bestanden |
| T-0734: Bild ohne Bildmodell → klare Meldung statt erfundener Antwort, Anhang bleibt; Hinweis sagt, dass Bilder auf dem PC bleiben | ✓ bestanden |
| T-0734: „Datei anhängen“ (Auswahl) → Karte mit Name und Hinweis „bleibt auf diesem PC“; Frage enthält den Dateitext, Gespräch zeigt 📎; falsche Art wird abgelehnt | nicht im Lauf |
| T-0775 ECHT KI fragen: Frage mit angehängter Textdatei geht an Claude (Abo), Antwort nennt den Inhalt der Datei (nur mit TRENDO_ECHT_CLAUDE) | übersprungen |
