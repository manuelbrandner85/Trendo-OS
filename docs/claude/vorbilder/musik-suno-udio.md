# Vorbild-Analyse Trendo Musik: Musik-KI-Dienste wie Suno und Udio (Clean-Room)

- Programme: „Trendo Musik“
- Vorbilder: Suno, Udio (nur Bedienkonzept aus öffentlicher Darstellung) – Erzeugung lokal mit ACE-Step 1.5 (MIT)
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
- ✓ „Im Stil von“: Stilmerkmale per Text-KI, ohne Namen und ohne nachgemachte Stimme
- ✓ Song-Analyse (librosa): Tonart, Tempo, Takt, Akkorde, Tonumfang – als Vorlage übernehmbar
- ✓ Eigene Aufnahme hinzufügen und als Cover neu einspielen (Hinweis: nur privat, GEMA)
- ◐ Tonart/Tempo bekannter Songs aus AcousticBrainz (CC0) über MusicBrainz – der Dienst läuft nur noch als Archiv und antwortet nicht immer
- ✓ Text-Werkzeuge: eingefügten Text gliedern, Silben je Zeile, Reime (Wörterbuch), Refrain wiederholen, Refrain eingängiger (KI)
- ✓ Abschnitt neu erzeugen (erprobt: nur der gewählte Bereich ändert sich)
- ✓ Cover in anderem Stil
- ✓ Liedtext mit KI (Claude, wenn verbunden)
- ✓ Bibliothek mit Suche, Favoriten, Umbenennen, Löschen in den Papierkorb, Export WAV/FLAC, Übergabe an den Videoschnitt
- ✓ Cover-Bilder: Motiv aus der Bild-KI (SDXL Turbo) zu Genre und Stimmung, Titel und Name mit echter Schrift, 4 Layouts, 1400 × 1400, in die MP3 eingebettet
- ✗ Verlängern (erprobt: liefert mit dem Turbo-Modell nur die alte Länge; Grundmodell geladen, Echttest am 03.11. wegen vollem Grafikspeicher nicht aussagekräftig)
- ✗ Spuren trennen / Begleitung zu Gesang (erprobt: mit dem Turbo-Modell keine einzelne Spur)
- ✗ Mitlaufender Liedtext beim Abspielen

## Bedienkonzept (Profi-Bauplan A1–A3)
Erstellen links, Bibliothek in der Mitte, gewähltes Lied rechts, Player unten – füllt das Fenster.

## Nächste Schritte
1. Verlängern, Spuren und Begleitung mit dem Grundmodell auf freier Grafikkarte prüfen (M10)
2. Mitlaufender Liedtext
3. Song-Analyse auch für Aufnahmen aus dem Mikrofon
