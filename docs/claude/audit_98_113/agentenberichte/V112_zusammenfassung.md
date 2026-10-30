# V112 – App Ecosystem, Plugin SDK & Compatibility: Abgleich Spezifikation ↔ Repo

Stand: Repo `C:\Users\<user>\Desktop\Trend Os`, HEAD `7e1b863` (read-only geprüft, nichts gebaut, keine Tests gestartet).
Quellen: `VERBESSERUNG_112_GODMODE.md` (1100 Zeilen, vollständig gelesen) und `V112_FUNKTIONSREGISTER.json` (50 Funktionen 112-E01 … 112-Q50, alle `APPROVED_SPEC / NOT_RUN`).
Detailtabelle: `V112.csv` (50 Zeilen, `;`-getrennt, UTF-8).

Hinweis zum Teststatus: Testbelege nennen vorhandene Testdateien/DisplayNames. Der letzte volle Lauf war am 30.10.2026 auf Commit `c062bb3` grün (Api 15/15, Core 845, App 432). Das ist **kein** PC_VERIFIED für V112.

## 1. Zählung je Status

| Status | Anzahl |
|---|---|
| NUR_SPEZIFIZIERT | 25 |
| TEILWEISE | 25 |
| IMPLEMENTIERT_MIT_TEST | 0 |
| IMPLEMENTIERT_OHNE_TEST | 0 |

Nach Programmbereich:
- Extension Studio E01–E10: 4 TEILWEISE (E02, E03, E04, E08), 6 NUR_SPEZIFIZIERT
- Marketplace M11–M20: 6 TEILWEISE (M11, M13, M14, M16, M17, M19), 4 NUR_SPEZIFIZIERT
- App Compatibility C21–C30: 7 TEILWEISE (C21, C23, C24, C25, C26, C28, C30), 3 NUR_SPEZIFIZIERT
- Secure Gateway S31–S40: 5 TEILWEISE (S31, S34, S36, S38, S39), 5 NUR_SPEZIFIZIERT
- Ecosystem QA Q41–Q50: 3 TEILWEISE (Q45, Q48, Q49), 7 NUR_SPEZIFIZIERT

„TEILWEISE“ heißt hier fast immer: ein vorhandener Baustein aus X-04, T-0203, QA-003/QA-004 oder T-0005 deckt einen Teil ab. Die spezifizierte 112-Funktion selbst (SDK, Signaturen, Transaktionen, Capability-Tokens, fünf native Programme) gibt es nicht.

## 2. Bestand (Belege)

- `src/Trendo.Core/Extensions.cs`: `ExtManifest`, `Extensions.Validate` (Id-/Versions-Regex, 4 Arten, Rechte-Whitelist, Dateiendungs-Whitelist ohne Programmcode, Symlinks abgewiesen), `Extensions.Hash`, `ExtensionStore.Install/Get/All/SetEnabled/Remove` (Audit `ext.*`), `ExtensionSamples`.
- `src/Trendo.Core/ExtensionCatalog.cs`: `Parse` (SHA-256 ist Pflicht, URL nur GitHub über https oder eine lokale ZIP), `InstallVerified` (Hash- und Id/Version-Abgleich), `BuildLocal`.
- `src/Trendo.App/MainWindow.W5Ext.cs` und `MainWindow.W5ExtCat.cs`: UI für Installieren, Ein/Aus, Entfernen, Nutzen sowie lokalen Katalog und GitHub-Katalog (Download in der App).
- Kompatibilität: `ExternalApps.cs` (5 Programme), `ToolCheck.cs` (`DependencyChecks.CheckAsync`), `Integrations.cs` (`ShellProcessLauncher.ForProgram` mit `ArgumentList`, `LaunchArgs` mit RBAC und Audit), `W7/Fusion.cs` (`OsMode`, `TripleMode.Capabilities`, `FusionStore.Route`), `tools/blender/*.py`, `tools/unreal/validate_project.py`.
- Sicherheit: `ApprovalGate.cs` (TTL, Ziel-Bindung, einmalig, `Halt`), `Rbac.cs`, `AuditLog.cs` (Hash-Kette), `ProjectStore.ResolveInProject`, `Secrets.cs`, `PromptAction/ConsentStore.cs`, `Sys/Sys03.cs` (Download-Quarantäne).
- Tests: `tests/Trendo.Core.Tests/W5ExtTests.cs` (2), `ExtCatalogTests.cs` (2), `tests/Trendo.App.Tests/ExtCatalogUiTests.cs` (1), dazu QA-003/QA-004/T-0005/T-0203/SYS-01/CORE-04-Tests als Basisbelege.

## 3. Sicherheitsbefund: Wie werden Erweiterungen heute geladen?

**Ergebnis: Es wird kein fremder Code geladen.** Im Code gibt es kein `Assembly.Load`, `LoadFrom`, `LoadFile` und kein `AssemblyLoadContext`. Die Suche über `src/` und `tests/` ergab 0 Treffer. Erweiterungen sind reine Daten: `.json/.md/.txt/.png/.jpg/.jpeg/.svg/.webp/.css`. Der Host deutet sie aus:
- `seite`: wird als `SitePage` geladen.
- `vorhaben`: Markdown wird in den Vorhabenordner kopiert.
- `links`: nur `https`-Links werden zu Knöpfen.

Das entspricht der Regel aus der Übergabe (Vertrauensebene A). Den heutigen Ansatz nicht durch `Assembly.Load` „erweitern“. Ein `AssemblyLoadContext` ist keine Sicherheitsgrenze. Prozess-Plugins (E05, E07, S32) bleiben BLOCKED, bis Q42 eine echte OS-Isolation belegt.

Risiken im heutigen Datenpfad:
1. **Kein ApprovalGate bei Erweiterungen.** Install, Remove und Katalog-Download laufen direkt per Klick (`MainWindow.W5Ext.cs ExtInstall`, `MainWindow.W5ExtCat.cs ExtCatalogInstall`). Der UI-Test `ExtCatalogUiTests` bestätigt die Installation ohne Freigabedialog. Auch `ApprovalGate.Halted` wird nicht geprüft, Installieren geht also auch während des Not-Stopps.
2. **Herkunft unbewiesen.** Erlaubt ist jeder GitHub-Account. Der SHA-256 kommt aus demselben ungeprüften Katalog und schützt damit nur vor Transportfehlern, nicht vor einem bösartigen Herausgeber. Es gibt keine Signatur (Ed25519: 0 Treffer). Die UI weist selbst auf „ungeprüft“ hin.
3. **Redirects und Offline.** Der `HttpClient` in `ExtCatalogLoadRemote` und `ExtCatalogInstall` folgt Weiterleitungen, weil `AllowAutoRedirect=false` fehlt. Den Offline-Modus prüft er nicht. Andere Stellen machen das richtig, z. B. `MainWindow.OpenRouter.cs`.
4. **Installation nicht atomar.** `ExtensionStore.Install` löscht ein vorhandenes Ziel, bevor es kopiert. Bei einem Abbruch geht die alte Version verloren. Eine gleiche Id wird still überschrieben, auch mit einer niedrigeren Version.
5. **Zip-Bomb nur grob abgefangen.** Begrenzt ist nur die Summe von `ZipArchiveEntry.Length` (50 MB). Es gibt keine Prüfung der Kompressionsrate, der Anzahl der Einträge oder doppelter Pfade, und keine Fuzz-Tests.
6. **Rechte nur deklariert.** `Permissions` (`dateien-lesen`, `internet-links`) werden geprüft, aber nirgends durchgesetzt oder angezeigt. Die einzige Fundstelle ist `Extensions.cs` Z.17.
7. Nebenbefund C24: `ExternalApps.Plan` baut für Blender Python-Code als String (`--python-expr`) und maskiert nur `\` und `'`. Ob ein präparierter Dateiname (z. B. mit Zeilenumbruch unter Linux) zur Code-Injektion führt, muss geprüft werden.

## 4. Die wichtigsten Lücken

1. **Kein Signatur- und Vertrauensmodell** (M11, M14, M13). Damit fehlt die Grundlage für alles Weitere im Marketplace.
2. **Erweiterungsaktionen umgehen ApprovalGate und Not-Stopp** (S36, S39, M16). Das widerspricht „keine Installationen ohne Zustimmung“.
3. **Kein transaktionaler Installer, keine Updates, kein Pinning, kein Rollback** (M16, M18, M19).
4. **Kein SDK/Manifest-v2** (E01, E03, E09): Es fehlen `schemaVersion`, `hostApi`, `dependencies`, `capabilities` und Datei-Hashes. Lizenz ist kein Pflichtfeld.
5. **Keine Plugin-gebundenen Capabilities** (S31, E06, S34): `Rbac` kennt nur 3 Rollen und 6 Rechte. Es gibt keine Token mit Projekt- und Plugin-Scope, Nonce oder Ziel-Hash.

## 5. Konflikte und Regelabgleich

- **IMP-112 fehlt im Register.** Weder `docs/spec/decisions.yaml` noch `requirements.yaml` enthalten 98–113. Die Freigabe vom 30.10.2026 steht nur in der Übergabe. Laut CLAUDE.md ist ein Decision-Delta an den Nutzer nötig, kein eigenmächtiger Eintrag.
- **X-04 steht in `requirements.yaml` als `IMPLEMENTED / PC_VERIFIED`** (Evidenz `reports/pc-0.9.2/tests-pc-4e6b81d.txt`). Das gilt nur für den Datenmechanismus. V112 darf diesen Status nicht erben.
- **GRUNDSATZ-01** (`requirements.yaml` → `principles`) verlangt: nur nach Zustimmung, aus offiziellen Quellen, mit Audit. Kataloge beliebiger GitHub-Accounts von Dritten und Installationen ohne Gate passen nicht dazu. Das muss entschieden werden (M11, S36).
- **Kein zweiter Kern:** Es gibt keinen Konflikt im Bestand. Bei der Umsetzung gilt: `ExtensionStore`, `ExtensionCatalog`, `ApprovalGate`, `Rbac`, `ConsentStore`, `AuditLog`, `ProjectStore`, `StudioRegistry`, `GlobalSearch` und die Sys03-Quarantäne erweitern, nicht duplizieren. `Dependencies.cs` betrifft Fachprogramme und ist kein Plugin-Resolver.
- **86 NEIN** (IMP-86 REJECTED), **97 OFFEN** (IMP-97 UNDECIDED), **SPEC-07 REJECTED**: V112 berührt keines davon. Q50/M20 dürfen keine CRM-/Service-Desk-Funktionen (86) einführen.
- **114+ pausiert**, 113 laut Spec nicht bestätigt: nicht berührt.
- **Kosten/Abos:** Die Spec fordert ausdrücklich keine Pflichtdienste. SBOM und Signatur sind ohne kostenpflichtige Dienste umsetzbar. Kein Konflikt.

## 6. Fehlende Quellen und Nachweise

- Die in der Spec genannte Datei `Trendo_OS_Verbesserung_112_..._GODMODE.md` (`specFile` im Register, sha256 `b76d…39ae`) wurde nicht gegen `VERBESSERUNG_112_GODMODE.md` gehasht. Die Identität ist also nicht geprüft.
- Die Spec verweist auf `START_HIER.md` und das ZIP. Diese wurden nicht gelesen, sie gehörten nicht zum Auftrag.
- `reports/` enthält keine V112-Belege. REL-06 und REL-07 (Blender/Unreal mit echten Daten) sind nicht gelaufen.
- Pro Funktion gibt die Spec keine Priorität vor, nur die Phasen P0–P6. Diese stehen in der Spalte `prioritaet_spec`.

## 7. Vorschläge für erste kleine, testbare Arbeitspakete

**AP-1: Erweiterungsaktionen über ApprovalGate und Not-Stopp führen (S36/S39/M16, Teil)**
- Dateien: `src/Trendo.Core/Extensions.cs` (`ExtensionStore` erhält optional `ApprovalGate`, `Install`/`Remove` prüfen `Halted` und einen gültigen Request mit Aktion `ext.install`/`ext.remove` und Paket-Hash als Target), `src/Trendo.App/MainWindow.W5Ext.cs`, `MainWindow.W5ExtCat.cs` (Request erzeugen, vorhandenen Freigabedialog nutzen).
- Ziel nicht atomar überschreiben: erst ins Staging kopieren, dann umbenennen und das alte Verzeichnis als Backup behalten.
- Tests: `tests/Trendo.Core.Tests/W5ExtTests.cs`. Neue Fälle: ohne Freigabe blockiert, bei Not-Stopp blockiert, falsches Ziel/Hash blockiert, Abbruch lässt die alte Version intakt. `tests/Trendo.App.Tests/ExtCatalogUiTests.cs` an den Freigabeschritt anpassen.
- Builder `03-security-permissions`/`05-core-api`, QA `19-qa-verification`.

**AP-2: Katalog-Downloads härten (S35/M11, Teil)**
- Datei: `src/Trendo.App/MainWindow.W5ExtCat.cs`. Gemeinsamer `HttpClient` mit `AllowAutoRedirect=false`. Weiterleitungen nur manuell folgen und dabei erneut gegen `ExtensionCatalog.IsRemote` prüfen. Im Offline-Modus abbrechen. Den Download auditieren. Die Prüflogik testbar nach `src/Trendo.Core/ExtensionCatalog.cs` verschieben (z. B. `RedirectAllowed(Uri)`). Der Core bleibt netzfrei (QA-019).
- Ergänzend: Zip-Bomb-Grenzen in `ExtensionStore.Install` (Kompressionsrate, Anzahl der Einträge, doppelte Pfade).
- Tests: `tests/Trendo.Core.Tests/ExtCatalogTests.cs` (Redirect auf fremde Domain, http-Downgrade), `W5ExtTests.cs` (Zip-Bomb, doppelte Einträge).
- Builder `05-core-api`, Security-Review `03-security-permissions`, QA `19-qa-verification`.

Beide Pakete setzen die Freigabe durch Manuel und einen Auftrag `tasks/T-<nr>-*.md` voraus.
