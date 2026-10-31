# Bericht T-0244 – Ehrliche Texte und ehrliche Tests

- **Status:** IMPLEMENTED, automatisierte Tests grün.

## Änderung
- Texte: `src/Trendo.App/MainWindow.Spec04.cs` („innerhalb der Richtwerte (auf dem Gerät nicht geprüft)“, „Deine Messung (ms)“, „Laut deiner Messung …“), `src/Trendo.Core/Hardware.cs` („sollte hier gut laufen“).
- Tests: neues Attribut `tests/Trendo.Core.Tests/NeedsFactAttribute.cs` (Programm/Pfad/Projektdatei fehlt → übersprungen mit Grund) und `tests/Trendo.Api.Tests/NeedsLanFactAttribute.cs`; in der App (xUnit v3) `Assert.Skip`.
- Umgestellt: LetterQa (2), TextDoc (1), W4Dev (1), W4Media (1), W4Oss (1), W4Studio3D (1), Sys10 (2), Api (1), App MediaEdit/W4MediaUi/WebsitePreview (3).
- Abgetrennt (Programmteil mitten im Test als eigener Test): `Sys02.Ffmpeg_real`, `Sys10.NewAndRead_real`, `Sys10.PdfMergeSplit`, `Sys10.ScanOcr`, `Sys10.StripMeta_real`.
- `tests/Trendo.App.Tests/W6UiTests.cs` an die neuen Texte angepasst.
- Version 0.9.20.

## Nachweise
- Volltest: Api 15/15, Core 848 bestanden/10 übersprungen (vorher 1), App 436 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0244/volltest.log`, SHA-256 `ba5e90494a762485e7ee22ca539338ed0f040860c8ddb31871c623e17db8c8fd`
- Übersprungen auf diesem PC (mit Grund im Log): 6× LibreOffice/Tesseract (Sys10), 2× LetterQa, BlueZ (Linux), Blender.

## Auffällig / offen
- Der Blender-Test lief bei der gezielten Ausführung (Blender gefunden), im Volltest meldete er sich als übersprungen. `Cli.Find("blender")` liefert also je nach Testlauf unterschiedliche Ergebnisse – Ursache noch nicht geklärt.
- Weitere stille Abbrüche mit anderem Muster (z. B. `if (!ok) return;`) wurden nicht gesucht.
