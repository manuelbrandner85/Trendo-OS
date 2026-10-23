# Bericht: Tests überspringen fehlende Programme sichtbar

**Auftrag:** Stille grüne Tests bei fehlendem externen Programm finden und sichtbar überspringen. SPEC-10 mit festen Messwerten stabil machen. SYS-03-PDF darf nicht hängen. Studio-Karten beschleunigen, ohne eine Karte auszulassen. Share.cs nicht anfassen.

**Branch:** `qa/skip-ohne-externe-programme`

**PR:** https://github.com/manuelbrandner85/Trendo-OS/pull/278

## Geänderte Dateien

Produkt, nur zwei Nähte. Ohne gesetzte Quelle bleibt das bisherige Verhalten.

- `src/Trendo.App/MainWindow.Spec10.cs` – `PcSampleSource`, sonst die alte Live-Formel
- `src/Trendo.App/MainWindow.axaml.cs` – `DependencyProbe`, sonst `CheckAsync`

Tests:

- `tests/Trendo.Api.Tests/Trendo.Api.Tests.csproj`, `tests/Trendo.Api.Tests/ApiTests.cs`
- `tests/Trendo.Core.Tests/Trendo.Core.Tests.csproj`
- `tests/Trendo.Core.Tests/CameraRobotTests.cs`, `LetterQaTests.cs`, `Sys02Tests.cs`, `Sys10Tests.cs`, `Tests.cs`, `W4DevTests.cs`, `W4GameTests.cs`, `W4MediaTests.cs`, `W4OssTests.cs`, `W4Studio3DTests.cs`, `W5FusionTests.cs`, `W7Tests.cs`, `WinRealTests.cs`
- `tests/Trendo.App.Tests/StudioClickTests.cs`, `Sys03UiTests.cs`, `UiTests.cs`, `W4MediaUiTests.cs`, `W6UiTests.cs`, `W7UiTests.cs`, `WebsitePreviewTests.cs`

`src/Trendo.App/MainWindow.Share.cs` ist nicht geändert.

## Testergebnis

Alter Stand ist Commit `af4f3a7`. Neuer Stand ist dieser Branch. Alle Läufe: `dotnet test … -c Release`. App zusätzlich mit `--blame-hang-timeout 120s`.

| Projekt | Vorher | Nachher | Status |
| --- | --- | --- | --- |
| Api | 15 gesamt, 15 bestanden, 0 übersprungen, 0 fehlgeschlagen, 2,025 s | 15 gesamt, 15 bestanden, 0 übersprungen, 0 fehlgeschlagen, 1,381 s | geprüft |
| Core | 539 gesamt, 529 bestanden, 10 übersprungen, 0 fehlgeschlagen, 9,563 s | 555 gesamt, 531 bestanden, 24 übersprungen, 0 fehlgeschlagen, 7,785 s | geprüft |
| App, ganzer Lauf | abgebrochen. Blame nach 2 Minuten ohne Regung, laufender Test `StudioClickTests.Alle_Studios_Karten`. Bis dahin 16 bestanden, Summe unbekannt | 228 gesamt, 228 bestanden, 0 übersprungen, 0 fehlgeschlagen, 3,545 min. Blame hat nicht abgebrochen | vorher nicht prüfbar, nachher geprüft |
| App ohne die beiden bekannten Hänger (alter Stand) | 220 bestanden, 0 übersprungen, 0 fehlgeschlagen, 3 min 32 s | entfällt, der ganze Lauf ist grün | geprüft |

Die 10 Übersprungen vorher sind die Windows-Tests (`WinFact`). Die 24 nachher sind dieselben 10 plus 14 neue, sichtbare Sprünge, weil Blender, LibreOffice, Tesseract oder der Windows-Anmeldeinformationsspeicher hier fehlen. FFmpeg, Git, Node, Python, das .NET SDK und pdfinfo sind da und laufen echt (FFmpeg-Medien etwa 4 s, .NET-Vorlage etwa 2 s).

Studio-Karten, Klasse allein: 8 bestanden, 0 übersprungen, 0 fehlgeschlagen, 16 s. Im Gesamtlauf, parallel zu den anderen UI-Tests: Teil 1 und 5 je 4 s, Teil 2 3 s, Teil 3 5 s, Teil 4 und 6 je 7 s, Startseite 1 s. Alle Karten bleiben geprüft. Kein stiller Hänger.

SYS-03-P14 ist auf diesem Rechner bestanden, Dauer 20 s. Chrome hat ein PDF geliefert, das die A4-Prüfung erfüllt, inklusive Prüfsumme im Protokoll. Der Skip für „kein A4“ und der Skip für die Zeitüberschreitung sind in diesem Lauf nicht angesprungen.

## Share.cs

`MainWindow.Share.cs` Zeile 22 wartet beim Schließen höchstens eine Sekunde, und nur wenn `ActiveShare` gesetzt ist. Ist keine Freigabe offen, läuft `.Wait(1000)` nicht. Die Kartentests starten keine Freigabe. Die Datei hält diese Tests nicht auf. Geprüft. Nicht geändert, daran arbeitet PR #276.

## Offene Punkte und Risiken

- Auf einem Rechner mit Blender, LibreOffice oder Tesseract laufen die jetzt übersprungenen Tests echt. Das ist hier nicht geprüft, die Programme fehlen.
- Die inneren Windows-Prüfungen (Defender, Chrome-Druck, qpdf, Anmeldedatenspeicher) laufen hier nicht, weil `WinFact` die Methode vorher überspringt.
- Dieser Chrome hat A4 geschafft. Die bekannte Letter-Abweichung (612×792 pt) ist auf diesem Image diesmal nicht aufgetreten. Der Skip dafür ist im Test, aber in diesem Lauf nicht ausgeführt.
- QA-017, QA-019 und die Codesignatur sind nicht geprüft und nicht als bestanden markiert.

## Nicht geprüft

- Ein Windows-PC mit den echten Werkzeugen.
- Ob der A4-Skip und der Chrome-Timeout-Skip den Text so anzeigen, wie er im Code steht. Der Druck selbst ist geprüft und bestanden.
- Produktverhalten außerhalb der beiden Nähte. Druckargumente, Papierformat und `Cli.Run` sind unverändert.
