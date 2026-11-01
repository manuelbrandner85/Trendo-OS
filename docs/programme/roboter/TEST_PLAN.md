# Kameras & Roboter steuern – Testplan

Studio: Kameras & Roboter steuern (CORE-09) · Bereich: Technik & Fachgebiete · Seite: `roboter` · Stand: 0.9.28 (Stand 83bf3c3)

27 Tests sprechen dieses Programm an.

- CORE-09 F11: Objekte zählen
- CORE-09 F12: Belichtung
- CORE-09 F13: Schärfe
- CORE-09 F14: Bewegung
- CORE-09 F15: Maske speichern
- CORE-09 F16: Kalibrierung
- CORE-09 F17: Greifbahn
- CORE-09 F18: Reichweite
- CORE-09 F19: Servo-Pulse
- CORE-09 F20: Fahrprofil
- CORE-09 UI: Bild prüfen – zwei rote Teile werden markiert, Maske, Ziel aus dem Bild, Bewegung zwischen zwei Bildern
- CORE-09 UI: Echte Fahrt geht erst nach Freigabe über TCP raus, Not-Aus sendet STOP
- CORE-09 UI: Echte Fahrt wartet auf Freigabe, Not-Aus stoppt
- CORE-09 UI: Klick in die Draufsicht setzt das Ziel, Wegpunkt hinzufügen/verschieben/löschen, Weg simulieren, Greifbahn, Programm-CSV
- CORE-09 UI: Not-Aus ist da, Simulation fährt ohne Freigabe
- CORE-09 UI: ONVIF trägt eine Kamera ein, FFmpeg wird geprüft
- CORE-09 UI: Webcam, Livebild, Foto, Aufnahme, IP-Adresse
- CORE-09: Aufnahme startet einen Prozess und schreibt die Datei beim Stoppen
- CORE-09: Bild holen liest die Datei, die FFmpeg geschrieben hat
- CORE-09: FFmpeg-Argumente für Webcam, RTSP, Aufnahme
- CORE-09: Geräteadresse ist Rechner und Port
- CORE-09: Mehrere Zeilen gehen nach einer Freigabe an das Gerät
- CORE-09: Not-Aus sendet STOP und blockiert die nächste Fahrt
- CORE-09: ONVIF-Probe, Profile und Stream-Adresse
- CORE-09: PNG/BMP lesen, Farbobjekt finden, Kinematik hin und zurück, Fahrprogramm
- CORE-09: Simulation und Trockenlauf senden nichts, Echt braucht Freigabe
- CORE-09: Webcams aus sysfs, DirectShow nur Video, RTSP-Adresse
