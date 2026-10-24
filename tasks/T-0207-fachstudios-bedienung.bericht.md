# T-0207 Bericht: Fach-Studios: Auswahl, Prüfung, Stand, Grafik

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Umgesetzt (gilt für alle 14 Fach-Studios über `FachTool`)
- Auswahllisten statt Freitext: Ja/Nein-Felder, Kurzwahl (d/h, N/S, 90/95/99, reihe/parallel, r/c) und feste Mengen (Baustandard, Raumart, Einheiten, Lagerung, Euler-Fall, Werkstoff, Schraubengröße/-klasse, CNC-Werkstoff, Farben der Bildauswertung, Rechenart). Hinweistexte ohne Aufzählungen.
- Zahlenfelder prüfen sofort (rote Kante + Hinweis), Einheit steht in der Beschriftung.
- Stand aller Werkzeuge (Eingaben und Ergebnisse) wird gespeichert und beim nächsten Start geladen (`fach-stand.json` im Datenordner).
- Grafik im Studio: `SvgLite` zeichnet die eigenen SVGs (Grundriss, Strecke, Histogramm, Streudiagramm, G-Code-Vorschau) unter dem Ergebnis; neues Balkenbild für die Statik (`Engineering.BeamSvg`); Grundriss-Vorschau in „Räume & Wohnen“.
- Rechenformeln unverändert; Katalognamen unverändert.
- Tests `FachBedienungTests` (5), `W6Ui.Run` bedient Auswahllisten.

## Name ↔ Umfang
Siehe `tasks/T-0213-namen-versprechen.bericht.md` (Live streamen, Kameras & Roboter, Gegenstände einscannen u. a.) – Entscheidung Manuel.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
