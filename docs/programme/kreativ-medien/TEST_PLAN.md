# Medien bearbeiten – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.114 (Stand 99cbb5c)

14 Tests sprechen dieses Programm an.

- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe)
- T-0205 (UI): Bild wählen → Bearbeiten-Bereich, Speichern zeigt Ergebnis
- T-0205 (UI): Video → Länge, Zeitleiste mit Bildern, Ausschnitt Start–Ende, zwei Clips aneinanderhängen
- T-0715 Medien bearbeiten (Bild): Helligkeit und Text – Rückgängig mehrere Schritte, Notsicherung, zweites Fenster nichts, Karte bringt exakt den Stand, „Bild speichern“ schreibt das echte Bild und räumt weg
- T-0715 Medien bearbeiten (Video, echtes FFmpeg): Clipliste und Ausschnitt – Rückgängig, Notsicherung, Karte, „Ausschnitt speichern“ schneidet echt und atomar
- T-0732 Gegenprüfung Medien (UI): Fenster schließen bricht den laufenden Stapel ab – FFmpeg endet, keine Zwischendatei im Ausgabeordner
- T-0732 Medien (UI): drei Videos und ein Bild auf die Werkbank ziehen, „Ton herausholen“, Ausgabeordner wählen, Start – Fortschritt je Datei, MP3s im Ordner
- T-0732 Medien (UI): „Abbrechen“ hält den Stapel mitten in einer Datei an – keine halbe Datei im Ausgabeordner
