# Filme machen – Testplan

Studio: Filme machen (STUDIO-FILM-01) · Bereich: Kreativ & Medien · Seite: `film-programm` · Stand: 0.9.113 (Stand 1978f1a)

7 Tests sprechen dieses Programm an.

- Film mit echtem FFmpeg, vom Klick bis zur Datei: Beispiel-Kurzfilm, Clips über die Dateiauswahl, Foto auf eine Einstellung gelegt, Musik, Klick auf „Film rendern“ → MP4 und SRT; ffprobe prüft Dauer, Spuren, Auflösung; danach „Im Videoschnitt weiterbearbeiten“
- Film rendern: Fortschritt und Abbrechen (Export und Tafeln im Test ersetzt) – der Balken erscheint, „Abbrechen“ beendet den Lauf, es bleibt keine Datei
- Film-KI: „Drehbuch mit KI schreiben“ schreibt Drehbuch und Shotliste ins offene Dokument (KI im Test ersetzt), ein Schritt, rückgängig; unbrauchbare Antwort ändert nichts; der KI-Auftrag „Drehbuch-Gerüst“ landet im Dokument; Rendern startet dabei nie
- Film: Beispiel öffnen, Drehbuch als Seite, Einstellungen vorschlagen, Figuren übernehmen, Beschreibung bleibt, Export und Rückgängig
- Film: Drehbuch aus einer fremden Fountain-Datei öffnen; ein Dokument aus Teil 1 (ohne Material und Schnitt-Einstellungen) bleibt lesbar
- Film: Leerzustand zeigt den ersten Schritt; Fotos ohne Drehbuch werden zu Einstellungen; Auswahl, Tastatur (Pfeile, Alt+Pfeil, Entf), Rechtsklick-Menü, Dauer ändern, Rückgängig
- Film: PDF ohne typst meldet verständlich, was fehlt
