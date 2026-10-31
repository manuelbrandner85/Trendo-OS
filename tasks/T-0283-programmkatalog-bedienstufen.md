# T-0283 – Masterprompt 131: Programmkatalog, Produktionspakete, drei Bedienstufen

- Freigabe: Manuel im Chat am 31.10.2026 (Masterprompt „GODMODE 131“, „Ja, weiter“). Mehrsprachigkeit DE/IT/EN bewusst **nicht** gebaut – Manuel: „nur deutsch“.

## Drei Bedienstufen (Masterprompt 7, Abnahmekriterium 14)
- `UserLevel` (Kern): Einfach · Erweitert · Professionell; Einstellung `UserSettings.Level` (Standard Erweitert, Kinderprofil Einfach).
- Programm-Hülle: Umschalter im Kopf jedes Programms und unter Bedienung → Sehen. Werkzeuge haben eine Stufe (`ProgramCmd.Level`, `ProgramSpec.GroupLevels`/`CmdLevels`); Einfach blendet Fachwerkzeuge aus (z. B. Platine, Ausgleichskurven, Umformen, Eurocode) und zeigt nur das erste Exportformat.
- Erweitert zeigt alle bisherigen Werkzeuge – nichts Vorhandenes verschwindet.
- Professionell zusätzlich: Befehlssuche (alle Befehle, auch die der Stufe verborgenen), Verlauf mit Sprung zurück, Projektdaten direkt bearbeiten (nur gültiges JSON-Objekt, ein Schritt im Verlauf, rückgängig machbar).
- Wechsel ändert nie das Dokument (Test vergleicht die Daten vorher/nachher).

## Zentraler Programmkatalog (Masterprompt 19)
- `ProgramCatalog`/`ProgramEntry` (Kern), Seite „Programmkatalog“ (System, Suche nach Programm/Funktion/Format, Bereichsfilter, „Öffnen“ startet das echte Programm).
- Programme = geöffnete Seiten je Fenster (Reiter gehören dazu), nur über Startmenü/Suche/Studio erreichbare – 85 Programme in 54 Studios.
- Jede Angabe ist abgeleitet, nicht behauptet: Arbeitsfläche aus den tatsächlich angezeigten Bedienelementen, Fach-Engine aus echten Verwendungen von Kern-Typen, Formate und externe Programme aus dem Quelltext, Funktionen aus den freigegebenen Funktionskatalogen, Vorbild-Analysen aus `docs/claude/vorbilder`, Tests aus den Testnamen, Modulstatus aus `requirements.yaml` (nur gelesen).
- Ehrlicher Befund: viele Seiten haben „keine eigene Arbeitsfläche erkannt“ – das sind die Kandidaten für den Umbau zu echten Programmen.

## Produktionspakete (Masterprompt 17)
- `docs/programme/<seite>/` mit README, ARCHITECTURE, REA_REFERENCE_ANALYSIS, FEATURE_MATRIX, IMPLEMENTATION_STATUS, USER_WORKFLOWS, TEST_PLAN, TEST_RESULTS, PERFORMANCE_REPORT, LICENSE_REPORT, INTEGRATION_REPORT; Übersicht `docs/programme/KATALOG.md`, Daten `katalog.json` (im Programm eingebettet).
- Testergebnisse nur aus echtem Lauf (TRX); ohne Lauf steht dort „noch kein Testlauf eingelesen“, „Getestet“ nur bei bestandenem Test. Leistung: ehrlich „nicht gemessen“.
- Neu erzeugen: `TRENDO_SHOT=seite TRENDO_KATALOG=. dotnet run --project tools/Trendo.Render -c Release -- x.png 1600 1000 start`, mit Ergebnissen zusätzlich `TRENDO_KATALOG_TRX=<ordner mit .trx>`.

## Tests
- Kern `MP131_Levels` (2), `MP131_Catalog` (3); App `ProgramLevel_Ui` (2), `ProgramCatalog_Ui` (1, prüft auch, dass der mitgelieferte Katalog alle angemeldeten Programme enthält).
