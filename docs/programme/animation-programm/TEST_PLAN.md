# Animation & Motion – Testplan

Studio: Animation & Motion (STUDIO-ANIM-01) · Bereich: Kreativ & Medien · Seite: `animation-programm` · Stand: 0.9.113 (Stand 1978f1a)

6 Tests sprechen dieses Programm an.

- Animation mit echtem FFmpeg, vom Klick bis zur Datei: erste Form, auf der Bühne ziehen setzt Schlüssel bei 0 s und 2 s, Raute in der Zeitleiste ziehen und Strg+Z, Kurve per Rechtsklick; MP4 (ffprobe) zeigt die Form bei t=0 links und am Ende rechts; GIF und animiertes SVG
- Animation: Ebene anlegen, Schlüssel setzen, Zeitregler zeichnet neu, Bewegung anwenden, SVG und Video
- Animation: alte Ebenen-Dokumente öffnen weiter, Hochformat 1080×1920, Text und SVG auf der Bühne, Farbe als Schlüssel
- Logo aus leerer Vorlage mit der Maus: Stern zeichnen, verschieben, drehen, skalieren, Strg+Z; Zeichenstift-Pfad mit Bézier-Rundung schließen, Punkt ziehen; Entf, Strg+D, Rechtsklick-Menü, Raster
- Logo-Exporte echt geprüft: SVG in Chrome gerendert und Pixel mit der Vorschau verglichen, PDF mit PDFium gezeichnet, PNG 512/1024/2048, Favicon, Paket mit hell/dunkel/einfarbig/negativ
- Logo: fremdes SVG hineinziehen und umfärben, PNG einsetzen; Vorschlag erzeugen ersetzt nur mit Rückgängig; Namensfeld ändert den Text in der Zeichnung
