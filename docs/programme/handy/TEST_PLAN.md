# Handy verbinden – Testplan

Studio: Handy verbinden (X-08) · Bereich: System & Sicherheit · Seite: `handy` · Stand: 0.9.114 (Stand 99cbb5c)

22 Tests sprechen dieses Programm an.

- Handy (UI): Schritt für Schritt – Ort wählen → Code → Handy erreicht → verbunden → Bedienen erlauben; Hilfe nach 30 s mit Windows-Freigabe
- Handy (UI): Überall/Tailscale – installieren → anmelden → App aufs Handy; jeder Schritt erst, wenn der vorige geprüft ist
- Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen
- Handy-App: Apps-Liste, App öffnet in Handy-Größe auf dem Handy (nicht am PC), Navigation bleibt auf dem Handy, Zurück zum Start
- T-0767 (UI): Kabel-Weg mit dem echten adb dieses PCs – Anzeige „möglich“, „Handy suchen“ läuft echt (ohne Handy: leere Liste)
- T-0767 (UI): Lauf aufzeichnen → „Datei senden …“ an ein Handy → Entfernen → Aufzeichnung speichern; Kabel-Weg ehrlich
- T-0777 Katalog: im echten Register stehen SYS-04, SYS-07, CORE-04 und X-08 nicht mehr als PC_VERIFIED im Katalog
- T-0777 Nachbesserung: Studio-Karten und Fortschritt zeigen SYS-04, SYS-07, CORE-04, X-08 nicht mehr als „auf dem PC geprüft“ (Register unverändert)
- Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal
- X-08 Android-Seite: Modell/Version/Akku per adb, Bildschirmfoto und neuestes Foto nur mit Zustimmung, Handy-Temp gelöscht
- X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage
- X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja
- X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung
- X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt
- X-08: BlueZ sucht ohne zu koppeln, koppelt nur nach Zustimmung, OBEX und Akku
- X-08: Datei, Bildschirm, Apps und Überschriften nutzen dieselbe Seriennummer für Kabel und WLAN
- X-08: Kabel, WLAN-Adresse und TLS-Name, mDNS, Koppeln nur mit Zustimmung und nur im Heimnetz
- X-08: QR folgt dem Android-Text, Maske 0 hat die Format-Bits 0x5412, der Code ist fest und wurde gelesen
- X-08: Windows-Suche nutzt WinRT und koppelt nicht, fsquirt erst nach Zustimmung, böse Kennung fliegt raus
- X-08: adb und scrcpy stehen im Katalog, winget ohne --scope user, Prüfung version bzw. --version
- X-08: adb-Ausgabe lesen, Freigabe-Status verständlich, Senden nur mit Zustimmung und gültiger Datei
- X-08: bekanntes Handy nur nach Zustimmung, mDNS vor der alten Adresse, ohne Code in der Datei
