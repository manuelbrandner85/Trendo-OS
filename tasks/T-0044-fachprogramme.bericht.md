# T-0044: Paket 44, Fachprogramme nur bei Bedarf

**Auftrag:** Fachprogramme nicht vorinstallieren. Verbindung prüfen, bevor etwas geöffnet wird. Der Installieren-Knopf spricht nur eine austauschbare Schnittstelle an. Den echten Download baut ein anderer Auftrag.
**Branch:** `cursor/paket-44-fachprogramme-a21a`
**Pull Request:** #271, nicht gemergt.

## Was die Wörter heißen

- **Fachprogramm:** ein schweres Programm außerhalb von Trendo, zum Beispiel Blender oder LibreOffice.
- **Verbindungsprüfung:** ein kurzer Test, ob das Programm antwortet. Erst danach gilt es als bereit.
- **Freigabe:** die vorhandene einmalige Erlaubnis. Sie gilt nur für genau diesen einen Vorgang.
- **Schnittstelle `IProgramInstaller`:** die Steckdose, an die der echte Installer später angeschlossen wird. Dieser PR liefert die Steckdose und einen Fake für Tests, nicht den Installer.

## Punkt 6: alte Knöpfe „Starten“ und „Einrichten“

Auf den Seiten Programme, Dokumente, Software und Sendung gehen diese Knöpfe jetzt durch die Prüfung und die Freigabe.

Ohne bestandene Prüfung wird nichts geöffnet und nichts heruntergeladen.

LibreOffice, OBS und VS Code starten wieder, aber nur nach „Verbindung prüfen“ und nach der Freigabe. „Starten“ und „In … öffnen“ starten die Prüfung nicht selbst. Die Prüfung schaut, ob die Programmdatei da ist, und fragt die Version ab, ohne ein Fenster offenzuhalten: `soffice --version`, `code --version`, und außerhalb von Windows `obs --version`. Unter Windows liest OBS nur die Dateiversion und startet dafür keinen Prozess. Die Abfrage wartet höchstens acht Sekunden. Antwortet sie mit Code 0, heißt das Ergebnis Bestanden, und der alte Knopf öffnet das Programm. Fehlt die Datei, ist keine Version lesbar oder läuft die Zeit ab, heißt das Ergebnis Fehlgeschlagen. Es wird dann nichts geöffnet.

Die übrige Werkzeugliste bleibt wie bisher. Dort startet LibreOffice nicht von selbst, weil `--version` unter Windows ein Fenster öffnen und hängen kann. Nur der Knopf, den jemand drückt, stellt diese kurze Frage.

3D und Spiele sind nicht umgebaut. Dort bleibt der alte Weg, bis #251 und #253 gemergt sind.

## Punkt 7: kein automatischer Download

Beim Öffnen der Seite passiert nichts. Es läuft nichts im Hintergrund. „Einrichten“ auf den genannten Seiten lädt nicht. Der Installieren-Knopf fehlt, solange kein Installer eingetragen ist und der Schalter aus ist. Beide sind von Haus aus aus.

## Punkt 8: Installieren-Knopf, nur gegen die Schnittstelle

Manuel hat den Knopf freigegeben. Er ist in der Liste „Externe Programme“ eingebaut, aber nur sichtbar, wenn beides stimmt: der Schalter `InstallierenSichtbar` ist an, und jemand hat einen `IProgramInstaller` eingetragen. Trendo trägt von selbst keinen ein. Deshalb sieht man den Knopf im normalen Programm nicht.

Ablauf, wenn der Schalter an ist:

1. „Installieren“ liest nur das Angebot.
2. Die Rückfrage nennt Quelle, Größe und Lizenz.
3. „Ja, installieren“ holt die einmalige Freigabe und ruft dann nur `InstallAsync` auf. Es gibt keinen eigenen Download und kein winget in diesem Knopf.
4. Der Fortschritt zeigt Prozent und einen kurzen Text. „Abbrechen“ stoppt den Vorgang.
5. Danach prüft Trendo, ob der Installer das Programm als da meldet, und sucht die Datei. Die Verbindungsprüfung startet dabei nicht. Die startet nur „Verbindung prüfen“.
6. Eine Fehlermeldung kommt so an, wie der Installer sie auf Deutsch liefert.

Ist `IsFree` falsch oder `NeedsUserStep` wahr, gibt es nur einen Hinweis mit Link. `InstallAsync` wird nicht aufgerufen. Die Seite öffnet sich erst, wenn man „Seite ansehen“ drückt.

`ProgramOffer` hat `Sha256` und `SizeBytes`. Beide dürfen leer sein. Die lesbare Größe steht in `Groesse`. Fehlt sie, rechnet die Oberfläche aus `SizeBytes`, wenn die Zahl da ist.

`InstallProgress` stand in der Signatur, aber ohne Feldnamen. Hier heißt es `Percent` und `Meldung`. Wenn BUILD & RELEASE andere Feldnamen nutzt, muss man das beim Zusammenführen anpassen.

Der Katalog als YAML ist nicht in diesem PR. Eine Quelle wird nur akzeptiert, wenn sie eine https-Herstellerseite oder eine Paketkennung wie „Hersteller.Programm“ ist. Ob das Paket wirklich das offizielle ist, prüft der echte Baustein.

Die alte Methode im Adapter, die winget aufrufen kann, bleibt für ihre bisherigen Tests. Der neue Knopf ruft sie nicht auf.

## Zehn Funktionen

Gelesen in `requirements.yaml`, Abschnitt `packages`, Eintrag `PAKET-44`, auf PR #275, Commit `06f8446`. Die Datei `requirements.yaml` auf diesem Branch ist nicht geändert.

| Punkt | Name | Stand | Grund |
| --- | --- | --- | --- |
| P44-01 | Programmkatalog | erledigt | Die Seite „Externe Programme“ listet alle Programme aus dem Katalog. |
| P44-02 | Nicht vorladen | erledigt | Nach dem Öffnen steht überall „Nicht geladen“. Es startet kein Programm. |
| P44-03 | Erkennen | erledigt | „Suchen“ zeigt bei einer vorhandenen Datei „Gefunden, nicht geprüft“ und startet nichts. |
| P44-04 | Prüfen auf Knopfdruck | erledigt | Nur „Verbindung prüfen“ startet die Prüfung. Das Ergebnis heißt Bestanden oder Fehlgeschlagen. Wo eine stille Prüfung ein Fenster öffnen würde, bleibt es bei „nicht geprüft“. |
| P44-05 | Öffnen nur mit Freigabe | erledigt | Ohne bestandene Prüfung und ohne Freigabe wird nichts geöffnet. |
| P44-06 | Alle Wege über dieselbe Prüfung | wartet | „Starten“ und „Einrichten“ öffnen nichts ohne bestandene Prüfung. 3D und Spiele bleiben beim alten Weg, bis #251 und #253 gemergt sind. |
| P44-07 | Kein automatischer Download | erledigt | Beim Start, offline und bei fehlgeschlagener Prüfung wird nichts geladen. |
| P44-08 | Fehlendes Programm per Knopf | wartet | Rückfrage mit Quelle, Lizenz und Größe ist gebaut. Erst nach Ja spricht der Knopf den Installer an und sucht danach. Der echte Download, zum Beispiel winget, wartet auf den Baustein von BUILD & RELEASE. Bis dahin ist der Knopf aus. |
| P44-09 | Geheimnisse schützen | erledigt | Ein Test prüft, dass ein geheimer Wert nicht im Protokoll steht. |
| P44-10 | Echter Start auf dem PC | wartet | Abnahme über QA. Je ein belegter Lauf von Blender, Unreal und LibreOffice auf Manuels PC fehlt. Hier nicht abgehakt. |

## Geänderte Dateien

- `src/Trendo.Core/Fachprogramm.cs` (kurze Versionsabfrage für LibreOffice, OBS und VS Code)
- `src/Trendo.Core/ProgramInstaller.cs`
- `src/Trendo.App/MainWindow.Programs.cs`
- `src/Trendo.App/MainWindow.W1Office.cs` (Hinweistext auf der Dokumentenseite)
- `tests/Trendo.Core.Tests/FachprogrammTests.cs`
- `tests/Trendo.Core.Tests/ProgramInstallerTests.cs`
- `tests/Trendo.App.Tests/FachprogrammUiTests.cs`
- `tests/Trendo.App.Tests/ProgramInstallerUiTests.cs`
- `tests/Trendo.App.Tests/ProgramsUiTests.cs`
- `docs/build-log/2026-10-22-paket-44-fachprogramme.md`

## Testergebnis

`dotnet test Trendo.sln -c Release` am 22.10.2026, mit der Versionsabfrage:

- Api: 15 bestanden, 0 Fehler
- Core: 541 bestanden, 10 übersprungen (`WinRealTests`), 0 Fehler
- App: 235 bestanden, 1 Fehler, 236 gesamt

Der eine Fehler ist `T-0209` („Jeder sichtbare Knopf … wirkt“): der Reiter „Zeit & Kalender“ auf der Seite Kleine Helfer wurde in diesem Lauf als „ohne Wirkung“ gemeldet. Dieselbe Prüfung allein danach: 1 bestanden, 0 Fehler, 22 Sekunden. Der Reiter gehört nicht zu LibreOffice, OBS oder VS Code. Die neuen Prüfungen waren in dem Lauf dabei und sind nicht der gemeldete Fehler.

Abgleich mit P44-01 bis P44-10, nur die betroffenen Tests, am 23.10.2026:

- Core, Paket 44: 10 bestanden, 0 Fehler
- App, Katalog, Knöpfe und Installieren: 20 bestanden, 0 Fehler

Die ganze Suite für diesen Abgleich wird danach noch einmal gezählt. Der Lauf davor bleibt der Stand weiter oben.

Der Kopf von Chrome für den PDF-Test startete in diesem Lauf und war danach von selbst weg. Die App ist für das bekannte Papierformat dieses Chrome nicht geändert.

## Nicht geprüft

- Kein echtes LibreOffice, OBS oder VS Code. `soffice --version`, `obs --version` und `code --version` liefen nur gegen einen Fake. Die Dateiversion von OBS unter Windows ist ein Ersatz im Test, keine echte Programmdatei.
- Kein echtes Blender, Unreal, Krita, Inkscape, Kdenlive, Audacity, COLMAP oder Meshroom. Kein winget, kein echter Download.
- Kein Windows-PC. QA-017, QA-019 und die Codesignatur sind nicht bestanden. Punkt 10 der Abnahme ist nicht abgehakt.
- 3D- und Spiele-Dateien sind nicht geändert. Ein Spiele-Test prüft, dass Unreal dort noch den alten Weg geht.
- #252 KI-Laden ist nicht angefasst. Die Punkte 23, 25, 34, 86 und Language Studio 2.0 bleiben aus. Punkt 35 ist nicht aktiviert.
- Der YAML-Katalog und der echte Installer von BUILD & RELEASE sind nicht dabei.
