# Betriebsmodus – Testergebnisse

Studio: Startmodus wählen (SYS-01) · Bereich: System & Sicherheit · Seite: `modus` · Stand: 0.9.36 (Stand 70e872a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 15 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Anmeldung: „Beim Start nach PIN fragen“ lässt sich abschalten | ✓ bestanden |
| Apps: WhatsApp installieren → Desktop und Startmenü, öffnet als App-Fenster mit eigenem Speicher, Entfernen löscht die Anmeldung | ✓ bestanden |
| CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen | ✓ bestanden |
| Einstellungen-App wie Windows 11: Bereiche links, Suche über alle Einstellungen, Betriebsmodus öffnet den Stick-Schreiber ohne PC-Einrichtung und ohne „vom PC entfernen“ | ✓ bestanden |
| Ersteinrichtung im Vollbild: Claude freiwillig zuerst; Stick/SSD zeigen echte Laufwerke mit GB, zu kleine gesperrt, Windows-Laufwerk nie; ehrlicher Hinweis solange das Abbild fehlt | ✓ bestanden |
| Ersteinrichtung: nach Neustart geht es beim letzten Schritt weiter, eine schon festgelegte PIN wird nicht erneut abgefragt | ✓ bestanden |
| SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten | ✓ bestanden |
| SYS-01: Modus aus echten Merkmalen, Fähigkeiten ehrlich (Blockiert statt vorgetäuscht), USB nur Anleitung | ✓ bestanden |
| Stick-Schreiber schützt: Start-/Systemlaufwerk, Windows-Laufwerk, interne Platte als „Stick“ und zu kleine Laufwerke werden verweigert | ✓ bestanden |
| Stick-Schreiber: Abbild im Release erkannt, Laufwerk wählen, Löschen erst nach Eintippen des Buchstabens, laden, prüfen, schreiben (virtuelle Festplatte), Boot-Tasten | ✓ bestanden |
| TV-Vollbild: kleines Fenster „Verkleinern“ beim Start und bei Mausbewegung, ein Klick verkleinert | ✓ bestanden |
| X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage | ✓ bestanden |
| X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja | ✓ bestanden |
| X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung | ✓ bestanden |
| X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt | ✓ bestanden |
