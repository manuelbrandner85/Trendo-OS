# Geräteverbund (PC, Pi, Android) – Testplan

Studio: PC, Pi & Handy verbinden (CORE-04) · Bereich: System & Sicherheit · Seite: `verbund` · Stand: 0.9.28 (Stand c95b269)

19 Tests sprechen dieses Programm an.

- CORE-04 Begleiter: Einmal-Code, 5 Fehlversuche verbrennen den Code, Token nur als Hash gespeichert
- CORE-04 Begleiter: Entkoppeln vergleicht den Hash zeitgleich, abgelaufene PCs verschwinden, alte Fehlversuche auch
- CORE-04 Begleiter: Netz nur mit Schalter oder gespeicherter Einstellung, --local gewinnt
- CORE-04 Begleiter: Pi-Werte aus /proc und /sys (Abbild eines Raspberry Pi 5)
- CORE-04 Begleiter: Rate-Limit und Sperre pro Client, der Nachbar bleibt frei, aufgeweitete Werte werden gekappt
- CORE-04 Begleiter: Token- und Code-Gültigkeit einstellbar, abgelaufen abgelehnt, Altbestand ohne Datum läuft auch ab
- CORE-04 Begleiter: echter HTTP-Server – Health offen, alles andere nur mit Token, Kopplung, echte OCR, Entkoppeln
- CORE-04 Begleiter: echter Server begrenzt Kopplung pro Client, sperrt Fehlversuche, anderer Client koppelt weiter
- CORE-04 Begleiter: echter Server hört standardmäßig nur auf 127.0.0.1, 127.0.0.2 erst nach Freigabe
- CORE-04 Begleiter: echter Server lehnt abgelaufenes Zugriffstoken ab, frisches Token funktioniert
- CORE-04 Begleiter: nur Heimnetz-/Loopback-Absender werden bedient
- CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen
- CORE-04: Aufgaben laufen lokal zuerst, sonst auf erreichbarem Gerät; nicht erreichbar wird ehrlich gemeldet
- CORE-04: Gekoppeltes Gerät nur mit Heimnetz-Adresse speichern; Token nie in der Gerätedatei; Entfernen
- SYS-01 UI: Betriebsmodus zeigt aktuellen Modus und Fähigkeiten
- X-08 UI: Bildschirm, Apps und Überschriften über die WLAN-Adresse, scrcpy nur nach Frage
- X-08 UI: Bluetooth sucht ohne zu koppeln, adb-Einrichtung erst nach Ja
- X-08 UI: Handy suchen (adb), Freigabe-Hinweis, Senden erst nach Zustimmung
- X-08 UI: Verbindungsart, WLAN nur nach Haken, QR ohne gespeicherten Code, Wiederverbinden fragt
