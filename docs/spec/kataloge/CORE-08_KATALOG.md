# CORE-08 – Videos & Fotos bearbeiten: Funktionskatalog

Funktionen nach den Vorbildern (Audacity, Adobe Audition, iZotope RX; GIMP, Krita, Photoshop; Kdenlive, Shotcut, DaVinci Resolve; VLC – nur Funktionen, Clean-Room), Stand 04.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

## Audio bearbeiten

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | Audacity | umgesetzt | AudioEditor_Ui |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | Audacity | umgesetzt | AudioEditor_Ui |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | Audition | umgesetzt | AudioEditor_Ui |
| AU-04 | Export WAV/MP3 | Audacity | umgesetzt | AudioEditor_Ui |
| AU-05 | Rauschen nach Profil entfernen | Audacity Noise Reduction, RX De-noise | umgesetzt | AudioDspTests |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | RX De-hum | umgesetzt | AudioDspTests |
| AU-07 | Stille kürzen | Audacity Truncate Silence, Descript | umgesetzt | AudioDspTests |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | Audition Match Loudness | umgesetzt | AudioDspTests |
| AU-09 | Kompressor | Audition Dynamics | umgesetzt | AudioDspTests |
| AU-10 | Spektrogramm-Ansicht | RX Spectral Editor | umgesetzt | AudioDspTests |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | Audacity, RX | umgesetzt | AudioEditor_Ui |
| AU-12 | Spektrales Retuschieren | RX | offen | – |

## Bildbearbeitung

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | Photoshop, GIMP | umgesetzt | Bildbearbeitung_Ui |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | GIMP | umgesetzt | Bildbearbeitung_Ui |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | Krita, Inkscape | umgesetzt | ImageVectorTests |
| BI-04 | Tonwerte und Auto-Tonwerte | Photoshop | umgesetzt | ImageOpsTests |
| BI-05 | Kurven (Vorlagen, monoton) | Photoshop, GIMP | umgesetzt | ImageOpsTests |
| BI-06 | Füllen mit Toleranz | GIMP Farbeimer | umgesetzt | ImageOpsTests |
| BI-07 | Reparieren (Fleck aus der Umgebung) | Photoshop Bereichsreparatur | umgesetzt | ImageOpsTests |
| BI-08 | Klonen (Kopierstempel) | Photoshop, GIMP | umgesetzt | ImageOpsTests |
| BI-09 | Verlauf linear/kreisförmig | Photoshop, GIMP | umgesetzt | Bildbearbeitung_Ui |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | Photoshop, Krita | umgesetzt | Bildbearbeitung_Ui |
| BI-11 | Lasso und Zauberstab | Photoshop, GIMP | offen | – |

## Videoschnitt

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | Kdenlive, Shotcut | umgesetzt | VideoSchnitt_Ui |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | Kdenlive | umgesetzt | VideoEditTests |
| VI-03 | Überblenden und Titel | Shotcut | umgesetzt | VideoEditPlusTests |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | Resolve Render Queue | umgesetzt | VideoSchnittPlus_Ui |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | Resolve Deliver | umgesetzt | VideoExportTests |
| VI-06 | EDL und FCPXML | Resolve | umgesetzt | VideoExportTests |
| VI-07 | Ducking (Musik unter Sprache) | Resolve Fairlight | umgesetzt | VideoExportTests |
| VI-08 | Untertitel erzeugen und einbrennen | Resolve, Kdenlive | umgesetzt | VideoSubtitleTests |
| VI-09 | Tastatur J/K/L, I/O, Marken | Resolve, Premiere | umgesetzt | VideoSchnittPlus_Ui |
| VI-10 | Fehlende Dateien neu verknüpfen | Resolve Relink | umgesetzt | VideoSchnittPlus_Ui |
| VI-11 | Schnitte auf den Takt | Premiere Beat Edit | umgesetzt | BeatDetectTests |
| VI-12 | Farbkorrektur je Stück | Resolve Color | offen | – |

## Medien bearbeiten

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | Windows Fotos | umgesetzt | MediaEditTests |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | VLC, Shotcut | umgesetzt | MediaEditTests |
