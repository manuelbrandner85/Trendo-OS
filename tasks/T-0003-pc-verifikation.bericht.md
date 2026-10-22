# T-0003 Bericht: PC-Verifikation

**Auftrag:** 22 Module mit Status TESTED_BOX auf diesem Windows-PC mit Rohprotokoll belegen und nur bei grünem Lauf auf PC_VERIFIED setzen.
**Branch:** `grok/T-0003-pc-verifikation`

## Geänderte Dateien

- `reports/pc-t0003/*.trx` – 44 Modulprotokolle (Core und App) plus `suite-core.trx`, `suite-app.trx`, `suite-api.trx`. Läufe lagen schon vor. Liste geprüft: keins der 22 Module fehlt.
- `reports/pc-t0003/UEBERSICHT.md` – 22 Zeilen, Filter, Anzahl, Ergebnis, Datei.
- `requirements.yaml` – nur `test_status` und `evidence` dieser 22 Module. Neu: `PC_VERIFIED`, Nachweis `reports/pc-t0003/UEBERSICHT.md`.
- `docs/build-log/2026-10-22-pc-verifikation.md` – Baulog.
- `.grok/config.toml` – eine bereits von Grok Bot gesetzte Zeile `Get-FileHash*` mitcommittet. Sonst nichts an der Datei geändert.
- `tasks/T-0003-pc-verifikation.md` – Auftrag, bisher nicht versioniert.
- `tasks/T-0003-pc-verifikation.bericht.md` – dieser Bericht.

Kein Code unter `src/`, keine Testdateien geändert. BUS-* nicht angefasst.

## Liste der Protokolle

Vorhanden und ausgewertet: CORE-02, CORE-03, CORE-09, CORE-10, CORE-11, CORE-12, SYS-04, SYS-05, SYS-06, SYS-07, SYS-08, SYS-09, SPEC-08, SPEC-09, SPEC-10, SPEC-12, SPEC-13, SPEC-14, SPEC-15, SPEC-16, X-02, X-05, jeweils `-core.trx` und `-app.trx`. Zusätzlich die drei Gesamtsuiten. Nichts nachzuholen.

Filter der Modulläufe: `DisplayName~<Modul>`. Die Anzahlen in den Protokollen decken die Tests, deren Anzeigename mit der Modul-ID beginnt (Core und App, inklusive der Ausbau-Fälle Fnn). `tests/Trendo.Api.Tests` nennt diese IDs nicht.

## Testergebnis

Release, Ausgabe „Bestanden!“, Zähler deckungsgleich mit `Counters` der `.trx` (alle `failed=0`):

- `Trendo.Core.Tests`: Fehler 0, erfolgreich 427, übersprungen 0, gesamt 427, Dauer 16 s (`suite-core.trx`)
- `Trendo.App.Tests`: Fehler 0, erfolgreich 132, übersprungen 0, gesamt 132, Dauer 4 m 8 s (`suite-app.trx`)
- `Trendo.Api.Tests`: Fehler 0, erfolgreich 14, übersprungen 0, gesamt 14, Dauer 2 s (`suite-api.trx`)

Gefilterte Modul-Tests zusammen: Core 117, App 32, alle bestanden. Einzelzeilen in der Übersicht.

## Offene Punkte / Risiken

- SHA-256 der `.trx` steht nicht in der Übersicht. Ein einzelner `Get-FileHash`-Aufruf ist erlaubt und lief. Die Summen sind hier nicht eingetragen, damit keine vertauschte Summe aus parallelen Aufrufen in den Nachweis kommt. Grok Bot ergänzt sie.
- Alle 47 `.trx`-Dateien enthalten den Windows-Kontonamen und den Rechnernamen im Testlauf-Kopf (`name`, `runUser`, Deployment-Wurzel) sowie absolute Pfade `C:\Users\<user>\Desktop\Trend Os\repo\...` in `storage` und `codeBase`. Nicht gelöscht, wie vorgegeben.
- Die Rohprotokolle bleiben damit personenbezogen. Wer das öffentliche Repo liest, sieht Konto und Rechner.
- QA-017, QA-019 und die Codesignatur sind nicht geprüft und nicht als bestanden gewertet.

## Nicht geprüft

- Kein Start der installierten Desktop-App und kein Klick mit der Maus. Geprüft haben die automatischen Tests.
- Die Gesamtsuiten wurden in dieser Sitzung nicht noch einmal gestartet. Ausgewertet sind die `.trx` vom 2026-10-22, etwa 02:42–02:48 Uhr. Danach gab es keine Änderung an `src/` oder `tests/`.
- Keine echte Hardware-Sonderprüfung über die Tests hinaus (kein separates winget-Installieren, kein Tor-Browser, kein Mailversand, kein zweiter Bildschirm).
- BUS-* und alle übrigen Module unverändert gelassen. SPEC-07 nicht gebaut.
