# STUDIO-MUSIK-01 – Trendo Musik: Funktionskatalog

Funktionen nach den Vorbildern (Suno, Udio, ElevenLabs Music, Stable Audio, AIVA, Moises, LANDR – nur Funktionen, Clean-Room), Stand 04.11.2026.
„Stand“: umgesetzt = im Code mit Test; „Test“ nennt die Testklasse.

| Nr | Funktion | Vorbild | Stand | Test |
|---|---|---|---|---|
| MU-01 | Lied aus einem Satz (Einfach-Modus) | Suno | umgesetzt | MusikStudio_Ui |
| MU-02 | Profi-Modus mit Stil, Text, Tempo, Tonart, Takt | Suno Custom | umgesetzt | MusikStudio_Ui |
| MU-03 | Abschnitt neu erzeugen | Suno Replace, Udio Inpainting | umgesetzt | MusikExtendUiTests |
| MU-04 | Verlängern | Suno Extend | umgesetzt | MusikExtendUiTests |
| MU-05 | Cover in anderem Stil | Suno Cover, Udio Remix | umgesetzt | MusikCoverUiTests |
| MU-06 | Spuren trennen (Gesang, Schlagzeug, Bass, Rest) | Suno Stems, Moises | umgesetzt | MusikSpurenUiTests |
| MU-07 | Mitsingen mit Trefferanzeige | Moises | umgesetzt | MusikMitsingenUiTests |
| MU-08 | Klang-Referenz und Persona | Suno Inspo/Persona | umgesetzt | MusikProWerkzeuge_Ui |
| MU-09 | Ablaufplan je Abschnitt | ElevenLabs composition plan | umgesetzt | MusikProWerkzeuge_Ui |
| MU-10 | Mastering-Stile und Ziel je Plattform | LANDR | umgesetzt | MusicProTests |
| MU-11 | Mastern nach Referenz | LANDR Reference | umgesetzt | MusicProTests |
| MU-12 | Üben: Tempo, Tonart, Klick, Kapo | Moises | umgesetzt | MusicProTests |
| MU-13 | MIDI-Export Melodie und Akkorde | AIVA | umgesetzt | MusicProTests |
| MU-14 | Nahtlose Loops | Stable Audio | umgesetzt | MusicProTests |
| MU-15 | Kurzclip aus dem Refrain | Suno Hooks | umgesetzt | MusicProTests |
| MU-16 | Cover nach eigenem Original mit Ampel | Suno Cover | umgesetzt | MusikCoverWerkstatt_Ui |
| MU-17 | Eigene Stimme (nur live, Zustimmung) | Suno Voice, Kits.ai | umgesetzt | MusikCoverWerkstatt_Ui |
| MU-18 | Cover-Bild mit Titel | Suno | umgesetzt | MusikCoverUiTests |
| MU-19 | Begleitung zu eigenem Gesang | Suno Add Instrumental | offen (braucht XL-Modell, 24 GB) | – |
| MU-20 | Instrument dazu erzeugen | Suno Studio | offen (braucht XL-Modell, 24 GB) | – |
| MU-21 | Mitlaufender Liedtext beim Abspielen | Spotify, Apple Music | umgesetzt | MusikProfi26_Ui, SongFollowTests |
| MU-22 | Tonart- und Tempo-Raster mit Takten | Ableton, Logic | umgesetzt | MusikProfi26_Ui, SongFollowTests |
| MU-23 | Fassungen nebeneinander, A/B an derselben Stelle | Mastering-A/B | umgesetzt | MusikProfi26_Ui, SongFollowTests |
