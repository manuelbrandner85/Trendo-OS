# Geräteverbund (PC, Pi, Android) – Testergebnisse

Studio: PC, Pi & Handy verbinden (CORE-04) · Bereich: System & Sicherheit · Seite: `verbund` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 9 · Fehlgeschlagen 0 · Übersprungen 5 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| CORE-04 Begleiter: Einmal-Code, 5 Fehlversuche verbrennen den Code, Token nur als Hash gespeichert | ✓ bestanden |
| CORE-04 Begleiter: Entkoppeln vergleicht den Hash zeitgleich, abgelaufene PCs verschwinden, alte Fehlversuche auch | ✓ bestanden |
| CORE-04 Begleiter: Netz nur mit Schalter oder gespeicherter Einstellung, --local gewinnt | ✓ bestanden |
| CORE-04 Begleiter: Pi-Werte aus /proc und /sys (Abbild eines Raspberry Pi 5) | ✓ bestanden |
| CORE-04 Begleiter: Rate-Limit und Sperre pro Client, der Nachbar bleibt frei, aufgeweitete Werte werden gekappt | ✓ bestanden |
| CORE-04 Begleiter: Token- und Code-Gültigkeit einstellbar, abgelaufen abgelehnt, Altbestand ohne Datum läuft auch ab | ✓ bestanden |
| CORE-04 Begleiter: echter HTTP-Server – Health offen, alles andere nur mit Token, Kopplung, echte OCR, Entkoppeln | übersprungen |
| CORE-04 Begleiter: echter Server begrenzt Kopplung pro Client, sperrt Fehlversuche, anderer Client koppelt weiter | übersprungen |
| CORE-04 Begleiter: echter Server hört standardmäßig nur auf 127.0.0.1, 127.0.0.2 erst nach Freigabe | übersprungen |
| CORE-04 Begleiter: echter Server lehnt abgelaufenes Zugriffstoken ab, frisches Token funktioniert | übersprungen |
| CORE-04 Begleiter: nur Heimnetz-/Loopback-Absender werden bedient | ✓ bestanden |
| CORE-04 UI: mit echtem Begleiter koppeln (Code vom Gerät), Details holen, Erreichbarkeit, Aufgabe zuordnen, entfernen | übersprungen |
| CORE-04: Aufgaben laufen lokal zuerst, sonst auf erreichbarem Gerät; nicht erreichbar wird ehrlich gemeldet | ✓ bestanden |
| CORE-04: Gekoppeltes Gerät nur mit Heimnetz-Adresse speichern; Token nie in der Gerätedatei; Entfernen | ✓ bestanden |
