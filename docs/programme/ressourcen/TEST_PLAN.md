# Speicher & Leistung – Testplan

Studio: Leistung anzeigen (X-09) · Bereich: System & Sicherheit · Seite: `ressourcen` · Stand: 0.9.112 (Stand d6f9fcd)

8 Tests sprechen dieses Programm an.

- SYS-11/X-09: Apps hat genau eine Unterebene; Programme und Speicher-Seite öffnen
- Speicherplatz-Karte UI: Ordner einlesen, Kachel anklicken wählt, zweiter Klick geht hinein, Papierkorb erst nach Ja
- Task-Manager: Reiter Leistung mit Kurven, Prozesse sortierbar und durchsuchbar, Beenden nur für eigenes Trendo-Werkzeug nach Rückfrage
- X-09 (T-0763): Grafiklast je Programm aus „GPU Engine“-Instanzen – je Prozess die höchste Grafik-Einheit, wie im Windows-Task-Manager
- X-09 (T-0763): Spalte Grafik je Programm, sortierbar; Netzlast je Programm ehrlich als nicht vorhanden gekennzeichnet
- X-09 (T-0763, echt): Lastprobe – Prozessorlast eines ausgelasteten Prozesses weicht höchstens 5 Prozentpunkte von den Windows-Leistungsindikatoren ab
- X-09 (T-0763, echt): hängendes Trendo-Werkzeug nach Rückfrage beenden (echter Prozess, per Kennung); fremdes Programm bleibt unberührt
- X-09: Prozesse werden nach Namen gruppiert, nach Speicher sortiert und Studios zugeordnet
