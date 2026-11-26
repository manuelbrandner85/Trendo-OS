# T-0772 Punkt 4 – REGION und „ezdxf-Linse“: geprüft, bleiben gemeldet

Werkzeugstand auf diesem PC: `werkzeuge.txt` (nur nachgesehen, nichts installiert). Python 3.13 ist da, **ezdxf nicht** (auch nicht im FreeCAD-1.1-Ordner). FreeCAD 1.1 ist da, LibreCAD/ODA-Konverter/AutoCAD nicht.

## REGION

Was in der Datei steht: Eine REGION enthält keine Linien, sondern einen ACIS-Körper.
- Bis AutoCAD 2010 (AC1024): SAT-Text in den Gruppen 1/3, jedes Zeichen mit einer einfachen Ersetzung verschleiert. Darin eine eigene Topologie: body → lump → shell → face → loop → coedge → edge → vertex/point, Kurven als `straight-curve`, `ellipse-curve` oder `intcurve` (B-Spline, teils nur prozedural beschrieben, z. B. als Schnitt zweier Flächen).
- Ab AutoCAD 2013 (AC1027): binär (SAB) im eigenen Abschnitt ACDSDATA, die REGION verweist nur über eine Kennung darauf.

Was ein echtes Lesen bräuchte:
1. Entschlüsseln des SAT-Texts (einfach) und ein eigener Leser für die SAT-Datensätze samt Versionen (Felder ändern sich je ACIS-Version).
2. Ein zweiter Leser für das binäre SAB-Format und für den ACDSDATA-Abschnitt.
3. Kanten in Schleifen ordnen (coedge-Verkettung, Richtung, Außen-/Innenschleife) und Kurven abtasten; `intcurve` ohne Kontrollpunkte (prozedural) ließe sich nur mit einem Geometriekern auswerten → dort weiter melden.
4. Gegenprüfung mit echten REGION-Dateien aus AutoCAD/BricsCAD oder einem Werkzeug, das ACIS liest.

Lizenz: Für eigenes Lesen des Dateiformats ist keine fremde Lizenz nötig (Clean-Room nach öffentlichen Formatbeschreibungen); ACIS selbst (Spatial) dürfte nicht eingebunden werden.

Urteil: **bleibt gemeldet.** Aufwand grob 600–1000 Zeilen plus Tests für eine ebene Teilmenge (Linien, Kreisbögen, Ellipsen, B-Splines mit Kontrollpunkten), und vor allem: **Auf diesem PC gibt es kein Programm, das eine echte REGION schreibt oder ACIS liest** (FreeCAD exportiert DXF ohne REGION; AutoCAD/BricsCAD/ODA fehlen). Ein Leser ohne echte Gegenprobe wäre geraten – beim Fräsen ist eine falsch gelesene Kontur schlimmer als eine gemeldete. Voraussetzung für einen späteren Ausbau: echte REGION-Dateien beider Formate (vor/ab 2013) und ein Programm zum Gegenprüfen.

Heute schon sicher: Eine REGION wird gemeldet, ihre Ausdehnung gilt als unsicherer Bereich, Konturen darin werden ausgelassen (`Region_and_lens_stay_reported`, T-0760).

## „ezdxf-Linse“

Der Fall: ein Bogen in einer LWPOLYLINE/HATCH-Kante, bei dem Gruppe 73 = 0 („im Uhrzeigersinn“) steht und Anfangs-/Endwinkel in [0°, 360°) liegen. ezdxf und AutoCAD lesen das als Komplementwinkel (Linse), wörtlich gelesen ergibt dieselbe Bytefolge eine „Erdnuss“. Beide Ränder schließen sich, beide sind gültige Geometrie.

Was fehlt, damit Trendo mehr als meldet: **keine Software, sondern eine Information in der Datei.** Auch mit installiertem ezdxf bekäme man nur ezdxfs eigene Lesart, nicht die Absicht des Zeichners. Ein Unterscheidungsmerkmal könnte nur der Ersteller sein (z. B. Kennung „ezdxf“ im Kopf) – das wäre eine Vermutung über das Programm, nicht über die Form; bei einer falschen Vermutung würde ein Loch an der falschen Stelle gefräst. Deshalb bleibt es bewusst bei der Meldung ohne Form.

Mit ezdxf auf dem PC ließe sich nur zusätzlich belegen, wie ezdxf selbst solche Dateien schreibt und liest (Rundreise). Das ist **BLOCKIERT: ezdxf ist nicht installiert** (Voraussetzung: `pip install ezdxf` durch den Nutzer).
