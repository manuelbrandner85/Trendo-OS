# Videos mit KI – Testergebnisse

Studio: Videos mit KI (STUDIO-VIDEOAI-01) · Bereich: KI & Automatik · Seite: `video-ki-programm` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 4 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| T-0699 Bilder erkennen: Leerzustand mit erstem Schritt, Ordner erkennen (Objekte mit Rahmen, Beschreibung, Schlagworte), Suche „zeig alle Fotos mit Hund“, Seitendatei, CSV, nach Neustart noch da | ✓ bestanden |
| T-0699 Bilder erkennen: ohne jedes Werkzeug ehrlicher Hinweis mit Einrichten-Weg, nichts erfunden | ✓ bestanden |
| T-0699 Echtlauf Bilder erkennen: echtes YOLOX-S erkennt Hund und Fahrrad, echtes Tesseract liest gedruckten Text; Suche findet beide Fotos, Seitendatei geschrieben | übersprungen |
| T-0699 Echtlauf Videos mit KI: echte Bild-KI malt 2 Schlüsselbilder, echte Stimme spricht, FFmpeg rendert MP4 mit Ton und Untertitel; ffprobe prüft, Standbilder einer Szene unterscheiden sich | übersprungen |
| T-0699 Videos mit KI: eine goldene Hauptaktion, Sprecher je Szene, Musik, Untertitelspur, Szene mit Video-KI bewegt → Clip statt Kamerafahrt; ohne Stimme ehrlicher Hinweis | ✓ bestanden |
| Videos mit KI: Szenen planen, ohne Bild-KI klarer Hinweis, Bilder mit Herkunftsnachweis, eigenes Bild, Video mit Einblendung, nie überschreiben, Exporte | ✓ bestanden |
