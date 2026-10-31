# T-0292 – Videoschnitt: Abspielen, Überblenden, Texteinblendungen, Export im Hintergrund

- Freigabe: Manuel im Chat am 31.10.2026 (Auswahl „Videoschnitt ausbauen“).
- **Abspielen im Programm** (▶ / Leertaste): 6 Bilder je Sekunde; Vorschaubilder je Stück aus einem einzigen FFmpeg-Lauf, zwischengespeichert; Abspielkopf, Zeit und Titel laufen mit, die Seite wird dabei nicht neu aufgebaut. Ton spielt im Programm nicht (ehrlich: nur Bild).
- **Überblenden** ins nächste Stück (0–5 s, begrenzt auf die Stücklänge): Länge und Startzeiten berücksichtigen die Überlappung; Zeitleiste zeigt einen Keil mit Dauer; Export mit `xfade` (Bild) und `acrossfade` (Ton), Stücke ohne Überblendung werden angehängt.
- **Texteinblendung** je Stück: in der Vorschau unten mittig mit Schatten; Export mit `drawtext` (Arial/Segoe UI bzw. DejaVu Sans, Text sicher maskiert).
- **Export im Hintergrund** (⬇ Video exportieren): Fortschritt aus `-progress`, Balken rechts, „Export abbrechen“ beendet nur den eigenen FFmpeg-Prozess und löscht die halbe Datei; man kann weiterarbeiten. Der bisherige Sofort-Export bleibt als „Video (MP4) sofort“.
- Tests: Kern `VideoEditPlusTests` (Längen/Startzeiten/Aufruf; **mit echtem FFmpeg**: Überblenden + Titel exportiert 5 s mit Ton, Fortschritt bis 100 %, Abspielbilder, Abbrechen löscht die halbe Datei); App `VideoSchnittPlus_Ui` (Überblenden/Titel setzen, Vorschau-Titel, Abspielen, Fortschritt, Abbrechen); 102 Querschnitt-/Programmtests und 74 Kerntests grün.
