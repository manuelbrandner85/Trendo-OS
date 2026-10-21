# Zurück zur vorherigen Version (SYS-13-P25)

Wenn eine neue Version von Trendo OS Probleme macht, kannst du die vorige Version wieder installieren. Deine Daten bleiben erhalten.

1. In Trendo: **System → Qualität → Rückfall** öffnen und **„Sicherung für Rückfall anlegen“** drücken. Trendo sichert Einstellungen und Protokoll in `sicherungen/vor-rueckfall-…`.
2. Den angezeigten Link öffnen (GitHub-Releases, Version davor) und dort `TrendoOS-Setup.exe` herunterladen.
3. Prüfsumme vergleichen: Die Datei `SHA256SUMS.txt` aus demselben Release muss zur heruntergeladenen Datei passen.
4. Trendo schließen und das ältere Setup starten. Es installiert pro Benutzer, ohne Administrator.
5. Automatische Updates in den Einstellungen ausschalten, sonst holt Trendo die neue Version gleich wieder.

Hinweise:
- Ältere Versionen lesen neuere Einstellungsdateien; unbekannte Angaben werden übersprungen. Falls etwas fehlt, die Sicherung aus Schritt 1 zurückkopieren.
- Ein Rückfall ist auf dem PC noch nicht geprüft worden (Status: TESTED_BOX). Die Schritte 1–3 sind automatisch getestet.
