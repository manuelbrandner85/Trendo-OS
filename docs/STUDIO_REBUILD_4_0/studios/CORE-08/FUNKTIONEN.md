# Funktionsliste: Videos & Fotos bearbeiten

Studio CORE-08 · Videos & Fotos bearbeiten · Bereich Kreativ & Medien

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | umgesetzt | AudioEditor_Ui |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | umgesetzt | AudioEditor_Ui |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | umgesetzt | AudioEditor_Ui |
| AU-04 | Export WAV/MP3 | umgesetzt | AudioEditor_Ui |
| AU-05 | Rauschen nach Profil entfernen | umgesetzt | AudioDspTests |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | umgesetzt | AudioDspTests |
| AU-07 | Stille kürzen | umgesetzt | AudioDspTests |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | umgesetzt | AudioDspTests |
| AU-09 | Kompressor | umgesetzt | AudioDspTests |
| AU-10 | Spektrogramm-Ansicht | umgesetzt | AudioDspTests |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | umgesetzt | AudioEditor_Ui |
| AU-12 | Spektrales Retuschieren, Mehrspur-Projekt | umgesetzt | AudioPro_Ui, AudioMultiTests |
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | umgesetzt | Bildbearbeitung_Ui |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | umgesetzt | Bildbearbeitung_Ui |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | umgesetzt | ImageVectorTests |
| BI-04 | Tonwerte und Auto-Tonwerte | umgesetzt | ImageOpsTests |
| BI-05 | Kurven (Vorlagen, monoton) | umgesetzt | ImageOpsTests |
| BI-06 | Füllen mit Toleranz | umgesetzt | ImageOpsTests |
| BI-07 | Reparieren (Fleck aus der Umgebung) | umgesetzt | ImageOpsTests |
| BI-08 | Klonen (Kopierstempel) | umgesetzt | ImageOpsTests |
| BI-09 | Verlauf linear/kreisförmig | umgesetzt | Bildbearbeitung_Ui |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | umgesetzt | Bildbearbeitung_Ui |
| BI-11 | Lasso und Zauberstab, inhaltsbasiert füllen, frei ziehbare Kurvenpunkte | umgesetzt | Bildbearbeitung_Ui, ImageOpsTests |
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | umgesetzt | VideoSchnitt_Ui |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | umgesetzt | VideoEditTests |
| VI-03 | Überblenden und Titel | umgesetzt | VideoEditPlusTests |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | umgesetzt | VideoSchnittPlus_Ui |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | umgesetzt | VideoExportTests |
| VI-06 | EDL und FCPXML | umgesetzt | VideoExportTests |
| VI-07 | Ducking (Musik unter Sprache) | umgesetzt | VideoExportTests |
| VI-08 | Untertitel erzeugen und einbrennen | umgesetzt | VideoSubtitleTests |
| VI-09 | Tastatur J/K/L, I/O, Marken | umgesetzt | VideoSchnittPlus_Ui |
| VI-10 | Fehlende Dateien neu verknüpfen | umgesetzt | VideoSchnittPlus_Ui |
| VI-11 | Schnitte auf den Takt | umgesetzt | BeatDetectTests |
| VI-12 | Farbkorrektur je Stück | umgesetzt | VideoProTests |
| VI-13 | Tonpegel je Stück | umgesetzt | VideoSchnittPro_Ui |
| VI-14 | Übergangsarten und Titel mit Lage/Größe | umgesetzt | VideoProTests |
| VI-15 | Zweite Bildspur (Bild im Bild, Logo, Einblendung) | umgesetzt | VideoProTests |
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | umgesetzt | MediaEditTests |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | umgesetzt | MediaEditTests |
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | umgesetzt | AudioEditor_Ui |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | umgesetzt | AudioEditor_Ui |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | umgesetzt | AudioEditor_Ui |
| AU-04 | Export WAV/MP3 | umgesetzt | AudioEditor_Ui |
| AU-05 | Rauschen nach Profil entfernen | umgesetzt | AudioDspTests |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | umgesetzt | AudioDspTests |
| AU-07 | Stille kürzen | umgesetzt | AudioDspTests |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | umgesetzt | AudioDspTests |
| AU-09 | Kompressor | umgesetzt | AudioDspTests |
| AU-10 | Spektrogramm-Ansicht | umgesetzt | AudioDspTests |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | umgesetzt | AudioEditor_Ui |
| AU-12 | Spektrales Retuschieren, Mehrspur-Projekt | umgesetzt | AudioPro_Ui, AudioMultiTests |
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | umgesetzt | Bildbearbeitung_Ui |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | umgesetzt | Bildbearbeitung_Ui |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | umgesetzt | ImageVectorTests |
| BI-04 | Tonwerte und Auto-Tonwerte | umgesetzt | ImageOpsTests |
| BI-05 | Kurven (Vorlagen, monoton) | umgesetzt | ImageOpsTests |
| BI-06 | Füllen mit Toleranz | umgesetzt | ImageOpsTests |
| BI-07 | Reparieren (Fleck aus der Umgebung) | umgesetzt | ImageOpsTests |
| BI-08 | Klonen (Kopierstempel) | umgesetzt | ImageOpsTests |
| BI-09 | Verlauf linear/kreisförmig | umgesetzt | Bildbearbeitung_Ui |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | umgesetzt | Bildbearbeitung_Ui |
| BI-11 | Lasso und Zauberstab, inhaltsbasiert füllen, frei ziehbare Kurvenpunkte | umgesetzt | Bildbearbeitung_Ui, ImageOpsTests |
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | umgesetzt | VideoSchnitt_Ui |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | umgesetzt | VideoEditTests |
| VI-03 | Überblenden und Titel | umgesetzt | VideoEditPlusTests |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | umgesetzt | VideoSchnittPlus_Ui |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | umgesetzt | VideoExportTests |
| VI-06 | EDL und FCPXML | umgesetzt | VideoExportTests |
| VI-07 | Ducking (Musik unter Sprache) | umgesetzt | VideoExportTests |
| VI-08 | Untertitel erzeugen und einbrennen | umgesetzt | VideoSubtitleTests |
| VI-09 | Tastatur J/K/L, I/O, Marken | umgesetzt | VideoSchnittPlus_Ui |
| VI-10 | Fehlende Dateien neu verknüpfen | umgesetzt | VideoSchnittPlus_Ui |
| VI-11 | Schnitte auf den Takt | umgesetzt | BeatDetectTests |
| VI-12 | Farbkorrektur je Stück | umgesetzt | VideoProTests |
| VI-13 | Tonpegel je Stück | umgesetzt | VideoSchnittPro_Ui |
| VI-14 | Übergangsarten und Titel mit Lage/Größe | umgesetzt | VideoProTests |
| VI-15 | Zweite Bildspur (Bild im Bild, Logo, Einblendung) | umgesetzt | VideoProTests |
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | umgesetzt | MediaEditTests |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | umgesetzt | MediaEditTests |
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | umgesetzt | AudioEditor_Ui |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | umgesetzt | AudioEditor_Ui |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | umgesetzt | AudioEditor_Ui |
| AU-04 | Export WAV/MP3 | umgesetzt | AudioEditor_Ui |
| AU-05 | Rauschen nach Profil entfernen | umgesetzt | AudioDspTests |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | umgesetzt | AudioDspTests |
| AU-07 | Stille kürzen | umgesetzt | AudioDspTests |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | umgesetzt | AudioDspTests |
| AU-09 | Kompressor | umgesetzt | AudioDspTests |
| AU-10 | Spektrogramm-Ansicht | umgesetzt | AudioDspTests |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | umgesetzt | AudioEditor_Ui |
| AU-12 | Spektrales Retuschieren, Mehrspur-Projekt | umgesetzt | AudioPro_Ui, AudioMultiTests |
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | umgesetzt | Bildbearbeitung_Ui |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | umgesetzt | Bildbearbeitung_Ui |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | umgesetzt | ImageVectorTests |
| BI-04 | Tonwerte und Auto-Tonwerte | umgesetzt | ImageOpsTests |
| BI-05 | Kurven (Vorlagen, monoton) | umgesetzt | ImageOpsTests |
| BI-06 | Füllen mit Toleranz | umgesetzt | ImageOpsTests |
| BI-07 | Reparieren (Fleck aus der Umgebung) | umgesetzt | ImageOpsTests |
| BI-08 | Klonen (Kopierstempel) | umgesetzt | ImageOpsTests |
| BI-09 | Verlauf linear/kreisförmig | umgesetzt | Bildbearbeitung_Ui |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | umgesetzt | Bildbearbeitung_Ui |
| BI-11 | Lasso und Zauberstab, inhaltsbasiert füllen, frei ziehbare Kurvenpunkte | umgesetzt | Bildbearbeitung_Ui, ImageOpsTests |
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | umgesetzt | VideoSchnitt_Ui |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | umgesetzt | VideoEditTests |
| VI-03 | Überblenden und Titel | umgesetzt | VideoEditPlusTests |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | umgesetzt | VideoSchnittPlus_Ui |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | umgesetzt | VideoExportTests |
| VI-06 | EDL und FCPXML | umgesetzt | VideoExportTests |
| VI-07 | Ducking (Musik unter Sprache) | umgesetzt | VideoExportTests |
| VI-08 | Untertitel erzeugen und einbrennen | umgesetzt | VideoSubtitleTests |
| VI-09 | Tastatur J/K/L, I/O, Marken | umgesetzt | VideoSchnittPlus_Ui |
| VI-10 | Fehlende Dateien neu verknüpfen | umgesetzt | VideoSchnittPlus_Ui |
| VI-11 | Schnitte auf den Takt | umgesetzt | BeatDetectTests |
| VI-12 | Farbkorrektur je Stück | umgesetzt | VideoProTests |
| VI-13 | Tonpegel je Stück | umgesetzt | VideoSchnittPro_Ui |
| VI-14 | Übergangsarten und Titel mit Lage/Größe | umgesetzt | VideoProTests |
| VI-15 | Zweite Bildspur (Bild im Bild, Logo, Einblendung) | umgesetzt | VideoProTests |
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | umgesetzt | MediaEditTests |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | umgesetzt | MediaEditTests |
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | umgesetzt | AudioEditor_Ui |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | umgesetzt | AudioEditor_Ui |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | umgesetzt | AudioEditor_Ui |
| AU-04 | Export WAV/MP3 | umgesetzt | AudioEditor_Ui |
| AU-05 | Rauschen nach Profil entfernen | umgesetzt | AudioDspTests |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | umgesetzt | AudioDspTests |
| AU-07 | Stille kürzen | umgesetzt | AudioDspTests |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | umgesetzt | AudioDspTests |
| AU-09 | Kompressor | umgesetzt | AudioDspTests |
| AU-10 | Spektrogramm-Ansicht | umgesetzt | AudioDspTests |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | umgesetzt | AudioEditor_Ui |
| AU-12 | Spektrales Retuschieren, Mehrspur-Projekt | umgesetzt | AudioPro_Ui, AudioMultiTests |
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | umgesetzt | Bildbearbeitung_Ui |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | umgesetzt | Bildbearbeitung_Ui |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | umgesetzt | ImageVectorTests |
| BI-04 | Tonwerte und Auto-Tonwerte | umgesetzt | ImageOpsTests |
| BI-05 | Kurven (Vorlagen, monoton) | umgesetzt | ImageOpsTests |
| BI-06 | Füllen mit Toleranz | umgesetzt | ImageOpsTests |
| BI-07 | Reparieren (Fleck aus der Umgebung) | umgesetzt | ImageOpsTests |
| BI-08 | Klonen (Kopierstempel) | umgesetzt | ImageOpsTests |
| BI-09 | Verlauf linear/kreisförmig | umgesetzt | Bildbearbeitung_Ui |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | umgesetzt | Bildbearbeitung_Ui |
| BI-11 | Lasso und Zauberstab, inhaltsbasiert füllen, frei ziehbare Kurvenpunkte | umgesetzt | Bildbearbeitung_Ui, ImageOpsTests |
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | umgesetzt | VideoSchnitt_Ui |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | umgesetzt | VideoEditTests |
| VI-03 | Überblenden und Titel | umgesetzt | VideoEditPlusTests |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | umgesetzt | VideoSchnittPlus_Ui |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | umgesetzt | VideoExportTests |
| VI-06 | EDL und FCPXML | umgesetzt | VideoExportTests |
| VI-07 | Ducking (Musik unter Sprache) | umgesetzt | VideoExportTests |
| VI-08 | Untertitel erzeugen und einbrennen | umgesetzt | VideoSubtitleTests |
| VI-09 | Tastatur J/K/L, I/O, Marken | umgesetzt | VideoSchnittPlus_Ui |
| VI-10 | Fehlende Dateien neu verknüpfen | umgesetzt | VideoSchnittPlus_Ui |
| VI-11 | Schnitte auf den Takt | umgesetzt | BeatDetectTests |
| VI-12 | Farbkorrektur je Stück | umgesetzt | VideoProTests |
| VI-13 | Tonpegel je Stück | umgesetzt | VideoSchnittPro_Ui |
| VI-14 | Übergangsarten und Titel mit Lage/Größe | umgesetzt | VideoProTests |
| VI-15 | Zweite Bildspur (Bild im Bild, Logo, Einblendung) | umgesetzt | VideoProTests |
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | umgesetzt | MediaEditTests |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | umgesetzt | MediaEditTests |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Plugins (VST/LV2) – bewusst nicht, keine fremden Programme laden

## Teilweise vorhanden

- ◐ Generative Füllung per KI (Photoshop Generative Fill) – nur über die freie KI „Bilder verbessern“
