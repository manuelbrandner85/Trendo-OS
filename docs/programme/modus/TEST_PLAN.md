# Betriebsmodus – Testplan

Studio: Startmodus wählen (SYS-01) · Bereich: System & Sicherheit · Seite: `modus` · Stand: 0.9.36 (Stand c50f512)

15 Tests sprechen dieses Programm an.

- Anmeldung: „Beim Start nach PIN fragen“ lässt sich abschalten
- Apps: WhatsApp installieren → Desktop und Startmenü, öffnet als App-Fenster mit eigenem Speicher, Entfernen löscht die Anmeldung
- CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen
- Einstellungen-App wie Windows 11: Bereiche links, Suche über alle Einstellungen, Betriebsmodus öffnet den Stick-Schreiber ohne PC-Einrichtung und ohne „vom PC entfernen“
- Ersteinrichtung im Vollbild: Claude freiwillig zuerst; Stick/SSD zeigen echte Laufwerke mit GB, zu kleine gesperrt, Windows-Laufwerk nie; ehrlicher Hinweis solange das Abbild fehlt
- Ersteinrichtung: nach Neustart geht es beim letzten Schritt weiter, eine schon festgelegte PIN wird nicht erneut abgefragt
- SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten
- SYS-01: Modus aus echten Merkmalen, Fähigkeiten ehrlich (Blockiert statt vorgetäuscht), USB nur Anleitung
- Stick-Schreiber schützt: Start-/Systemlaufwerk, Windows-Laufwerk, interne Platte als „Stick“ und zu kleine Laufwerke werden verweigert
- Stick-Schreiber: Abbild im Release erkannt, Laufwerk wählen, Löschen erst nach Eintippen des Buchstabens, laden, prüfen, schreiben (virtuelle Festplatte), Boot-Tasten
- TV-Vollbild: kleines Fenster „Verkleinern“ beim Start und bei Mausbewegung, ein Klick verkleinert
- X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage
- X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja
- X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung
- X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt
