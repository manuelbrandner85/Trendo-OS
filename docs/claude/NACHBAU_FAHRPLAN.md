# Fahrplan: Jedes Studio-Programm nachbauen (außer Blender und Unreal)

**Vorgabe (Manuel, 2026-10-25):** „Schau am besten, dass jedes Programm, jedes Studio nachgebaut wird mit allen Funktionen, außer Blender und Unreal Engine. Die KI sollen automatisch installiert werden, außer die, wo eine API verlangt wird – da soll man direkt auf die Seite geleitet werden und man kann die API in Trendo OS eintragen.“

**Leitlinie (Manuel, 2026-10-25):** „Jedes Studio oder Programm soll alles können, also das Maximum rausholen, aber leicht verständlich für den Nutzer.“ Daraus folgt für jeden Nachbau:
- Der volle Funktionsumfang des Vorbilds ist das Ziel, nicht nur der Alltag.
- Vorne steht das Einfache: wenige große Knöpfe für das Häufigste und klare deutsche Wörter statt Fachbegriffen. Jede Taste sagt, was passiert.
- Alles Weitere liegt eine Ebene tiefer unter „Mehr“ bzw. „Profi“ (höchstens zwei Ebenen). Nichts wird versteckt, was das Original kann.
- Jede Funktion hat einen kurzen Hilfetext, die Trendo KI kann sie auf Zuruf ausführen, und Fehler werden in Alltagssprache erklärt.

**Vorgehen je Programm** (wie bei Trendo PDF, T-0223):
1. Funktionsumfang des mitgelieferten Originals mit REA ermitteln (Belege, beobachtet/abgeleitet/unbekannt).
2. Nachbau in Stufen, jede Stufe mit Tests und Abgleich gegen das Original.
3. Ist eine Stufe fertig, ersetzt sie im Studio das Original. Das Original bleibt im Vollpaket auswählbar, bis alle Funktionen nachgebaut sind.

## Weitere Vorgaben (Manuel, 2026-10-26)
- **Schönere Oberfläche:** Jedes Studio-Programm erhält eine schönere, verständlichere Oberfläche im Stil des neuen Desktops.
- **Beschreibung zu jedem Programm**, auch zu Programmen von GitHub: ausführlich und leicht verständlich. Sie beantwortet: Was ist das? Wofür nutze ich es? Wie funktioniert es (Schritt für Schritt)? Was kann es alles? Woher stammt es, unter welcher Lizenz, wie groß ist es? Wie entferne ich es wieder?
- **Programme von GitHub:** finden oder auswählen. Auf Wunsch installiert Trendo sie automatisch, nach Zustimmung, in `TrendoWerkzeuge`.
- **Einzeln deinstallieren:** Jedes Programm lässt sich einzeln entfernen.
- **Deinstallation von Trendo OS:** entfernt alle zugehörigen Programme mit.
- **Einrichtung:** Nach „Zustimmen & installieren“ wird der Knopf ausgegraut. Danach erscheint ein Vollbild-Fortschritt mit Animation, der sich bis zum Ende nicht wegklicken lässt.
- **Logo:** Vorschlag 2 „Saphir-Kristall“.

## Zwei Arten von Programmen
- **Bedienprogramme** (mit eigener Oberfläche): werden komplett nachgebaut.
- **Rechenkerne** (FFmpeg, Tesseract, COLMAP, Git, Python, Ollama, adb): Das sind Bibliotheken oder Laufzeiten mit Millionen Zeilen Fachcode (Video-Decoder, Texterkennung, 3D-Rekonstruktion). Sie bleiben als unsichtbarer Kern im Vollpaket. Trendo baut dafür die komplette eigene Oberfläche, sodass man das Original nie sieht.

## Reihenfolge (Vorschlag)
| # | Trendo-Programm | Vorbild | Art | Stand |
|---|---|---|---|---|
| 1 | Trendo PDF | SumatraPDF, qpdf | Bedienprogramm | Stufe 1 fertig (P1–P6) |
| 2 | Trendo Text | LibreOffice Writer | Bedienprogramm | freigegeben (T1–T10) |
| 3 | Trendo Tabelle | LibreOffice Calc | Bedienprogramm | freigegeben (C1–C8) |
| 4 | Trendo Präsentation | LibreOffice Impress | Bedienprogramm | offen |
| 5 | Trendo Archiv | 7-Zip | Bedienprogramm | offen |
| 6 | Trendo Player | mpv | Oberfläche über FFmpeg-Kern | offen |
| 7 | Trendo Audio | Audacity | Bedienprogramm | offen |
| 8 | Trendo Bild | Krita | Bedienprogramm | offen |
| 9 | Trendo Vektor | Inkscape | Bedienprogramm | offen |
| 10 | Trendo Video | Kdenlive | Oberfläche über FFmpeg-Kern | offen |
| 11 | Trendo Aufnahme & Stream | OBS Studio | Oberfläche über FFmpeg-Kern | offen |
| 12 | Trendo Code | VSCodium | Bedienprogramm | offen |
| 13 | Trendo Handy | scrcpy | Oberfläche über adb-Kern | offen |
| 14 | Texterkennung, Scan zu 3D, Git, Python, KI | Tesseract, COLMAP, Git, Python, Ollama | Rechenkerne mit Trendo-Oberfläche | Oberflächen teilweise vorhanden |
| – | 3D Studio, Game Studio | Blender, Unreal Engine | bleiben Originale (Vorgabe) | – |

## KI (Teil derselben Vorgabe)
- **Lokale, kostenlose KI-Modelle** (Ollama-Katalog in `src/Trendo.Core/W2/KiSources.cs`) werden nach dem Setup automatisch geladen, ohne Klick. Geladen werden nur Modelle, die zum Arbeitsspeicher des PCs passen. Das kleine Modell liegt schon im Vollpaket, die übrigen kommen von der offiziellen Ollama-Bibliothek.
- **KI-Bibliothek nach Arten (Manuel, 2026-10-26):** Kostenlose Modelle werden nicht nur aufgelistet, sondern laufen tatsächlich in Trendo OS. Sie sind sauber nach Arten sortiert: Chat, Bild, Video, Sprache (Sprechen/Zuhören), Musik, Geräusche, Übersetzen, Code, Texterkennung, 3D und weitere. Jede Art hat eine eigene Laufzeit in Trendo. Stufe 1 ist umgesetzt (T-0227, Seite „KI-Bibliothek“):
- Ollama für Chat und Text
- stable-diffusion.cpp für Bild und Video
- whisper.cpp für Sprache zu Text
- Piper für Vorlesen
- Real-ESRGAN für Bilder verbessern

Musik-KI: ACE-Step 1.5 ist eingebaut (Stufe 2, offizielles Komplettpaket mit lokalem Server). Der Probelauf am PC steht noch aus.
- **KI-Dienste mit API-Schlüssel:** Die Seite „Dienste & Schlüssel“ listet jeden Anbieter. „Schlüssel holen“ öffnet die offizielle Seite des Anbieters, der Schlüssel wird in Trendo eingetragen, geprüft und im Windows-Anmeldeinformationsspeicher abgelegt. Kostenpflichtige Dienste bleiben freiwillig, Trendo funktioniert immer ohne sie.
