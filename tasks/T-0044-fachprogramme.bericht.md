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

LibreOffice, OBS und VS Code starten wieder, aber nur nach einer kurzen Prüfung und nach der Freigabe. Die Prüfung schaut, ob die Programmdatei da ist, und fragt die Version ab, ohne ein Fenster offenzuhalten: `soffice --version`, `code --version`, und außerhalb von Windows `obs --version`. Unter Windows liest OBS nur die Dateiversion und startet dafür keinen Prozess. Die Abfrage wartet höchstens acht Sekunden. Antwortet sie mit Code 0, gilt die Prüfung als bestanden, und der alte Knopf öffnet das Programm. Fehlt die Datei, ist keine Version lesbar oder läuft die Zeit ab, erscheint ein deutscher Satz. Es wird dann nichts geöffnet.

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
5. Danach prüft Trendo, ob der Installer das Programm als da meldet, und macht die Verbindungsprüfung erneut.
6. Eine Fehlermeldung kommt so an, wie der Installer sie auf Deutsch liefert.

Ist `IsFree` falsch oder `NeedsUserStep` wahr, gibt es nur einen Hinweis mit Link. `InstallAsync` wird nicht aufgerufen. Die Seite öffnet sich erst, wenn man „Seite ansehen“ drückt.

`ProgramOffer` hat `Sha256` und `SizeBytes`. Beide dürfen leer sein. Die lesbare Größe steht in `Groesse`. Fehlt sie, rechnet die Oberfläche aus `SizeBytes`, wenn die Zahl da ist.

`InstallProgress` stand in der Signatur, aber ohne Feldnamen. Hier heißt es `Percent` und `Meldung`. Wenn BUILD & RELEASE andere Feldnamen nutzt, muss man das beim Zusammenführen anpassen.

Der Katalog als YAML ist nicht in diesem PR. Die Quelle zeigt Trendo so an, wie das Angebot sie liefert. Ob sie offiziell ist, prüft dieser PR nicht. Das gehört zum echten Baustein.

Die alte Methode im Adapter, die winget aufrufen kann, bleibt für ihre bisherigen Tests. Der neue Knopf ruft sie nicht auf.

## Zehn Funktionen

Gesucht in `docs/spec/MASTER_BIS_94.md` und `reports/master-94/` auf `cursor/master-bis-94-5d61`, und in `docs/state/` auf `state/docs-state-init`. Die Masterzeile sagt nur: zehn Funktionen, Fachprogramme wie Blender oder Unreal nur bei Bedarf. Das Protokoll `docs/state/DECISIONS.md` vom 23.10.2026 sagt, Manuel habe alle zehn freigegeben, und benennt nur Punkt 7 und Punkt 8. Eine Liste mit zehn Namen gibt es dort nicht. Es wurden keine Namen erfunden.

Bekannt und in diesem PR umgesetzt, ohne die übrigen zu erfinden:

- Punkt 6: alte Knöpfe „Starten“ und „Einrichten“
- Punkt 7: kein Download von selbst
- Punkt 8: fehlendes Programm nur auf Knopf, mit Rückfrage
- Punkt 10: Abnahmeliste von QA, hier nicht abgehakt

Abweichung bei Punkt 8: die Freigabe nutzt das vorhandene Freigabetor (`ApprovalGate`). Der Download selbst geht nicht über die alte winget-Strecke in `Dependencies`. Den echten Baustein baut BUILD & RELEASE. Dieser PR ruft nur `IProgramInstaller` auf. Eine Quelle wird nur akzeptiert, wenn sie eine https-Herstellerseite oder eine Paketkennung wie „Hersteller.Programm“ ist. Ob das Paket wirklich offiziell ist, prüft der echte Baustein später. Der YAML-Katalog fehlt noch.

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

Gezielte Tests am 22.10.2026, bevor die ganze Suite noch einmal läuft:

- Core, Paket 44: 10 bestanden, 0 Fehler (darunter Versionsabfrage, Zeitablauf, Dateiversion von OBS)
- App, Knöpfe der externen Programme: 12 bestanden, 0 Fehler
- App, Paket 44 inkl. Installieren-Knopf: 7 bestanden, 0 Fehler

Die Zahlen der ganzen Suite stehen nach dem erneuten `dotnet test Trendo.sln -c Release` in der nächsten Ergänzung dieses Berichts. Der Stand davor, mit Quellensperre: Api 15, Core 538 plus 10 übersprungen, App 232, 0 Fehler.

## Nicht geprüft

- Kein echtes LibreOffice, OBS oder VS Code. `soffice --version`, `obs --version` und `code --version` liefen nur gegen einen Fake. Die Dateiversion von OBS unter Windows ist ein Ersatz im Test, keine echte Programmdatei.
- Kein echtes Blender, Unreal, Krita, Inkscape, Kdenlive, Audacity, COLMAP oder Meshroom. Kein winget, kein echter Download.
- Kein Windows-PC. QA-017, QA-019 und die Codesignatur sind nicht bestanden. Punkt 10 der Abnahme ist nicht abgehakt.
- 3D- und Spiele-Dateien sind nicht geändert. Ein Spiele-Test prüft, dass Unreal dort noch den alten Weg geht.
- #252 KI-Laden ist nicht angefasst. Die Punkte 23, 25, 34, 86 und Language Studio 2.0 bleiben aus. Punkt 35 ist nicht aktiviert.
- Der YAML-Katalog und der echte Installer von BUILD & RELEASE sind nicht dabei.
