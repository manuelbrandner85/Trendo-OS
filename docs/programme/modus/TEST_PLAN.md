# Betriebsmodus – Testplan

Studio: Startmodus wählen (SYS-01) · Bereich: System & Sicherheit · Seite: `modus` · Stand: 0.9.36 (Stand df39ed6)

7 Tests sprechen dieses Programm an.

- CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen
- SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten
- SYS-01: Modus aus echten Merkmalen, Fähigkeiten ehrlich (Blockiert statt vorgetäuscht), USB nur Anleitung
- X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage
- X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja
- X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung
- X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt
