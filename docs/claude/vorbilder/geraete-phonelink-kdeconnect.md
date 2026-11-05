# Vorbild-Analyse Geräte und Handy: Smartphone-Link, KDE Connect, Windows Geräte-Manager, Raspberry Pi Connect (Clean-Room)

- Programme: „Geräte & Laufwerke“, „Handy verbinden“, „Geräteverbund (PC, Pi, Android)“
- Vorbilder: KDE Connect/GSConnect (frei), Microsoft Smartphone-Link, Windows „Geräte“ und Datenträgerverwaltung, Raspberry Pi Connect, Tailscale – Funktionsumfang aus öffentlicher Doku
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

### Geräte & Laufwerke
- ✓ Geräte und Laufwerke auf einen Blick, Probleme und neu angeschlossene Geräte hervorgehoben (wie Windows „Geräte“)
- ✓ Nur lesen: Trendo ändert keine Datenträger, Treiber oder Firmware (Projektregel)
- ✗ „Was belegt den Platz?“ je Laufwerk wie WinDirStat – noch nicht

### Handy verbinden
- ✓ QR-Code mit der Handy-Kamera scannen, „Verbinden“ tippen, fertig (wie Smartphone-Link); Code läuft nach kurzer Zeit ab
- ✓ Zuhause im WLAN oder unterwegs über Tailscale; nur Adressen im eigenen Netz
- ✓ Bleibt verbunden: ist ein Handy gekoppelt, startet der Handy-Zugang mit Trendo
- ✓ Trendo als App auf dem Handy: alle Studios und Programme mit eigenen Symbolen, Zurück, Handy-Startbildschirm
- ✓ Android per Kabel/WLAN über adb (offizielle Platform-Tools) nach Zustimmung
- ◐ Dateien zwischen Handy und PC – über Trendo-Seiten, kein eigenes „Datei senden“ wie KDE Connect

### Geräteverbund (PC, Pi, Android)
- ✓ Raspberry Pi koppeln: Code steht auf dem Pi, Trendo schickt ihn und erhält ein Token (wie Raspberry Pi Connect)
- ✓ Verbund unter „System“ in zwei Ebenen
- ✗ Aufträge automatisch auf das stärkste Gerät verteilen – noch nicht

## Bedienkonzept (Profi-Bauplan A1–A3)
Je Gerät eine Karte mit Zustand und einem Hauptknopf; Koppeln in höchstens zwei Schritten.

## Nächste Schritte
1. „Datei senden“ zwischen Handy und PC
2. Platzbelegung je Laufwerk
