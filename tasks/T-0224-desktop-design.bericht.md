# Bericht T-0224: Desktop im neuen Referenz-Design

**Auftrag:** „Ja, bau den Desktop nach dem neuen Referenzbild um (T-0224).“ (Manuel, 2026-10-25), Referenz `docs/design/referenz/desktop-2026-10-25.png`.
**Branch/Worktree:** `claude/T-02xx`, Worktree `integration`

## Umgesetzt
- **Kopfleiste:** Logo A mit „Trendo OS“. In der Mitte die große KI-Eingabe mit Leuchtkugel („Trendo KI – Was möchtest du heute erstellen?“, Enter oder Pfeil fragt die Trendo KI; ohne KI öffnet sich die KI-Seite mit Einrichtung). Rechts Not-Stopp, Suche, Freigaben, Online-Dienste, Datum/Uhrzeit, Benutzer.
- **Chips:** Erstellen · Analysieren · Recherchieren · Automatisieren · Planen · Programmieren · Visualisieren · Mehr – jeder führt auf eine vorhandene Seite.
- **Mitte:**
  - „Studios“ als 24 Bildkacheln (6 × 4) mit den lokal erzeugten Kartenbildern; Reihenfolge und Ausblenden wie bisher aus den Einstellungen.
  - „Systemmodule“ (10 Kacheln auf echte Seiten).
  - „Aktuelle Projekte“ als Bildkarten aus dem echten Projektspeicher, ohne erfundene Fortschrittsbalken.
- **Links:** Start, Command Center, Studios, Projekte, Agenten, Assets, GitHub & Code, Geräte, Mobile, Einstellungen. Darunter Erdkugel und „Trendo Ökosystem“ mit echten Werten (Gerät, aktive Agenten, Projekte, System-Status) und die Besitzer-Karte.
- **Rechts:**
  - KI-Router: lokale KI, großes Modell, Kostenlimit 0 €.
  - Geräte: dieser PC mit echten CPU/RAM/SSD/GPU-Ringen, dazu Handy und Raspberry Pi über ihre Seiten.
  - Agenten Live: Stand aus dem Agenten-Zentrum.
  - Studio Manager: Schalter blenden Studios auf dem Desktop ein und aus, gespeichert.
  - Open Source: zählt die mitgelieferten Programme.
- **Bilder** lokal mit ComfyUI + FLUX.2 klein (`tools/design/ki_desktopbilder.py`, Herkunft `docs/design/DESKTOPBILDER.json`): Erdkugel `Assets/desktop/erde.jpg`, neuer Hintergrund „Orbit“ `Assets/wallpapers/orbit.jpg`.
- **Hintergrund:** „Orbit“ ist neuer Standard. Einstellungsformat 3: Wer noch den alten Standard „Abendrot“ hatte, bekommt einmalig „Orbit“; eine andere eigene Wahl bleibt (Test `T0224_Hintergrund`).
- **Designfläche:** 1536 × 864 (16:9) statt 1536 × 800; Spalten 200 · Mitte · 300.
- Keine Fremdmarken aus dem Bild übernommen (Name, Logo, Personenfotos, Handy-Ansicht).

## Belege
`reports/t0224-desktop/`: `vorher-1536x1024.png` (Stand T-0218), `desktop-1672x941.png` (Größe der Referenz), `desktop-1024x768.png`, `desktop-3840x2160.png` – gerendert mit `tools/Trendo.Render`, keine Überlappung.

## Testergebnis
`dotnet build Trendo.sln -c Release` → 0 Warnungen, 0 Fehler.

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---:|---:|---:|---:|
| Trendo.Api.Tests | 15 | 0 | 0 | 15 |
| Trendo.Core.Tests | 679 | 1 | 0 | 680 |
| Trendo.App.Tests | 329 | 2 | 0 | 331 (35 min 11 s, Exitcode 0) |
| Pakettests | 8 | – | 0 | 8 |

Verlauf:
- **Erster Volltest:** 4 Fehler.
  - Der Test zum Einstellungsformat erwartete noch Version 2.
  - Bei „Bewegung reduzieren“, „Einfacher Modus“ und „Sparsam“ wurden die Einstellungen auf neu gebaute Teile des Desktops nicht angewendet. Das war ein echter Fehler und ist behoben: Kopf, Spalten, Mitte, Projektkarten und Agentenzeilen bekommen sie jetzt immer.
- **Zweiter Volltest:** 1 Fehler in „SPEC-10 UI“. Es war ein Aufräumfehler des Test-Frameworks beim Neustart der Testumgebung, eine Thread-Meldung nach 1 ms. Einzeln lief der Test zweimal grün, der dritte komplette App-Lauf oben ebenfalls.

## Nicht geprüft / offen
- Nur gerendert und mit Tests geprüft, nicht im installierten Setup am Bildschirm angesehen (dafür braucht es ein neues Setup, Release nur mit OK).
- Das Handy-Bild rechts im Referenzbild ist nicht Teil des Desktops (Mobile-Ansicht wäre ein eigener Auftrag).
- Echte DPI-Stufen (125 %, 150 %) nicht am Gerät geprüft.
