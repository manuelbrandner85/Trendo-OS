# T-0208 – Prüfergebnis und Plan (nur Analyse)

Stand: 22.10.2026, Branch `cursor/t-0208-business-18f9` bei `1382abd`.
Verglichen mit `origin/main` bei `af4f3a7` (Merge-Lauf #243–#259 läuft). Kein Rebase, keine Umsetzung, nichts gemergt.

Nicht angefasst und im folgenden Plan ausgenommen:

- `PromptActions.CreateDefault` bzw. die Prompt-Aktion-Registrierung (`PromptAction/*`, #243–#259, besonders #249 und #259)
- die Capability-Matrix (`docs/improvement-42/studio-capability-matrix.md`, `CapabilityCatalog.cs`)
- `RebuildAdapter` / `src/Trendo.Core/Rebuild/Adapter.cs` (X-10, #248)

Bestätigte Studios bleiben. Kein Studio wird gelöscht oder durch einen Platzhalter ersetzt. Keine Zahlung, kein Versand, keine Veröffentlichung, keine kostenpflichtige KI.

## Prüfergebnis

### Was der Branch schon kann

Auftrag `tasks/T-0208-business.md`: stumme Knöpfe in Betrieb, Marke und Markt melden sich, der CSV-Import stürzt bei leerem Pfad nicht ab, Einträge sind sofort sichtbar und lassen sich ändern und löschen.

Der Branch erfüllt diesen Auftrag im Code, gemessen am Stand vor dem Merge-Lauf:

| Kriterium | Branch | `origin/main` (Commit `558bb24`) |
|---|---|---|
| Leere Pflichtfelder: eigener Satz | ja, `BizNeed` | ja, kürzeres `Say` |
| Pflichtfeld rot | ja, Rahmenstärke 2 über `Mark` | nein |
| Eintrag sofort in der Liste | ja | ja (Seite wird neu aufgebaut) |
| Ändern und Löschen | ja, Kontakte, Tarife, Mitglieder, Ware, Anfragen, Kundenweg, Ablauf, Mitbewerber, Probleme, Tests | nein |
| Import per Dateidialog, leerer Pfad ohne Absturz | ja, `StorageProvider` plus Test-Haken `BizFileDialog`; eingefügte CSV-Zeilen gehen auch | nur Hinweis „Pfad eintragen“, weiterhin getippter Pfad, kein Absturz |
| `MainWindow.W5Launch.cs` | unverändert, wie im Bericht vermerkt | unverändert durch T-0208 |

Geänderte Dateien gegenüber dem gemeinsamen Vorfahren `57f000b`: `MainWindow.W5Ops.cs`, `W5Brand.cs`, `W5Market.cs`, `Look.cs` (drei Hilfetexte), `MainWindow.axaml.cs` (Hilfe-Knopf klappt zu), `tests/Trendo.App.Tests/BusinessButtonTests.cs`, Bericht und Baulog.

Die Seiten selbst sind die bestehenden Studios: Geschäftsidee, Markt, Marke & Kunden, Betrieb, Start & Übergabe, Geld. Katalognamen in `StudioCatalog.cs` sind auf diesem Branch unberührt.

### Was auf main schon anders liegt

`origin/main` enthält eine kleinere T-0208-Fassung (`558bb24`, 22.10.2026 04:02): dieselben 14 Knöpfe bekommen einen Satz, der leere CSV-Pfad wirft nicht mehr. Dazu `tests/Trendo.App.Tests/BusinessFeedbackTests.cs` (12 leere Eingaben plus „Tarif Gold“). Ändern, Löschen, rote Rahmen und der Dateidialog fehlen dort.

PR [#223](https://github.com/manuelbrandner85/Trendo-OS/pull/223) zu diesem Branch ist seit 22.10.2026 16:37 geschlossen, ohne Merge. Der Schließkommentar verweist auf das 3D-Studio (#230). Das trifft T-0208 nicht. Die reichere Fassung (Dialog, Ändern, Löschen) ist nicht auf `main`.

Danach hat `main` weitergezogen (Design 41, Webseiten-Studio, weitere Merges bis `af4f3a7`). Dieser Branch sitzt noch auf `57f000b`. Ein späterer Merge kollidiert voraussichtlich in:

- `MainWindow.W5Ops.cs`, `W5Brand.cs`, `W5Market.cs` (dieselben `return`-Zeilen, die `558bb24` schon umgeschrieben hat)
- `MainWindow.axaml.cs` (auf `main` stark umgebaut; hier nur der Hilfe-Knopf)
- `MainWindow.Look.cs` (Hilfewörterbuch)

`W5Launch.cs` ist zwischen Branch und dem T-0208-Commit auf `main` nicht auseinander gelaufen. Ob Design 41 die Hülle um die Seite ändert, ist beim späteren Merge zu prüfen, nicht jetzt.

### Was fehlt: echte Dateien

Interne Listen liegen als JSON unter `business/<idee>/*.json` (`IdeaList`). Das ist Speicher, kein Export, den man weitergibt. Sichtbare Nutzerdateien schreiben heute nur einzelne Knöpfe, und die stammen aus dem älteren Stand, nicht aus diesem Branch:

| Studio-Seite | Knopf | Datei |
|---|---|---|
| Geschäftsidee | Bauplan erstellen | `…-Bauplan.md` im Arbeitsordner Business |
| Marke | Markenfarben speichern | `…-Marke.css` |
| Marke | Als Dokument speichern | `…-Kundenweg.md` |
| Betrieb | GiroCode | `girocode.txt` (Text, kein QR-Bild) |
| Betrieb | Häufige Fragen | `…-FAQ.md` |
| Geld | Als CSV speichern / Rechnung erstellen | CSV bzw. Rechnungsdatei über `Finance` |
| Start & Übergabe | Webseite bauen, Paket, Sichern | Ordner, Zip, lokale Sicherung. Nichts wird hochgeladen. |

Ohne eigene Datei bleiben, obwohl der Knopf jetzt eine Meldung zeigt:

- Betrieb: Kontakt, Tarif, Mitglied, Ware, Anfrage, übernommene Tageszahlen, Plan-gegen-Ist
- Marke: Farbberechnung (bis jemand extra speichert), SEO-Prüfung, Trichter, einzelne Ablaufschritte
- Markt: Mitbewerber, Problem, Nische, Varianten, Test und Bewertung
- Geschäftsidee: „Speichern und bewerten“ schreibt nur `ideen.json` und zeigt die Punkte auf der Seite

Der Auftragstext T-0208 verlangt diese Exporte nicht. Der offene Wunsch ist, dass dieselben Studios das Ergebnis als Datei ablegen, ohne die Prompt-Aktion-Schicht von #249/#259 zu benutzen. #249 schreibt Bauplan und HTML-Rechnung bereits über `BusinessPromptAdapter` und trägt das in die Matrix ein. Diese Dateien und die Matrix bleiben unangetastet. Die Studio-Knöpfe bekommen eigene Exporte in den bestehenden `Business*.cs`-Formatierern.

### Was kaputt oder riskant ist

- Der Branch ist nicht integriert. Gegen den laufenden `main` ist er veraltet. Ein Rebase jetzt würde den Merge-Lauf stören.
- Die letzte gemeldete Testreihe auf diesem Branch (Bericht, etwa 18:03 Uhr Europe/Berlin, Release) war nicht vollständig grün: Core 417 bestanden / 10 übersprungen, Api 14 bestanden, App 136 bestanden / 1 Fehler. Der Fehler ist `SYS-03-P14` (PDF über das Image-Chrome, US Letter). Das ist in `AGENTS.md` als bekannt eingestuft und kein Business-Defekt. Diese Zahlen sind nicht erneut gelaufen.
- `Act` existiert zweimal (`Action` in `W45.cs`, `Func<Task>` in `Sys02.cs`). „Übernehmen“ übergibt ein `async`-Lambda. Heute kompiliert das. Nach dem Merge mit Design 41 muss die Wahl der Überladung stimmen, sonst läuft der Dialog neben dem Test her.
- Der Knopf-für-Knopf-Test klickt jeden sichtbaren aktiven Knopf, auch „Übernehmen“ ohne gesetztes `BizFileDialog`. Ein hängender Dateidialog wäre ein Fehlschlag. Der Bericht sagt, der Filter mit 7 Fällen war damals grün. Nicht erneut geprüft.
- `requirements.yaml` führt BUS-001…030 als `IMPLEMENTED` / `TESTED_BOX`. Das ist der ältere Box-Stand, kein Beleg für diesen Branch. QA-021…024 in `docs/spec/qa/QA_ABNAHME.md` bleiben `NOT_RUN`. Dieser Plan ändert daran nichts.
- GiroCode bleibt Text. Ein QR-Bild wäre ein Zusatzprogramm und ist nicht Teil des nächsten Schritts.
- Start & Übergabe darf kein Live-Deployment werden. `BusinessLaunch.Package` erzeugt nur ein lokales Paket plus `VEROEFFENTLICHEN.md`.

## Umsetzungsplan (erst nach dem Merge-Lauf)

Voraussetzung: #243–#259 sind auf `main` oder bewusst liegen geblieben. Dann `main` in diesen Branch mergen (kein Rebase, kein Force-Push). Konflikte nur in den T-0208-Zeilen auflösen: Meldungen, rote Rahmen, Ändern/Löschen und Dateidialog behalten. Die Prompt-Registrierung, die Matrix und `Rebuild/Adapter.cs` bei einem Konflikt zugunsten von `main` lassen.

Danach, nur in den Business-Studios:

1. Export-Knöpfe neben den bestehenden Listen, Beschriftung „Als Datei speichern“. Die Listen und die bestätigten Seiten bleiben.
2. Eine Markdown- oder CSV-Datei je Block, im Arbeitsordner Business, Name aus `DevStudio.SafeName` plus festem Zusatz. Inhalt aus den vorhandenen Rechenfunktionen, keine neuen Kennzahlen erfinden.
   - Betrieb: `Kontakte.md`, `Tarife.md`, `Mitglieder.md`, `Ware.md`, `Anfragen.md`, `Zahlen.csv` (dieselben Spalten wie der Import: Datum;Besucher;Anmeldungen;Verkäufe).
   - Marke: SEO-Prüfung und Trichter in `Sichtbarkeit.md`. Farben weiter als `…-Marke.css`, Kundenweg weiter als `…-Kundenweg.md`.
   - Markt: `Markt.md` mit Mitbewerbern, Problemen, Nischen, Tests und der vorhandenen Eigenständigkeitszahl.
   - Geschäftsidee: „Speichern und bewerten“ zusätzlich `…-Bewertung.md` (Punkte, Entscheidung, Blocker, Hinweis „Schätzung, keine Garantie“). „Bauplan erstellen“ bleibt.
3. Jeder Export-Knopf sagt den Dateinamen. Leere Liste: Satz „Bitte zuerst … anlegen“, keine leere Datei.
4. Kein Öffnen eines Bezahldienstes, kein Mailversand, kein Upload, kein Domain-Kauf. Paket und Sicherung auf „Start & Übergabe“ bleiben lokal.
5. `W5Launch.cs` nur anfassen, wenn der Merge ihn ohnehin konfliktfrei lässt. Keine neue Veröffentlichung.
6. Sauberer Nachbau (X-10) und `RebuildAdapter` bleiben außen vor, auch wenn der Reiter in `BusinessTabs` hängt.

Konflikthinweis für den PR: Diese Exporte liegen in `MainWindow.W5*.cs` und bei Bedarf in `BusinessOps.cs`, `BusinessBrand.cs`, `BusinessMarket.cs`, `BusinessStudio.cs`. Sie sind keine zweite Prompt-Engine.

## Testplan

Erst ausführen, wenn die Umsetzung beginnt. In dieser Analyse ist keiner dieser Läufe neu gestartet.

1. `dotnet build Trendo.sln -c Release`
2. `dotnet test tests/Trendo.Core.Tests -c Release`
3. `dotnet test tests/Trendo.Api.Tests -c Release`
4. `dotnet test tests/Trendo.App.Tests -c Release`
5. Gezielt die vorhandenen Fälle `W5_Bus_*`, `BusinessButtonTests` und auf `main` schon `BusinessFeedbackTests`. Erwartete Sätze aneinander anpassen, damit nicht zwei Tests denselben Knopf mit verschiedenem Wortlaut verlangen (auf `main` steht bei Tests „Vermutung“, im Branch „Annahme“; bei Ware „Produktnamen“ gegen „Namen der Ware“).
6. Neue Fälle, die eine Datei wirklich lesen: nach dem Knopf existiert die Datei, der Inhalt enthält den angelegten Namen, eine leere Liste erzeugt keine Datei und zeigt den Hinweis. Temporäres Datenverzeichnis wie `Ui.Open()`.
7. Import: leerer Pfad ohne Dialog-Haken zeigt den Hinweis und lässt die Statistik leer; mit `BizFileDialog` wird eine Zeile übernommen; eine Zeile ohne Datei im Textfeld ebenfalls; ein erfundener Pfad ohne Semikolon stürzt nicht ab.
8. `SYS-03-P14` auf diesem Image nicht als Regression der Business-Änderung werten, solange nur das Papierformat von `google-chrome` scheitert. Nicht abschwächen.
9. Kein Status `TESTED` oder `PC_VERIFIED` ohne Rohlog und SHA-256 unter `reports/`. QA-017, QA-019 und Codesignatur nicht als bestanden markieren.

## Offenes

- Merge-Lauf auf `main` abwarten. Diesen Branch nicht rebasen, bis das ausdrücklich freigegeben ist.
- PR #223 bleibt zu. Diese Datei ist der Arbeitsstand; sie setzt nichts um.
- Fenster-Dateidialog unter Windows, Winget, GiroCode als Bild, 3D-Studio und Logo sind nicht geprüft.
- Ob `BusinessFeedbackTests` und `BusinessButtonTests` nach dem Merge beide grün bleiben, ist offen, bis der Testplan gelaufen ist.
