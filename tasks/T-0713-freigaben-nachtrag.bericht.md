# Bericht T-0713: Freigaben-Nachtrag (offene Punkte aus B4)

**Branch:** `claude/T-0713-freigaben-nachtrag` (vom Integrationszweig `claude/T-0685-b1-integration`, eigene Kopie in `%TEMP%\trendo-auftrag-T-0713`).
**Grundlage:** `tasks/T-0706-freigaben-kern.bericht.md`, Abschnitt „Offene Punkte“ 2–5.

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| 1 Sammel-Freigabe | Ersteinrichtung (`Setup.cs RunSetupInstall`), „alle fehlenden installieren“ (`axaml.cs InstallAll`) und „Reparieren“ (`Setup.cs MakeFix`) installierten über die Lizenz-Zustimmung mit `_deps.InstallAsync` – ohne Tor, ohne Rückfrage je Komponente | Eine Sammel-Rückfrage (`MainWindow.Freigabe.cs SammelDialog`): je fehlender Komponente ein vorbelegter Haken mit Wofür, Wirkung (nur für dich / Administratorrechte / lokale KI), Lizenz, Größe, Herkunft; „Abbrechen“ vorbelegt, Esc lehnt ab, ohne Haken ist „einrichten“ gesperrt. Je gewählter Komponente genau eine Anfrage + Freigabe der angemeldeten Person (`SammelFreigabeAsync`), eingelöst nur über `ApprovalGate.Execute` / `DependencyManager.InstallApprovedAsync` (`InstallFreigegebenAsync`). Nicht eingelöste (Abbruch, Fehler, Not-Stopp) werden mit dem neuen `ApprovalGate.Discard` verworfen. `InstallOne` verlangt jetzt immer eine Freigabe-Id; der Weg ohne Tor ist weg. Kinderprofil: keine Rückfrage, keine Anfrage, Hinweis „Frag einen Erwachsenen“, Audit `freigabe.nicht.erlaubt` | Core `T0713FreigabenNachtrag.Sammel_genau_einmal_je_Komponente`; UI (echter Dialog) `T0713FreigabenNachtragUi.Alle_fehlenden_mit_Sammel_Dialog`, `Reparieren_mit_Sammel_Dialog`, `Kinderprofil_sammelt_nicht`; Architekturtest `FreigabenKernUi.Keine_Selbstfreigabe_im_Code` **verschärft** (keine Ausnahme mehr für `_deps.InstallAsync(` und `InstallOne(` ohne Freigabe) |
| 2 Ablauf | `Decide` prüfte das Ablaufdatum nicht (nur `Execute`) | `Approve` einer abgelaufenen Anfrage → `ApprovalException` + `approval.expired`; `Deny` einer abgelaufenen Anfrage markiert sie still als abgelaufen (die Person wollte ohnehin nichts); `Execute` schreibt zusätzlich `approval.expired`; `Pending()` bietet Abgelaufenes nicht mehr an. `StudioWorkflow.Approve` setzt den Auftrag bei `ApprovalException` auf „fehlgeschlagen“ statt ihn ewig warten zu lassen (bestehender Test `PromptActionTests.Freigabe_laeuft_ab` verlangt das) | `Abgelaufen_wird_nicht_freigegeben`, `Erteilt_aber_abgelaufen`, bestehend `Tests.Expired_blocks`, `PromptActionTests.Freigabe_laeuft_ab` |
| 3 Persistenz | Freigaben nur im Arbeitsspeicher; ein gespeicherter KI-Auftrag in „wartet auf Freigabe“ ließ sich nach einem Neustart nie mehr freigeben („Keine offene Freigabe“) | **Ja, aber nur für fortsetzbare Anfragen** (`Resumable`): KI-Auftrag (`prompt:*`, der Auftrag liegt gespeichert vor) und die eigenen Integrationen „URL öffnen“ / „Projektordner öffnen“ (Ziel steht in der Anfrage). Alle anderen (Installieren, Ablauf, Agenten, Teilen …) hängen an einer laufenden Rückfrage im Fenster und werden bewusst **nicht** gespeichert – nach einem Neustart fragt die Seite neu. Gespeichert wird atomar (`AtomicFile`) nach `<Datenordner>/freigaben/offen.json`, nur offene, nie erteilte; Datei verschwindet, wenn nichts offen ist. Beim Laden: abgelaufen → verworfen; übernommen wird nur, was das Prüfprotokoll (Hash-Kette intakt) genau so als angefragt und noch nicht entschieden kennt, nur erlaubte Aktionen, Laufzeit höchstens 10 Minuten ab Protokolleintrag, anfragende Person und Zeitpunkt aus dem Protokoll, Beschreibung neu gebildet (keine Texte/Vorschau aus der Datei); vor Freigabe und Ausführung wird bei fortsetzbaren Anfragen erneut im Protokoll nachgesehen (zweites Fenster mit demselben Datenordner). `Integrations.Execute` prüft das Ziel vor dem Start erneut (nur https bzw. genau ein Projektordner) | `Offene_Anfrage_ueberlebt_Neustart`, `Neustart_nimmt_nur_Gepruftes`, `Faelschung_wird_abgefangen`, `Zwei_Fenster_einmalig` |
| 4 PIN-Hinweis | kein Hinweis, wenn neben einem Kinderprofil ein Erwachsenen-Profil ohne PIN besteht | `PersonStore.AdultsWithoutLockBesideChild()` (Kern) + Karte „Kinderschutz: eine PIN fehlt“ oben in den Einstellungen und auf der Personen-Seite, Knopf „PIN festlegen“ (nicht golden): eigenes Profil → Anmeldung einrichten; anderes Profil → Personen-Seite mit Hinweis zum Wechseln. Nur Hinweis, nichts erzwungen; im Kinderprofil unsichtbar | Core `Pin_Hinweis`; UI `Pin_Hinweis_in_Einstellungen`; Bilder `reports/T-0713/einstellungen-pin-hinweis.png`, `personen-pin-hinweis.png` |
| 5 LocalOwner | Not-Stopp „Fortsetzen“, App-Center-Plan/Ablehnen, Heimnetz-Teilen, Projektmappe (Ordner/URL anfragen), KI-Auftrag-Kontext und -Abbruch liefen als `Principal.LocalOwner` | auf `CurrentPrincipal` umgestellt; „Nein“ geht über den neuen Baustein `Ablehnen(id)` (Erwachsene lehnen ab, Kinderprofil zieht die eigene Anfrage zurück). Ein Kinderprofil kann den Not-Stopp nicht mehr aufheben | `Kinderprofil_sammelt_nicht` (Not-Stopp), bestehend `NotStopp_Ui`, `QA004_UiApproval`, `SYS09_Ui`, `PromptBarTests` |

### Bewusst NICHT umgestellte `Principal.LocalOwner`-Stellen (Liste)
- **Programmstarts per Klick** (`Integrations.LaunchDirect/LaunchArgs`, rund 30 Stellen in `axaml.cs`, `Dateien.cs`, `Sys03.cs`, `Sys11.cs`, `W1*.cs`, `W4*.cs`, `TvLegal.cs`, `VideoExporte.cs`, `StudioMcp.cs`, `SourceApps.cs`): Der Kern verlangt dort `ApproveAction`; mit `CurrentPrincipal` könnte ein Kinderprofil keine Datei, keinen Player, keinen Papierkorb mehr öffnen (und die Klick-Handler würfen ungefangen). Laut Profil-Regel („Installieren nur durch Erwachsene“) dürfen Kinder Programme öffnen → Produktentscheidung nötig. Besonders zu prüfen: `SourceApps.cs:337` (startet `cmd.exe /c`), `Sys03.cs` (Tor-Browser).
- **Programm-Dateien (Auftrag: nicht anfassen, T-0710/T-0711):** `Core09Cam.cs:447/481/493` (Bewegungsfreigabe Request/Deny), `Live.cs:303/334` (Livestream Ask/Deny), `PdfF3.cs:83` und `W1Letter.cs:236` (Texterkennung laden), `W1Letter.cs:348/352` (`LetterGate.Approve`, `TrySendExternally`), `W4Web.cs:319` (Webseiten-Paket), `W5Fusion.cs:32/94`, `MainWindow.Programs.cs:44/68/341/417/418`. Vorschlag: Request → `CurrentPrincipal`, Deny → `Ablehnen(id)`.
- **Kern-Vorgaben ohne Person:** `AgentRuns.cs:436`, `Agents.cs:223`, `Workflows.cs:171` (Standard), `AppCenter.Plan` (Standard, jetzt mit optionaler Person) – die App reicht jetzt die Person durch, wo sie umgestellt ist.
- **Kommandozeile** `Program.cs:86/92/95` (`--install-deps --accept-licenses`, ausdrücklich, ohne Fenster).
- Der Rest (rund 600 Vorkommen) ist `Principal.LocalOwner.Name` als Protokoll-Akteur ohne Rechteprüfung.

## Sicherheitsprüfung (Unteragent, Rolle 03-security-permissions, nur lesen)
Befunde und Behebung:
1. MITTEL – geladene Anfrage mit beliebiger Aktion/Ziel könnte über die Freigaben-Seite eine Datei starten → **behoben**: nur erlaubte Aktionen werden geladen; `Integrations.IsValidTarget` prüft vor jedem Start erneut (Test `Faelschung_wird_abgefangen`).
2. MITTEL – Laufzeit, Texte, anfragende Person aus der Datei → **behoben**: nicht mehr gespeichert bzw. aus dem Protokoll, Laufzeit ≤ 10 Min. ab Protokolleintrag.
3. MITTEL/NIEDRIG – kaputtes Protokoll ließ das Laden bzw. die Prüfung werfen → **behoben** (Laden fängt ab und übernimmt nichts; Prüfung gilt dann als „abgeschlossen“).
4. NIEDRIG/MITTEL – Abgleich mit anderen Fenstern nur für geladene Anfragen → **behoben** für alle fortsetzbaren Anfragen. Rest-Risiko: kein prozessübergreifendes Sperren; zwei Fenster überschreiben gegenseitig `offen.json` (betrifft nur den Neustart-Schutz, nie eine doppelte Ausführung – die prüft das Protokoll).
5. NIEDRIG – fehlender Audit-Eintrag → **behoben** (`approval.closed.elsewhere`).
6. NIEDRIG – Anfrage blieb nach Not-Stopp in der Sammel-Freigabe offen → **behoben**.
7. NIEDRIG – Verwerfen nicht in `finally`, kein Abbruch bei Not-Stopp → **behoben** (Ersteinrichtung und „alle fehlenden“).
8. NIEDRIG – LocalOwner in Programm-Dateien → **offen** (Programm-Dateien laut Auftrag nicht anfassen, siehe Liste).
9. NIEDRIG (Datenschutz) – Vorschau in `offen.json` → **behoben** (wird nicht mehr gespeichert; Ziel steht wie bisher auch im Protokoll).
Nicht behoben (außerhalb des Auftrags): Die Audit-Hash-Kette ist ohne Schlüssel – wer den Datenordner beschreiben kann, kann Einträge anhängen. Vorschlag: HMAC-Schlüssel aus dem SecretStore/DPAPI.

## Angepasste bestehende Tests (nicht geschwächt)
- `FreigabenKernUi.Keine_Selbstfreigabe_im_Code`: Ausnahmen für den Installationsweg ohne Freigabe gestrichen – strenger.
- `W0_SetupWizard.Wizard_flow`: drückt jetzt im echten Sammel-Dialog „einrichten“ (vorher gab es keine Rückfrage) – prüft zusätzlich, dass der Dialog erscheint.

## Rot-Grün
Die neuen Tests nutzen `ApprovalGate.Discard`, `ApprovalGate.MaxResumableTtl`, `Integrations.IsValidTarget`, `PersonStore.AdultsWithoutLockBesideChild`, `MainWindow.SammelConfirm/LastSammel`, `sammel-*`, `pin-hinweis*` – auf dem alten Stand nicht übersetzbar bzw. nicht vorhanden, damit rot. `PromptActionTests.Freigabe_laeuft_ab` war mit der Zwischenfassung rot (Auftrag blieb wartend) und ist mit der Korrektur in `StudioWorkflow.Approve` grün.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (T0713, FreigabenKern, QA0*, PromptAction, AgentRun, W3, Workflow, Profile/Person, Integration, Dependenc, Approval, NoFixedUser …): **227 bestanden, 1 Fehler, 1 übersprungen**. Der Fehler ist `QA019_Offline.Core_works_offline` („System.Net.Http“ im Kern) – **schon auf dem Integrationszweig vorhanden** (`DevBuild.cs`, `Kie/Kie.cs` nutzen `HttpClient`, von T-0713 nicht berührt). Rohlog `reports/T-0713/core-tests.log`.
- App, 26 Klassen (u. a. `T0713FreigabenNachtragUi`, `FreigabenKernUi`, `W0_SetupWizard`, `SetupFeedbackTests`, `ProposalsUiTests`, `ComponentsUi`, `StudioDepsUi`, `InstallFeedbackUiTests`, `AppsAndSetupResumeTests`, `NotStopp_Ui`, `QA004_UiApproval`, `W0b_Look`, `W0b_Hintergrund_Farbe_Ui`, `X02_Ui`, `SYS09_Ui`, `PromptBarTests`): **121 von 122 bestanden**; `T0681_Ui.CodeEditor_laeuft_im_Hintergrund_und_bricht_ab` fiel nur im großen Lauf (Zeitverhalten des Kindprozesses), einzeln zweimal grün (6/6). Rohlog `reports/T-0713/app-tests.log`.
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests OK**, `reports/T-0713/baseline.log`.
- Bilder (Render 1600×1000 mit `TRENDO_PIN_HINT=1`): Einstellungen und Personen-Seite. Beim Rendern liegt der Sperrbildschirm darüber (mehrere Personen → Sperre beim Start); die Karte ist dahinter oben sichtbar, keine goldene zweite Hauptaktion. Prüfsummen `reports/T-0713/SHA256SUMS.txt`.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien (für dieses Querschnittspaket)
1. Leere Vorlage → echtes Ergebnis: **NICHT GETESTET** (kein Fachprogramm in diesem Paket).
2. Fremddatei importieren/exportieren: **NICHT GETESTET** (nicht Gegenstand).
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – offene, fortsetzbare Freigaben überleben einen Neustart (atomar, Test mit neuem Kern auf demselben Datenordner); kein harter Abbruch des App-Prozesses getestet.
4. Claude/KI führt Fachaufgabe aus: **NICHT GETESTET** (nicht Gegenstand; KI-Auftrag ist nur fortsetzbar geworden).
5. Test vom Klick bis zur echten Datei: **TEILWEISE** – vom Klick über den echten Dialog bis zur Ausführung im Tor (Testmodus: es wird nichts installiert; beim „Reparieren“-Test ist das Werkzeug durch `InstallOverride` ersetzt, das Tor echt). Eine echte Installation wurde nicht ausgeführt (würde Software auf den PC laden).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Dialog per Tastatur (Esc = Abbrechen, Abbrechen vorbelegt); sonst nicht Gegenstand.

## Was NICHT geprüft wurde
- Keine echte Installation (kein winget/Download) – nur Testmodus bzw. ersetzter Runner.
- Zwei echte App-Prozesse gleichzeitig (nur zwei Kerne im selben Testprozess).
- Darstellung des Sammel-Dialogs als Bild (nur per UI-Test geprüft).
- Kein Volltest.

## Offene Punkte
- `Principal.LocalOwner` in Programm-Dateien und bei Programmstarts (Liste oben) – Produktentscheidung: dürfen Kinderprofile Programme starten?
- Audit-Kette mit Schlüssel (HMAC) absichern; prozessübergreifende Sperre für `offen.json`.
- Claude-Brücke (offener Punkt 1 aus T-0706) war nicht Teil dieses Auftrags.

## Berührte Dateien
- Kern: `src/Trendo.Core/ApprovalGate.cs`, `Integrations.cs`, `Profile.cs`, `TrendoCore.cs`, `W3/Packages.cs`, `PromptAction/StudioWorkflow.cs`
- App: `src/Trendo.App/MainWindow.Freigabe.cs`, `MainWindow.Setup.cs`, `MainWindow.axaml.cs`, `MainWindow.Lock.cs`, `MainWindow.Look.cs`, `MainWindow.NotStopp.cs`, `MainWindow.AppCenter.cs`, `MainWindow.Share.cs`, `MainWindow.Projektmappe.cs`, `MainWindow.PromptAction.cs`
- Werkzeug: `tools/Trendo.Render/Program.cs` (Schalter `TRENDO_PIN_HINT`)
- Tests: neu `tests/Trendo.Core.Tests/T0713FreigabenNachtragTests.cs`, `tests/Trendo.App.Tests/T0713FreigabenNachtragUiTests.cs`; geändert `tests/Trendo.App.Tests/FreigabenKernUiTests.cs`, `tests/Trendo.App.Tests/UiTests.cs`
- Belege `reports/T-0713/*`, Auftrag und Bericht `tasks/T-0713-*`
- Nicht angefasst: `MainWindow.ProgramShell.cs`, Programm-Dateien.
