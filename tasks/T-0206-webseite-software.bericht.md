# T-0206 Bericht: Webseite bauen: Code, Vorschau

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC)

## Umgesetzt
- Bereich „Code ansehen und ändern“: Dateiliste, Editor mit Zeilennummern, „Speichern“ baut die Browser-Vorschau neu.
- „In VS Code öffnen“, wenn installiert (PATH oder Standardordner), sonst Hinweis.
- Änderung per Text („Überschrift in …“) zuerst als Vorschau (− alte / + neue Zeilen, `WebsiteStudio.PreviewRevise` auf einer Kopie), dann „Übernehmen“ oder „Verwerfen“.
- Test `Code_editor_and_change_preview`.

## Offen
Die Änderungshilfe ist regelbasiert (Überschrift, Untertitel, Farbe, Ergänzung); eine KI-Formulierung freier Änderungen ist nicht eingebaut.

## Tests
Gesamtlauf Release am 2026-10-24 (Stand inkl. T-0218, Windows-PC): `dotnet build Trendo.sln -c Release` 0 Warnungen, 0 Fehler · Trendo.Api.Tests 15/15 bestanden · Trendo.Core.Tests 542 bestanden, 1 übersprungen (von 543) · Trendo.App.Tests 300 bestanden, 1 übersprungen (T-0202-Klick-Audit, nur mit TRENDO_ALL_BUTTONS=1), 24 min 28 s · Paket-Tests `tests/claude_handoff` 8/8 OK.
