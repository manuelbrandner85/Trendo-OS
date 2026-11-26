# Medien bearbeiten – Testergebnisse

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 14 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei | ✓ bestanden |
| CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen | ✓ bestanden |
| CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt | ✓ bestanden |
| CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich | ✓ bestanden |
| CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben | ✓ bestanden |
| CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell | ✓ bestanden |
| G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe) | ✓ bestanden |
| T-0205 (UI): Bild wählen → Bearbeiten-Bereich, Speichern zeigt Ergebnis | ✓ bestanden |
| T-0205 (UI): Video → Länge, Zeitleiste mit Bildern, Ausschnitt Start–Ende, zwei Clips aneinanderhängen | ✓ bestanden |
| T-0715 Medien bearbeiten (Bild): Helligkeit und Text – Rückgängig mehrere Schritte, Notsicherung, zweites Fenster nichts, Karte bringt exakt den Stand, „Bild speichern“ schreibt das echte Bild und räumt weg | ✓ bestanden |
| T-0715 Medien bearbeiten (Video, echtes FFmpeg): Clipliste und Ausschnitt – Rückgängig, Notsicherung, Karte, „Ausschnitt speichern“ schneidet echt und atomar | ✓ bestanden |
| T-0732 Gegenprüfung Medien (UI): Fenster schließen bricht den laufenden Stapel ab – FFmpeg endet, keine Zwischendatei im Ausgabeordner | ✓ bestanden |
| T-0732 Medien (UI): drei Videos und ein Bild auf die Werkbank ziehen, „Ton herausholen“, Ausgabeordner wählen, Start – Fortschritt je Datei, MP3s im Ordner | ✓ bestanden |
| T-0732 Medien (UI): „Abbrechen“ hält den Stapel mitten in einer Datei an – keine halbe Datei im Ausgabeordner | ✓ bestanden |
