# Vorbild-Analyse Elektronik: Falstad Circuit Simulator (Clean-Room)

- Quelle: öffentliche Web-App falstad.com/circuit (CircuitJS1, GPL-2.0, github.com/sharpie7/circuitjs1); weitere Vorbilder aus der Recherche: KiCad (Schaltplan/Platine), LTspice/ngspice (SPICE-Simulation), Qucs-S
- Werkzeug: Bedienung im eingebauten Browser angesehen (Beispiel „RLC-Schwingkreis“). REA-Webanalyse nicht möglich (braucht CDP-Debug-Browser). Kein Code übernommen.
- Beobachtet: Leitungsfarbe nach Spannung (grün +, grau 0, rot −), gelbe Punkte zeigen Stromfluss (Tempo nach Stromstärke), Oszilloskop-Streifen unten mit Max-Werten je Bauteil, Simulationszeit und Zeitschritt, Werkzeugleiste mit Bauteilen, Schalter per Klick.

## Übernommen in Trendo „Schaltung & Simulation“ (0.9.27)
Raster-Schaltplan, Bauteile durch Ziehen, Spannungsfarben, Strompunkte, Oszilloskop für das gewählte Bauteil, Schalter per Klick, Start/Pause/Tempo; eigene Ergänzungen: DIN-Schaltzeichen, Prüfung (LED-Überstrom, ¼-W-Widerstand, Kurzschluss), SPICE-Netzliste für ngspice/LTspice, Stückliste, SVG.

## Noch nicht
Transistoren/Operationsverstärker/ICs, Platinen-Layout (PCB), Wechselstrom-Frequenzgang (AC-Analyse), Messgeräte als Bauteil, Unterschaltungen.
