# Vorbild-Analyse Trendo Musik: Musik-KI-Dienste wie Suno und Udio (Clean-Room)

- Programme: „Trendo Musik“
- Vorbilder: Suno, Udio, ElevenLabs Music, Google Lyria/ProducerAI, Stable Audio, AIVA, Soundraw, Moises, LANDR (nur Funktionen aus öffentlicher Darstellung, auch kostenpflichtige) – Erzeugung lokal mit ACE-Step 1.5 (MIT); Recherche 04.11.2026 in docs/programme/MUSIK-KI-RECHERCHE.md
- Werkzeug: Bedienkonzept aus öffentlicher Doku; kein fremder Code, keine fremden Grafiken, Namen oder Texte. Funktionen von ACE-Step am 03.11.2026 auf einer RTX 5070 (12 GB) echt erprobt.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Einfach-Modus: ein Satz → Lied mit Gesang (Text schreibt die Text-KI von Trendo, sonst ACE-Step selbst – dann wählt es die Länge)
- ✓ Profi-Modus: Stil, eigener Liedtext mit Abschnitten, Titel, Instrumental
- ✓ Zwei Varianten je Auftrag (erprobt: 32 s für 2 × 30 s)
- ✓ 50 Gesangssprachen (erprobt: Spanisch)
- ✓ Länge bis 10 Minuten (erprobt: über 8 Minuten ohne planendes Sprachmodell, sonst gekürzt)
- ✓ Tempo, Tonart, Takt als eigene Felder – nachgemessen 03.11.2026: A-Moll/3-4 und E-Dur/4-4 genau, 140 BPM → 143,6, 90 → 92,3 (Tempo im Stiltext wird ignoriert)
- ✓ Über 180 Genres in 11 Gruppen mit Suche und Tempo-Vorschlag (Begriffe aus ACE-Steps eigener Genre-Liste), 14 Gesangsarten (Duett, Chor, Rap + Gesang …), Tonlage, Instrumente
- ✓ Mastering wie für Streamingdienste nach jedem Lied (−14 LUFS, −1 dBTP; 03.11.2026 gemessen: ACE-Step liefert −13,4 LUFS/−0,7 dBTP/LRA 15,7 → −14,0/−1,0/10,7)
- ✓ Liedtexte mit Songwriter-Regeln, Prüfung auf KI-Floskeln, gefundene ersetzt die Text-KI gezielt
- ✓ YouTube-Link als Vorlage: Titel/Interpret per oEmbed, Stil/Tonart/Tempo aus freien Daten – kein Download (YouTube-Bedingungen, § 95a UrhG), kein fremder Liedtext
- ✓ Meine Stimme (S1): 20 s singen → Umfang, bequemer Bereich, Stimmlage, Klangfarbe/Hauch (Schätzung), passende Tonarten; „Für mein nächstes Lied“
- ✓ Mitsingen (S5) über die Begleitung mit Live-Treffer-Anzeige (YIN, Oktave zählt), Aufnahme mischen (S6) mit Versatz, Hall, Lautstärke, danach Mastern
- ✓ Musikvideo: Shotliste aus Abschnitten/Energie/Takt, Schnitt auf Taktgrenzen (Refrain schneller), Kamerafahrt je Teil, Film-Looks als eigene 3D-LUT, Korn, Vignette, Breitbild-Balken, Liedtext eingeblendet (Zeiten aus der Gesangsspur, Schätzung), 16:9 und 9:16, Clips eigene oder Pexels; KI-Clips (Wan 2.2) und lippensynchroner Sänger folgen nach der Messung
- ✓ „Im Stil von“: aus dem Web nachgeschlagen (MusicBrainz CC0: Genres, Solo/Band, Gründungsjahrzehnt; Wikipedia: Kurzbeschreibung mit Quelle), die Text-KI verfeinert darauf – ohne Namen und ohne nachgemachte Stimme
- ✓ Song-Analyse (librosa): Tonart, Tempo, Takt, Akkorde, Tonumfang – als Vorlage übernehmbar
- ✓ Eigene Aufnahme hinzufügen und als Cover neu einspielen (Hinweis: nur privat, GEMA)
- ◐ Tonart/Tempo bekannter Songs aus AcousticBrainz (CC0) über MusicBrainz – der Dienst läuft nur noch als Archiv und antwortet nicht immer
- ✓ Text-Werkzeuge: eingefügten Text gliedern, Silben je Zeile, Reime (Wörterbuch), Refrain wiederholen, Refrain eingängiger (KI)
- ✓ Abschnitt neu erzeugen (erprobt: nur der gewählte Bereich ändert sich)
- ✓ Cover in anderem Stil
- ✓ Liedtext mit KI (Claude, wenn verbunden)
- ✓ Bibliothek mit Suche, Favoriten, Umbenennen, Löschen in den Papierkorb, Export WAV/FLAC, Übergabe an den Videoschnitt
- ✓ Cover-Bilder: Motiv aus der Bild-KI (SDXL Turbo) zu Genre und Stimmung, Titel und Name mit echter Schrift, 4 Layouts, 1400 × 1400, in die MP3 eingebettet
- ✓ Verlängern mit dem Grundmodell (03.11.2026 auf der RTX 5070 echt geprüft: 30 → 60 s in 9 s, Anfang unverändert, Fortsetzung schließt an); Turbo und Grundmodell zugleich passen nicht in 12 GB – Trendo lädt jeweils eins
- ✓ Spuren trennen mit Demucs (MIT): Gesang, Schlagzeug, Bass, Rest und Begleitung (03.11.2026 echt geprüft: 3:26 Minuten in 24 s, Summe = Original mit 30 dB, Übersprechen Gesang/Begleitung 0,02); ACE-Step selbst trennt nachgemessen nicht
- ✗ Begleitung zu Gesang erzeugen (ACE-Step „complete“ – 04.11.2026 auch mit dem Grundmodell nachgemessen: folgt dem Gesang nicht, 0,06; braucht das XL-Modell ab 24 GB)
- ✗ Instrument dazu erzeugen (ACE-Step „lego“, nachgemessen: Streicher folgen den Akkorden nicht, 0,40 = Zufall)
- ✓ Klang-Referenz und Persona (Suno „Inspo“): Datei oder eigenes Lied als Klangvorbild – gemessen: Klangabstand 101 statt 139
- ✓ Ablaufplan je Abschnitt (ElevenLabs „composition plan“): Teile mit Takten und Stimmung → Textgerüst und Länge
- ✓ Mastern wie LANDR: Stile warm/ausgewogen/offen, Ziel je Plattform (−8 bis −16 LUFS), Angleichen an ein Referenzlied (7 Bänder, ±6 dB)
- ✓ Üben wie Moises: Tempo 50–150 % ohne Tonhöhe, Tonart ±6 Halbtöne (Rubber Band), Klick im erkannten Takt, Kapodaster-Vorschlag
- ✓ MIDI-Export wie AIVA: Melodie und Akkorde als Standard-MIDI-Datei
- ✓ Nahtlose Loops wie Stable Audio (Takte im Tempo, Überblendung ohne Knacken)
- ✓ Kurzclip wie Suno Hooks: stärkster Abschnitt als Hochkant-Video 1080 × 1920 mit Titelbild und Wellenform
- ✓ Cover-Werkstatt M1–M8: Lied suchen, eigenes Original untersuchen, Cover mit gleicher Melodie (gemessen 55 % auf der Gesangsspur), Ampel, erfundene Epochen-Stimmen, Klang wie damals, Karaoke, eigene Stimme (nur live, mit Zustimmung)
- ✓ Mitlaufender Liedtext beim Abspielen (05.11.2026, wie Spotify/Apple Music): laufende Zeile gold, Zeile anklicken spielt ab dort; Zeiten aus „Lied verstehen“, sonst geschätzt
- ✓ Tonart- und Tempo-Raster (05.11.2026, wie Ableton/Logic): Tonart, Tempo, Takt, Taktlänge, Tonleiter, Paralleltonart, Takte mit Abschnitten und Abspielmarke, Klick springt in den Takt
- ✓ Fassungen nebeneinander vergleichen (05.11.2026, A/B wie beim Mastering): Original, Cover, Abschnitt neu, Verlängert, mit eigener Stimme; die andere Fassung spielt an derselben Stelle weiter

## Bedienkonzept (Profi-Bauplan A1–A3)
Erstellen links, Bibliothek in der Mitte, gewähltes Lied rechts, Player unten – füllt das Fenster.

## Nächste Schritte
1. Mitlaufender Liedtext
2. Song-Analyse auch für Aufnahmen aus dem Mikrofon
