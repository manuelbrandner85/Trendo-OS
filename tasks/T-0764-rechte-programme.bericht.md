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
