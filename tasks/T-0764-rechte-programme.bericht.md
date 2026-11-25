# Bericht T-0764: Rechte im Kern, Freigaben, Programm-Seite (SYS-11), Geräte auswerfen (SYS-07)

**Branch:** `claude/T-0764-rechte-programme` (Basis `main` = Release 0.9.109, d8df585a5)
**Status:** umgesetzt, gezielte Tests grün (Zahlen unten), kein Volltest (macht die Hauptsitzung)

## 1. Rechte im Kern

**Vorher (Beleg):** Audit SYS-11/SYS-12 „Rechte“: Die Kindersperre war nur ein gesperrter Knopf (`MainWindow.AddHub.cs:200`). Die übrigen Wege prüften keine Rolle:
- `Software.InstallPortable`, Offline-Installer (`LaunchDirect(Principal.LocalOwner, …)`),
- `Vollpaket.InstallEach/Uninstall`, `GitHubApps.Install/Uninstall`, `SourceApps` (Einrichten ganz ohne Freigabe),
- `WebApps.Install` (Akteur „lokaler-besitzer“), KI-Laufzeiten/-Modelle, Ollama-Modelle entfernen.

Rot-Beleg: `reports/T-0764/rot-app-rechtekernui.log`. Auf dem alten Stand scheitern 4 von 5 Tests: Ein Kinderprofil entpackt Portable, startet den Offline-Installer, installiert eine App und entfernt ein mitgeliefertes Programm. Die Erwachsenen-Gegenprobe ist grün.

**Jetzt:**
- Neues Recht `Permission.ManageSoftware`. Nur die Rolle Besitzer hat es; das Kinderprofil ist Rolle Benutzer (`Principal.ForProfile`).
- Neue Prüfung `Rbac.DemandSoftware(by, was, audit)`. Sie schreibt `rbac.denied` mit dem Vorgang ins Protokoll und wirft eine verständliche Meldung.
- Die hier aufgezählten Installations- und Entfernen-Wege im Kern nehmen die Person (Nachtrag: die Gegenprüfung fand weitere Wege in der App, siehe Abschnitt „Nachbesserung“) (`Principal by`) als Pflichtparameter und lehnen ab, **bevor** etwas geladen, entpackt, gestartet oder gelöscht wird:
  - `Software.InstallPortable`, neu `Software.StartOfflineInstaller` (prüft beim Start zusätzlich die Prüfsumme erneut),
  - `Vollpaket.InstallPackage/InstallEach/InstallMissing/Uninstall`, `DependencyManager.InstallAsync`, `FachprogrammAdapter.InstallierenAsync`,
  - `GitHubApps.Install/Uninstall`, `SourceApps.BeginInstall` (neu, vor dem Laden)/`Register`/`Remove`,
  - `KiLibraryStore.InstallRuntime/InstallModelFile`, `WebApps.Install/Uninstall`,
  - `AppCenter.ExecuteAsync/RunAsync` (winget): Ein Kinderprofil führt nichts aus, auch nicht mit einer Freigabe, die ein Erwachsener erteilt hat,
  - neu `TrendoPrograms.Remove/Restore/Purge`.
- Die App reicht überall `CurrentPrincipal` durch (die Rolle aus dem Profil). Die Kommandozeile `--install --approve` bleibt ausdrücklich „Besitzer“.
- Die Oberfläche fängt die Ablehnung ab. Es kommt kein Absturz in async-Klicks und keine falsche Meldung „läuft noch“ oder „fertig“.
- Ollama-Modelle entfernen (Hub, KI-Bibliothek, KI-Quellen): Die Prüfung liegt in der App, weil `OllamaBackend` in der App liegt (offen, siehe unten).
- **Nebenbei gefundener Fehler:** `InstallPortable` mit dem Namen „..“ zeigte auf den übergeordneten Ordner. „Ersetzen“ hätte ihn samt Inhalt gelöscht (`Directory.Delete(dest, true)`). Neu ist `Software.SafeName`; Namen mit Punkt am Anfang werden abgewiesen. Test `RechteKern.Portable`.

**Nachweis (Kern, echt, ohne Ersatz):** `tests/Trendo.Core.Tests/RechteKernTests.cs`, 8 Tests:
- Rollen;
- Portable einschließlich „..“;
- Offline-Installer einschließlich ausgetauschter Datei;
- Vollpaket;
- GitHub/Quellcode/KI/Apps;
- App-Center: Kind mit Erwachsenen-Freigabe → winget läuft nicht;
- Zusatzprogramme;
- „Kind gibt nichts frei“: Tor, Ablehnen, Not-Stopp fortsetzen, Brief-Freigabe, Audio-Plugin-Freigabe.

Hinweis zu Rot: Diese Kern-Tests kompilieren auf dem alten Stand nicht, weil der Kern dort gar keine Person annahm. Das ist der Befund. Die verhaltensbezogene Rot-Probe ist der App-Test oben.

**App-Nachweis:** `RechteKernUi`, 5 Tests: Klick bzw. Aufruf an der Oberfläche vorbei. Der Kern lehnt ab, es wird nichts entpackt, gestartet, installiert oder entfernt.

## 2. Freigabe nicht im selben Klick

Die 28 Stellen aus dem Audit hat T-0706 (B4) schon umgestellt (Tabelle in `tasks/T-0706-freigaben-kern.bericht.md`). Ich habe den heutigen Stand jede Stelle einzeln geprüft. `Approvals.Approve(` steht nur noch in:

| Stelle | Weg | Urteil |
|---|---|---|
| `MainWindow.Freigabe.cs` `FreigabeErteilen` | zweiter, eigener Schritt (Dialog, Karte, Seite „Freigaben“) | richtig, Rolle `CurrentPrincipal` |
| `MainWindow.Freigabe.cs` `FreigabeAusEinstellung` | Dauerfreigabe, die die Person als Einstellung setzt („Freie Modelle automatisch laden“, `X05.cs:40`) | richtig, nur Erwachsene |
| `Program.cs:93` | Kommandozeile `--install --approve` | bewusst: ausdrücklicher Schritt der bedienenden Person |

Aufrufer der zweiten Schritte, alle geprüft:
- `FreigabeErteilen`:
  - `AgentRun.cs:80` (Rückfrage-Karte),
  - `AppCenter.cs:159` (Dialog „Abbrechen“ vorbelegt),
  - `axaml.cs:832` (Seite „Freigaben“),
  - `Live.cs:321` (zweiter Schritt),
  - `Share.cs:57` (Dialog),
  - `W4Web.cs:330` (zweiter Schritt),
  - `W5Fusion.cs:40` (zweiter Schritt).
- `FreigabeAusRueckfrage` („Ja“ auf einer sichtbaren Karte):
  - `AddHub.cs:52/65` (hub-yes),
  - `axaml.cs:1314/1317` (studio-deps),
  - `ClaudeBridge.cs:407`,
  - `Live.cs:177` (live-ffmpeg-yes),
  - `Phone.cs:247` (phone-…-yes),
  - `Programs.cs:70/361`,
  - `W5Ext.cs:20`,
  - `WebApps.cs:99`.

**In diesem Paket korrigiert:**
1. `axaml.cs` studio-deps: Das „Ja“ für das KI-Modell richtete stillschweigend auch **Ollama** mit ein. Das stand nicht auf der Karte. Jetzt steht es dort sichtbar („Dazu richtet Trendo zuerst Ollama ein … ‚Ja' gilt für beides“).
2. `Live.cs:303` Anfrage und `Live.cs:334` Ablehnen liefen als fester Besitzer. Jetzt laufen sie als angemeldete Person (`Ablehnen`: Ein Kind zieht die eigene Anfrage zurück).
3. `W5Fusion.cs:32/100`: Anfrage und „Nein“ laufen als angemeldete Person.
4. `W4Web.cs:319` Website-Paket: Die Anfrage läuft als angemeldete Person.
5. `W1Letter.cs:348/352` Brief „Endgültige Fassung freigeben“: Vorher war es fest der Besitzer, ein Kinderprofil konnte freigeben. Jetzt ist es `CurrentPrincipal`; der Kern (`LetterGate.Approve`) lehnt das Kind ab.
6. `AudioPluginHost.cs:41` Audio-Plugin vertrauen (fremder Programmcode): Der Kern nimmt jetzt die Person, ein Kind wird abgelehnt.

**Andere „Approve“-Wege, bewusst unverändert:**
- `ClaudeLearn.cs:62` `CLearn.Approve`: eine Arbeitsregel für Claude („gilt ab jetzt“), ein Ja/Nein der Person ohne Wirkung nach außen.
- `PromptAction.cs:451` `PromptFlow.Approve`: läuft schon mit `CurrentPrincipal` nach eigener Rückfrage.
- `PromptAction/ConnectedAdapters.cs:443`: Die KI-Installation installiert als `LocalOwner`, aber nur über das Tor nach Freigabe einer Person mit Freigaberecht. Ein Kind kommt dort nicht hin.

Bestehende Tests sind nicht geschwächt. Die Signaturänderungen sind mechanisch angepasst (Person `Principal.LocalOwner` ergänzt). Der Architekturtest `FreigabenKernUi.Keine_Selbstfreigabe_im_Code` läuft unverändert grün.

## 3. SYS-11 Programm-Seite

**Vorher:**
- „Programm entfernen“ verwies nur auf die Windows-Einstellungen (`MainWindow.Sys11.cs:67`).
- Portable Programme aus „Pflegen“ tauchten nirgends auf und ließen sich nicht entfernen.
- Vollpaket- und GitHub-Programme wurden sofort endgültig gelöscht.

**Jetzt:**
- Neuer Kern `src/Trendo.Core/Sys/TrendoPrograms.cs`:
  - Liste aller von Trendo eingerichteten Programme (Portable, Vollpaket, GitHub) mit Programmdatei, Herkunft und Größe.
  - `Remove` verschiebt nur einen Ordner direkt in Trendos eigenem Wurzelordner nach `.entfernt/<zeit>-<name>`, in einem Umbenennen (ganz oder gar nicht; ist eine Datei offen, ändert sich nichts).
  - Abgewiesen werden Pfadtricks, Verknüpfungen und fremde Pakete.
  - Dazu `Restore` (überschreibt nie) und `Purge` (endgültig).
  - GitHub-Einträge werden aus der Liste genommen und beim Wiederherstellen wieder eingetragen.
- Neue Seite `prog-programm` (`MainWindow.ProgrammSeite.cs`):
  - Name, Herkunft, Ordner, Größe;
  - „Starten“ (einzige goldene Hauptaktion), „Ordner zeigen“, Aktualisieren (GitHub: suchen bzw. „Auf vX aktualisieren“; Portable und Vollpaket mit ehrlichem Hinweis, wie es geht);
  - „Entfernen …“ → Rückfrage → „Ja, entfernen“.
- „Pflegen“ hat eine neue Karte „Von Trendo eingerichtete Programme“ mit „Öffnen“ je Programm und der Sicherung („Wiederherstellen“, „Endgültig löschen …“ mit Rückfrage).
- Fremde Programme: Der Verweis auf Windows bleibt ehrlich, neu ist der Knopf „Programme verwalten (winget)“.

**Nachweis:**
- Core `SYS11_TrendoProgramme`, 5 Tests, echtes Dateisystem: Liste, Entfernen → Sicherung → Wiederherstellen → endgültig löschen, GitHub-Eintrag, Grenzen und Kinderprofil, gesperrte Datei → nichts verschoben.
- App `ProgrammSeiteUi`, 2 Tests: Klickweg Pflegen → Öffnen → Starten → Entfernen → Wiederherstellen, ohne `ms-settings`. Im Kinderprofil verschiebt „Ja, entfernen“ nichts.
- Bilder `prog-pflege.png` und `prog-programm.png` angesehen (Render mit Beispieldaten `TRENDO_PROG_DEMO`).

## 4. SYS-07 Geräte

**Vorher:** reine Liste, kein Auswerfen, Problemhinweis immer gleich, neue Sticks erst nach „Neu suchen“ (`MainWindow.Devices.cs`, `W3/Devices.cs`).

**Jetzt:**
- Neu `src/Trendo.Core/W3/DeviceEject.cs`.
- `DeviceEject.Refuse/Eject`: Ausgeworfen werden nur Wechseldatenträger. Nie das Systemlaufwerk und nie das Laufwerk mit dem Trendo-Programm, den Daten oder dem Werkzeugordner. Bei einer Ablehnung wird das Betriebssystem gar nicht gefragt. Alles steht im Protokoll.
- `WindowsDriveEjector`: Laufwerk → `IOCTL_STORAGE_GET_DEVICE_NUMBER` → SetupAPI (GUID_DEVINTERFACE_DISK) → übergeordnetes USB-Gerät → `CM_Request_Device_EjectW`. Das läuft ohne Administratorrechte, mit drei Versuchen. Danach wird geprüft, ob das Laufwerk weg ist. Ein Einspruch von Windows (z. B. „Datei noch offen“) erscheint in Alltagssprache.
- `DeviceEject.NextStep`: Windows-Problemcode (Name `CM_PROB_…` oder Zahl) → nächster Schritt in Alltagssprache. Die Geräteliste liest dafür jetzt die Spalte `Problem` von `Get-PnpDevice`, auf diesem PC geprüft: Ausgabe `CM_PROB_NONE`. Ältere Ausgaben ohne die Spalte gehen weiter.
- Auf der Seite „Geräte“:
  - „Auswerfen“ an jedem Wechseldatenträger;
  - alle 2 s ein schneller Blick auf die Laufwerke (nur `DriveInfo`, keine PowerShell, nur solange die Seite offen ist); ein neu eingesteckter Stick erscheint mit „Neu eingesteckt: …“ samt Auswerfen;
  - Problemgeräte zeigen „Nächster Schritt: …“.

**Nachweis:**
- Core `SYS07_Auswerfen`, 6 Tests:
  - nie System/Trendo/fest;
  - Problemcodes;
  - PnP-Spalte;
  - Laufwerksliste;
  - **echt:** `WindowsDriveEjector` für einen freien Laufwerksbuchstaben → „nicht gefunden“, nichts ausgeworfen;
  - **echt, nur lesen:** Zuordnung C: → Datenträger-Geräteknoten über SetupAPI gefunden (die Struktur-Übergaben an Windows stimmen).
- App `SYS07_Auswerfen_Ui`: Stick einstecken → erscheint beim nächsten Nachsehen; Auswerfen → „kann jetzt abgezogen werden“, der Eintrag ist weg; C: hat keinen Knopf; Problemgerät zeigt den nächsten Schritt. Laufwerke und Auswurf sind hier **ersetzt**.
- Bild `geraete.png` angesehen.
- **Echter Auswurf: NICHT GETESTET.** An diesem PC steckt kein Wechseldatenträger (geprüft: nur `C:`, Typ 3). Einen fremden Stick des Nutzers werfe ich nicht aus. Voraussetzung für den Beleg: ein eigener Test-Stick, dann Seite „Geräte“ → „Auswerfen“.

## Nachbesserung nach der Gegenprüfung (Commit „T-0764 fix1“)

Die unabhängige Gegenprüfung von `fbbed41b9` fand Wege, über die ein Kinderprofil weiter installieren oder entfernen konnte. Rot-Belege (Stand `fbbed41b9`, Testdatei nur für den Beleg, nicht eingecheckt): `reports/T-0764/rot-fix1-app.log` (7 von 7 rot) und `reports/T-0764/rot-fix1-cli-kinderprofil.log` (Kommandozeile im Kinderprofil richtete ein Paket ein, Rückgabewert 0).

| Befund | Was geändert | Test (grün) |
|---|---|---|
| H1 KI-Quellen (LM Studio, Jan, portable) | `KiSourceInstall`: zuerst die Rolle (vor jedem Download); dann Freigabe – `karte: true` bei „Ja, laden“/hub-yes, sonst eigener Dialog (Hub „Aktualisieren“); Laden, Einrichten und Installer-Start nur in `Approvals.Execute`; Knöpfe im Kinderprofil gesperrt; Protokoll-Akteur `CurrentPrincipal` | `RechteKernFix1Ui.KiQuelle_Installer` (Kind: kein Download, kein Installer, Knopf gesperrt; Erwachsene: Download erst nach `execution.start`; „Nein“ lädt nichts) |
| H2 Deinstaller | `GitHubRunUninstaller(Principal by, …)` mit `Rbac.DemandSoftware` als erster Zeile (gilt für Programme.cs, AddHub.cs und KiSourceRemove); `KiSourceRemove` prüft zuerst; Meldung „nur Erwachsenen-Profil“ statt „läuft noch“/„nichts geändert“ | `RechteKernFix1Ui.Deinstaller_Kind` |
| H3 Claude Code einrichten | Rolle zuerst; `FreigabeAsync(Wirkung("app.install","claude-code"))`; PowerShell-Installer nur in `Approvals.Execute`; der Testmodus greift erst innerhalb der Ausführung | `RechteKernFix1Ui.Claude_einrichten` (Kind: nichts; „Abbrechen“: `approval.denied`, nichts; „Ja“: `execution.start` vor `claude.testmodus`) |
| M1 KI-Brücke | `ClaudeActions.NeedsConsent`: Knöpfe `src-install*`, `src-remove*`, `hub-yes/-install/-update/-remove`, `setup-yes`, `btn-yes-`, `btn-install`, `btn-remove`, `btn-gh-`, `prog-detail-`, `trendo-prog-`, `claude-install`, `wiz-claude`, `kilib-`, `model-remove`, `portable-run`, `offline-`, `uninstall-`, `dev-eject` sowie Texte mit „laden“, „herunterlad“, „install“, „deinstall“, „auswerf“ fragen immer. **Abweichung:** „entfern“ fragt nicht in jedem Fall, weil der bestehende Test „Stab entfernen“ im rückgängig machbaren Dokument ohne Rückfrage verlangt; Programme sind über Knopfnamen, „deinstall“ und das bestehende „Programm … entfernen“ abgedeckt. | Core `RechteKernFix1.Bruecke_fragt` (10 Fälle), `Bruecke_Dokument_bleibt`; bestehende `TrendoMcpTests` grün |
| M2 Kommandozeile | `--install-deps …`: Person aus dem Profil des Datenordners (`CliPrincipal`); Kinderprofil → „FAIL … Erwachsenen-Profil“, Rückgabewert 4, Protokoll `rbac.denied`; Anfrage, Freigabe und Installation mit dieser Rolle | **echt**: `RechteKernFix1Ui.Kommandozeile_Kind` startet `TrendoOS.exe` mit einem lokalen Testpaket (Kind: Rückgabewert 4, nichts eingerichtet; Erwachsene: Rückgabewert 0, Paket da) |
| M3 Dateimanager | `FilesOpen`: Installer, Pakete, Skripte und Registrierungsdateien (`Software.IsInstallerLike`: .msi/.msix/.appx/.bat/.cmd/.ps1/.vbs/.js/.reg/… und .exe mit setup/install/unins/update) brauchen `ManageSoftware` – geprüft vor Testersatz und Testmodus; Dokumente und gewöhnliche Programme bleiben für Kinder offen | `RechteKernFix1Ui.Dateien_Installer`, Core `RechteKernFix1.Installer_erkennen` (14 Fälle) |
| M4 Auswurf-Schutz | `DeviceEject.Refuse` löst Schutzpfade auf (`WindowsDiskResolver.FinalPath` = `GetFinalPathNameByHandle`: Junction, Verknüpfung, subst) und vergleicht den physischen Datenträger (`IOCTL_STORAGE_GET_DEVICE_NUMBER`) von Schutzpfad und Ziel – deckt zwei Partitionen auf demselben Gerät ab; Schutzliste um Vollpaket-Ordner und Benutzerordner ergänzt. Nichts echt ausgeworfen. | Core `RechteKernFix1.Auswurf_ganzes_Geraet` (Zuordnung ersetzt), **echt nur lesen** `Auswurf_Aufloesung_echt` (Junction → Ziel, Datenträger von C:); App `RechteKernFix1Ui.Auswurf_selbes_Geraet` |
| N1 Liste | `InstallPortable` schreibt die Kennung `.trendo-portable.json`; die Liste führt nur Ordner mit Kennung. Ältere Stände ohne Kennung erscheinen nur, wenn ein Programm darin liegt und der Name kein Trendo-Arbeitsordner ist (`rueckgaengig`, `laufzeiten`, …); sie sind als „älterer Stand ohne Kennung“ markiert. | Core `RechteKernFix1.Nur_echte_Programme` |
| N2 Wiederherstellen | verlangt Art GitHub, `gh.Id == meta.Id`, `ValidRepo`, `IdFor(Repo) == Id` und einen relativen Programmpfad ohne `..`; sonst `ArgumentException`, nichts verschoben | Core `RechteKernFix1.Wiederherstellen_prueft_Sicherung` |
| N3 Akteure/Meldungen | Akteur `CurrentPrincipal` in KiSourceInstall/Remove/Start, GitHubRunUninstaller, GitHubUpdate, n8n, FreeSewing; Ollama-Entfernen setzt die Seitenmeldung | durch die Tests oben (Akteur „kinderprofil“ im Protokoll) |

**Systematische Nachsuche** (Muster und Ergebnis):
1. `Process.Start(` / `new ProcessStartInfo(` in `src/Trendo.App` und `src/Trendo.Core`: 134 Treffer in 57 Dateien. Jede Fundstelle mit „install/einricht/setup/uninstall/deinstall/npm/pip/winget“ im Umfeld wurde gelesen. Neu gefunden und abgesichert (Rolle vor Testmodus und Download):
   - `AutomationInstall` (n8n/Node-RED per npm),
   - `SewingSetup` (FreeSewing per npm),
   - `WebAxeSetup` (axe-core per npm),
   - `ClaudeExtRun` (Plugins/MCP-Server für Claude Code hinzufügen, umschalten, entfernen),
   - `StickOnlyFinish` (Trendo selbst vom PC entfernen).
   Test: `RechteKernFix1Ui.Weitere_Wege_Kind`; rot auf `fbbed41b9` für n8n, siehe `rot-fix1-app.log`.
2. `InstallArgs(`, `"install"`, `pip`, `winget`, `msiexec`, `Cli.Run(npm`: keine weiteren Funde. glTF-Transform läuft schon über das Tor (`deps.npm`), KiPy (uv) über das Tor der KI-Bibliothek.
3. Öffentliche Kern-Methoden `Install*/Uninstall*/Register/Remove` (erste Runde) und dazu `GitHubApps.Register` (Installer-Weg) – jetzt mit `Principal`.
4. `LaunchDirect(` mit vom Nutzer gewählten Dateien: Offline-Installer (erste Runde) und Dateimanager (M3).
   - **Nicht vollständig geprüft:** übrige `LaunchDirect`-/`Launch`-Ziele (feste Programmpfade, URLs, Ordner). Ein Kind kann dort ein Programm starten, aber keinen Installer aus einer gewählten Datei.

## Zweite Prüfrunde (Commit „T-0764 fix2“)

Die Prüfung von `4cdd1c7e6` hat alle zehn ersten Befunde als behoben bestätigt und neue Wege gefunden.

**Rot auf 4cdd1c7e6:**
- `reports/T-0764/rot-fix2-app.log`: 5 von 5 rot (N-A, N-C, N-D, N-E/N-F, N-G).
- `reports/T-0764/rot-fix2-cli-familie.log`: Familien-PC, Hauptprofil ohne `--pin` → Paket eingerichtet, Exit 0.
- Die Rot-Tests starten keine echten Programme: FakeLauncher, Downloads/Schritte/npm ersetzt.
- Der Kodierungsfehler in `rot-fix1-cli-kinderprofil.log` („�“ statt „…“ in der Fortschrittszeile) ist berichtigt; die Prüfsumme in `SHA256SUMS.txt` ist neu.

| Befund | Was geändert | Test (grün) |
|---|---|---|
| N-A Python-KI aus Studios | Neu `KiPyInstall` für die Studio-Aufrufer (Musik: Spuren, Analyse, Stimmen, eigene Stimme; Wissen; Vision; IFC; Open3D): Rolle zuerst, dann `FreigabeAsync(Wirkung("ki.pyapp", Modell))`, Ausführung nur in `Approvals.Execute`. Ablehnungen und Tor-Fehler kommen als `InvalidOperationException` mit verständlichem Satz, die alle Aufrufer schon abfangen (kein Absturz). Die eigentliche Einrichtung `KiPyInstallCore` prüft die Rolle als erste Zeile; die KI-Bibliothek ruft sie in ihrem eigenen Tor auf. Akteur ist `CurrentPrincipal`. | `RechteKernFix2Ui.PythonKi`: Kind → kein Download, kein Schritt, auch über die Seite „Wissen“. Erwachsene → „Nein“: nichts; „Ja“: Download erst nach `execution.start`. |
| N-B Kommandozeile Familien-PC | Gibt es weitere Personen oder ist für das Profil eine Anmeldung eingerichtet, braucht `--install-deps` die PIN bzw. das Passwort dieses Profils (`--pin`, geprüft mit `ProfileLock.Verify`). Ohne Anmeldung, aber mit Personen: Exit 4 mit dem Hinweis, erst eine PIN festzulegen. Die PIN wird nie ausgegeben oder protokolliert. Ohne Personen und ohne Sperre bleibt alles wie bisher. **Hinweis:** Eine PIN auf der Kommandozeile ist in der Prozessliste sichtbar. | **echt** `RechteKernFix2Ui.Kommandozeile_Familie`: ohne Sperre 4; ohne PIN 4; falsche PIN 4; richtige PIN 0 und Paket da; PIN nicht in Ausgabe und Dateien. |
| N-C Starten zentral | `Integrations.LaunchDirect/LaunchArgs` prüfen Rolle und Ziel selbst (`DemandLaunch`). Erwachsene: wie bisher. Kinderprofil: Dokumente, Ordner, Adressen und Trendo-eigene Programme ja; Installer, Skripte, Verknüpfungen und fremde .exe nein. Andere Rollen (Agent, API): weiter gar nicht. Alle 32 Aufrufer in der App laufen über `LaunchAs`/`LaunchArgsAs` mit `CurrentPrincipal`; eine Ablehnung zeigt einen Hinweis, es gibt keinen Absturz. Trendo-Ordner kommen über `Integrations.TrustedProgramPath` (Werkzeugordner, Programmordner, Portable-Ordner; nicht `.entfernt`). | Core `RechteKernFix2.Zentral_im_Kern`; App `RechteKernFix2Ui.Starten_zentral` (Suche/Downloads/Desktop-Ziel über `OpenHit`, Dateimanager). Der bestehende Test „Direktstart: nur Owner“ bleibt unverändert grün. |
| N-D Umgehungen | `IsInstallerLike` erkennt zusätzlich .lnk, .url, .cpl, .hta, .application, .appref-ms, .msu, .jar, .diagcab, .settingcontent-ms, .gadget und .msc. Neu `NeedsSoftwareRight`: jede .exe außerhalb von Trendos Programmordnern. Meldung für Kinder: `Rbac.NurErwachseneStart`. **Abweichung:** Das Ziel einer .lnk wird nicht aufgelöst; Verknüpfungen öffnet ein Kinderprofil gar nicht. | Core `RechteKernFix2.Recht_noetig` (17 Fälle); App `Starten_zentral` (13 Dateiarten). |
| N-E Rückkehr/Update | `RollbackNow` mit Rollenprüfung; Knöpfe „Zurück zu Version …“ und „jetzt installieren“ sind für Kinder gesperrt; das Installieren von Hand prüft die Rolle; `upd-` steht in der Namensregel der KI-Brücke. Automatische Updates nach der Einstellung eines Erwachsenen laufen weiter (Sicherheitsupdates). | `RechteKernFix2Ui.Rueckkehr` (Ersatz-Updater: Kind → nicht gefragt, Erwachsene → einmal). |
| N-F „laden“ | Die Textregel greift nur noch bei Programmbezug (herunterladen, install, deinstall, auswerf, „Programm … laden/einrichten/entfernen/starten“, „KI/Modell laden“, „Zurück zu Version“); „Ja, laden“ erkennt der Knopfname. | Core `RechteKernFix2.Laden_nur_mit_Programmbezug`; `RechteKernFix1.Bruecke_fragt` weiter grün. |
| N-G npm-Pakete | n8n/Node-RED, FreeSewing und axe-core laufen über `FreigabeAsync` und `Approvals.Execute` (Zwei-Schritt) – zusätzlich zur Rollenprüfung aus fix1. | `RechteKernFix2Ui.N8n_Zwei_Schritte` („Nein“: nichts; „Ja“: Einrichtung erst im Tor). FreeSewing und axe-core: Im Testmodus endet der Weg vor npm, die Rückfrage ist also nur im Code belegt (**NICHT GETESTET**). |

**Tests der zweiten Runde:**
- Build: 0 Fehler.
- Core gezielt (RechteKernFix2/Fix1/RechteKern, SYS11_TrendoProgramme, SYS07_Auswerfen, FreigabenKern, Dependencies_Core, alle Klassen aus Tests.cs, TrendoMcpTests und Sys11Tests): 198 von 198 grün. Log: `gruen-fix2-core.log`.
- App gezielt: 242 Testklassen – alle Klassen mit Bezug zu KiPy, Musik, Wissen, Vision, IFC/Open3D, Updates, Starten/Downloads/Suche, Dateimanager, n8n/FreeSewing/axe, KI-Quellen, Claude, KI-Brücke, W1*, Freigaben und RechteKern, dazu EveryButton_Alltag; ohne Browser- und Echtlauf-Klassen. Ergebnis: 1084 Tests, 0 Fehler, 5 übersprungen. Log: `gruen-fix2-app.log`.

**Suchmuster dieser Runde und Ergebnis:**
1. `Integrations.LaunchDirect(` / `LaunchArgs(` in `src/Trendo.App`:
   - 32 Stellen sind auf `LaunchAs`/`LaunchArgsAs` umgestellt.
   - Ausnahme: `FachprogrammAdapter` (Programs.cs:417–418). Er startet katalogisierte Programme nur nach Freigabe im Tor, weiter als Besitzer.
2. `KiPyInstall(`: alle 8 Studio-Aufrufer gehen jetzt über die Freigabe; die KI-Bibliothek ruft `KiPyInstallCore` in ihrem Tor auf.
3. `Updater.RollbackTo` / `InstallUpdateNow`: Knöpfe und Rückkehr sind geprüft.
4. `InstallArgs(` / npm: alle drei Wege über das Tor.

**Verbleibende ungeprüfte Wege (ehrlich):**
- Direkte `Process.Start`-Aufrufe ohne `Integrations` (rund 100 Stellen) starten bekannte Werkzeuge: ffmpeg, mpv, Ollama, KI-Server, Programme aus dem Werkzeugordner, cmd für Quellcode-Programme. Ob irgendwo eine vom Nutzer gewählte fremde Datei so gestartet wird, ist nicht einzeln belegt.
- Das Ziel einer `.lnk` wird nicht aufgelöst; für Kinder ist sie gesperrt.
- Eine PIN auf der Kommandozeile ist in der Prozessliste sichtbar.
- Ein Erwachsenen-Profil ohne PIN neben einem Kinderprofil hebt die Trennung über „Wechseln“ weiter auf (bekannter Hinweis aus T-0713).

## Dritte Prüfrunde (Commit „T-0764 fix3“)

Die Prüfung von `43a4b058c` hat N-A bis N-F bestätigt. Neue Wege gab es über `TrustedProgramPath`.

**Rot auf 43a4b058c:** `reports/T-0764/rot-fix3-core.log` – 13 von 13 rot (P3-1 bis P3-5). Die Testdatei war nur für den Beleg da und ist nicht eingecheckt; sie startet nichts echt (FakeLauncher).

| Befund | Was geändert | Test (grün) |
|---|---|---|
| P3-1 Vertrauen nach Herkunft | Neu `Sys/ProgramTrust`. Beim Einrichten (Portable, Vollpaket, Zusatzprogramm-ZIP über `InstallPortable`, GitHub-Programme) schreibt Trendo eine Kennungsdatei mit der SHA-256 jeder .exe. Vertrauenswürdig ist eine .exe nur, wenn sie dort eingetragen ist und ihr Inhalt unverändert ist. Das Ergebnis wird je Pfad, Größe und Änderungszeit zwischengespeichert. Erwachsene prüfen gar nicht (kein Rechnen). Hineinkopierte, ausgetauschte und Altbestand-Programme ohne Prüfsummen gelten im Kinderprofil nicht. `TrustedProgramPath` der App nutzt nur das, dazu die eigene Programmdatei von Trendo. | Core `RechteKernFix3.Herkunft` (eingerichtet: ja; kopiert, eigener Ordner, Altbestand, ausgetauscht: nein; GitHub-Kennung; Erwachsene wie bisher) |
| P3-2 Interpreter und freie Parameter | `ProgramTrust.Interpreters` (python, pythonw, uv, pip, git, bash, node, npm, 7z, code/codium/vscodium, adb, powershell, pwsh, cmd, wscript, cscript, mshta, rundll32, regsvr32, reg, msiexec, winget, java, curl, ollama …) sind im Kinderprofil nie vertrauenswürdig, wenn Person oder Datei sie wählen. `LaunchArgs` und die eigene Verknüpfung erlauben für Kinder als Parameter nur vorhandene Dateien oder Ordner; feste Aufrufe kennzeichnet Trendos Code selbst (`internalCall`). „Verknüpfung anlegen“ mit eigenen Parametern oder mit fremdem Programm geht nur für Erwachsene. | Core `RechteKernFix3.Interpreter_und_Parameter`; App `RechteKernFix3Ui.Verknuepfung` |
| P3-3 Adressen | Für Kinder gelten nur http, https, mailto, die drei genutzten ms-settings-Seiten und der Papierkorb. `file:` wird wie ein Pfad geprüft. Alles andere (ms-appinstaller:, search-ms:, ms-msdt:, steam:// …) braucht das Recht. | Core `RechteKernFix3.Schemata` (12 Fälle) |
| P3-4 vom Code gewählte Programme | Im Kinderprofil startet ein Programm ohne Wahl der Person nur, wenn es von Trendo geprüft ist oder an einem festen Ort liegt („Programme“, `%LOCALAPPDATA%\Programs`, winget-Ablage). Gespeicherte Pfade aus tools.json gelten im Kinderprofil nur unter dieser Bedingung (`IntegratedChecked`). Die gemeinsame Prüfung `ToolStartAllowed` greift beim direkten Blender-Start der Kreatur-Werkstatt. **Ehrlich:** `%LOCALAPPDATA%\Programs` ist für die Person beschreibbar. | Core `RechteKernFix3.Feste_Orte`; App `RechteKernFix3Ui.Werkzeugpfad_tools_json` (umgebogener Blender-Pfad: Kind nein, Erwachsene ja) |
| P3-5 Dateiarten | Zusätzlich gelten .chm, .sct, .wsc, .xll, .docm, .xlsm, .pptm sowie .dotm, .xltm, .potm, .ppsm. | Core `RechteKernFix3.Weitere_Dateiarten` |

Ein bestehender Test ist an die strengere Regel angepasst: In `RechteKernFix2Ui.Starten_zentral` bekommt das „Trendo-eigene Programm“ jetzt eine Kennungsdatei. Vorher reichte der Ordnername; genau das war der Befund.

**Tests der dritten Runde:**
- Build: 0 Fehler.
- Core gezielt: 242 von 242 grün (`gruen-fix3-core.log`).
- App gezielt: 260 Testklassen (alle mit Bezug zu Starten, Dateimanager, Downloads, Suche, Sys11, Kreatur/3D/Blender, Studio-Start, Video-Export, Spiele, Web, KI-Quellen, Python-KI, Updates, npm, Claude, KI-Brücke, Freigaben, RechteKern; EveryButton_Alltag) – 1146 Tests, **0 Fehler**, 5 übersprungen (`gruen-fix3-app.log`). Der Läufer endete trotzdem mit Exit 1, weil nach dem letzten Test noch ein Vordergrund-Thread lief („Foreground threads were left running“). Welcher Test ihn hinterlässt, ist nicht ermittelt; ein Testfehler ist es nicht.

**Weiter ungeprüft (ehrlich):**
- Direkte `Process.Start`-Aufrufe ohne `Integrations` (rund 100) – ffmpeg, mpv, Ollama, KI-Server (`KiSourceStart`), Blender/Unreal in anderen Werkstätten (außer Kreatur), Musik-Werkzeuge, Programme aus dem Werkzeugordner. Ihre Pfade kommen aus Trendos Erkennung. Nur bei der Kreatur-Werkstatt greift die gemeinsame Prüfung; bei den übrigen nur, soweit der Pfad über `ToolPath` (tools.json-Prüfung) gelesen wird.
- KI-Laufzeiten aus der KI-Bibliothek bekommen keine Kennungsdatei. Im Kinderprofil gelten sie beim Öffnen über den Dateimanager deshalb nicht als vertrauenswürdig; die KI-Bibliothek selbst startet sie direkt.
- Quellcode-Programme starten über `cmd.exe /c`. Weil System32 kein „fester Ort“ in diesem Sinn ist, startet sie ein Kinderprofil nicht (strenger als nötig, bewusst so).
- Das Ziel einer `.lnk` wird nicht aufgelöst; für Kinder ist sie gesperrt.

## Vierte Prüfrunde (Commit „T-0764 fix4“)

Grundlage ist die Gegenprüfung von `f8492745c`.

**Rot auf f8492745c:** 14 von 14 rot. Die Testdateien gab es nur für diesen Beleg, sie sind nicht eingecheckt; sie starten nichts echt (Testmodus, FakeLauncher).
- `reports/T-0764/rot-fix4-core.log`: 13 von 13 (M1, M2, M3, N1, N2, N3 mit 7 Fällen, N4).
- `rot-fix4-app.log`: 1 von 1 (H).

| Befund | Was geändert | Test (grün) |
|---|---|---|
| H Code-Werkstatt und weitere Befehlswege | Neu `DarfAusfuehren` (Kern: `Rbac.DemandSoftware`). Im Kinderprofil startet bei Ausführen, Testen, Bauen, Paket und Terminal der Code-Werkstatt nichts. Der Hinweis lautet „Programme, Skripte und Befehle ausführen darf nur ein Erwachsenen-Profil“ und wird vor dem Testmodus geprüft. Damit laufen .ps1, .bat, .cmd, .vbs und .js für Kinder nie. Nachgesucht wurde mit `powershell`, `cmd.exe`, `/c`, `-Command`, `--python`, `bash`, `wscript`, `cscript` und `Process.Start` mit Befehlszeile. Ebenso abgesichert sind: Claude Code im Terminal (`ClaudeOpen`), der eingebettete Claude-Agent (`ClaudeRun`, KI mit Shell-Werkzeug), n8n/Node-RED starten (`AutomationStart`) und Spiele hochladen (`cmd /k butler/steamcmd`). Blender `--python` (Kreatur-Werkstatt, Kleidung) läuft nur mit Trendos eigenen, festen Skripten; Blender selbst gilt für Kinder als Interpreter (siehe N3). | App `RechteKernFix4Ui.Code_Werkstatt` (Kind: beide Wege abgelehnt, Hinweis sichtbar; Erwachsene: unverändert), `Weitere_Befehlswege` |
| M1 Fälschbare Kennung | Die Vertrauensliste liegt jetzt in Trendos Datenbereich (`<Hauptordner>\sicherheit\programm-vertrauen.json`). Jeder Eintrag aus Pfad und SHA-256 trägt einen HMAC-SHA256. Der Schlüssel ist zufällig, per DPAPI (CurrentUser) geschützt und wird nur beim Einrichten durch Trendo erzeugt. `.trendo-portable.json` im Programmordner ist nur noch ein Hinweis. Alte Kennungen übernimmt Trendo einmalig in einem Erwachsenen-Profil, ohne Rückfrage und nur für unveränderte Dateien. Die Programmordner werden mit `RegisterStore` angemeldet (keine globale Einstellung, daher keine Wechselwirkung zwischen parallelen Tests). Der Hinweis zu den Grenzen steht in Alltagssprache auf der Seite „Personen“, sobald es ein Kinderprofil gibt. | Core `RechteKernFix4.Liste_mit_HMAC` (gefälschte Kennung, Eintrag ohne gültigen HMAC und veränderte Prüfsumme werden abgelehnt; Schlüsseldatei echt per DPAPI geschützt), `Uebernahme`; App `Hinweis_sichtbar` |
| M2 Feste Orte | Für Kinder gelten fest nur „Programme“ und „Programme (x86)“. `%LOCALAPPDATA%\Programs` und die winget-Ablage gelten nur mit gültigem Listeneintrag oder gültiger Authenticode-Signatur des erwarteten Herausgebers. Dafür wird `Authenticode.Check` (WinVerifyTrust, T-0755) wiederverwendet. Die Zuordnung `ExpectedPublishers` ist bewusst klein (Code.exe → Microsoft Corporation); unbekannte Programme brauchen einen Listeneintrag. | Core `Feste_Orte_streng`; **echt** `Signatur_Herausgeber`: das installierte VS Code an diesem PC, WinVerifyTrust plus Herausgeber-Abgleich |
| M3 Ordner | Im Kinderprofil öffnet `LaunchAs(Ordner)` Trendos eigenen Dateimanager. Das gilt für alle Aufrufer, unter anderem „Im System öffnen“, „Ordner öffnen“ bei Downloads, „Ordner zeigen“ und „Ordner“ in Dateilisten. Der Kern lehnt Ordner und `shell:` für Kinder ab, auch den Papierkorb im Explorer. Zwei bestehende Testfälle (`shell:RecycleBinFolder` in Fix2 und Fix3) sind deshalb von „erlaubt“ auf „braucht das Recht“ umgestellt. | App `Ordner_im_Dateimanager` (Kind: Dateimanager, nichts gestartet; Erwachsene: wie bisher); Core `Ordner` |
| N1 Zwischenspeicher | Es gibt keinen Zwischenspeicher mehr: Vor jedem Start im Kinderprofil wird die Prüfsumme neu gerechnet. Bei Erwachsenen wird gar nicht gerechnet. | Core `Neu_rechnen_und_DLL` (gleich groß, gleich datiert, ausgetauscht) |
| N2 DLL | Die Liste erfasst `.exe` und `.dll`. Jede `.dll` im Ordner der `.exe` muss eingetragen und unverändert sein. | Core `Neu_rechnen_und_DLL` (hineingelegte und ausgetauschte DLL) |
| N3 Interpreter | Neu in der Liste: hh, mmc, msbuild, installutil, regasm, regsvcs, forfiles, wsl, wslg, explorer, pcalua, cmstp, msdt, odbcconf, dotnet, csc, tclsh, Rscript, blender, soffice. bash, pythonw, py, conhost, schtasks, certutil und bitsadmin standen schon drin. Blender und soffice laufen für Kinder nur mit fest von Trendo gesetzten Parametern. | Core `Interpreter` (13 Fälle, Start und freie Parameter) |
| N4 Steuerzeichen | Ein Ziel mit `char.IsControl` braucht das Recht. | Core `Steuerzeichen` |

**Tests:**
- Build: 0 Fehler.
- Core gezielt: 262 von 262 grün (`gruen-fix4-core.log`).
- App gezielt: 262 Testklassen (alle Klassen der berührten Seiten: Code-Werkstatt, Claude, n8n, Spiele, Dateimanager, Downloads, Starten, Sys11, 3D/Kreatur/Blender, Updates, KI-Quellen/-Bibliothek, Python-KI, Freigaben, KI-Brücke, RechteKern*; EveryButton_Alltag) – 1152 Tests, 0 Fehler, 5 übersprungen, **Exitcode 0** (`gruen-fix4-app.log`). **Testlauf mit Exitcode 1 (seit fix3):** Derselbe Lauf mit dem Stand vor der Korrektur endete mit „Foreground threads were left running“, Exitcode 1, 0 Fehler (`rot-fix4-app-lauf-exitcode1.log`). Die 18 seit fix3 neu im Lauf enthaltenen Klassen enden einzeln alle mit Exitcode 0, ebenso das Paar Kreatur_Ui + RechteKernFix3Ui. Als Ursache haben meine eigenen Testklassen statische Ersatz-Hooks gesetzt und wieder zurückgesetzt, die parallel laufende Klassen mitbenutzen: `KreaturRunOverride` (RechteKernFix3Ui), `ConnRunOverride` (RechteKernFix1Ui) und `ClaudeOpenOverride` (zunächst in RechteKernFix4Ui). Diese Tests prüfen jetzt ohne statische Hooks (über `BlenderPath`/`ToolStartAllowed` und das Protokoll). Danach endet derselbe Lauf mit Exitcode 0. **Ehrlich:** Der genaue Thread ist nicht per Stack nachgewiesen (kein dotnet-dump/-stack auf dem PC). Belegt ist nur „vorher 1 Lauf mit Exitcode 1 → nachher 1 Lauf mit Exitcode 0“ bei gleicher Klassenliste.

**Grenzen (ehrlich):**
- Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze. Ein Programm dieses Kontos kann den DPAPI-Schlüssel ebenso entschlüsseln und Dateien in Trendos Ordnern ändern.
- Wirksamer Kinderschutz braucht ein eigenes Windows-Standardkonto. So steht es auch auf der Personen-Seite.
- Direkte `Process.Start`-Aufrufe für bekannte Werkzeuge (ffmpeg, mpv, Ollama, KI-Server, Musik-Werkzeuge, Blender und Unreal außerhalb der Kreatur-Werkstatt) sind weiter nicht einzeln über `ToolStartAllowed` geführt. Ihre Pfade stammen aus Trendos Erkennung beziehungsweise aus der geprüften tools.json.

## Fünfte Prüfrunde (Commit „T-0764 fix5“)

Grundlage ist die Gegenprüfung von `90d49ca70`.

**Rot auf 90d49ca70:** `reports/T-0764/rot-fix5-app.log` – 8 von 8 rot (H1 mit 3 Fällen, H2, H3/N3, M1, M2 mit 2 Fällen).
- Bei H2 zeigt der Beleg genau den Befund: `Authenticode.Check` hält eine kopierte cmd.exe namens Code.exe für „Gueltig, Microsoft Corporation, Katalog=True“.
- Die Testdatei gab es nur für diesen Beleg; sie ist nicht eingecheckt. Sie startet nichts echt und nutzt keine statischen Hooks.

| Befund | Was geändert | Test (grün) |
|---|---|---|
| H1 Konsolen-Programme | Neu im Kern `ProgramTrust.ConsolePrograms`: VS Code, Codium, Cursor, Windsurf, Zed, Blender, Unreal-Editor, alles, was mit „Godot“ beginnt, LibreOffice (soffice/swriter/scalc …), Visual Studio, JetBrains-IDEs, Android Studio, Eclipse, Notepad++, Sublime, Arduino, Spyder, Jupyter, Thonny, IDLE, Kate, Geany, Unity sowie Excel/Word/PowerPoint/Access (Makros). Im Kinderprofil verweigert `DemandLaunch` sie unabhängig von `internalCall`, mit dem Hinweis „Programme mit eigener Skript-Konsole … öffnet nur ein Erwachsenen-Profil“. Damit sind gesperrt: W4Web „In VS Code öffnen“, W4Studio3D (Blender-Oberfläche), W4Game (Unreal) und ProgGame „Spielen“ (jetzt über `LaunchArgsAs` statt direktem `Process.Start`). Zusätzlich gesperrt: Kreatur „In Blender bauen (sichtbar)“ und die Kleidungs-Anprobe (Blender ohne `-b`). Erlaubt bleibt Blender im Hintergrund (`-b --python` mit Trendos eigenem Skript), also „Vorschaubild rendern“. | Core `RechteKernFix5.Konsolen_Programme` (5 Programme, auch fester Aufruf; Erwachsene wie bisher); App `Konsole_und_Papierkorb` |
| H2 Signatur | `ExpectedPublishers` verlangt jetzt (1) eine in der Datei eingebettete, gültige Signatur – keine Katalog-Signatur (neu `Authenticode.CheckEmbeddedStrict`), (2) den erwarteten Herausgeber und (3) passende Versionsinfos `OriginalFilename` und `ProductName` (für Code.exe: electron.exe / Visual Studio Code, am echten VS Code dieses PCs abgelesen). | **echt** `RechteKernFix5.Signatur_streng`: kopierte cmd.exe als Code.exe → nein; echtes VS Code → ja |
| H3 Code mit KI | `CodeAiRunTests`: zuerst `DarfAusfuehren`, dann `FreigabeAsync(Wirkung("code-ki.test"))`, Ausführung in `Approvals.Execute`. Auch der Testlauf nach „Mit Claude ändern“ (CodeAiKi) prüft die Rolle zuerst. Weitere Aufrufer von `TestCommand`/`BuildCommand`/`RunCommand`: Code-Werkstatt (schon mit `DarfAusfuehren`), KI-Weg `SoftwarePromptAdapter` (nur nach Freigabe, die ein Kind nicht erteilen kann). Die übrigen Fundstellen zeigen den Befehl nur an. | App `RechteKernFix5Ui.Code_mit_KI_Tests` (Kind: keine Anfrage, nichts gestartet; Erwachsene: „Nein“ → nichts ausgeführt) |
| M1 Übernahme | `MigrateMarkers` ist ersatzlos gestrichen. Liegt eine alte Kennungsdatei vor, das Programm steht aber nicht in der geschützten Liste, lautet die Ablehnung im Kinderprofil: „Dieses Programm stammt aus einem älteren Trendo-Stand. Bitte einmal als Erwachsener neu einrichten …“. Für Erwachsene ändert sich nichts. | Core `RechteKernFix4.Keine_Uebernahme` (ersetzt den früheren Übernahme-Test) |
| M2 Ganzer Ordner | Die Liste erfasst beim Einrichten alle Dateien des Programmordners samt Unterordnern (außer der Kennungsdatei). Vor dem Start im Kinderprofil muss jede Datei eingetragen und unverändert sein; neue Dateien führen zur Ablehnung (fail-closed). In der App läuft diese Prüfung für Trendo-Programme im Kinderprofil in `Task.Run`, mit Hinweis „Programm wird geprüft …“ und anschließender Meldung. | Core `RechteKernFix5.Ganzer_Ordner` (neue .py in Unterordner, ausgetauschte app.asar, neue .ini); App `Pruefung_im_Hintergrund` |
| N1 Sperrlisten | `CheckEmbeddedStrict` prüft die Sperrlisten der ganzen Kette (`WTD_REVOKE_WHOLECHAIN`, `WTD_REVOCATION_CHECK_CHAIN`), nur aus dem Zwischenspeicher von Windows (`WTD_CACHE_ONLY_URL_RETRIEVAL`) – ohne Warten auf das Netz. Fehlt dort eine Auskunft, gilt die Signatur als nicht bestätigt. Das bestehende `Authenticode.Check` (Gefahren-Prüfung T-0755) bleibt unverändert. | im Test `Signatur_streng` enthalten (echtes VS Code besteht mit Sperrlisten-Prüfung) |
| N3 Papierkorb | Der Papierkorb-Knopf in der Leiste ist im Kinderprofil ausgeblendet; die irreführende Meldung entfällt. | App `Konsole_und_Papierkorb` |

Angepasste bestehende Tests (wegen der strengeren Regeln, nicht abgeschwächt):
- `RechteKernFix3.Feste_Orte` und `RechteKernFix4.Feste_Orte_streng` nutzen statt blender.exe ein gewöhnliches Programm, denn Blender ist jetzt ein Konsolen-Programm.
- `RechteKernFix2Ui.Starten_zentral` wartet auf die Hintergrundprüfung (M2).

**Tests:**
- Build: 0 Fehler.
- Core gezielt: 289 von 289 grün (`gruen-fix5-core.log`). Darin enthalten sind alle Authenticode-Nutzer (T0755-Klassen) – das bisherige Verhalten ist unverändert.
- App gezielt: 1157 Tests in 266 Klassen, 0 rot, jeder Teil mit Exitcode 0 (`gruen-fix5-app.log`); dazu EveryButton_Alltag einzeln mit Exitcode 0 (`gruen-fix5-app-everybutton.log`). Der Lauf ist in 6 Prozesse aufgeteilt. Grund: Der PC war durch ein fremdes Blender dauerhaft zu 100 % ausgelastet, und ein einzelner Prozess mit allen Klassen endete zweimal mit Exitcode 1 (Zeitlimit von EveryButton, danach „Foreground threads were left running“, `fix5-app-lauf1-zeitlimit.log`). Teil 03 und 04 (Seitenrundgänge wie PathPickAuditTests) liefen zuerst ins Zeitlimit von 1500 s, ohne roten Test, und waren bei der Wiederholung mit 3600 s grün mit Exitcode 0. Alle seit fix4 neuen Klassen wurden zusätzlich einzeln mit Exitcode 0 geprüft.

**Grenzen und offene Punkte:**
- Ein Programm, das beim Laufen Dateien in seinem eigenen Ordner anlegt (Einstellungen, Zwischenspeicher), lässt sich danach im Kinderprofil erst wieder starten, wenn ein Erwachsener es neu einrichtet. Das ist bewusst fail-closed.
- Die Herausgeber-Liste ist bewusst klein (nur Code.exe, und das ist ohnehin ein Konsolen-Programm).
- Ohne Sperrlisten-Auskunft im Zwischenspeicher gilt eine Signatur nicht. Das Netz wird nicht abgefragt.
- Weiter gilt: Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze.

## Sechste Prüfrunde (Commit „T-0764 fix6“)

Grundlage ist die Gegenprüfung von `f99bd3045`. Sie hatte zwei hohe Befunde (A, B) und drei niedrige (C, D, E).

**Rot auf f99bd3045:**
- `reports/T-0764/rot-fix6-core.log`: 11 von 17 rot. Darunter alle Fälle der Erlaubnisliste (A) und die vier erlaubten Fälle bei Zwischenspeicher und Protokollen (C).
- `reports/T-0764/rot-fix6-app.log`: 4 von 4 rot.
  - B: Godot/Unity (`G.exe` lief), Unreal (`Build.bat`, `UnrealEditor-Cmd.exe`, `UnrealEditor.exe` liefen), Spiele-Bibliothek und Kittl.
  - E: Kreatur übergab `--python <Datei im Zwischenspeicher>`.
- Der Test für den Startausdruck (E) kam erst mit der neuen Hilfsfunktion dazu. Er ist deshalb nur grün belegt.

### A – Erlaubnisliste statt Sperrliste

**Was gilt jetzt:** Im Kinderprofil startet ein Programm (.exe) nur, wenn beides zutrifft:
1. Es ist genau eine erlaubte Vollpaket-Komponente: Programmordner direkt unter einem angemeldeten Werkzeugordner plus genauer Pfad darin, nicht nur der Dateiname (`ProgramTrust.ChildPrograms`, `InChildAllowList`).
2. Es ist geprüft: Eintrag in der geschützten HMAC-Liste, der ganze Ordner ist unverändert (M2).

Die Prüfung sitzt zentral in `Integrations.DemandLaunch`. Sie gilt für Ziele und Programme, mit und ohne `internalCall`. Ein fester Ort wie „Programme“ reicht nicht mehr. `InFixedLocation` gilt nur noch für Trendos eigene Hintergrundaufrufe, zum Beispiel Blender `-b` mit Trendo-Skript.

Alles andere bekommt den Hinweis `Rbac.NurErwachseneProgramm`: „Dieses Programm öffnet nur ein Erwachsenen-Profil. Im Kinderprofil starten Trendos eigene Werkstätten … und der PDF-Betrachter.“ Programme mit Skript-Konsole bekommen weiter `NurErwachseneKonsole`. Die Konsolen-Liste bleibt als zusätzliche Sicherung.

**Jeder Eintrag geprüft:**

| Programm | Entscheidung | Grund |
|---|---|---|
| SumatraPDF (`sumatra\SumatraPDF.exe`) | erlaubt | Reiner Betrachter, keine Skripte, keine Erweiterungen |
| mpv | nicht erlaubt | Eingebaute Konsole mit Befehl `run` startet beliebige Programme; lädt Lua-Skripte aus dem Benutzerordner |
| VLC | nicht erlaubt | Lua-Erweiterungen |
| mGBA | nicht erlaubt | Lua-Skripte |
| f3d | nicht erlaubt | Befehlskonsole |
| Krita, GIMP, Inkscape | nicht erlaubt | Python-Erweiterungen bzw. Skript-Konsole |
| OBS | nicht erlaubt | Python- und Lua-Skripte |
| Audacity | nicht erlaubt | Makros, Nyquist, mod-script-pipe |
| Kdenlive, LibreOffice | nicht erlaubt | Skripte bzw. Makros |
| Blender, Godot, Unreal, Unity, alle Editoren | nicht erlaubt | Skript-Konsole, führen Projektcode aus |
| scrcpy/adb | nicht erlaubt | Steuert das Handy |
| 7-Zip, Git, Python | nicht erlaubt | Interpreter und Werkzeuge |

Trendos interne mpv-Nutzung bleibt, denn sie hat keine Tastatur und keine Konsole:
- Der Fernseh-Player ist eingebettet und läuft mit `--input-vo-keyboard=no` und `--no-terminal`; gesteuert wird er über eine Pipe.
- Der Ton-Player hat kein Fenster.

**Ehrlich festgehalten:**
- Kinder können kreative Fremdprogramme wie Krita, GIMP, Inkscape, OBS oder Audacity nicht mehr selbst starten.
- Trendos eigene Malwerkstatt und die übrigen eigenen Werkstätten bleiben nutzbar.
- Auch von Trendo eingerichtete, geprüfte Programme außerhalb der Liste (GitHub- und Portable-Programme) startet nur ein Erwachsener.
- An der Oberfläche:
  - „Im Player öffnen“ (mpv/VLC) öffnet für Kinder das Windows-Programm des Dateityps; dabei gilt die zentrale Dokumentprüfung.
  - „In Bambu Studio öffnen“ zeigt den Hinweis.

**Tests:**
- Core `RechteKernFix6.Erlaubnisliste_nicht_drin` (Krita, OBS, Inkscape, Audacity, GIMP, eigenes Programm – eingerichtet und unverändert, trotzdem nein; Erwachsene ja).
- Core `Erlaubnisliste_Sumatra` (richtige Komponente ja; gleicher Name in anderem Ordner, tiefer verschachtelt oder unter „Programme“ nein; ausgetauschte Datei nein).

### B – Spiele-Studio und weitere Ausführwege

**Neu geschützt mit `DarfAusfuehren`, Rolle als erste Prüfung.** Zentral auch ohne Hinweisfenster über `DarfAusfuehrenStill`, das aus Hintergrund-Threads laufen kann und vor jedem Testersatz greift:

| Stelle | Was dort ausgeführt wird |
|---|---|
| `ProgGameEngines.EngineRun` (zentral) + `GameEngineTest` | Godot: Import, Level-, Spiel- und Spielstand-Prüfung. Unity: EditMode und PlayMode |
| `ProgGameUnreal.UnrealRun` (zentral) + `GameUnrealCheck` + `GameUnrealPlay` | `Build.bat`, `UnrealEditor-Cmd`, Welt und Mensch, Editor mit `Process.Start` |
| `ProgGameRelease.GameUnrealPackage` | RunUAT (Paket) |
| `ProgGameMixamo.MixamoToUnreal` | UnrealEditor-Cmd |
| `GameLibrary.LibStart` (zentral) + `LibPlayGodot` | Godot-Projekte aus der Bibliothek, mGBA |
| `PodDesigner.KittlCli` + `PodKittlLogin` | Claude Code (`mcp …`, Anmelde-Konsole) |
| `SourceApps.SourceRun` (zentral) + `SourceStart` | Build-Werkzeuge im Quellcode-Ordner, Start der Quellcode-Programme |
| `Connections.ConnRun` (zentral) | gh, git, claude. Den GitHub-Status fragt das Kinderprofil gar nicht erst ab |
| `Print3D` „In Bambu Studio öffnen“ | `Process.Start`; Kinder bekommen den Hinweis (A) |
| `Sys02` „Im Player öffnen“ | `Process.Start` mpv/VLC; Kinder öffnen das Windows-Programm über die zentrale Prüfung |

Kinder spielen und prüfen weiter in Trendos eigener Vorschau ohne Engine. Der Hinweis ist `NurErwachseneEngine`.

**Schon vorher geschützt (4./5. Prüfrunde):**
- Code-Werkstatt, Code mit KI (Tests, Claude-Änderungen), n8n/Node-RED (Einrichten und Start), Hochladen (butler/SteamCMD), Claude Code und Claude-Erweiterungen, Blender-Anprobe.
- npm-Einrichtungen (axe-core, FreeSewing, glTF-Transform) und alle Installer (Programme, Telegram, Tailscale, KI-Quellen) laufen nur nach einer Freigabe, die ein Kind nicht erteilen kann.

**Systematisch durchgesehen und nicht geändert, weil kein Code aus Nutzerordnern ausgeführt wird:**
- Medien- und Datenwerkzeuge mit festen Trendo-Aufrufen: FFmpeg (viele Stellen), whisper.cpp, Tesseract, pdftoppm, qpdf.
- Entpacken mit 7-Zip, Drucken mit SumatraPDF, Defender-Prüfung, Drucken zu PDF mit Edge/Chrome, Browser-App-Fenster.
- Interner mpv (Fernsehen, Ton), adb/scrcpy, netsh.
- Bambu-Studio-Befehlszeile (Slicen eines Modells).
- `git clone` (Klonen führt keinen Code des Repos aus).
- Syntaxprüfung mit `py_compile` und `node --check` (führt nichts aus).
- Versionsabfragen (`--version`).
- Trendo selbst (zweites Fenster, Stick-Schreiber mit Windows-Rückfrage), `ollama serve`, Deinstallation.

**Tests:**
- App `RechteKernFix6Ui.Engines_Kind`: Kind startet nichts, Erwachsene wie bisher.
- App `Unreal_Kind`.
- App `Bibliothek_und_Kittl_Kind`.

### C – Zwischenspeicher und Protokolle

`ProgramTrust.IgnoredInFolderCheck`: Die Ordnerprüfung übergeht diese Dateien, sofern sie kein Code sind:
- `*.log`,
- alles in Ordnern namens `cache` oder `logs`.

Code bleibt immer streng (`ProgramTrust.CodeExtensions`): `.exe`, `.dll`, `.pyd`, `.py`, `.pyc`, `.asar`, `.js`, `.lua`, `.ps1`, `.bat`, `.cmd`, `.vbs`, `.jar` und weitere sowie Dateien ohne Endung.

Nicht ausgenommen, ehrlich festgehalten:
- **`.pyc` und `__pycache__`:** Python führt auch eine `.pyc` aus, deren Kopfdaten (Zeit, Größe) zur `.py` passen, deren Inhalt aber ein anderer ist. Die Prüfung „passende .py ist geprüft“ wäre deshalb wackelig.
- **Portable Einstellungsordner und -dateien:** Sie können Befehlszeilen oder Skripte enthalten, zum Beispiel `SumatraPDF-settings.txt` (`InverseSearchCmdLine`) oder mpv `portable_config`.

Ein Programm, das dort schreibt, richtet ein Erwachsener danach einmal neu ein.

**Test:** Core `RechteKernFix6.Zwischenspeicher` mit 10 Fällen.

### E – Kreatur-Skript

Blender bekommt keinen Skriptpfad mehr, sondern `--python-expr` mit einem kurzen Startausdruck (`Studio3D.VerifiedScriptExpr`). Blender liest die Datei einmal, vergleicht die SHA-256 mit dem eingebauten Inhalt und führt genau diese Bytes aus. Ein Austausch zwischen Prüfen und Starten ist so nicht möglich; eine veränderte Datei bricht mit „TRENDO-SKRIPT-VERAENDERT“ ab.

Der Pfad wird als JSON-Zeichenkette eingesetzt, Anführungszeichen sind maskiert. Eine ungültige Prüfsumme lehnt die Funktion ab.

**Tests:**
- App `Kreatur_Ui` (verschärft: kein `--python`, Prüfsumme des eingebauten Skripts im Ausdruck).
- Core `RechteKernFix6.Skript_Startausdruck`.

### D

Wie vereinbart nicht geändert. Bleibt als niedriger, offener Punkt aus der Gegenprüfung bestehen.

### Angepasste bestehende Tests

Grund ist die Erlaubnisliste; abgeschwächt ist keiner:
- Fix2 `Zentral_im_Kern`: Ein vertrauenswürdiges Trendo-Programm ist für Kinder jetzt abgelehnt, mit Meldung.
- Fix3 `Herkunft`, `Interpreter_und_Parameter`, Fix4 `Liste_mit_HMAC`, `Neu_rechnen_und_DLL`, Fix5 `Ganzer_Ordner`, Fix2Ui `Starten_zentral` und Fix5Ui `Pruefung_im_Hintergrund`: Der erlaubte Fall nutzt jetzt die Komponente `sumatra\SumatraPDF.exe`.
  - In `Neu_rechnen_und_DLL` prüft zusätzlich `Verified` direkt, dass DLLs erkannt werden.
  - `Interpreter_und_Parameter` prüft zusätzlich, dass ein geprüftes Programm außerhalb der Liste abgelehnt wird.
- Fix3 `Feste_Orte`: Ein Programm unter „Programme“ ist für Kinder jetzt abgelehnt (`NurErwachseneProgramm`); Erwachsene wie bisher.

### Tests gesamt

- Build: 0 Fehler.
- Core gezielt: 208 von 208 grün (`gruen-fix6-core.log`).
- App gezielt (`gruen-fix6-app.log`): 194 Klassen, die die geänderten Wege nutzen, in 5 Prozessen plus EveryButton_Alltag, zusammen 928 Tests. Teil 01 bis 04 und EveryButton liefen mit Exitcode 0.
- Teil 00 hatte einen roten Test: den echten Chrome-Lauf `GameFree_Ui.Browser_side_win_and_lose` (Zeitverhalten unter Last). Er hängt nicht an dieser Änderung.
- Danach kam eine letzte Änderung: `GameEngineTest` prüft die Rolle ohne Hinweisfenster, weil die Funktion auch im Hintergrund läuft.
- Nachlauf mit dem Endstand: Teil 00 komplett, GameStage3_Ui, alle RechteKern-Oberflächenklassen und Kreatur_Ui, zusammen 221 Tests.
  - Nachlauf 1: kein roter Test, aber Exitcode 1. Grund ist ein Aufräumfehler des Avalonia-Testrahmens nach einem Audio-Test („different thread owns it“).
  - Nachlauf 2: 0 rot, Exitcode 0.

### Grenzen und offene Punkte

- **KI-Programme:** Trendos eigene KI-Programme (Python-Umgebungen für Musik, Bilder, IFC) und Werkzeuge wie FFmpeg liegen im Werkzeugordner. Ein Erwachsener hat sie eingerichtet, der Ordner ist für das Windows-Konto beschreibbar. Sie laufen mit festen Trendo-Aufrufen auch im Kinderprofil, ihre Dateien werden aber nicht vor jedem Start gehasht.
- **Weitere Blender-Skripte:** Im Kern übergeben Studio3D, PlanPhoto, Mixamo und ModelConverter weiter `--python <Datei>`. E ist nur für die Kreatur umgesetzt.
- **Dokumente:** Sie öffnet Windows mit dem Standardprogramm des Dateityps. Das kann ein Programm außerhalb der Erlaubnisliste sein.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz weiter keine harte Grenze.

## Siebte Prüfrunde (Commit „T-0764 fix7“)

Grundlage ist die Gegenprüfung von `6de0e9798`. Sie hatte einen hohen Befund (Dokumente gehen an das Windows-Standardprogramm), einen mittleren (SumatraPDF) und zwei niedrige.

**Rot auf 6de0e9798:**
- `reports/T-0764/rot-fix7-core.log`: 22 von 22 rot.
  - H: 11 Dateiarten werden an Windows übergeben.
  - M: SumatraPDF startet im Kinderprofil.
  - N: `.pth` gilt nicht als Code; `.json`, `.ini`, `.txt`, `.bin` in `cache`/`logs` sind ausgenommen; Blender bekommt an sieben Stellen `--python <Datei>`.
- `reports/T-0764/rot-fix7-app.log`: 2 von 2 rot. Das Kind öffnet `brief.txt` über Windows statt im eigenen Betrachter, und eine ZIP-Datei mit Programm wird entpackt.
- Für den Rot-Beleg lief die Core-Testdatei ohne die Tests, die neue Schnittstellen brauchen (`InAllowList`, `BlenderScript`, `UnpackAs`). Diese Tests sind nur grün belegt.

### H – Kinderprofil übergibt nie etwas an Windows

**Kern, fail-closed:** `Integrations.DemandLaunch` lehnt im Kinderprofil jedes Ziel ab, das keine erlaubte Adresse ist (`Rbac.NurErwachseneDatei`: „Diese Datei kann nur ein Erwachsener öffnen.“).
- Das gilt für Dokumente, Archive, Abbilder, Skripte und `file:`-Adressen.
- Erlaubt bleiben nur http, https, mailto und die einzelnen ms-settings-Seiten, wie bisher geprüft.
- Zusammen mit der leeren Erlaubnisliste (M) gilt: Ein Kinderprofil übergibt nichts an ShellExecute und startet kein Programm.

**App:** `LaunchAs` und der Dateimanager (`FilesOpen`, vor Testersatz und Testmodus) leiten Dateien im Kinderprofil auf Trendos eigene Betrachter um (`OpenInOwnViewer`):

| Dateiart | Eigener Betrachter |
|---|---|
| PDF | Trendo PDF |
| .docx, .odt, .txt, .md, .rtf | Trendo Text |
| .xlsx, .ods, .csv | Tabelle |
| .pptx, .odp | Präsentation |
| Bilder | Fotos |
| Musik | Trendos eigener Player |
| Videos | Medien bearbeiten |
| .glb, .gltf, .obj, .stl | 3D-Modelle ansehen |

Für alles andere erscheint der Hinweis, mit Protokoll „rbac.denied“.

**Umgestellte direkte Aufrufe:**
- Diashow `OpenMedia` (vorher ShellExecute).
- Mediathek `LibraryPlay`.
- Drucken: `PrintNow` mit SumatraPDF bzw. Windows-Verb „print“; Kinder bekommen `NurErwachseneDrucken`.
- Drucken aus Text und Tabelle (SumatraPDF): Kinder sehen das PDF in Trendo.
- Alle übrigen `LaunchAs`- und `Launch`-Aufrufe mit Dateien laufen über die zentrale Umleitung, zum Beispiel FileRow „Öffnen“, `OpenOfficeDoc`, Toast „Öffnen“ und Suche.
- `ShellProcessLauncher` wird nur über `Integrations` erreicht, also über die Kern-Prüfung.

**Weitere ShellExecute-Stellen**, die nur hinter einer Freigabe laufen (Kinder können keine erteilen): Telegram-, Tailscale- und KI-Quellen-Setup. Kittl-Konsole und Hochladen sind schon über `DarfAusfuehren` geschützt.

**Archive (geprüft):**
- Vorher hat Trendos Entpacken (`Archives.Unpack`, nur ZIP) ausführbaren Inhalt nicht geprüft.
- Jetzt gilt `Archives.UnpackAs`: Im Kinderprofil nur ZIP und nur ohne ausführbare Einträge (Code-Endungen inklusive `.pth`, Installer, Verknüpfungen, Abbilder, Dateien ohne Endung), sonst `NurErwachseneArchiv`.
- .7z und .rar entpackt Trendo im Kinderprofil nicht.
- .iso, .img, .vhd, .vhdx, .cab, .wim und .esd (`Software.MountLike`) sind gesperrt: Sie gehen nicht an Windows, und in einem ZIP verhindern sie das Entpacken.

### M – SumatraPDF aus der Erlaubnisliste

`ProgramTrust.ChildPrograms` ist leer. Grund: SumatraPDF liest `ExternalViewers` und `InverseSearchCmdLine` aus %LOCALAPPDATA% und kann damit beliebige Befehle starten.

Der Mechanismus bleibt: Der Abgleich an Komponente und Pfad ist mit einer Testliste prüfbar (`InAllowList`), ohne die echte Liste zu ändern.

**Ehrlich:** Kinder starten keine Fremdprogramme mehr. PDFs öffnen sie in Trendos eigenem PDF-Betrachter.

### N – Niedrige Befunde

- **`.pth` und Konfiguration:** `.pth` gehört jetzt zu `CodeExtensions`. In `cache\` und `logs\` sind nur noch `*.log` und reine Datenendungen (`ProgramTrust.DataExtensions`: Bilder, `.tmp`, `.cache`) ausgenommen.
  - Nicht mehr ausgenommen: `.json`, `.ini`, `.txt`, `.cfg` und `.bin`/`.dat` (Electron lädt zum Beispiel `snapshot_blob.bin`).
- **Neuer Weg `BlenderScript`:** Er gilt für alle Blender-Hintergrundläufe und die Anprobe, nämlich Studio3D (Modell/Vorschau/Umwandeln und Szene), Studio3DEngines, Studio3DPro, PlanPhoto, ModelConverter, MixamoBlender, ProgModeGarment und Kreatur.
  - Blender liest jede Datei selbst, prüft die SHA-256 gegen den Inhalt, den Trendo geschrieben hat, und führt genau diese Bytes aus.
  - PlanPhoto: Das mitgeladene Modul `trendo_moebel` wird ebenso geprüft und vorab in `sys.modules` eingetragen, damit `import` nicht die Datei liest. Die Prüfsummen kommen aus den eingebauten Skripten; ohne sie startet PlanPhoto nichts.
  - Ein Test durchsucht den Quelltext: Kein Blender-Aufruf übergibt mehr `--python <Datei>`.

### Angepasste bestehende Tests

Grund sind die strengeren Regeln; abgeschwächt ist keiner:
- **Erlaubnisliste:** Dort, wo ein Kind bisher SumatraPDF startete (Fix3 `Herkunft`, `Interpreter_und_Parameter`, Fix4 `Liste_mit_HMAC`, `Neu_rechnen_und_DLL`, Fix5 `Ganzer_Ordner`, Fix6 `Erlaubnisliste_Sumatra`, `Zwischenspeicher`, Fix5Ui `Pruefung_im_Hintergrund`, Fix2Ui `Starten_zentral`), wird jetzt die Ablehnung erwartet.
  - Die Herkunfts- und Ordnerprüfung selbst prüft `ProgramTrust.Verified` direkt (geprüft ja/nein); den Abgleich prüft `InAllowList`.
- **Dateien:** Fix1Ui `Dateien_Installer` und Fix2Ui `Starten_zentral` erwarten, dass das Kind Dokumente in Trendo Text öffnet und nichts an den Testersatz (Windows) geht.
- **Zwischenspeicher:** Fix6 `Zwischenspeicher` erwartet `logs\start.txt` und `cache\*.bin` jetzt streng.
- **Startausdruck:** Fix6 `Skript_Startausdruck` prüft den gemeinsamen Startausdruck.

### Tests

- Build: 0 Fehler.
- Core gezielt: 281 von 281 grün, Exitcode 0 (`gruen-fix7-core.log`).
- App gezielt (`gruen-fix7-app.log`): 245 Klassen, die die geänderten Wege nutzen, in 6 Prozessen plus EveryButton_Alltag, zusammen 1159 Tests. 0 rot, jeder Prozess mit Exitcode 0, alles mit dem Endstand.

### Grenzen und offene Punkte

- **Eigene Betrachter:** Betrachter für Musik und Video nutzen intern FFmpeg bzw. mpv ohne Fenster und Tastatur, mit festen Trendo-Aufrufen. Werkzeuge im beschreibbaren Werkzeugordner werden weiter nicht vor jedem Start gehasht.
- **Weitere Dateiarten:** Dateiarten ohne eigenen Betrachter (zum Beispiel .kra, .blend, .py, Archive) öffnet nur ein Erwachsener.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze.

## Achte Prüfrunde (Commit „T-0764 fix8“)

Grundlage ist die Gegenprüfung von `0f9633f90`. Sie hatte einen hohen Befund (mpv lädt %APPDATA%\mpv), zwei mittlere (Windows-Dateidialoge im Kinderprofil, `sys.path` in Blender-Skripten) und zwei niedrige (Entpacken).

**Rot auf 0f9633f90:**
- `reports/T-0764/rot-fix8-core.log`: 9 von 15 Tests rot, darunter 4 der 10 Namensfälle. Rot waren:
  - H: mpv-Argumente des Player-Fensters, mpv-Starts im Quelltext, Versionsprüfung.
  - M1: 59 System-Dialoge außerhalb des Helfers.
  - M2: `sys.path.insert` in `trendo_foto.py`, Startausdruck ohne Bereinigung.
  - N: `spiel.exe ` mit Leerzeichen am Ende, `CON.txt`, `aux.png`, `COM1.jpg`, ZIP-Bombe, mehr als 10 000 Einträge.
- Sechs Namensfälle lehnte der alte Stand schon ab, aber nur zufällig über andere Regeln (Datei ohne Endung, `.exe` am Ende, Fehler beim Entpacken). Es sind `spiel.exe.`, `spiel.exe. .`, `nul`, `lpt9`, `bild.png:boese.exe` und `ordner./bild.png:x`.
- `reports/T-0764/rot-fix8-app.log`: Die eingebetteten Player starteten mpv ohne `--no-config`.
- Für den Rot-Beleg liefen die Testdateien ohne die Tests, die neue Schnittstellen brauchen (`MpvSafe`-Tastenbelegung, Trendos Auswahl `PickOpenFiles`/`PickSaveFile`). Diese Tests sind nur grün belegt.

### H – mpv nie mit %APPDATA%\mpv

Neu im Kern ist `MpvSafe.Args()`. Jeder mpv-Start von Trendo bekommt es, für alle Profile:
- `--no-config`, `--load-scripts=no`, `--input-default-bindings=no`
- `--input-conf=<Trendo-Datei>`; die Datei wird vor jedem Start neu geschrieben.

Betroffen sind:
- der Ton-Player (`MpvAudio.StartArgs`, auch die Vorschau im Videoschnitt),
- der eingebettete Fernseh-Player (`MpvView.Args`),
- das eigene Player-Fenster für Erwachsene (`MediaLibrary.Player`); `Sys02.Play` übergibt jetzt alle Argumente.

**Tastatur:** Die eingebetteten Player bleiben ohne Tastatur (`--input-vo-keyboard=no`, `--no-terminal`) und bekommen eine leere Belegung. Das eigene Fenster bekommt Trendos feste, sichere Belegung (`MpvSafe.SafeKeys`): Pause, Spulen, Lautstärke, Stumm, Vollbild, Beenden. `MpvSafe.IsSafeInputConf` erlaubt genau diese Befehle, also kein `run`, `subprocess`, `load-script`, `script-binding` und keine Befehlsketten.

**Konsole (`--script-opts` geprüft):** Die eingebaute Konsole öffnet sich nur über eine Tastenbindung. Die Standardbindungen sind aus, und Trendos Belegung enthält keine Konsole. Benutzerskripte lädt mpv nicht mehr (`--load-scripts=no`).

Eigene Optionen wie `--load-console=no` setze ich bewusst nicht. Ihre Namen haben sich zwischen mpv-Fassungen geändert, und eine unbekannte Option bricht den Start ab.

**Weitere Startstellen (durchsucht):**
- Die Versionsprüfung startet `mpv --no-config --version`.
- VLC startet ohne Lua-Erweiterungen (`--no-lua`).
- ffplay startet Trendo nirgends.

Ein Quelltext-Test stellt sicher, dass jede mpv-Startstelle die festen Argumente nutzt.

### M1 – Datei-Dialoge im Kinderprofil

Neuer zentraler Helfer `MainWindow.KidPicker.cs` mit `PickOpenFiles`, `PickSaveFile` und `PickFolders`. Alle 59 Aufrufe in 48 Dateien laufen jetzt darüber.

- **Erwachsene:** unverändert der Dialog des Systems.
- **Kinderprofil:** Trendos eigener Auswahl-Dialog.
  - Er zeigt nur den Benutzerordner und die bekannten Ordner und kommt nicht weiter nach oben.
  - Man kann darin nicht starten, umbenennen oder „Öffnen mit“ wählen.
  - Beim Speichern akzeptiert er nur einfache Dateinamen: keine Pfade, keine Gerätenamen, keine Datenströme, keine Programm- oder Skript-Endungen.
  - Das Ergebnis kommt als Objekt des Systems zum lokalen Pfad zurück (`TryGetFileFromPathAsync`), ohne Dialog.

Kein Aufruf war zu riskant für die Umstellung; gesperrt ist dafür keine Funktion. Ein Quelltext-Test stellt sicher, dass `StorageProvider.Open…/Save…PickerAsync` nur noch im Helfer vorkommen (genau drei Stellen).

### M2 – `sys.path` in Blender

`trendo_foto.py` setzt `sys.path` nicht mehr; `trendo_moebel` lädt Trendo geprüft vorab in `sys.modules`. `BlenderScript.Expr` bereinigt `sys.path`, bevor irgendein Trendo-Skript läuft:
- **Raus:** Skriptordner, Temp, %APPDATA% (Blender-Benutzerskripte), der aktuelle Ordner als genauer Eintrag, `''` und `.`.
- **Bleibt:** Blenders eigene Ordner neben dem Programm.

Der Startausdruck ist jetzt eine einzige Base64-Zeile (`import base64;exec(…)`, lesbar über `BlenderScript.Decode`). Der Grund ist ein Fund aus dem gezielten Lauf: Der mehrzeilige Ausdruck mit Anführungszeichen wurde von der Windows-Kommandozeile falsch zerlegt, sobald Blender über ein Startskript (.cmd) aufgerufen wird (`W4_Core05_Ui`).

Alle eingebauten Blender-Skripte (`Assets/blender/*.py`, `Assets/ki/trendo_anprobe.py`) sind frei von `sys.path`-, `importlib`-, `__import__`- und `runpy`-Tricks; das prüft ein Test. Die übrigen Skripte unter `Assets/ki` sind keine Blender-Skripte, sondern starten installierte KI-Programme.

### N – Entpacken im Kinderprofil

- **Namen:** `Archives.UnsafeEntryName` normalisiert jeden Pfadteil wie Windows (`TrimEnd(' ', '.')`). Unsicher sind auch alternative Datenströme (`:`), Gerätenamen (CON, PRN, AUX, NUL, COM1–9, LPT1–9, CONIN$, CONOUT$, auch mit Endung) und Steuerzeichen.
- **Grenzen vorab** (aus dem Inhaltsverzeichnis, ohne zu entpacken): höchstens 2 GB entpackt, höchstens 10 000 Einträge. Ein Packverhältnis über 100:1 bei mehr als 10 MB gilt als verdächtig.
- **Erwachsene:** unverändert.

### Tests

- Build: 0 Fehler.
- Core gezielt: 301 von 301 grün, Exitcode 0 (`gruen-fix8-core.log`).
- App gezielt (`gruen-fix8-app.log`): 320 Klassen in 7 Prozessen plus EveryButton_Alltag, zusammen 1369 Tests.
- Teil 00 bis 05 und EveryButton liefen mit Exitcode 0.
- Teil 06 hatte einen roten Test: `W4_Core05_Ui`, Blender über ein Startskript (siehe M2). Daraufhin ist der Startausdruck jetzt eine Base64-Zeile.
- Nachlauf mit dem Endstand: Teil 06 komplett, alle Blender-, 3D-, Kreatur-, Foto-, Anprobe- und Mixamo-Klassen sowie alle RechteKern-Oberflächenklassen, zusammen 99 Klassen und 408 Tests. 0 rot, Exitcode 0.

### Grenzen und offene Punkte

- **mpv-Fassung:** Die Belegung im Player-Fenster ist bewusst knapp. Weitere mpv-Funktionen per Taste (Untertitel, Tonspur) gibt es dort nicht mehr.
- **Kinder-Auswahl:** Sie zeigt keine Vorschaubilder.
- **Werkzeugordner:** Werkzeuge darin werden weiter nicht vor jedem Start gehasht.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze.

## Neunte Prüfrunde (Commit „T-0764 fix9“)

Grundlage ist die Gegenprüfung von `d73089760`. Sie hatte einen mittleren Befund (`PickInto` umgeht den Helfer) und sechs niedrige.

**Rot auf d73089760:**
- `reports/T-0764/rot-fix9-core.log`: 5 von 5 rot.
  - M: System-Dialog in `MainWindow.Files.cs:185/191`.
  - N1: mpv über `LaunchAs(player, …)` bzw. ohne gesperrte Tastendatei.
  - N2: Datei ohne „--“.
  - N3: Anprobe ohne `--factory-startup`.
  - N4: fehlende Tasten.
- `reports/T-0764/rot-fix9-app.log`: 2 von 2 rot. „Datei wählen …“ am Pfad-Feld öffnete beim Kind keine Trendo-Auswahl, und der Fernseh-Player bekam die Adresse ohne „--“.
- Für den Rot-Beleg lief die Core-Datei ohne die Tests, die neue Schnittstellen brauchen (`MpvSafe.Release`, `TargetAllowed`). Diese Tests sind nur grün belegt.

### M – „Durchsuchen …“ an Pfad-Feldern

- `PickInto` nutzt jetzt `PickFolders` bzw. `PickOpenFiles`. Kinder bekommen Trendos Auswahl, Erwachsene den System-Dialog.
- **Quelltext-Test verschärft:** Jedes `.OpenFilePickerAsync(`, `.SaveFilePickerAsync(` und `.OpenFolderPickerAsync(` außerhalb von `MainWindow.KidPicker.cs` ist rot, egal über welche Variable. Ebenso `TryGetFolderFromPathAsync`/`TryGetFileFromPathAsync` und Avalonias Launcher (`Launcher.Launch…`, `LaunchFileAsync`, `LaunchUriAsync` …).
- **Ganzes Repo durchsucht:** Danach gibt es keine weiteren Stellen.

### N1 – mpv in Videoliste und Mediathek

`FileRow` (Videos in der Musik-Ansicht und in den Dateilisten) startet mpv nicht mehr über `LaunchAs(player, file)`, sondern über `Play`, also `MediaLibrary.Player` mit `MpvSafe`. Kinder bekommen dort Trendos eigenen Betrachter.

Der Quelltext-Test erfasst jetzt auch `Launch`/`LaunchAs`/`LaunchArgsAs` mit einem mpv-Pfad als Variable. Außerdem muss jedes `ProcessStartInfo(<mpv-Pfad>)` die Tastendatei bis zum Ende sperren (`HoldUntilExit`).

### N2 – „--“ und erlaubte Ziele

`MpvSafe.TargetAllowed` lässt nur http, https, rtmp, rtmps und rtsp zu oder eine vorhandene lokale Datei mit vollem Pfad (eigene Aufnahmen, Mediathek). Abgelehnt sind file:, memory:, av:/lavfi:, alles mit „-“ am Anfang und Steuerzeichen. Es gilt für:
- `MpvView.Args`: Adresse hinter „--“, sonst eine Ausnahme.
- `MpvView.Play`, auch beim schnellen Umschalten per `loadfile`.
- `TvPlayUrl`: Meldung statt Start.
- `MpvAudio.Load`.
- `MediaLibrary.Player`: mpv und VLC bekommen die Datei hinter „--“; `Sys02.Play` prüft vorher.

### N3 – `--factory-startup`

- Ergänzt bei der Anprobe.
- Bei der Kreatur und beim ModelConverter steht es jetzt direkt in der Argumentliste des Skripts.
- Jeder Blender-Lauf mit Trendo-Skript hat es; ein Quelltext-Test prüft das.
- **Bewusst ohne:** die Blender-Oberfläche für Erwachsene (eigene Datei öffnen, `ExternalApps` „In Blender öffnen“), damit ihre eigenen Einstellungen und Add-ons bleiben. Kinder starten die Blender-Oberfläche nicht (Konsolen-Programm).

### N4 – Tasten

`SafeKeys` hat jetzt zusätzlich:
- `cycle sub`, `cycle audio`
- `add speed ±0.1`, `set speed 1`
- `screenshot`
- `add chapter ±1`
- `seek ±60` (Umschalt + Pfeil)
- `frame-step`, `frame-back-step`

`IsSafeInputConf` erlaubt genau diese Befehle zusätzlich, weiter ohne `run`, `subprocess`, `script-*`, `load-script`, `set` für Pfad-Optionen, Ketten und Anführungszeichen. Kinder bekommen dieselbe Belegung, eingebettete Player die leere.

### N5 – Tastendatei (TOCTOU)

- Jeder Start bekommt eine eigene Datei.
- Ablauf: schreiben, dann mit `FileShare.Read` sperren. Andere dürfen nur lesen; Ändern, Umbenennen und Löschen schlagen fehl.
- Anschließend wird der Inhalt über genau diesen gesperrten Zugriff geprüft. Wurde die Datei zwischen Schreiben und Sperren ausgetauscht, startet nichts.
- Die Sperre hält, bis mpv endet (`HoldUntilExit`); danach wird die Datei gelöscht.
- **`memory://`** für `--input-conf` nutze ich nicht. Mitgeliefert ist der shinchiro-Build 20261021, und ob er das für input.conf sicher kann, ließe sich nur mit einem echten mpv-Lauf belegen. Ich habe es nicht geraten.

### N6 – Syntaxprüfung des Startausdrucks

Weggelassen, mit Grund: Dafür müsste der Test ein fremdes Python starten, und ob ein solches sicher vorhanden ist, ist nicht gewährleistet. Der Ausdruck wird weiter über `BlenderScript.Decode` inhaltlich geprüft.

### Tests

- Build: 0 Fehler.
- Core VOLLSTÄNDIG (`gruen-fix9-core.log`), ohne Filter: 3602 Tests, davon 3560 grün und 42 übersprungen, Exitcode 0 (Lauf 3, ohne parallele Testsammlungen).
- Lauf 1 und 2 (parallel) hatten je einen roten Zeit- bzw. Leistungstest ohne Bezug zu T-0764: einmal `KiModelT0703Tests` (Zeitgrenze, einzeln zweimal grün), einmal T-0747-fix2 „Zickzack“ (Geschwindigkeit). Beide Auszüge stehen im Log.
- Der vollständige Lauf enthält bestehende Tests, die vorhandene Werkzeuge wie FFmpeg nutzen, wenn sie installiert sind.
- App gezielt (`gruen-fix9-app.log`): 223 Klassen in 5 Prozessen plus EveryButton_Alltag, zusammen 955 Tests.
- Teil 00 bis 03 und EveryButton liefen mit Exitcode 0.
- Teil 04 hatte einen roten Zeittest mit echtem FFmpeg ohne Bezug zu T-0764 (`TonT0732_Ui.Record_until_stop`: 2,92 s statt 3 s).
- Die Wiederholung von Teil 04 war mit allen 154 Tests grün, Exitcode 0.

### Grenzen und offene Punkte

- **Blender-Oberfläche:** Erwachsene öffnen sie weiter mit ihren eigenen Einstellungen (ohne `--factory-startup`).
- **Werkzeugordner:** Werkzeuge darin werden weiter nicht vor jedem Start gehasht.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze.

## Zehnte Prüfrunde (Commit „T-0764 fix10“)

Grundlage ist die Gegenprüfung von `b0fddd56b`. Sie hatte einen mittleren Befund (mpvs yt-dlp-Hook ist aktiv; das Kind kann in Trendos Werkzeugordner schreiben) und niedrige Punkte.

**Rot auf b0fddd56b:**
- `reports/T-0764/rot-fix10-core.log`: 4 von 4 rot. Es fehlte `--ytdl=no`, mpv/VLC starteten ohne eigenen leeren Arbeitsordner, und udp/rtp wurden abgelehnt.
- `reports/T-0764/rot-fix10-app.log`: 2 von 2 rot. Das Kind entpackte ein ZIP in `werkzeuge\mpv`, und es fügte eine eigene M3U-Quelle hinzu.
- Für den Rot-Beleg lief die Core-Datei ohne die Tests, die neue Schnittstellen brauchen (`ProtectedFolders`, `FileBrowser.WriteGuard`, `MpvSafe.WorkDir`/`CleanupStale`). Diese Tests sind nur grün belegt.

### M a – yt-dlp-Hook aus

`MpvSafe.Args()` setzt jetzt `--ytdl=no`, für jeden mpv-Start; ein Test prüft das. Im echten Lauf (siehe unten) lädt mpv den `ytdl_hook` danach nicht mehr.

### M b – leerer eigener Arbeitsordner

`MpvSafe.WorkDir()` liefert `%TEMP%\trendo-mpv-<PID>\leer`. Vor jedem Start wird der Ordner geleert; ist er danach nicht leer, startet nichts. Der Ordner ist geschützt (M c).

Jeder mpv- und VLC-Start setzt `WorkingDirectory` darauf: `MpvAudio`, Fernseh-Player (`MpvView`) und eigenes Player-Fenster (`Sys02.Play`). Ein Quelltext-Test erzwingt das.

### M c – Kinder schreiben nie in Trendos eigene Ordner (eigentliche Ursache)

Neuer zentraler Helfer `ProtectedFolders` im Kern.

**Geschützt sind:**
- der Hauptordner aller Profile und der Datenordner (mit werkzeuge, programme, sicherheit, cache, tools, Einstellungen, Profilen, Protokollen),
- der Werkzeugordner, das Vollpaket, der Installationsordner und der Programmordner,
- Trendos mpv-Ordner in Temp.

**Freigegeben bleiben** die eigenen Projekte (`projekte`).

**Wie geprüft wird:** Der längste passende Eintrag entscheidet. Geprüft werden der Pfad wie angegeben und aufgelöst (`GetFinalPathNameByHandle`: Junction, Symlink, subst), auch für noch nicht vorhandene Ziele. Ein unklarer Pfad gilt als geschützt.

**Wo die Regel greift:**
- **Dateimanager:** `FileBrowser.WriteGuard` prüft vor jedem Anlegen, Kopier-Ziel, Verschieben (Quelle und Ziel), Umbenennen, Löschen, Papierkorb und Zurücklegen. Das gilt auch für Einfügen aus der Zwischenablage, Ziehen und Ablegen sowie Rückgängig/Wiederholen, weil alle über diese Prüfung laufen. Ausgeblendete Elemente ändern daran nichts. Lesen und Kopieren heraus bleiben erlaubt.
- **Entpacken:** `Archives.UnpackAs` prüft das Ziel.
- **Speichern- und Ordner-Auswahl im Kinderprofil:** `KidPickAllowed` lässt geschützte Ordner nicht zu.

**Weitere Schreibwege, durchgesehen:**
- Downloads landen im Download-Ordner, nicht in Trendos Ordnern.
- Projekt-Exporte und „Speichern unter“ laufen über die Kinder-Auswahl.

Erwachsene: unverändert. Bei Ablehnung gibt es den Hinweis `Rbac.NurErwachseneTrendoOrdner` und „rbac.denied“.

### M d – Fernseh-Quellen

`TvAddSource` (M3U per Adresse oder Datei, Xtream) richtet nur ein Erwachsener ein; das Kind bekommt `Rbac.NurErwachseneTvQuelle`. Die eingerichteten Sender bleiben nutzbar.

### Niedrige Punkte

1. **Bildschirmfotos:** Das eigene mpv-Fenster für Erwachsene bekommt `--screenshot-directory=<Bilder>\Trendo`; der Ordner wird beim Start angelegt.
2. **Multicast:** `TargetAllowed` erlaubt zusätzlich `udp://` und `rtp://` (IPTV-Multicast).
3. **Aufräumen:** `MpvSafe.CleanupStale()` läuft beim Start. Es löscht `%TEMP%\trendo-mpv-<PID>` nur, wenn alle drei Bedingungen zutreffen:
   - Der Name passt genau.
   - Es läuft kein Prozess mit dieser PID. Bei unklarem Stand wird nichts angefasst.
   - Der Ordner enthält nur Trendos eigene Dateien (`trendo-leer-/trendo-tasten-*.conf`, Ordner `leer`).
   Fremde Ordner bleiben; ein Test belegt das.
4. **Echter Lauf mit der mitgelieferten mpv-Fassung** v0.41.0-1227-ge471496c5 (shinchiro 20261021), Protokoll `reports/T-0764/mpv-pruefung-fix10.log`. Es lief nur der eigene Prozess, ohne Fenster, Bild und Ton.
   - `mpv --no-config --ytdl=no --version` wird angenommen (Exitcode 0).
   - Bei einem Lauf mit `--vo=null --ao=null --length=0.3 -- av://lavfi:sine` und gesperrter Tastendatei (`FileShare.Read`) liest mpv die Datei („parsed: 2 binds“) und nimmt „--“ an. Es lädt keinen `ytdl_hook` und endet sofort (Exitcode 0).
   - Weiter geladen werden mpvs eingebaute Skripte (osc, console, select, commands, stats, auto_profiles, positioning). Sie sind nur über Tastenbindungen oder IPC erreichbar: Die Standardbindungen sind aus, Trendos Belegung bindet nichts davon, und die IPC-Leitung nutzt nur Trendo.

### Angepasster Test

Fix7Ui `Kind_Archiv` legt die ZIP-Dateien jetzt im eigenen Ordner des Kindes ab. Der Trendo-Datenordner ist für Kinder geschützt. Der Test ist nicht abgeschwächt.

### Tests

- Build: 0 Fehler.
- Core VOLLSTÄNDIG (`gruen-fix10-core.log`), ohne Filter und ohne parallele Testsammlungen: 3610 Tests, davon 3568 grün und 42 übersprungen, Exitcode 0.
- App gezielt (`gruen-fix10-app.log`): 226 Klassen in 5 Prozessen plus EveryButton_Alltag, zusammen 1021 Tests.
- Teil 01 bis 04 und EveryButton liefen mit Exitcode 0.
- Teil 00 hatte keinen roten Test, endete aber mit Exitcode 1. Grund ist der bekannte Aufräumfehler des Avalonia-Testrahmens (`V-16 Adressbuch`, „different thread owns it“).
- Die Wiederholung von Teil 00 war mit allen 228 Tests grün, Exitcode 0.

### Grenzen und offene Punkte

- **Werkzeugordner:** Werkzeuge darin werden weiter nicht vor jedem Start gehasht. Kinder können dort aber über Trendo nichts mehr ablegen.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze. Andere Programme dieses Kontos können weiter schreiben.

## Elfte Prüfrunde (Commit „T-0764 fix11“)

Grundlage ist die Gegenprüfung von `7911e2d4e`.

**Befund:** Ein Kind konnte im Trendo-Dateimanager eine setup.exe, eine .lnk oder eine .url nach `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` kopieren. Windows startet sie bei der nächsten Anmeldung.

**Grund für den Wechsel zur Freigabeliste:** Die Sperrliste der Trendo-Ordner (10. Prüfrunde) schützte nur Trendos eigene Orte. Windows hat aber viele Orte, an denen eine Datei etwas auslöst: Autostart, Startmenü, SendTo, AppData, ProgramData, Vorlagen und mehr. Jede Prüfrunde fand einen weiteren. Eine Sperrliste wird hier nie vollständig. Darum gilt jetzt das Umgekehrte: Kinder schreiben nur in ausdrücklich freigegebene Orte, und sie legen nirgends Dateien an, die etwas starten oder auslösen.

**Rot auf 7911e2d4e:**
- `reports/T-0764/rot-fix11-core.log`: 9 von 9 rot.
  - Die Kind-Schreibprüfung des alten Stands ließ Autostart (setup.exe, .lnk, .url), AppData, Startmenü, SendTo, ProgramData und Temp zu.
  - Im Dateimanager gingen das Umbenennen in .exe und das Kopieren einer .lnk durch.
  - Diese Rot-Datei gab es nur für den Beleg; sie ist nicht eingecheckt.
- `reports/T-0764/rot-fix11-app.log`: 2 von 2 rot. Der Dateimanager kopierte setup.exe und .lnk und benannte in .exe um, „Dieser PC“ war erreichbar, und der Download einer setup.exe kam in die Warteschlange.

### M1 – Freigabeliste (`ChildWrite` im Kern)

**Freigegeben sind:**
- Dokumente, Bilder, Musik, Videos, Desktop (`Environment.SpecialFolder`) und Downloads (FOLDERID_Downloads) des Kontos, jeweils aufgelöst. Bei OneDrive-Umleitung zählt das Ziel.
- `DataRoot\projekte`.
- Im Testmodus der Testordner.
- Der eigene Trendo-Ordner des Kindes (`Files.Home`) nur, wenn er selbst kein Systemort ist. Im normalen Betrieb ist das der ganze Benutzerordner mit AppData; der wird nie Freigabe.

**Regeln:**
- Eine Wurzel darf keine Laufwerks- oder Profilwurzel sein und keinen Systemort enthalten oder in einem liegen. Systemorte sind AppData (Roaming, Local, LocalLow), Autostart, Startmenü, SendTo, ProgramData, Programme, Windows, Temp und Vorlagen.
- Die einzige Ausnahme ist `AllowInside` für Trendos „projekte“ unter %APPDATA%: Sie darf in einem Systemort liegen, aber keinen enthalten.
- Alles andere ist für Kinder schreibgeschützt, fail-closed: andere Benutzer, Laufwerkswurzeln und Wechseldatenträger eingeschlossen.
- Pfad und aufgelöstes Ziel müssen beide in der Freigabe liegen. Eine Junction oder ein Symlink mit Ziel draußen gilt als draußen.
- `ProtectedFolders` (Trendo-Ordner) wirkt zusätzlich.

**Wo die Regel greift:**
- **Dateimanager:** `FileBrowser.WriteGuard` beim Anlegen (Ordner samt neuem Namen), beim Kopieren und Verschieben (Ziel samt Namen, bei Ordnern jede enthaltene Datei), beim Umbenennen (neuer Name), Löschen, Papierkorb, Zurücklegen und Rückgängig/Wiederholen.
- **Entpacken:** `Archives.UnpackAs` prüft das Ziel.
- **Kinder-Auswahl beim Speichern und Öffnen:** `KidPickRoots`/`KidPickAllowed`.
- **Download-Manager.**

### M2 – keine ausführbaren oder auslösenden Dateien

`ChildWrite.BlockedName` sperrt:
- die Endungen aus `ProgramTrust.CodeExtensions` und Installer (`IsInstallerLike`),
- dazu .lnk .url .scf .library-ms .settingcontent-ms .appref-ms .hta .chm .msc .cpl .jar .reg .ps1 .psm1 .vbs .vbe .js .jse .wsf .wsh .bat .cmd .com .pif .scr .msi .msp .appx .msix .xll .application .inf,
- Office-Dateien mit Makros und weitere Auslöser.

Geprüft wird jede Teilendung (Doppelendung wie „bild.exe.png“) nach dem Kürzen von Punkten und Leerzeichen am Ende. Gesperrt sind auch Datenströme („:“) und Gerätenamen.

Die Regel gilt beim Kopieren, Verschieben, Umbenennen („a.txt“ → „a.exe“), Speichern (Kinder-Auswahl), Entpacken (ZIP-Einträge, jetzt auch .lnk/.url/.scf …) und Herunterladen. Lesen und Öffnen im eigenen Betrachter bleiben wie bisher; ShellExecute bleibt ohnehin gesperrt.

### M3 – Downloads, die ein Kind auslöst

- **Download-Manager:** Ziel-Ort und Name werden vor dem Einreihen geprüft. Eine gesperrte Endung bricht mit verständlicher Meldung ab („Abgebrochen: … legt nur ein Erwachsener an“).
- **Medien-Bibliothek:** Das Dateiformat aus den Quelldaten wird vorher geprüft.
- **3D-Bibliothek:** Ausführbare Dateien in einem Kinder-Download werden sofort entfernt.
- **Studio- und KI-Ausgaben:** Sie schreibt Trendo selbst mit festen Endungen (Bild, Ton, Video, 3D) in Trendos Ordner.

### M4 – Dateimanager im Kinderprofil

Er zeigt nur die freigegebenen Wurzeln. „Dieser PC“, nicht freigegebene Ordner und „nach oben“ über die Wurzeln hinaus führen zurück in die Freigabe. AppData erscheint auch mit „Ausgeblendete Elemente“ nicht, weil es außerhalb liegt. `List` läuft weiter über die Schreibprüfung, ohne Namensprüfung; der Kommentar ist angepasst.

### Niedrige Punkte

- **5) Arbeitsordner:** `MpvSafe.WorkDir()` legt je Start einen frischen Ordner `leer-<GUID>` an. Ist der mpv-Ordner oder der neue Ordner ein Umleitungspunkt, bricht der Start ab. Entfernt wird der Ordner beim Ende des Players (`HoldUntilExit(…, workDir)`). `MpvSafe.SafeDelete` entfernt Junctions und Symlinks selbst und folgt nie ihrem Ziel. Das gilt auch für `CleanupStale`.
- **6) Steuerleitung:** Der Name der Fernseh-Steuerleitung ist jetzt zufällig (`trendo-tv-<Slot>-<GUID>`). Der Ton-Player war es schon.
- **7) Testcode:** In `RechteKernFix7Ui.Kind_Archiv` steht `Directory.CreateDirectory(…)` wieder als Code.

### Was weiter geht (geprüft)

Kinder speichern Bilder, Musik und Videos und exportieren Projekte in die Freigabe. Sie laden erlaubte Dateien herunter (Bild in die Warteschlange) und entpacken ZIPs ohne Code dort. Für Erwachsene ist alles unverändert; das prüfen die Tests mit.

### Angepasste Tests

Grund sind die strengeren Regeln; abgeschwächt ist keiner:
- **Fix7 `Archive_Kind`, Fix8 `Entpacken_Namen`/`Entpacken_Grenzen`, Fix10 `Umweg_und_Entpacken`:** Der Testordner wird als freigegebener Ort angemeldet. Sonst würde schon der Ort ablehnen und die eigentliche Regel bliebe ungeprüft.
- **Fix10 `Arbeitsordner_leer`:** jetzt je Start ein frischer Ordner.
- **Fix10 `Dateimanager_Kind`:** neue Signatur der Prüfung.

### Tests

- Build: 0 Fehler.
- Core VOLLSTÄNDIG mit dem Endstand (`gruen-fix11-core.log`), ohne Filter und ohne parallele Testsammlungen: 3671 Tests, davon 3629 grün und 42 übersprungen, Exitcode 0. Ein erster vollständiger Lauf vor dem Zwischenspeichern der Freigabe-Wurzeln hatte dasselbe Ergebnis.
- App gezielt (`gruen-fix11-app.log`): 257 Klassen in 6 Prozessen plus EveryButton_Alltag, zusammen 1079 Tests.
- Teil 01, 02, 04, 05 und EveryButton liefen mit Exitcode 0.
- Teil 00: kein roter Test, aber Exitcode 1 wegen des bekannten Avalonia-Aufräumfehlers.
- Teil 03: ein roter Test, `RechteKernFix4Ui.Ordner_im_Dateimanager`. Er öffnete beim Kind einen Ordner außerhalb der Freigabe, den der Dateimanager Kindern jetzt bewusst nicht mehr zeigt. Der Test nutzt jetzt einen Ordner der Freigabe und ist nicht abgeschwächt.
- Teil 03 brauchte im Hauptlauf auffällig lange (1968 s), und die erste Wiederholung lief ins Zeitlimit. Einzeln waren alle 50 Klassen grün und schnell. Die Freigabe-Wurzeln werden jetzt zwischengespeichert, die Systemorte nur einmal aufgelöst.
- Wiederholung von Teil 00 und 03 mit dem Endstand: 432 Tests in 348 s, 0 rot, Exitcode 0.

### Grenzen und offene Punkte

- **Eingebettete Webansichten:** Downloads in Claude-Anmeldung und Vorschau (`NativeWebView`/WebView2) steuert WebView2 selbst. Trendo kann sie dort nicht abfangen.
- **Gemeinsames Konto:** Unter einem gemeinsamen Windows-Konto ist der Kinderschutz keine harte Grenze. Andere Programme dieses Kontos können weiter überall schreiben, wo das Konto darf.

## Abnahmekriterien

| # | Kriterium | Urteil |
|---|---|---|
| 1 | Aus leerer Vorlage echtes Ergebnis | nicht anwendbar (Verwaltung, kein Dokument-Studio) |
| 2 | Fremddatei importieren/exportieren | nicht anwendbar |
| 3 | Prozessabbruch ≤ 30 s | TEILWEISE: Entfernen ist ein einziges Umbenennen mit danach geschriebener Beschreibung (`AtomicFile`). Bricht der Prozess genau dazwischen ab, liegt der Ordner ohne Beschreibung in `.entfernt` und erscheint nicht in der Liste; nichts geht verloren. |
| 4 | Claude/KI-Fachaufgabe | nicht Teil des Pakets |
| 5 | Test vom Klick bis zur echten Datei | VOLLWERTIG für Rechte und Programm-Seite (echte Ordner, echte ZIP). Geräte: TEILWEISE (Laufwerke/Auswurf ersetzt, echter Auswurf NICHT GETESTET) |
| 6 | Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen | FEHLT für die neue Programm-Seite (nur Knöpfe) |

## Testergebnisse

Belege liegen in `reports/T-0764/` mit `SHA256SUMS.txt`.

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- **Rot** (alter Stand, nur der neue App-Test): `RechteKernUi` 5 Tests, 4 fehlgeschlagen, 1 bestanden (Gegenprobe Erwachsene). Log: `rot-app-rechtekernui.log`.
- **Core gezielt**, 27 Testklassen: 152 Tests, 151 bestanden, 1 übersprungen, 0 Fehler. Log: `gruen-core-gezielt.log`.
  - Neu: RechteKern 8, SYS11_TrendoProgramme 5, SYS07_Auswerfen 6.
  - Berührt: Dependencies_Core, FreigabenKern, GitHubApps/Installer, KiLibrary, LetterQa, PluginHost, QA004_ApprovalGate, Rbac_Core, SYS04_AppCenter, SYS07_Devices, SourceApps, Sys11_Programme, T0222_Vollpaket, T0755_*, WebApps, X02, Wave0, ToolCheck, Paket44_Fachprogramme, PciIds, NoFixedUserTests, CORE11.
- **Core Nachlauf** T0755_Gegenpruefung + T0755_Gefahren (Datei durch die Signaturanpassung berührt): 10 Tests, 0 Fehler. Log: `gruen-core-t0755-gegenpruefung.log`.
- **App gezielt**, 57 Testklassen aller berührten Seiten (u. a. FreigabenKernUi, EveryButton_Alltag, Sys11_Programme_Ui, SYS04/06/07_Ui, StudioDepsUi, ComponentsUi, KiLibrary, GitHub*, SourceApps_Ui, LiveStudio, W1_Letter, X05_Ui, AudioPluginHost_Ui): 169 Tests, 0 Fehler, 3 übersprungen (Echtläufe mit Schalter). Log: `gruen-app-gezielt.log`.
- **Nachlauf** nach der letzten Änderung (Fenstertitel „Programm“): ProgrammSeiteUi, StartMenu_Ui, RechteKernUi, SYS07_Auswerfen_Ui, FreigabenKernUi: 16 Tests, 0 Fehler. Log: `gruen-app-nachlauf.log`.
- **Bilder** (angesehen, nicht im Repo, Pfade mit Benutzernamen): `prog-pflege`, `prog-programm`, `geraete`. Dunkel-Gold, je eine goldene Hauptaktion auf den neuen Flächen. Auf „Pflegen“ gab es schon vorher mehrere goldene Knöpfe; das ist nicht Teil dieses Pakets.

**Nachbesserung (fix1):**
- Build: 0 Fehler.
- **Rot** auf `fbbed41b9`: `rot-fix1-app.log` (7 von 7 rot) und `rot-fix1-cli-kinderprofil.log` (Kommandozeile im Kinderprofil: Paket eingerichtet, Rückgabewert 0). M4 und N2 haben keinen Verhaltens-Rotbeleg: Ihre Tests brauchen neue Schnittstellen (Zuordnung, geprüfte Sicherung) und kompilieren auf dem alten Stand nicht.
- **Core gezielt** (RechteKernFix1, RechteKern, SYS11_TrendoProgramme, SYS07_Auswerfen, TrendoMcpTests, GitHubApps/Installer, Sys11_Programme, Dependencies_Core, FreigabenKern, T0222_Vollpaket, SYS07_Devices): 121 von 121 grün. Log: `gruen-fix1-core.log`.
- **App gezielt**, 117 Testklassen: alle Klassen der berührten Seiten, nämlich RechteKernFix1Ui (8), RechteKernUi, ProgrammSeiteUi, SYS07*, FreigabenKernUi, KiSourceSetupUiTests, Claude*, ClaudeBridge_Ui, Connections_Ui, N8nWorkspace_Ui, WebAxe_Ui, W0_SetupWizard, AppsAndSetupResumeTests, EveryButton_Alltag, GitHub*, KiLibrary, X05, StudiosBueroGeld_Ui, dazu alle Klassen mit Dateimanager-Bezug. Ausgenommen sind nur Browser- und Echtlauf-Klassen. Ergebnis: 550 Tests, 0 Fehler, 1 übersprungen. Log: `gruen-fix1-app.log`.

## Was NICHT geprüft wurde
- Kein Volltest.
- Echter Auswurf eines USB-Sticks (kein eigener Test-Stick vorhanden).
- Der 2-s-Timer im laufenden Fenster. Getestet ist die Nachseh-Logik (`DevicesPollNow`); im Testmodus läuft kein Timer.
- Echte Installationen (winget, GitHub-Download). Die Rechteprüfung liegt vor jedem Netz- oder Prozessaufruf und ist ohne Ersatz geprüft; die Installationswege selbst sind unverändert.
- Narrator-Bedienung und Update-/Rückkehr-Lauf (SYS-12, nicht Teil dieses Pakets).

## Offene Punkte
1. **SYS-04 + SYS-11 zusammenführen** (Audit Nr. 18): nicht gemacht, zu groß für dieses Paket. Jetzt gibt es nur gegenseitige Verweise (Knopf „Programme verwalten (winget)“, Programm-Seite nur für Trendo-eigene Programme). Vorschlag: winget-Programme im App-Center bekommen dieselbe Programm-Seite.
2. **Weitere `Principal.LocalOwner`-Stellen** in der App (665, vorher 680). Das sind überwiegend Protokoll-Akteure und das Starten von Programmen/Dateien über `LaunchDirect`. `LaunchDirect` verlangt `ApproveAction`; mit `CurrentPrincipal` könnte ein Kind gar nichts mehr öffnen. Vorschlag: ein eigenes Recht „Programme starten“ (Kind ja) statt `ApproveAction`, dann umstellen.
3. Ollama-Modelle entfernen und laden laufen über `OllamaBackend` in der App. Die Prüfung liegt dort (`DarfSoftware`), Laden über das Tor. Vorschlag: das Backend in den Kern ziehen.
4. Die Programm-Seite hängt nicht in der Einstellungen-Hülle (wie „Starten“/„Prüfen“ von SYS-11 auch). Tastenkürzel und Rechtsklick fehlen.
5. Hub „Meine Installationen“ verlinkt noch nicht auf die Programm-Seite.
6. `CapabilityCatalog`-Texte (Audit Nr. 11–13) sind nicht angefasst.
