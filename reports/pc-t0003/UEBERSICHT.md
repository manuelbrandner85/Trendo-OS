# PC-Verifikation T-0003

Lauf am 2026-10-22 auf diesem Windows-PC, Konfiguration Release.
Filter je Modul: `DisplayName~<Modul>` in `tests/Trendo.Core.Tests` und `tests/Trendo.App.Tests`.
Rohprotokoll: `reports/pc-t0003/<Modul>-core.trx` und `<Modul>-app.trx`.
Zähler aus `Counters` der jeweiligen `.trx`. In allen 44 Modulprotokollen ist `failed="0"`, `passed` gleich `total`, Ergebniszusammenfassung `Completed`.

`tests/Trendo.Api.Tests` enthält keine dieser Modul-IDs. Dafür liegt die komplette Api-Suite als `suite-api.trx` bei.

SHA-256 steht in dieser Übersicht noch nicht. Grok Bot ergänzt die Prüfsummen.

| Modul | Testfilter | Testanzahl | Ergebnis | Datei |
| --- | --- | --- | --- | --- |
| CORE-02 | DisplayName~CORE-02 | 5 (Core 2, App 3) | grün | CORE-02-core.trx, CORE-02-app.trx |
| CORE-03 | DisplayName~CORE-03 | 4 (Core 3, App 1) | grün | CORE-03-core.trx, CORE-03-app.trx |
| CORE-09 | DisplayName~CORE-09 | 13 (Core 11, App 2) | grün | CORE-09-core.trx, CORE-09-app.trx |
| CORE-10 | DisplayName~CORE-10 | 4 (Core 3, App 1) | grün | CORE-10-core.trx, CORE-10-app.trx |
| CORE-11 | DisplayName~CORE-11 | 3 (Core 2, App 1) | grün | CORE-11-core.trx, CORE-11-app.trx |
| CORE-12 | DisplayName~CORE-12 | 4 (Core 3, App 1) | grün | CORE-12-core.trx, CORE-12-app.trx |
| SYS-04 | DisplayName~SYS-04 | 3 (Core 2, App 1) | grün | SYS-04-core.trx, SYS-04-app.trx |
| SYS-05 | DisplayName~SYS-05 | 4 (Core 3, App 1) | grün | SYS-05-core.trx, SYS-05-app.trx |
| SYS-06 | DisplayName~SYS-06 | 3 (Core 2, App 1) | grün | SYS-06-core.trx, SYS-06-app.trx |
| SYS-07 | DisplayName~SYS-07 | 3 (Core 2, App 1) | grün | SYS-07-core.trx, SYS-07-app.trx |
| SYS-08 | DisplayName~SYS-08 | 3 (Core 2, App 1) | grün | SYS-08-core.trx, SYS-08-app.trx |
| SYS-09 | DisplayName~SYS-09 | 4 (Core 3, App 1) | grün | SYS-09-core.trx, SYS-09-app.trx |
| SPEC-08 | DisplayName~SPEC-08 | 12 (Core 10, App 2) | grün | SPEC-08-core.trx, SPEC-08-app.trx |
| SPEC-09 | DisplayName~SPEC-09 | 14 (Core 12, App 2) | grün | SPEC-09-core.trx, SPEC-09-app.trx |
| SPEC-10 | DisplayName~SPEC-10 | 15 (Core 13, App 2) | grün | SPEC-10-core.trx, SPEC-10-app.trx |
| SPEC-12 | DisplayName~SPEC-12 | 3 (Core 2, App 1) | grün | SPEC-12-core.trx, SPEC-12-app.trx |
| SPEC-13 | DisplayName~SPEC-13 | 12 (Core 10, App 2) | grün | SPEC-13-core.trx, SPEC-13-app.trx |
| SPEC-14 | DisplayName~SPEC-14 | 11 (Core 9, App 2) | grün | SPEC-14-core.trx, SPEC-14-app.trx |
| SPEC-15 | DisplayName~SPEC-15 | 12 (Core 10, App 2) | grün | SPEC-15-core.trx, SPEC-15-app.trx |
| SPEC-16 | DisplayName~SPEC-16 | 11 (Core 9, App 2) | grün | SPEC-16-core.trx, SPEC-16-app.trx |
| X-02 | DisplayName~X-02 | 3 (Core 2, App 1) | grün | X-02-core.trx, X-02-app.trx |
| X-05 | DisplayName~X-05 | 3 (Core 2, App 1) | grün | X-05-core.trx, X-05-app.trx |

Modulzeilen: 22. Summe der gefilterten Tests: Core 117, App 32, zusammen 149, alle bestanden.

Gesamtsuiten ohne Filter, dieselbe Release-Ausgabe, Zähler der `.trx`:

| Projekt | Datei | Ergebnis |
| --- | --- | --- |
| Trendo.Core.Tests | suite-core.trx | 427 ausgeführt, 427 bestanden, 0 fehlgeschlagen |
| Trendo.App.Tests | suite-app.trx | 132 ausgeführt, 132 bestanden, 0 fehlgeschlagen |
| Trendo.Api.Tests | suite-api.trx | 14 ausgeführt, 14 bestanden, 0 fehlgeschlagen |
