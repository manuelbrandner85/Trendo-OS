# Handy verbinden – Testplan

Studio: Handy verbinden (X-08) · Bereich: System & Sicherheit · Seite: `handy` · Stand: 0.9.36 (Stand 302f050)

25 Tests sprechen dieses Programm an.

- CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen
- Handy (UI): Schritt für Schritt – Ort wählen → Code → Handy erreicht → verbunden → Bedienen erlauben; Hilfe nach 30 s mit Windows-Freigabe
- Handy (UI): Überall/Tailscale – installieren → anmelden → App aufs Handy; jeder Schritt erst, wenn der vorige geprüft ist
- Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen
- Handy-App: Apps-Liste, App öffnet in Handy-Größe auf dem Handy (nicht am PC), Navigation bleibt auf dem Handy, Zurück zum Start
- Handy: QR öffnen → verbinden → Foto senden → Text senden → Datei vom PC laden; Fremde und Ungekoppelte abgewiesen
- Handy: Trendo bedienen nur mit Erlaubnis am PC – dann Bildschirm, Tippen, Schreiben, Tasten, Scrollen; Scan wird gemeldet; Unterwegs-Link meldet an
- Neuerungen im Vollbild: nicht wegklickbar (Esc, Klick daneben), läuft von selbst weiter, Pfeile blättern, „Los geht's“ erst auf der letzten Folie
- SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten
- Update sofort: geladenes Update startet 30-Sekunden-Countdown (alles gesichert), danach Installation; „Jetzt neu starten“ geht sofort
- Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal
- Updates: suchen und im Hintergrund laden; „beim nächsten Start“ installiert beim Beenden, „sofort“ mit Einrichtungsbildschirm, Sicherung und Fenstern
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
