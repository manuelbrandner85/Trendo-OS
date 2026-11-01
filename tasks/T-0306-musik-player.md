# T-0306 – Medien: Musik & Videos als Player

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Musik & Videos als Player“).
- Vorher: Liste der 40 neuesten Musik-/Videodateien, Abspielen nur extern.
- Jetzt (`MainWindow.MusicPlayer.cs`, `MpvAudio`, Kern `Trendo.Core.Media.MusicLibrary`, `Playlists`, `PlayQueue`):
  - Bibliothek aus „Musik“, „Videos“ und dem Projektordner (im Hintergrund eingelesen); Tags aus ID3v1, ID3v2.3/2.4 (Latin1, UTF-16, UTF-8) und FLAC/Vorbis, sonst aus „Künstler/Album/01 Titel“ bzw. „Künstler - Titel“.
  - Alben als Cover-Raster (eingebettetes Bild, cover.jpg/folder.jpg im Ordner, sonst Verlauf mit Initialen), Albumseite mit großem Cover, Abspielen/Zufällig/Warteschlange.
  - Alle Titel mit Suche, Künstler, Videos (öffnen im Player-Fenster).
  - Wiedergabelisten anlegen, Titel hinzufügen („＋ Liste“), Einträge entfernen, Liste löschen.
  - Player-Leiste: Cover, Titel, ⏮ ⏯ ⏭, Fortschritt mit Zeit (Klick/Ziehen springt), Zufall, Wiederholen (aus/alle/eins), Lautstärke, Warteschlange (anspringen, nach oben, entfernen), „Als Nächstes“.
  - Ton: mpv ohne Bild im Hintergrund über JSON-IPC; Position, Länge und Titelende kommen vom Player. Fehlt mpv, bietet die Leiste „Player einrichten“ (gleicher Weg wie TV).
- Vorschaubild: Render-Schalter `TRENDO_MUSIK=1` bzw. `=album`.
- Tests: `MusicLibraryTests` (Tags aller Formate, Cover, Pfad-Rückfall, Alben, Suche, Warteschlange, Listen), `MusicPlayer_Ui` (echte Klicks mit Ton-Attrappe: Album, Abspielen, Fortschritt, Pause, Titelende → nächster, Wiederholen, Springen, Lautstärke, Warteschlange, Liste), `MpvAudioTests` (echter mpv: Länge, Pause, Springen, Titelende genau einmal – übersprungen, wenn mpv fehlt); `Fotoalbum_Ui`, `ProgramFrameTests` grün.
