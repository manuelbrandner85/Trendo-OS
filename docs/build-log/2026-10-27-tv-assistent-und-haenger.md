# 2026-10-27 – TV & Live: KI-Assistent, Anbieter-Filter, Hänger behoben

## Anlass
- Nutzerwunsch: TV & Live per KI steuern („Blende alle Sender aus und behalte nur deutsche“), dasselbe für Filme und Serien, Serien und Filme nach Anbieter (Netflix, Amazon Prime, Disney+ …).
- Nutzerbericht: Trendo OS hängt oft („Keine Rückmeldung“).

## Ursache des Hängers
- Windows-Ereignisprotokoll: `AppHangB1` für TrendoOS.exe 0.9.12 (zweimal am 27.10.2026).
- Die Wiedergabeliste des Nutzers hat rund 339 000 Einträge (91 MB). `TvLibrary.Series()` las die gesamte Liste **für jede Serie erneut** (quadratischer Aufwand), und alle Listen wurden im Bedienfaden gelesen.
- Daneben: kaputte Cover-Bilder lösten im Hintergrund eine unbeobachtete `NullReferenceException` aus (Absturzberichte unter `logs/absturz-*.txt`).

## Änderungen
- `src/Trendo.Core/Tv/IpTv.cs`: Zwischenspeicher für gelesene Listen und den Zustand (`tv.json`), Serien und Folgen in einem Durchgang; Schreiben macht den Zwischenspeicher ungültig.
- `src/Trendo.App/MainWindow.Tv.cs`: große Listen (ab 4 MB) im Hintergrund lesen, mit Ladeanzeige; Parsen, Speichern und Programmführer-Abgleich außerhalb des Bedienfadens; gesperrte/ausgeblendete Gruppen einmal je Ansicht statt je Sender gelesen; früher als Adresse gespeicherte get.php-Quellen laufen über die Xtream-Schnittstelle.
- `src/Trendo.App/MainWindow.TvVod.cs`: Schnellwahl „Anbieter“ und „Sprache“ bei Filmen und Serien; ausgeblendete Gruppen gelten auch dort; Bildfehler werden abgefangen.
- `src/Trendo.Core/Tv/TvCommands.cs` (neu): feste deutsche Regeln, Anbieter- und Spracherkennung aus Kategorienamen, KI-Rückfall mit erlaubten Aktionen (JSON, höchstens 5).
- `src/Trendo.App/MainWindow.TvAssistant.cs` (neu): Eingabe „✨ Sag, was Trendo tun soll …“ mit Beispielen; Ausführung der Aktionen mit Rückmeldung und Audit-Eintrag.

## Tests
- Neu: `tests/Trendo.Core.Tests/TvCommandsTests.cs` (Regeln, Anbieter/Sprache, Liste mit 120 000 Einträgen), `TvStudioUiTests.Assistant_and_providers`.
- Ergebnisse des Volltests stehen in der Commit-Nachricht.

## Nicht geprüft
- Echte Wiedergabe beim Anbieter des Nutzers (bewusst kein Zugriff auf dessen Zugang).
- Ladezeit mit der echten 91-MB-Liste auf dem PC (nur mit 120 000 nachgebildeten Einträgen gemessen).
