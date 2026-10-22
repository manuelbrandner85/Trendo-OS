# CORE-09 – Kameras sehen, Roboter nur nach Freigabe

- 22.10.2026, 18:21 Uhr (Europe/Berlin), Linux-Testrechner, Branch `cursor/kameras-roboter-db3a`
- Studio „Kameras & Roboter steuern“: Webcams suchen, RTSP eintragen, ONVIF lesen oder im Netz suchen. Livebild, Foto und Aufnahme über FFmpeg. Kennwort nur im SOAP-Umschlag, in der Anzeige und im Protokoll geschwärzt.
- Roboter: Simulation und Probelauf senden nichts. „Wirklich senden“ erst nach Freigabe, genau eine Zeile an den Anschluss. Not-Aus bleibt sichtbar und stoppt Aufnahme und echte Bewegung.
- Die alten Werkzeuge (Objekt finden, Arm rechnen, Fahrprogramm) bleiben.
- Tests: Core 448 bestanden / 10 übersprungen / 0 Fehler. App 186 bestanden im Gesamtlauf, ein fremder GitHub-Suchtest leer, beim Wiederholen bestanden. Api 14 bestanden / 0 Fehler.
- Nicht geprüft: echte Kamera, echtes ONVIF-Gerät, echter Roboteranschluss. Kein PC_VERIFIED.
