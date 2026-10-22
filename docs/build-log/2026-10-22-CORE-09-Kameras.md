# CORE-09 – Kameras und Roboter steuern

- 22.10.2026 (Builder, Linux)
- Webcams aus dem System (Linux: sysfs, Windows: DirectShow über FFmpeg), IP-Kameras per rtsp://, ONVIF-Suche im lokalen Netz
- Livebild, Foto und Aufnahme über FFmpeg; fehlt FFmpeg, fragt die bestehende Werkzeugprüfung einmal
- Arm und Greifer: Simulation (Voreinstellung) und Trockenlauf senden nichts. Echte Befehle nur nach Freigabe im vorhandenen Freigabe-Tor. Not-Aus ist auf der Seite und schickt STOP
