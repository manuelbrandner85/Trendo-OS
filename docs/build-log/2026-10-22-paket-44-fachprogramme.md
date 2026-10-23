# Paket 44 – Fachprogramme nur bei Bedarf

- Gemeinsame Verbindung in `FachprogrammAdapter`: erkennen ohne Start, Prüfung nur auf Knopfdruck, anbinden nur nach bestandener Prüfung, öffnen nur mit Freigabe. Kein Prozess wird festgehalten.
- Seite „Externe Programme“ zeigt den Stand „Nicht geladen“, bis jemand „Verbindung prüfen“ drückt. Eine fehlgeschlagene Prüfung öffnet nichts. Eine bestandene Prüfung sagt das ehrlich und lädt das Programm nicht weiter.
- „Starten“ und „Einrichten“ öffnen nichts, solange „Verbindung prüfen“ nicht bestanden ist. Nur dieser Knopf startet die Prüfung. „Suchen“ meldet eine vorhandene Datei als gefunden, nicht geprüft, und startet nichts. 3D und Spiele bleiben beim alten Weg.
- LibreOffice, OBS und VS Code: Programmdatei muss da sein, und die Versionsabfrage muss in höchstens acht Sekunden antworten (`--version`, unter Windows bei OBS nur die Dateiversion, kein Prozess). Danach öffnet die vorhandene Freigabe das Programm. Sonst ein deutscher Hinweis, nichts gestartet.
- Installieren-Knopf nur gegen `IProgramInstaller`. Ohne eingetragenen Installer und ohne Schalter ist er unsichtbar. Kein eigener Download. Prüfsumme und Byte-Größe dürfen leer sein. Kostenpflichtig oder mit Extra-Schritt: nur Hinweis und Link. Quelle nur als https-Seite oder Paketkennung.
- Die zehn Namen stehen in PR #275, Commit 06f8446. Diese Datei `requirements.yaml` ist hier nicht geändert. P44-06 wartet auf #251 und #253. P44-08 wartet auf den echten Installer. P44-10 ist nicht abgenommen.
