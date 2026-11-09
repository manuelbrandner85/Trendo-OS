# Kleidung wie im Profi-Programm – 08.11.2026

Studio „Kleidung & Produkte entwerfen“ (SPEC-08). Eigener Code nach dem Vorbild von CLO 3D und Marvelous Designer. Vom Nutzer freigegeben: echter 2D-Schnitt, echte Stoffe, 3D-Anprobe, Produktion.

## Was neu ist
- **Echter 2D-Schnitt:** T-Shirt und A-Linien-Rock werden aus den Körpermaßen konstruiert, mit gekrümmtem Armloch, Ausschnitt und Armkugel, Nahtzugabe (1,2 cm, Saum 2,5 cm) und Fadenlauf. Die Armkugel ist rechnerisch 2 % länger als das Armloch.
- **Zuschnitt mit echten Formen:** Die Teile liegen dicht auf der Stoffbahn. Sie drehen sich nur um 180°, damit der Fadenlauf stimmt.
- **Sechs Kleidungsstücke in 3D:** T-Shirt, Pullover, Hemd, Kleid, A-Linien-Rock und Hose. Halsbündchen, Stehkragen, Manschetten und Bund sind echte, angenähte Teile.
- **Echte Stoffe:** Sieben Stoffe mit Gewicht, Bahnbreite, Dehnung, Preis und Physikwerten. Die Oberflächen kommen kostenlos von ambientCG (CC0) und werden beim ersten Gebrauch geladen.
- **3D-Anprobe:** Puppe nach Brust, Taille, Hüfte und Körpergröße. Die flachen Teile werden vernäht und fallen mit Blender-Stoffphysik, danach entstehen drei Fotos (vorn, schräg, hinten). Blender öffnet sich dabei sichtbar und schließt sich selbst.
- **Schaufensterpuppe:** eiförmiger Kopf, geformte Hände mit fünf Fingern, Füße, Waden und Knie.
- **Passform:** Ärmellänge aus der Armlänge (33 % der Körpergröße), ein Test prüft das für 150, 168 und 195 cm.
- **Farben und Muster frei wählbar:** Palette mit 16 Farben, eigene Farbe, Streifen, Karo, Punkte, eigenes Bild als Stoffdruck, jeweils in wählbarer Größe. Sichtbare Steppnähte an allen Kanten.
- **Produktion:** Schnitt 1:1 als PDF auf A4 mit Kontrollquadrat, Plotter-Datei (DXF), technisches Datenblatt mit Teilen, Nähten, Stoffbedarf und Kosten je Größe.

## Echte Läufe
- **T-Shirt, Baumwolle** (Bild `kleidung-anprobe-tshirt.png`): 5.715 Stoffpunkte, Stoffphysik 37 Sekunden bei 80 Schritten, Rendern auf der Grafikkarte (OptiX). Lauf sichtbar in Blender 5.2.3.
- **Rock, Wollstoff** (Bild `kleidung-anprobe-rock.png`): gestartet mit genau dem Aufruf des Programms, inklusive automatischem Schließen. Ergebnis: drei Bilder.

## Fehler, die erst der echte Lauf gezeigt hat
- Die Ruhelage der Stoffphysik lag sichtbar am Boden (Formschlüssel stand auf 1).
- Spitze Schultern der Puppe ließen das Shirt abrutschen.
- Flächen zeigten an den Nähten in verschiedene Richtungen, das gab Punkte auf der Naht.
- Der Rockbund stand wie ein Teller ab. Er liegt jetzt genau auf der gemessenen Taille.
- Die Hose rutschte herunter, weil der Bund nicht angenäht war; danach schaute die Puppe an Hüfte und Innenbein durch, bis die Startlage stimmte.
- Die Nahtzugabe war an Ecken falsch, ein Test hat das gefunden.

- Am Ausschnitt klaffte die Naht bis zu 18 cm, weil das Bündchen rundum gleich hoch lag; das verrutschte die Schulter. Gemessen und behoben.
- Blender blieb nach fertigem Bild beim Schließen einmal 17 Minuten hängen; es beendet sich jetzt selbst.

## Reality Check
Beide Bilder wirken wie Fotos von Kleidung auf einer Schneiderpuppe: Gewebe sichtbar, Licht von Softboxen, Kontaktschatten, Saum umgeschlagen.

Bewusste Grenzen:
- Der Bordeaux-Ton wirkt durch den Faserglanz etwas stumpfer als gewählt.
- Die Passform des T-Shirts ist eher weit und kastig.
- Säume sind vereinfacht umgeschlagen. Das Hemd hat in 3D keine Knopfleiste, nur einen Stehkragen.
- Der Rock zeigt an der Seitennaht und am Saum leichte Unruhe in der Stoffphysik.
- Denim ist eine Annäherung, es gibt keine freie Denim-Textur.

## Nicht geprüft (NOT RUN)
- Vergleich der Physik mit echtem Stoff oder mit CLO 3D.
- Ausdruck des A4-Schnitts auf Papier und Einlesen der DXF in eine echte Zuschnittsoftware.
- Das echte Laden der Stoffe durch das Programm; im Lauf lagen sie schon bereit.
- FreeSewing-Schnitte bleiben als 2D-Datei nutzbar, sind aber nicht an 3D angebunden.
