# T-0218 Bericht: Startseite und „Alle Studios“ im Referenz-Design

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)
**Auftrag:** `tasks/T-0218-referenz-design.md` – Wahl Manuel 2026-10-24: Bild 1 als Startseite, Bild 2 als „Alle Studios“.

## Umgesetzt
- **Startseite (Bild 1)**, `MainWindow.Shell.cs`:
  - Kopfleiste mit Logo, Reitern (Desktop, Studios, Apps, Projekte, KI, System, Einstellungen), Suche (Strg+K), Freigaben mit Zähler, Online-Dienste, Benutzer und Uhr samt Datum.
  - Linke Bereiche mit Schnellzugriff: Neue Aufgabe → Abläufe, Neues Projekt, Neuer Brief, Analyse starten → System, System-Check.
  - Mitte: 9 Studio-Kacheln (Katalognamen), Logo A groß mit Schriftzug, Slogan, 5 Aktionen und Lichtring.
  - Rechts: KI-Assistent (echter Status), Systemstatus als Ringe mit echten Messwerten (GPU „n. v.“, wenn keine Quelle), Netzwerk, aktuelle Projekte, Schnell-Tools (Screenshot speichert wirklich, Aufnahme, Notiz, Übersetzen).
  - Dock zeigt auch die eingebauten Trendo-Bereiche.
  - Designfläche 1536 × 800, proportional skaliert. Fenster und Startmenü nutzen die ganze Fläche.
- **„Alle Studios“ (Bild 2)**, `MainWindow.StudiosView.cs`:
  - Eingabezeile „Was möchtest du heute erstellen?“ (Enter sucht) und Bereichs-Chips als Filter.
  - Bildkarten für alle 54 Studios, mit Symbol, Name, Satz und Status.
  - Reihe „Systemmodule“ (öffnet echte Studios) und „Aktuelle Projekte“.
  - Rechts:
    - KI: lokal und kostenlos, Online-KI optional, 0 €.
    - Geräte: dieser PC mit Messwerten, Pi und Handy verbinden.
    - KI-Team: echte Rollen.
    - GitHub: nur lesen.
  - Die Studio-Prompt-Leiste entfällt auf dieser Seite, weil sie eine eigene Eingabe hat.
- **Bilder lokal mit KI** (ComfyUI + FLUX.2 klein 4B, 127.0.0.1):
  - Hintergrund „Abendrot“ (`Assets/wallpapers/abendrot.jpg`, Seed 47 von 3 Kandidaten, neuer Standard). Herkunft in `docs/design/HINTERGRUENDE.json`.
  - 54 Kartenbilder `Assets/studios/<ID>.jpg` (768 × 432, zusammen 1,5 MB). Herkunft mit Prompt und Seed in `docs/design/STUDIOBILDER.json`, Erzeuger `tools/design/ki_studiobilder.py`.
- Veralteten Satz „…beim laufenden Design-41-Auftrag“ (CORE-01) ersetzt, Matrix `docs/improvement-42/studio-capability-matrix.md` nachgezogen.
- **Tests angepasst:**
  - Standard-Hintergrund in `Tests.cs` und `W0bLookTests.cs`.
  - `RealClick` nennt bei Fehlern die Element-Kette.
  - Dock-Hinweise erscheinen über dem Knopf.
  - Einfacher Modus und Mindestschrift gelten auch für die festen Spalten.

## Nicht übernommen (bewusst)
- Logo aus den Bildern: Logo A bleibt.
- Studionamen aus den Bildern: Die Katalognamen bleiben.
- Die Namen „AION“ und „JARVIS“ sowie der Hinweis auf eine Ersatz-KI eines anderen Anbieters.
- Erfundene Zahlen (CPU 12 %, Projekt-Fortschritt, „3 Geräte verbunden“).
- Unreal als Oberfläche: Die Unreal-MCP-Verbindung war in dieser Sitzung nicht erreichbar. Die Bilder kommen deshalb aus FLUX.2.

## Decision-Delta
IMP-41 hatte den Desktop mit Kachelraster, Ringen und Schnell-Tools ersetzt. Die Abweichung ist seit 2026-10-24 als Hinweis bei IMP-41 in `docs/spec/decisions.yaml` eingetragen (Wahl Manuel, „mach automatisch“); die Entscheidung bleibt APPROVED.

## Tests
Gesamtlauf Release am 2026-10-24 (Windows-PC):
- `dotnet build Trendo.sln -c Release`: 0 Warnungen, 0 Fehler.
- Trendo.Api.Tests: 15/15 bestanden.
- Trendo.Core.Tests: 542 bestanden, 1 übersprungen (von 543).
- Trendo.App.Tests: 300 bestanden, 1 übersprungen, 24 min 28 s (Nachlauf nach der Darstellungs-Prüfung: 24 min 34 s, ebenso grün). Der übersprungene Test ist der T-0202-Klick-Audit.
- Paket-Tests `tests/claude_handoff`: 8/8 OK.

## Nachprüfung Darstellung (2026-10-24, Render `tools/Trendo.Render`)
- Geprüft: 1536 × 800 dunkel und hell, 1024 × 768, 3840 × 2160.
- Behoben:
  - Skalierung war bei Faktor 2 gedeckelt, jetzt 3. 4K füllt damit die Fläche.
  - Slogan und Ringwerte im Hellmodus waren kaum lesbar.
  - Die Statusfarbe hat jetzt im Hellmodus genug Kontrast.

## Nicht geprüft
- Echte Monitore mit 150 % DPI. Geprüft sind nur Renders.
- Pi-Edition: Bilder 1,5 MB plus Hintergrund, Leistung nicht gemessen.
