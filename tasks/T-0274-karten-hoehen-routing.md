# T-0274 – Karten: Höhen für Routen und Routing entlang von Wegen

- Freigabe: Manuel im Chat am 31.10.2026 („Erst offene Punkte“)
- Umgesetzt: Höhen über OpenTopoData (frei, ohne Schlüssel; EU-DEM 25 m in Europa, sonst SRTM 30 m) – Route wird alle 100 m verdichtet, höchstens 100 Punkte je Abruf mit 1 s Abstand, Ergebnisse auf der Platte zwischengespeichert, Tageslimit verständlich gemeldet. Routing entlang von Wegen über einen OSRM-Server, den der Nutzer einträgt (kein fest eingebauter fremder Dienst; Profile zu Fuß/Fahrrad/Auto). Beides nur nach ausdrücklicher Zustimmung je Abruf (Ziel-Host wird genannt, „Nein“ sendet nichts) und im Offline-Modus gesperrt; Trendo-Kennung im User-Agent.
- Gefunden und behoben: Verdichtung rechnete mit km statt m
- Tests: Core 2 (Blöcke, Pause, Zwischenspeicher, Tageslimit, Datensatzwahl; OSRM-Adresse und Antwort), App 1 (Zustimmung Nein/Ja, Höhenprofil, Routing ohne/mit Server) – ohne echtes Netz
- Offen: Offline-Kartenpakete (Massen-Download verbietet die OSM-Kachelrichtlinie – nur mit eigenem Kachelserver sinnvoll)
