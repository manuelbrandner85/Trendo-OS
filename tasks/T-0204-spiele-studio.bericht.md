# T-0204 Bericht: Spiele-Studio: eigenes Spiel

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Umgesetzt
- `GameStudio.GameSettings`: Tempo (1–10), Leben, Punkte zum Sieg, Farben (Spielfigur, Ziel/Ball, Hintergrund), Ton, Level-Raster, Spielfigur- und Hintergrundbild. Gespeichert als `spiel.json` + `config.js`; alle drei Vorlagen lesen nur `CFG` (Leben, Sieg, Ton per WebAudio, Bilder).
- Bilder werden in den Spielordner kopiert (Weitergabe), Prüfung von Farben/Level.
- `GameStudio.Export`: ZIP zum Weitergeben.
- Spiele-Studio: Editor nach „Anlegen“ oder „Bearbeiten“ (Liste), Level als klickbares Raster (Mauerbrecher, Hüpfspiel), „Speichern und Vorschau“ (Browser-Bild im Studio), „Spielen“, „Als ZIP weitergeben“.
- Unreal: zusätzlich „Vorhandenes Projekt öffnen“ (.uproject per Dialog); ohne Unreal klarer Hinweis.
- Tests `GameEditorTests` (3).

## Offen
- Untertitel „Eigene Spiele bauen – mit der Unreal Engine“ bleibt (Katalog nicht ändern); Vorschlag in T-0213.
- Vorschau braucht Chrome/Chromium; ohne erscheint ein Hinweis.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
