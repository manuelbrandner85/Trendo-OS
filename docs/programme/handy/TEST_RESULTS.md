# Handy verbinden – Testergebnisse

Studio: Handy verbinden (X-08) · Bereich: System & Sicherheit · Seite: `handy` · Stand: 0.9.36 (Stand 53a9750)

Lauf: `volltest-0.9.36-api.trx, volltest-0.9.36-app.trx, volltest-0.9.36-core.trx`

Bestanden 24 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen | ✓ bestanden |
| Handy (UI): Schritt für Schritt – Ort wählen → Code → Handy erreicht → verbunden → Bedienen erlauben; Hilfe nach 30 s mit Windows-Freigabe | ✓ bestanden |
| Handy (UI): Überall/Tailscale – installieren → anmelden → App aufs Handy; jeder Schritt erst, wenn der vorige geprüft ist | ✓ bestanden |
| Handy bedient Trendo: Bildschirmbild, Knopf antippen, Feld antippen und schreiben, Löschen | ✓ bestanden |
| Handy-App: Apps-Liste, App öffnet in Handy-Größe auf dem Handy (nicht am PC), Navigation bleibt auf dem Handy, Zurück zum Start | ✓ bestanden |
| Handy: QR öffnen → verbinden → Foto senden → Text senden → Datei vom PC laden; Fremde und Ungekoppelte abgewiesen | ✓ bestanden |
| Handy: Trendo bedienen nur mit Erlaubnis am PC – dann Bildschirm, Tippen, Schreiben, Tasten, Scrollen; Scan wird gemeldet; Unterwegs-Link meldet an | ✓ bestanden |
| Neuerungen im Vollbild: nicht wegklickbar (Esc, Klick daneben), läuft von selbst weiter, Pfeile blättern, „Los geht's“ erst auf der letzten Folie | ✓ bestanden |
| SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten | ✓ bestanden |
| Update sofort: geladenes Update startet 30-Sekunden-Countdown (alles gesichert), danach Installation; „Jetzt neu starten“ geht sofort | ✓ bestanden |
| Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal | ✓ bestanden |
| Updates: suchen und im Hintergrund laden; „beim nächsten Start“ installiert beim Beenden, „sofort“ mit Einrichtungsbildschirm, Sicherung und Fenstern | ✓ bestanden |
| X-08 Android-Seite: Modell/Version/Akku per adb, Bildschirmfoto und neuestes Foto nur mit Zustimmung, Handy-Temp gelöscht | ✓ bestanden |
| X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage | ✓ bestanden |
| X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja | ✓ bestanden |
| X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung | ✓ bestanden |
| X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt | ✓ bestanden |
| X-08: BlueZ sucht ohne zu koppeln, koppelt nur nach Zustimmung, OBEX und Akku | übersprungen |
| X-08: Datei, Bildschirm, Apps und Überschriften nutzen dieselbe Seriennummer für Kabel und WLAN | ✓ bestanden |
| X-08: Kabel, WLAN-Adresse und TLS-Name, mDNS, Koppeln nur mit Zustimmung und nur im Heimnetz | ✓ bestanden |
| X-08: QR folgt dem Android-Text, Maske 0 hat die Format-Bits 0x5412, der Code ist fest und wurde gelesen | ✓ bestanden |
| X-08: Windows-Suche nutzt WinRT und koppelt nicht, fsquirt erst nach Zustimmung, böse Kennung fliegt raus | ✓ bestanden |
| X-08: adb und scrcpy stehen im Katalog, winget ohne --scope user, Prüfung version bzw. --version | ✓ bestanden |
| X-08: adb-Ausgabe lesen, Freigabe-Status verständlich, Senden nur mit Zustimmung und gültiger Datei | ✓ bestanden |
| X-08: bekanntes Handy nur nach Zustimmung, mDNS vor der alten Adresse, ohne Code in der Datei | ✓ bestanden |
