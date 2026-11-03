# Vorbild-Analyse Trendo Musik: Musik-KI-Dienste wie Suno und Udio (Clean-Room)

- Programme: „Trendo Musik“
- Vorbilder: Suno, Udio (nur Bedienkonzept aus öffentlicher Darstellung) – Erzeugung lokal mit ACE-Step 1.5 (MIT)
- Werkzeug: Bedienkonzept aus öffentlicher Doku; kein fremder Code, keine fremden Grafiken, Namen oder Texte. Funktionen von ACE-Step am 03.11.2026 auf einer RTX 5070 (12 GB) echt erprobt.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Einfach-Modus: ein Satz → Lied (Text schreibt die KI)
- ✓ Profi-Modus: Stil, eigener Liedtext mit Abschnitten, Titel, Instrumental
- ✓ Zwei Varianten je Auftrag (erprobt: 32 s für 2 × 30 s)
- ✓ 50 Gesangssprachen (erprobt: Spanisch)
- ✓ Länge bis 10 Minuten (erprobt: über 8 Minuten ohne planendes Sprachmodell, sonst gekürzt)
- ✓ Tempo, Tonart, Takt als eigene Felder (erprobt: Tempo im Stiltext wird ignoriert)
- ✓ Abschnitt neu erzeugen (erprobt: nur der gewählte Bereich ändert sich)
- ✓ Cover in anderem Stil
- ✓ Liedtext mit KI (Claude, wenn verbunden)
- ✓ Bibliothek mit Suche, Favoriten, Umbenennen, Löschen in den Papierkorb, Export WAV/FLAC, Übergabe an den Videoschnitt
- ◐ Titelbild (Farbverlauf je Lied, noch kein KI-Bild)
- ✗ Verlängern (erprobt: liefert mit dem Turbo-Modell nur die alte Länge)
- ✗ Spuren trennen / Begleitung zu Gesang (erprobt: mit dem Turbo-Modell keine einzelne Spur)
- ✗ Mitlaufender Liedtext beim Abspielen

## Bedienkonzept (Profi-Bauplan A1–A3)
Erstellen links, Bibliothek in der Mitte, gewähltes Lied rechts, Player unten – füllt das Fenster.

## Nächste Schritte
1. Titelbilder mit der lokalen Bild-KI
2. Verlängern, Spuren und Begleitung mit einem Basis-Modell prüfen (Grafikspeicher!)
3. Mitlaufender Liedtext
