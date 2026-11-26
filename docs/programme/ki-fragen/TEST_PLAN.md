# KI fragen – Testplan

Studio: KI fragen (CORE-02) · Bereich: KI & Automatik · Seite: `ki-fragen` · Stand: 0.9.110 (Stand 0eb9d52)

18 Tests sprechen dieses Programm an.

- CORE-02 UI: Studio verweist auf „KI fragen“, Frage wird beantwortet
- CORE-02: Nur Quellen auf diesem Gerät – Cloud-Adressen werden abgelehnt
- CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort
- CORE-02: Ollama-Format (tags/chat) wird korrekt gesprochen
- CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll
- CORE-10 UI: Merken, Suchen, Löschen und Korrektur aus dem Gespräch
- Chatfenster: Enter sendet, Antwort als Sprechblase mit Tabelle und Code (Kopieren), Gespräch gespeichert und wieder zu öffnen, umbenennen, löschen
- Gedächtnis: aus, bis man es einschaltet; danach merkt der Chat Aussagen, Einträge sichtbar und löschbar; alles vergessen fragt nach
- T-0316: Claude-Karte – nicht installiert → „Claude Code einrichten“, nicht angemeldet → „Mit Claude verbinden“, verbunden → „✓ Mit deinem Claude-Abo verbunden (Max)“
- T-0717 KI fragen: noch nicht gesendete Nachricht – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen
- T-0734 ECHT: Ollama liest eine angehängte Datei, Antwort wächst sichtbar, Folgefrage kennt die Datei; Bildmodell erkennt die Farbe eines Bildes (nur mit TRENDO_ECHT_KIFRAGEN)
- T-0734 Gegenprüfung: Anhang beim Senden nicht lesbar (leer, gelöscht) oder Bild ohne Bildmodell → Frage bleibt im Eingabefeld, Anhang bleibt, Meldung nennt die Datei
- T-0734 Gegenprüfung: BMP wird vor dem Senden mit SkiaSharp in PNG umgewandelt – das Bildmodell bekommt ein echtes PNG
- T-0734 Gegenprüfung: Datei lesen (hier ein langsames pdftotext) läuft neben der Oberfläche; „Stopp“ bricht das Lesen in unter 3 s ab, Frage und Anhang bleiben
- T-0734 Gegenprüfung: während einer laufenden Antwort abgelegte Datei bleibt für die nächste Frage; nur die gesendeten Anhänge verschwinden
- T-0734: Antwort wächst sichtbar in der Sprechblase; Stopp beendet in unter 2 s und das Geschriebene bleibt mit „(abgebrochen)“ im Gespräch
- T-0734: Bild ohne Bildmodell → klare Meldung statt erfundener Antwort, Anhang bleibt; Hinweis sagt, dass Bilder auf dem PC bleiben
- T-0734: „Datei anhängen“ (Auswahl) → Karte mit Name und Hinweis „bleibt auf diesem PC“; Frage enthält den Dateitext, Gespräch zeigt 📎; falsche Art wird abgelehnt
