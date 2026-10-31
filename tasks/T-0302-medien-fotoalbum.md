# T-0302 – Medien als Fotoalbum

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Medien als Fotoalbum“).
- Vorher: Medien zeigte nur eine Liste der 40 neuesten Musik- und Videodateien; Fotos gab es nur als Einzelwerkzeuge (Betrachter, „Fotos ordnen“).
- Jetzt: Medien hat die Bereiche „🖼 Fotos“ und „🎵 Musik & Videos“ (`MainWindow.Fotoalbum.cs`, Kern `Trendo.Core.Sys.PhotoAlbum`, nutzt vorhandenes EXIF-Lesen, Papierkorb und Bildbearbeitung):
  - Fotos aus „Bilder“, dem Projektordner und selbst aufgenommenen Ordnern, im Hintergrund eingelesen; Aufnahmedatum aus EXIF, sonst Dateidatum.
  - Miniaturen nach Monat gruppiert, Größe per Regler, Miniaturen werden im Hintergrund geladen.
  - Klick wählt, Strg-Klick mehrere, Umschalt-Klick einen Bereich; Auswahl zu Favoriten oder in ein Album, aus Album entfernen.
  - Alben anlegen, Favoriten immer oben.
  - Doppelklick öffnet groß: ← → blättern, Esc schließt; echte Bildgröße und Megapixel, Ordner, Datum; ★, „In Bildbearbeitung öffnen“, „Im Ordner zeigen“.
  - Papierkorb nur nach „Ja“ (einzeln oder alle Kopien der Doppelten); Alben vergessen gelöschte Fotos.
  - Doppelte nach Inhalt (SHA-256), das älteste bleibt; zeigt, wie viel Platz frei würde.
- **Fehler vermieden:** Auswahl ändert die Ansicht ohne Neuaufbau, sonst käme der Doppelklick nie an (gleiche Ursache wie T-0298).
- Vorschaubild: Render-Schalter `TRENDO_FOTOS=1` bzw. `=gross`.
- Tests: `PhotoAlbumTests` (Einlesen mit EXIF, versteckte Ordner, Monate, Doppelte, Alben), `Fotoalbum_Ui` (echte Maus: Klick, Strg-Klick, Doppelklick; Pfeiltasten, Esc; Album, Favoriten; Doppelte und Papierkorb mit Nein/Ja); `MediaEditTests`, `OfficeNetzFeedbackTests`, `ProgramFrameTests`, `W4_Core08_Ui` grün.
