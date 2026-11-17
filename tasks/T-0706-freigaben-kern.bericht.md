# Bericht T-0706: Freigaben in den Kern (Paket B4)

**Branch:** `claude/T-0706-freigaben-kern` (Basis `claude/T-0685-b1-integration`, e398cd1)
**Status:** umgesetzt, gezielte Tests grün (Ausnahmen siehe unten), kein Volltest (macht die Hauptsitzung)

## Was vorher war (Beleg)
- Audit `docs/audit/2026-11-16-profi-audit/audit-5-entwickeln-system.md` „Rechte und Freigaben“ 1–2 und `audit-6-architektur.md` Zeile „Durchsetzung in der Desktop-App“: 28 Stellen `Approvals.Approve(…, Principal.LocalOwner)` in `src/Trendo.App`, meist direkt nach `Request` im selben Klick; die App arbeitet immer als Besitzer, Rollen wirken nicht.
- Zusätzlich gefunden: Die Seite „Freigaben“ und die Handy-API (`/api/approvals/{id}/approve`) führten **jede** offene Anfrage über `Integrations.Execute` aus, also `launcher.Open(r.Target)`. Ein offenes `deps.install ffmpeg` hätte dort „ffmpeg“ als Startziel geöffnet. Das ist geschlossen.

## Was jetzt ist
Neuer Baustein `src/Trendo.App/MainWindow.Freigabe.cs`, kein zweiter Broker. Er nutzt `ApprovalGate`, `Rbac` und `AuditLog`:
- `CurrentPrincipal = Principal.ForProfile(_settings.Child)`. Erwachsene sind `lokaler-besitzer` (Owner), das Kinderprofil ist `kinderprofil` (Rolle User: darf anfragen, nicht freigeben).
- `FreigabeAsync(Wirkung)` legt eine Anfrage an und öffnet einen eigenen Dialog. Der Dialog nennt die Wirkung, Lizenz, Größe und Herkunft und zeigt bei Abläufen den Probelauf als Vorschau. „Abbrechen“ ist vorbelegt, Esc und Schließen lehnen ab. Die Person entscheidet mit Ja oder Nein. Die Freigabe erteilt die angemeldete Person. Ein Kinderprofil wird gar nicht erst gefragt: Die Anfrage wird zurückgezogen und ins Protokoll geschrieben.
- `FreigabeAusRueckfrage(Wirkung)` ist nur für den Ja-Knopf einer schon sichtbaren Rückfrage-Karte (z. B. „X fehlt. Jetzt einrichten? [Ja] [Später]“). Die erlaubten Stellen stehen im Architekturtest.
- `FreigabeErteilen(id)` gilt für zweite, eigene Schritte („anfragen“, dann „ausführen“) und für die Seite „Freigaben“.
- `FreigabeAusEinstellung` ist die Dauerfreigabe, die die Person selbst als Einstellung setzt („Freie Modelle automatisch laden“). Sie gilt nur in Erwachsenen-Profilen.
- Ausgeführt wird nur über das Tor: `ApprovalGate.Execute` bzw. die Kern-Dienste `DependencyManager.InstallApprovedAsync`, `KiSourceSettings.EnableCloud`, `WorkflowEngine.ApplyApproval` und `FachprogrammAdapter.Aufrufen`.

Kern:
- `ApprovalGate.Withdraw` zieht eine Anfrage zurück. Das darf nur, wer angefragt hat, oder wer freigeben darf. Neues Feld `ApprovalRequest.RequestedBy`.
- `Principal.ForProfile`/`ChildProfile`.
- `DependencyManager.InstallApprovedAsync` und `InstallAction`.
- `KiSourceSettings.EnableCloud` und `CloudOnAction`.
- `Integrations.Handles` und `Integrations.Execute` führen nur noch eigene Aktionen aus (Ordner, https).
- `WorkflowEngine.RequestApproval(…, by)`.
- `LocalApi`: Fremde Aktionen gibt die API nicht frei (409). Sie werden am PC entschieden.

## Die 28 Stellen (`Approvals.Approve(` in `src/Trendo.App`, Basis e398cd1)
| # | Stelle (vorher) | Wirkung | Vorher | Nachher |
|---|---|---|---|---|
| 1 | AddHub.cs:64 | KI-Modell laden | Request+Approve im Klick | Antwort „Ja, installieren“ der Hub-Rückfrage → `FreigabeAusRueckfrage`, Laden in `Execute` |
| 2 | AgentRun.cs:80 | Agent-Schritt | eigene Rückfrage-Karte, Rolle fest Besitzer | wie vorher, Rolle `CurrentPrincipal` (`FreigabeErteilen`) |
| 3 | AppCenter.cs:77 | Programm installieren/aktualisieren/entfernen | Dialog (T-0681), Rolle fest | Dialog, `FreigabeErteilen` (Kind: abgelehnt) |
| 4 | axaml.cs:827 | Seite „Freigaben“ | Approve und `launcher.Open(Target)` für **jede** Anfrage | nur eigene Integrationen und Live: „Freigeben“; sonst Hinweis „wird auf der Seite entschieden, die gefragt hat“ und nur „Ablehnen“; Kind: Knopf gesperrt |
| 5 | axaml.cs:1272 | Zusatzprogramm für Studio | Request+Approve, Freigabe nie eingelöst | Rückfrage-Karte „Ja“ → `FreigabeAusRueckfrage` → `InstallOne(approvalId)` → `InstallApprovedAsync` |
| 6 | axaml.cs:1341 | Online-KI einschalten (Daten verlassen den PC) | Häkchen = Request+Approve, nie eingelöst | Dialog, dann `KiSourceSettings.EnableCloud` im Tor; Nein entfernt das Häkchen |
| 7 | ClaudeBridge.cs:401 | Claude bedient Programm | Chat-Rückfrage, Rolle fest | **unverändert** (Bereich B5/B6), siehe Übergabe |
| 8 | Core09Cam.cs:467 | Roboter bewegen (Live) | zweiter Schritt, Rolle fest | zweiter Schritt, `FreigabeErteilen` |
| 9 | KiLibrary.cs:153 | KI laden (Bibliothek) | Klick = Freigabe | Dialog; Ollama und Dateien/Laufzeit laufen in `Execute`; Not-Stopp hält zwischen den Dateien an |
| 10 | Live.cs:177 | FFmpeg installieren | Request+Approve, direkte Installation | Rückfrage-Karte „Ja, installieren“ → `InstallApprovedAsync` |
| 11 | Live.cs:320 | Live senden | zweiter Schritt, Rolle fest | zweiter Schritt, `FreigabeErteilen` |
| 12 | Phone.cs:247 | adb/scrcpy installieren | Request+Approve | Rückfrage-Karte → `InstallApprovedAsync` |
| 13 | Programs.cs:70 | Programm einrichten | Request+Approve, Freigabe nie eingelöst | Rückfrage-Karte → `InstallOne(approvalId)`; Test-Ersatz `InstallOverride` läuft ebenfalls im Tor |
| 14 | Programs.cs:134 | Fachprogramm starten („In … öffnen“, „Starten“) | Request+Approve im Klick | Dialog (Programm, Datei) → `FachprogrammAdapter.Aufrufen` im Tor |
| 15 | Programs.cs:280 | Fachprogramm „Jetzt öffnen“ | Request+Approve im Klick | wie 14 |
| 16 | Programs.cs:382 | Fachprogramm installieren | Request+Approve nach „Ja“ | Rückfrage-Karte → `FreigabeAusRueckfrage`, Installer im Tor |
| 17 | Runde2Tools.cs:22 | typst/mediamtx/streamlink | Klick = Freigabe | Dialog → `InstallApprovedAsync` |
| 18 | Share.cs:57 | Ordner im Heimnetz teilen | Dialog, Rolle fest | Dialog, `FreigabeErteilen` |
| 19 | Spec04Gltf.cs:50 | glTF-Validator | Klick = Freigabe | Dialog → `InstallApprovedAsync`; neu auch npm-Paket glTF-Transform über Dialog und Tor (vorher gar keine Freigabe) |
| 20 | Studio3DAssimp.cs:32 | assimp | Klick = Freigabe | Dialog → `InstallApprovedAsync` |
| 21 | Studio3DF3d.cs:32 | f3d | Klick = Freigabe | Dialog → `InstallApprovedAsync` |
| 22 | W4Web.cs:330 | Website-Paket | zwei Schritte, Rolle fest | zwei Schritte, `FreigabeErteilen` |
| 23 | W5Ext.cs:21 | Erweiterung installieren/entfernen | Request+Approve nach Rückfrage-Karte | Karte → `FreigabeAusRueckfrage` (Rolle geprüft) |
| 24 | W5Fusion.cs:40 | Ablauf über Studios | zwei Schritte, Rolle fest | zwei Schritte, `FreigabeErteilen` |
| 25 | Workflows.cs:115 | Ablauf darf Dateien verschieben | Probelauf+Request+Approve im selben Klick | Probelauf, dann Dialog mit Probelauf als Vorschau → `ApplyApproval` im Tor |
| 26 | X05.cs:39 | Freie Modelle automatisch laden | Approve als Besitzer | Dauerfreigabe durch die Einstellung (`FreigabeAusEinstellung`), nur Erwachsene; Kind lädt nichts |
| 27 | X05.cs:148 | KI-Modell laden (KI-Quellen) | Klick = Freigabe | Dialog → Laden im Tor |
| 28 | Program.cs:93 | Kommandozeile `--install --approve` | ausdrückliche Kommandozeilen-Freigabe | unverändert (bewusster Schritt der bedienenden Person) |

Weitere Installationswege, die die Sicherheitsprüfung gefunden hat, sind ebenfalls umgestellt. Sie lagen nicht unter den 28 Stellen:
- Komponenten-Seite „Installieren“, „Modell herunterladen“, Hub „Aktualisieren“, TV- und Musik-Player: Dialog und Tor.
- Tailscale und Telegram: Laden und Setup starten nur im Tor, nach Dialog bzw. Hub-Rückfrage.
- `InstallOne` arbeitet immer mit `CurrentPrincipal`. Ein Kinderprofil installiert damit nie, weil der Kern in `InstallAsync` ablehnt.

## Tests
- Core `FreigabenKern` (5 Tests):
  - Installieren, Online-KI und Ablauf: ohne, offene, falsche oder zweite Freigabe → `ApprovalException`, sonst genau einmal; Protokoll geprüft.
  - Kinderprofil darf nicht freigeben und keine fremde Anfrage zurückziehen.
  - `Integrations.Execute` öffnet fremde Ziele nicht.
- App `FreigabenKernUi` (5 Tests):
  - **echter Dialog**: Klick allein → nichts genehmigt oder ausgeführt; Abbrechen → `approval.denied`; „Einrichten“ → genau eine Ausführung, Freigabe verbraucht.
  - Kinderprofil aus `settings.json` → Rolle User, keine Rückfrage, Freigaben-Knopf gesperrt.
  - Freigaben-Seite führt fremde Wirkungen nicht aus.
  - Ablauf braucht ein Ja.
  - Architekturtest: kein `Approvals.Approve(` außerhalb des Bausteins, kein Umweg über eine Alias-Variable, `FreigabeAusRueckfrage`/`ExtApprove` nur an bekannten Karten, kein direktes `_deps.InstallAsync(`, `InstallOne` ohne Freigabe nur Ersteinrichtung/„alle fehlenden“, keine Anfrage mit Selbst-Freigabe innerhalb von 6 Zeilen (Ausnahme: X05-Dauerfreigabe).
- Testhilfe `Ui.Open` beantwortet die Rückfrage mit „Ja“ (`FreigabeConfirm`). Damit prüfen bestehende Tests weiter ihre Wirkung. Ablehnen, echter Dialog und Kinderprofil prüft `FreigabenKernUi`.
- Zahlen siehe Abschnitt „Testergebnisse“.

## Testergebnisse
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (gezielt: FreigabenKern, Fachprogramm, W2, LiveStream, Workflow, Approval, QA0, Api, Extension, PromptAction): 181 von 182 bestanden.
  - Rot ist nur QA-019 („keine Netz-Abhängigkeit im Core“). Das war schon auf der Basis so: `HttpClient` steckt bereits in `DevBuild.cs`/`Kie.cs`, und dieser Diff fügt keine Netz-Nutzung hinzu. QA-019 wird laut CLAUDE.md nie als bestanden markiert.
  - `FreigabenKern`: 5 von 5 bestanden.
- App: 59 berührte Testklassen (u. a. FreigabenKernUi, AssimpUi, F3d_Ui, GltfUi, Runde2ToolsUi, KiLibraryUiTests, KiModelT0703_Ui, SPEC15_LiveUi, PhoneEasyTests, Paket44_*, T0203_Programme_Ui, ComponentsUi, StudioDepsUi, QA004_UiApproval, SYS04/06_Ui, T0681_Ui, W5_X04_*, X05_Ui, CORE09_*, AgentRun_Ui, NotStopp_Ui, WebsiteStudioUiTests, W0_SetupWizard, W1_*).
  - 329 Tests, 327 bestanden, 1 übersprungen.
  - Der Architekturtest ist danach korrigiert: Fund in `InstallOne` selbst, also im bekannten offenen Weg. Die Nachprüfung `FreigabenKernUi` ergibt 5 von 5.
  - `CORE09_Ui.Image_check` ist rot, **auch auf der unveränderten Basis** (geprüft in einer Basis-Kopie: 1 von 2 rot).
  - `T0681_Ui` Code-Editor „Ausführen“ war im ersten Lauf einmal rot und im zweiten Lauf grün. Das ist zeitabhängig, diese Stelle ist nicht berührt.

## Was NICHT geprüft wurde
- Kein Volltest.
- Ein echter Installationslauf (winget/curl) mit dem neuen Weg wurde nicht ausgeführt. Die Tests ersetzen den Prozess-Runner bzw. laufen im Testmodus (`Isolated`). Der Kern-Weg `InstallApprovedAsync` → `InstallAsync` ist derselbe wie vorher.
- Der Dialog ist nicht im Bild geprüft (`Trendo.Render` rendert Seiten, keine Dialogfenster). Er nutzt dasselbe Muster wie die Rückfragen von App-Center und Teilen.

## Offene Punkte
1. **Claude-Brücke** (`MainWindow.ClaudeBridge.cs:401`, B5/B6): `BridgeConsent` gibt weiter als `Principal.LocalOwner` frei. Vorschlag: `FreigabeErteilen(request.Id)` statt `Approve`, bei `!CanApprove` sofort Nein. Danach die Datei aus `approveOk` im Architekturtest streichen.
2. **Ersteinrichtung und „alle fehlenden installieren“** (`Setup.cs:400ff`, `axaml.cs InstallAll`, „Reparieren“): Diese Wege laufen weiter über die Lizenz-Zustimmung und nicht je Komponente über das Tor. Die Rolle wird jetzt aber im Kern geprüft. Vorschlag: eine Sammel-Rückfrage mit Liste, je Komponente eine Freigabe.
3. **Fester Besitzer an weiteren Stellen**: Viele Protokoll- und Startaufrufe nutzen weiter `Principal.LocalOwner` (z. B. `Integrations.LaunchDirect`, `AppCenter.Plan` im Kern, `LetterGate.Approve` im Brief-Studio/T-0705, Löschen von KI-Modellen und Abläufen). Umgestellt sind alle Freigabe-Entscheidungen und alle Installationswege. Der Rest (rund 680 Vorkommen, meist Protokoll-Akteur) bleibt offen.
4. Freigaben liegen weiter nur im Arbeitsspeicher (Audit-6). `Decide` prüft das Ablaufdatum nicht (erst `Execute`). Ein Not-Stopp zwischen Ja und Ausführung wirft in einigen async-Handlern eine `ApprovalException`. Es passiert nichts, aber die Meldung ist roh (KI-Quellen, Hub).
5. Ein Erwachsenen-Profil ohne PIN neben einem Kinderprofil hebt die Rollentrennung auf („Wechseln“). Hinweis in der Personen-Seite fehlt.
6. W4Dev „Testen“ lief bereits seit T-0688 im Hintergrund (Code-Editor, Fortschritt, Abbrechen). Hier ist nichts geändert, Beleg ist `T0681_Ui.CodeEditor_*`.
