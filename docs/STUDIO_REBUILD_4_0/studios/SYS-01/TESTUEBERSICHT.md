# Testübersicht: Startmodus wählen

Studio SYS-01 · Startmodus wählen · Bereich System & Sicherheit

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

Gezählte Tests des Studios: 82. Gefordert für die Abnahme 4.0: mindestens 20 eigene positive und negative Testfälle.

## Dem Studio zugeordnete Tests

Die Zuordnung kommt aus dem Programmkatalog und ist ungenau: Sie kann Tests anderer Studios enthalten (siehe REA-Audit).

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

## Pflichtprüfungen der Abnahme 4.0

Alle Punkte offen, bis ein Testartefakt vorliegt: alte Funktionen, neue Funktionen, Bedienbarkeit, Erreichbarkeit, Dateikompatibilität, Fehlerbehandlung, Rechte, Leistung, Speicher, Stabilität, Tastatur und Maus, Ziehen und Ablegen, Rückgängig, Wiederherstellung, Exporte, Zusammenspiel mit anderen Studios.
