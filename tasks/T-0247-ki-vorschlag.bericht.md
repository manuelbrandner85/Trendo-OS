# Bericht T-0247 – KI-Umschreiben als Vorschlag (108-49)

- **Status:** IMPLEMENTED, automatisierte Tests grün (mit nachgebildeter KI). NICHT PC_VERIFIED, nicht mit einem echten Sprachmodell geprüft.

## Änderung
- `src/Trendo.Core/Office/TextTools.cs`: `DiffWords` (wortweiser Unterschied, nutzt die vorhandene LCS aus „Vergleichen“).
- `src/Trendo.App/MainWindow.TextPro.cs`: `TextAiRewrite` legt nur noch einen Vorschlag an; `TextAiDecide` übernimmt nur, wenn die Stelle unverändert ist; Vorschlagskarte mit rot/grün markierten Änderungen; veraltete Markierung nach „Zurück“ wird abgefangen.
- `src/Trendo.App/MainWindow.Text.cs`: Karte unter der Befehlsleiste.
- Tests: `tests/Trendo.App.Tests/TextAiProposalTests.cs` (neu), `TextDocTests.Diff_words` (neu).
- Version 0.9.21.

## Nachweise
- Volltest: Api 15/15, Core 856 bestanden/3 übersprungen, App 437 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0247/volltest.log`, SHA-256 `224aa95e2d013f313d8efa3531f723cf7e2bbfcef732e9007ad2fc1edb63e631`

## Offen
- Andere Bearbeitungen nach „Zurück“ könnten ebenfalls eine veraltete Markierung haben – nicht untersucht.
