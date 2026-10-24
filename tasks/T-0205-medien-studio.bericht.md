# T-0205 Bericht: Videos & Fotos bearbeiten

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Umgesetzt
- Datei per Dialog/Ziehen (Pfadfeld-Erkennung) oder aus „Zuletzt“; Länge über ffprobe.
- Video: Zeitleiste mit 6 Vorschaubildern, Regler Anfang/Ende, „Ausschnitt speichern“, „Zur Clipliste“; Ergebnis „Abspielen / Öffnen“ und „Im Ordner zeigen“.
- Clips aneinanderhängen (`MediaStudio.Concat`, neu kodiert).
- Bilder: zuschneiden, drehen, Helligkeit/Kontrast, Text, Speichern als JPG/PNG (`ImageTools.Apply`, Skia) mit Ergebnisvorschau.
- Ohne FFmpeg: klarer Hinweis, Bildbearbeitung geht trotzdem. FFmpeg wird nicht installiert.
- Tests `MediaEditTests` (4, Video mit echtem FFmpeg-Testclip).

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
