# Paket 44 – Fachprogramme nur bei Bedarf

- Gemeinsame Verbindung in `FachprogrammAdapter`: erkennen ohne Start, Prüfung nur auf Knopfdruck, anbinden nur nach bestandener Prüfung, öffnen nur mit Freigabe. Kein Prozess wird festgehalten.
- Seite „Externe Programme“ zeigt den Stand „Nicht geladen“, bis jemand „Verbindung prüfen“ drückt. Eine fehlgeschlagene Prüfung öffnet nichts. Eine bestandene Prüfung sagt das ehrlich und lädt das Programm nicht weiter.
- „Starten“ und „Einrichten“ auf Programme, Dokumente, Software und Sendung prüfen zuerst die Verbindung. Ohne bestandene Prüfung kein Öffnen und kein Download. 3D und Spiele bleiben beim alten Weg.
- LibreOffice, OBS und VS Code: Programmdatei muss da sein, und die Versionsabfrage muss in höchstens acht Sekunden antworten (`--version`, unter Windows bei OBS nur die Dateiversion, kein Prozess). Danach öffnet die vorhandene Freigabe das Programm. Sonst ein deutscher Hinweis, nichts gestartet.
- Installieren-Knopf nur gegen `IProgramInstaller`. Ohne eingetragenen Installer und ohne Schalter ist er unsichtbar. Kein eigener Download. Prüfsumme und Byte-Größe dürfen leer sein. Kostenpflichtig oder mit Extra-Schritt: nur Hinweis und Link. Quelle nur als https-Seite oder Paketkennung.
- Nicht behauptet: die zehn unbenannten Funktionen aus dem Master seien einzeln gebaut. Kein echter Blender- oder Unreal-Lauf. Der echte Installer ist nicht in diesem PR. Nicht abgenommen.
