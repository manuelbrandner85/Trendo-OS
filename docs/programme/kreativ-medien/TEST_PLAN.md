# Medien bearbeiten – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.36 (Stand 8b5609f)

14 Tests sprechen dieses Programm an.

- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08 Videoschnitt Profi: Look Schwarzweiß, Ton 50 %, Übergang Wischen, Titel oben – als ein Schritt rückgängig; Logo auf die zweite Bildspur, Ecke unten links, entfernen
- CORE-08 Videoschnitt echt mit FFmpeg: Schwarzweiß auf Stück 1 macht die Pixel grau, Stück 2 bleibt farbig, Logo auf der zweiten Spur erscheint, Länge stimmt
- CORE-08 Videoschnitt: Farbe, Tonpegel, Übergang und Titellage je Stück landen im Export-Befehl; alte Projekte ohne Zusatzwerte bleiben gleich
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- G3: Fachstudio zeigt keine Reiter fremder Studios; G10: keine Schrift unter 12 pt auf der Seite
- G6: Dateifeld zeigt Vorschau der gewählten Datei (Name, Größe)
- G7: Ergebnis-Karte mit Kopieren und Verlauf früherer Ergebnisse; G12: Benachrichtigung erscheint
- G8: Strg+Q öffnet „Was möchtest du tun?“, findet Studios und Knöpfe der Seite und führt sie aus; G9/G13: F11 Fokusmodus
- T-0205 (UI): Bild wählen → Bearbeiten-Bereich, Speichern zeigt Ergebnis
- T-0205 (UI): Video → Länge, Zeitleiste mit Bildern, Ausschnitt Start–Ende, zwei Clips aneinanderhängen
- T-0205: Clipliste maskiert Anführungszeichen, Vorschaubild-Aufruf ist korrekt
- T-0205: Zuschneiden + Drehen ergibt richtige Größe, Helligkeit ändert Pixel, Text wird gezeichnet, JPG/PNG
