# Bericht T-0243 – Entpacken/Packen mit Konfliktregel

- **Status:** IMPLEMENTED, automatisierte Tests grün. NICHT PC_VERIFIED.

## Änderung
- `src/Trendo.Core/Sys/Sys02.cs`: `ArchiveConflict` (Ask/Skip/KeepBoth/Overwrite), `ArchiveConflictException` (nennt die Dateien), `Archives.FreeName`, `UnpackConflicts`; `Unpack` und `Pack` brechen standardmäßig vor jeder Änderung mit Rückfrage ab; Zip-Slip-Schutz unverändert; `Pack` liefert den geschriebenen Pfad.
- `src/Trendo.App/MainWindow.Sys02.cs`: Rückfrage „Beide behalten / Vorhandene behalten / Ersetzen / Abbrechen“, Audit-Eintrag beim Entpacken, gewählte ZIP-Pfade bleiben nach einer Meldung stehen.
- Tests: `tests/Trendo.Core.Tests/Sys02Tests.cs` (`Zip_conflicts`), `tests/Trendo.App.Tests/ZipConflictUiTests.cs` (neu).
- `Software.InstallPortable` (Vollpaket) unberührt: entpackt in einen zuvor geleerten Ordner.
- Version 0.9.18, `installer/neuerungen/0.9.18.md`.

## Nachweise
- Volltest: Api 15/15, Core 849 bestanden/1 übersprungen, App 435 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0243/volltest.log`, SHA-256 `26666fde0ee0dde2c77455bbcbe91d5a8c889c9999e34d3fe6281b45bff0c52b`

## Nicht geprüft / offen
- Zip-Bomb-Grenzen (Größe/Anzahl) sind nicht Teil dieses Auftrags (eigener Punkt aus V107/V112).
- Echte Bedienung am PC.
