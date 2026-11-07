# Medien bearbeiten – Umsetzungsstand

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `kreativ-medien` · Stand: 0.9.36 (Stand 2963379)

Stufen: Geplant · In Arbeit · Implementiert · Getestet · Blockiert. „Getestet“ nur, wenn der zugehörige Test im letzten echten Lauf bestanden hat.

| Nr | Funktion | Stand | Test |
|---|---|---|---|
| AU-01 | Wellenform, Auswahl mit der Maus, Schneiden/Kopieren/Einfügen/Löschen | Implementiert | `AudioEditor_Ui` |
| AU-02 | Normalisieren, Ein-/Ausblenden, Lauter/Leiser | Implementiert | `AudioEditor_Ui` |
| AU-03 | Schritte-Liste, Rückgängig, Original bleibt unverändert | Implementiert | `AudioEditor_Ui` |
| AU-04 | Export WAV/MP3 | Implementiert | `AudioEditor_Ui` |
| AU-05 | Rauschen nach Profil entfernen | Implementiert | `AudioDspTests` |
| AU-06 | Brummen 50 Hz mit Oberwellen entfernen | Implementiert | `AudioDspTests` |
| AU-07 | Stille kürzen | Implementiert | `AudioDspTests` |
| AU-08 | Lautheit −16/−14 LUFS (BS.1770, wie FFmpeg ebur128) | Implementiert | `AudioDspTests` |
| AU-09 | Kompressor | Implementiert | `AudioDspTests` |
| AU-10 | Spektrogramm-Ansicht | Implementiert | `AudioDspTests` |
| AU-11 | Reparieren in der Oberfläche, Aufnahme an der Marke | Implementiert | `AudioEditor_Ui` |
| AU-12 | Spektrales Retuschieren, Mehrspur-Projekt | Implementiert | `AudioPro_Ui, AudioMultiTests` |
| BI-01 | Ebenen, Pinsel, Formen, Text, Verschieben, Zuschneiden, Export PNG | Implementiert | `Bildbearbeitung_Ui` |
| BI-02 | Filter (Helligkeit, Kontrast, Schwarzweiß …) | Implementiert | `Bildbearbeitung_Ui` |
| BI-03 | Vektorpinsel, Mischmodi, SVG speichern/öffnen | Implementiert | `ImageVectorTests` |
| BI-04 | Tonwerte und Auto-Tonwerte | Implementiert | `ImageOpsTests` |
| BI-05 | Kurven (Vorlagen, monoton) | Implementiert | `ImageOpsTests` |
| BI-06 | Füllen mit Toleranz | Implementiert | `ImageOpsTests` |
| BI-07 | Reparieren (Fleck aus der Umgebung) | Implementiert | `ImageOpsTests` |
| BI-08 | Klonen (Kopierstempel) | Implementiert | `ImageOpsTests` |
| BI-09 | Verlauf linear/kreisförmig | Implementiert | `Bildbearbeitung_Ui` |
| BI-10 | Auswahl und Ebenenmaske (eckig/rund, weiche Kante, umkehren) | Implementiert | `Bildbearbeitung_Ui` |
| BI-11 | Lasso und Zauberstab, inhaltsbasiert füllen, frei ziehbare Kurvenpunkte | Implementiert | `Bildbearbeitung_Ui, ImageOpsTests` |
| VI-01 | Spuren, Stücke ziehen, kürzen, teilen, löschen | Implementiert | `VideoSchnitt_Ui` |
| VI-02 | Schnittmodell (Länge, Teilen, Verschieben) | Implementiert | `VideoEditTests` |
| VI-03 | Überblenden und Titel | Implementiert | `VideoEditPlusTests` |
| VI-04 | Export im Hintergrund mit Fortschritt, Abbrechen | Implementiert | `VideoSchnittPlus_Ui` |
| VI-05 | Export-Vorlagen, Hochformat, Lautheit, NVENC | Implementiert | `VideoExportTests` |
| VI-06 | EDL und FCPXML | Implementiert | `VideoExportTests` |
| VI-07 | Ducking (Musik unter Sprache) | Implementiert | `VideoExportTests` |
| VI-08 | Untertitel erzeugen und einbrennen | Implementiert | `VideoSubtitleTests` |
| VI-09 | Tastatur J/K/L, I/O, Marken | Implementiert | `VideoSchnittPlus_Ui` |
| VI-10 | Fehlende Dateien neu verknüpfen | Implementiert | `VideoSchnittPlus_Ui` |
| VI-11 | Schnitte auf den Takt | Implementiert | `BeatDetectTests` |
| VI-12 | Farbkorrektur je Stück | Implementiert | `VideoProTests` |
| VI-13 | Tonpegel je Stück | Implementiert | `VideoSchnittPro_Ui` |
| VI-14 | Übergangsarten und Titel mit Lage/Größe | Implementiert | `VideoProTests` |
| VI-15 | Zweite Bildspur (Bild im Bild, Logo, Einblendung) | Implementiert | `VideoProTests` |
| ME-01 | Bild zuschneiden, drehen, Helligkeit, Text, JPG/PNG | Implementiert | `MediaEditTests` |
| ME-02 | Video Ausschnitt, Clips aneinanderhängen, Vorschaubilder | Implementiert | `MediaEditTests` |

0 von 40 Funktionen getestet.
