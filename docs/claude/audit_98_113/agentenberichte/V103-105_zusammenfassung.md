# Audit Verbesserungen 103–105 gegen Trendo OS (read-only)

- **Repo:** `C:\Users\<user>\Desktop\Trend Os`, HEAD `7e1b863` (.NET 10, Avalonia 12.1.4). Im Repo wurde nichts geändert, gebaut oder getestet.
- **Quellen:** `VERBESSERUNG_103_GODMODE.md` (669 Zeilen), `VERBESSERUNG_104_GODMODE.md` (649 Zeilen) und `VERBESSERUNG_105_GODMODE.md` (584 Zeilen). Alle drei wurden vollständig gelesen.
- **Detailmatrix:** `V103-105.csv` (150 Zeilen, Trennzeichen `;`). Semikolons in Test-DisplayNames sind dort durch Kommas ersetzt.
- **Teststatus:** Der letzte Volltest lief am 30.10.2026 auf Commit `c062bb3` und war grün (Api 15, Core 845, App 432). „Test vorhanden“ heißt in der Matrix nur: Der Test lief in diesem Volltest grün. Das ist **kein PC_VERIFIED**. Der Volltest lief nicht auf HEAD `7e1b863`. Python-Tests (`tests/claude_handoff`) sind in diesen Zahlen nicht enthalten.
- **Warum keine Funktion IMPLEMENTIERT_MIT_TEST ist:** Jede Funktion verlangt mehr, als der vorhandene Code leistet. Gefordert sind z. B. ein echter Canvas, Engine-/Browserläufe, Manifeste und eigene T-IDs. Deshalb steht keine der 150 Funktionen auf IMPLEMENTIERT_MIT_TEST oder IMPLEMENTIERT_OHNE_TEST. Vorhandener Code mit Test wird als **TEILWEISE** geführt, mit benannter Lücke.

## Übersicht

| Verbesserung | NUR_SPEZIFIZIERT | TEILWEISE | IMPLEMENTIERT_MIT_TEST | IMPLEMENTIERT_OHNE_TEST | Summe |
|---|---|---|---|---|---|
| 103 Game/3D | 27 | 23 | 0 | 0 | 50 |
| 104 Web/App/Code | 19 | 31 | 0 | 0 | 50 |
| 105 Business/Finance/E-Commerce | 24 | 26 | 0 | 0 | 50 |
| **Gesamt** | **70** | **80** | **0** | **0** | **150** |

Rund 20 der TEILWEISE-Funktionen bestehen nur aus Bausteinen, etwa einem Freitextfeld plus einer Prüfregel. Der Rest der jeweiligen Funktion fehlt.

---

## 103 – Game, 3D & Interactive Production

**Vorhandene Basis**
- `GameDesign.cs`: Spielbibel, Vorlagen, Prüfregeln.
- `MainWindow.ProgGame.cs`: ProgramShell-Programm CORE-06.
- `GameStudio.cs`: Browserspiele, Unreal-Erkennung und Projektanlage.
- `Scene3D`, `Mesh3D` und `MeshIO`: OBJ/STL/GLB/glTF/FBX(ASCII) mit Einheiten.
- `Studio3D.cs`: Blender-CLI.
- `W6/XrModels2.cs`: Texeldichte, LOD-Plan, Materialanalyse.
- `Assets.cs`: Poly Haven mit MD5 und Lizenzdatei.
- `tools/blender/*.py` und `tools/unreal/validate_project.py`.

**Wichtigste Lücken**
1. **Welt- und Charakterproduktion fehlt komplett.** Für 103-12 bis 103-30 gibt es keinen Code, außer Teilprüfungen in `inspect_asset.py` (103-25). Es fehlen Terrain, Straßen, Navmesh, Streaming, Living World, Character Creator, Rig und Animation.
2. **Nachweiskette Blender → Unreal ist nicht automatisiert getestet.**
   - `W4Studio3DTests.Blender_roundtrip` beendet sich ohne Blender mit `if (bl is null) return;` und besteht dann leer.
   - `smoke_asset.py`, `inspect_asset.py` und `validate_project.py` haben keine automatisierten Tests.
   - Ein Unreal-Import ist nirgends belegt (103-33, 103-37, 103-40).
   - Die Adapter sind vorhanden, eine getestete Anbindung gibt es nicht.
3. **Kein Abhängigkeitsgraph für Produktionsaufträge (103-04).** `StudioWorkflow`/`RunStore` speichern Läufe ohne Abhängigkeiten. Die SQLite-Jobs mit Worktrees liegen nur im Claude-Dev-Werkzeug `tools/trendo_ops.py`, nicht im Produkt.
4. **Game-Studio ist noch formularbasiert (103-02, 103-11).** Es gibt Bereichsreiter, Listen und ein 2D-Levelraster. Es fehlen Canvas, Gizmos, Timeline und persistente Layouts. Die Spec stuft genau das als „nicht vollwertig“ ein.
5. **Kein Build- und Release-Nachweis für Spiele (103-10, 103-49).** Es fehlen `build-report.json`, Hash, SBOM und UE-Packaging. Vorhanden sind nur ein Browserspiel-ZIP und die `.uproject`-Anlage.

**Konflikte und Sperren**
- **AION/GENESIS:** 103-20, 103-28, 103-29, 103-45 und 103-50 beschreiben GENESIS- bzw. AION-Spielmechanik (Rebirth, Lebenszyklus, Familien, Prolog). Diese Spiele dürfen nicht in Trendo neu geschrieben werden. In Trendo dürfen nur Werkzeug-Schemata und Prüfer entstehen. Abnahmen laufen in den eigenen Projektordnern und nur mit Nutzerfreigabe.
- **Keine zweiten Kerne:** 103-04 und 103-48 dürfen keinen zweiten Scheduler bzw. kein zweites Agentensystem einführen. Wo der Abhängigkeitsgraph leben soll, muss eine ADR klären (Agent 01/02).
- **Nicht gebaute Abhängigkeit:** 103-17 setzt die Studios 96 (Lighting/Acoustics) und 101 als Services voraus. `requirements.yaml` führt IMP-96 als `NOT_STARTED`, und die Originalbeschreibung fehlt im Repo.
- **Sprache:** 103-03 verlangt Lokalisierbarkeit, aber ohne Language Studio 2.0 (NEIN).

**Erste Arbeitspakete**
- **A1 – Spielprojekt-Manifest (103-01):** `game.project.json` mit `schema_version`, Projekt-ID, Engine-Version „DISCOVER“, Plattform und Lizenzstatus.
  - Code: neben `ProgramDocument` in `src/Trendo.Core/Programs/GameDesign.cs`.
  - Test: Roundtrip und ungültiger Pfad in `tests/Trendo.Core.Tests/GameDesignTests.cs`.
  - Rein Core, ohne Engine testbar.
- **A2 – Quest-FSM-Validator (103-06):** Stabile Quest-IDs, Zyklen- und Erreichbarkeitsprüfung, Erfolgs-/Fehlerpfade als Daten.
  - Code: Erweiterung von `GameDesign.Check`.
  - Test: neue Fälle in `GameDesignTests` (Zyklus, unerreichbarer Schritt, Reload).

---

## 104 – Web, App & Software Development

**Vorhandene Basis**
- Website-Studio (`src/Trendo.Core/Website/*`):
  - Brief, Stile, GLB/Video/Unreal-Hinweis.
  - `WebsiteQuality` mit SEO-/A11y-/Linkprüfung und „Jetzt beheben“.
  - Paket erst nach `ApprovalGate`.
- `WebsitePreview.cs`: Headless-Chrome-Screenshot über Loopback.
- `PageBuilder`: X-03-Bausteinliste.
- `DevStudio`: Vorlagen, Git, Tests über `Cli`.
- `W4Web`: Dateiliste, Editor mit Zeilennummern, Diff-Vorschau.
- Weitere Bausteine: `DependencyManager`, `ISecretStore`, `Rbac`/`ApiTokens` für die System-API, `scripts/vuln_scan.sh`, `nuget_licenses.py`.

**Wichtigste Lücken**
1. **App- und Backend-Studio fehlen fast vollständig.** 104-12 bis 104-18, 104-20, 104-31 bis 104-35, 104-37 und 104-39 haben keinen Code. Im Produkt gibt es **kein SQLite** (keine PackageReference), obwohl 104-16, 104-31 und 104-36 vorhandene SQLite-Dienste voraussetzen.
2. **Kein interaktiver Web-Canvas und keine interaktive Vorschau (104-02, 104-08).**
   - Der Builder ist eine Liste mit Hoch/Runter.
   - Die Vorschau ist nur ein Screenshot.
   - Der Browsertest wird ohne Chrome übersprungen.
3. **Code-Editor ohne Editor-Kern (104-21, 104-23, 104-25, 104-27).**
   - Es gibt TextBox plus Zeilennummern.
   - Es gibt kein AvaloniaEdit, kein LSP, kein DAP, kein Roslyn.
   - AvaloniaEdit braucht eine Lizenz- und Kompatibilitätsprüfung gegen Avalonia 12.1.4.
4. **Kein Komponentenmodell und keine Breakpoints (104-03, 104-04).**
5. **Deployment ist nur gesperrt, nicht gebaut (104-10, 104-40).** Die Sperre ist getestet. Freigabefähige Adapter, Health Check und Rollback fehlen.

**Konflikte und Sperren**
- **System-API ≠ Nutzerprojekt-Backend:** `Trendo.Api`, `LocalApi`, `ApiTokens` und `Rbac` dürfen nicht als Backend für Nutzerprojekte dienen (104-33, 104-34, 104-39).
- **Keine zweiten Kerne:** kein zweiter Scheduler (104-36), Secret Store (104-38) oder Security-Gate. Die Spec-Annahme „bestehendes SQLite-Job-System“ trifft nur auf `tools/trendo_ops.py` zu, ein Dev-Werkzeug.
- **Sprache:** 104-18 ohne Language Studio 2.0 (NEIN).
- **Registerstand:** 104 §11 nennt 105 „OFFEN“. Die 105-Spec sagt dagegen „JA am 27.10.2026“. Der Widerspruch ist im Register nicht aufgelöst (siehe unten).

**Erste Arbeitspakete**
- **B1 – Viewport-Prüfung (104-03/104-09):** `WebsiteQuality` prüft je Viewport 320/375/768/1024/1440 rein statisch (Media Queries, feste Breiten > Viewport). Statusfeld „gemessen / geschätzt / nicht geprüft“.
  - Code: `src/Trendo.Core/Website/WebsiteQuality.cs`.
  - Test: `tests/Trendo.Core.Tests/WebsiteStudioTests.cs`.
  - Ohne Chrome testbar.
- **B2 – Stabile Block-IDs und Undo/Redo im X-03-Builder (104-02, Vorstufe Canvas):**
  - Code: `src/Trendo.Core/PageBuilder.cs`, Undo über `ProgramDocument`-Muster.
  - Tests: `tests/Trendo.Core.Tests/W4BuilderTests.cs` (Export/Reimport ohne doppelte IDs) und `W4BuilderUiTests.cs`.

---

## 105 – Business, Finance & E-Commerce

**Vorhandene Basis**
- `Finance.cs`:
  - `Entry`, `Invoice` und `Totals` mit `decimal`.
  - `ImportCsv`, `NextInvoiceNumber`.
  - `Ledger` als globale JSON-Datei.
- `FinanceExtra.cs`:
  - `CategoryMemory`, `BudgetStore`, `TaxExport`.
  - `Shop` mit `AddOrder`, `Invoice`, `Figures`, `ShopHtml`.
- `BusinessOps.cs`: `Product`, `BizContact`, `ReorderPoint`, `IbanValid`/`GiroCode`, `Compare`, `DayStats`.
- `BusinessStudio`, `BusinessLaunch`, `LegalRates`.
- Tests: `W5FinanceTests`, `Teil4BusinessTests`, `BusOpsTests` und `PromptActionTests` (Rechnung/Zahlung buchen nichts).

**Wichtigste Lücken**
1. **Kein transaktionales Domänenmodell (V105-02/03/05/17/22).**
   - Buchungen haben keine IDs.
   - Es gibt keine doppelte Buchführung und keine Zahlungsevents.
   - Es gibt keinen Mandanten: `Ledger` ist eine einzige `finanzen/buchungen.json`.
   - `Ledger.RemoveAt` löscht hart, statt zu stornieren.
2. **E-Rechnung DE/IT fehlt vollständig (V105-15, V105-16).** Es gibt keinen XML-Generator und keinen Validator. Rechnungen sind nur HTML.
3. **Warenwirtschaft ist ein Rumpf (V105-21 bis V105-30).**
   - `Product` hat `Cost` und `Price` als `double`, wo die Spec `decimal` fordert.
   - `Stock` ist ein `int`.
   - Es gibt keine SKUs, Varianten, Lagerorte, Bewegungen, Retouren, Stücklisten (BOM), Bewertung oder Migration.
   - Produkte werden über den Namen identifiziert.
4. **Commerce ohne Checkout und Payment (V105-32 bis V105-38).** Es gibt nur die statische `ShopHtml`. `PayOptions` ist ein reiner Gebührenrechner.
5. **Steuersätze sind fest codiert (V105-08).** `LegalRates.VatStandard` hat keine versionierten Länderprofile mit `valid_from`/`source_url`.

**Konflikte und Sperren**
- **86 NEIN (CRM/Service-Desk) betrifft V105-45 direkt.** Im Bestand gibt es bereits CRM- bzw. Ticket-ähnlichen Code:
  - `BusinessOps.BizContact` mit Pipeline `Stages` und `PipelineValue`.
  - `Ticket`, `Sla`, `Overdue`, `Faq` (BUS-019/023).
  - Kundenanfragen-UI in `MainWindow.W5Ops.cs` (`BizTickets`).
  - `Shop.AddOrder` setzt außerdem Interessent → Kunde.

  Dieser Code stammt aus früher freigegebenen BUS-Funktionen. `decisions.yaml` sagt: „vorher bestätigte CRM-/Kommunikationsfunktionen gelten weiter“. **Er darf für V105-11, V105-39, V105-44 und V105-45 nicht ausgebaut werden.** Ausdrücklich ausgeschlossen sind: Segmente als Direktmarketing, Kundenportal mit Anfragen/Tickets, SLA-Erweiterungen.
- **Bestehende Tests sichern das von der Spec beanstandete Verhalten ab.** `Teil4BusinessTests` „Online verkaufen F1/F2/U1/F3“ erwartet:
  - Der Bestand sinkt schon bei der Bestellung.
  - Die Rechnung bucht die Einnahme.

  V105-05 und V105-22 verlangen das Gegenteil. Dafür braucht es ein **Decision Delta und eine bewusste Testanpassung**. Still umbauen ist nicht erlaubt.
- **Recht:** E-Rechnung, Steuerprofile, Widerruf und DSGVO (V105-08, 15, 16, 20, 39, 49) brauchen eine fachliche bzw. juristische Freigabe. Bis dahin gilt `BLOCKED_BY_LEGAL_REVIEW`.
- **Externe Wirkung:** Live-Payment, SdI-Übermittlung und Shop-Deploy (V105-16, 34, 40) laufen nur über `ApprovalGate` und mit ausdrücklicher Zustimmung.

**Erste Arbeitspakete**
- **C1 – Robuste Importerkennung (V105-04):** Zusätzlich zur heutigen Dublettenerkennung (Datum, Betrag, Text) einen Importfingerprint mit Datei-SHA256 und Zeilennummer einführen. Ein erneuter Import derselben Datei ergibt dann 0 neue Buchungen. Zwei echte gleiche Umsätze in einer Datei bleiben beide erhalten (heute wird der zweite verschluckt).
  - Code: `src/Trendo.Core/Finance.cs` (`Ledger.Import`).
  - Test: `tests/Trendo.Core.Tests/W5FinanceTests.cs`.
- **C2 – Preisbasis auf `decimal` mit Altdaten-Kompatibilität (V105-21 Vorstufe):** Ein Lese-Adapter macht aus dem Alt-JSON von `Product` (`double`) `decimal`-Werte. Bestandsdateien bleiben lesbar.
  - Code: `src/Trendo.Core/BusinessOps.cs`, `src/Trendo.Core/FinanceExtra.cs` (`Shop.AddOrder` castet heute `(decimal)prods[i].Price`).
  - Tests: `BusOpsTests` und `Teil4BusinessTests` mit Alt-JSON-Fixture und centgenauen Summen.
  - Kleiner Umfang, aber eine Schemaänderung: vorher Freigabe einholen.

---

## Übergreifend: fehlende oder widersprüchliche Quellen

1. **98–105 sind im Repo nicht registriert.**
   - `requirements.yaml` und `docs/spec/decisions.yaml` enden bei IMP-97 (UNDECIDED).
   - Es gibt keine Einträge IMP-98 bis IMP-105.
   - Es gibt kein Entscheidungs-Delta vom 27.10.2026 unter `docs/claude/`.
   - Die Specs liegen nur im Übergabeordner (Temp).

   Vor jeder Umsetzung muss Agent 02 ein Decision Delta anlegen. Nicht selbst eingetragen, weil das Repo read-only war.
2. **104 nennt 105 „OFFEN“, die 105-Datei nennt „JA 27.10.2026“.** Beim Nutzer bestätigen lassen.
3. **In den Specs genannte, aber nicht vorhandene Bausteine:**
   - „AssetRegistry“ (103 §9),
   - „IDeviceCapability“ (104-17),
   - produktseitiges „SQLite-Job-System“ (104-36; es gibt nur `tools/trendo_ops.py`),
   - „ProjectStore-konforme SQLite-Zugriffe“ (104-31).
4. **Fehlende Originalbeschreibungen von Abhängigkeiten:** IMP-95 (Architektur/BIM) und IMP-96 (Lighting/Acoustics) sind laut `requirements.yaml` ohne Originalbeschreibung. Die Verbesserungen 101 und 102 sind im Repo-Register nicht auffindbar.
5. **AION/GENESIS-Bibeln** wurden nicht geprüft (außerhalb des Auftrags-Scopes). 103 verlangt ihre Prüfung in den eigenen Projektordnern.
6. **Unklar:** Ob die Python-Tests in `tests/claude_handoff/test_package.py` beim letzten Volltest liefen, ist nicht belegt. Ebenso unklar ist, ob Tests mit stillem Abbruch auf dem Test-PC wirklich etwas geprüft haben:
   - `W4Studio3DTests.Blender_roundtrip`,
   - `WebsitePreviewTests` echter Browser,
   - `W4DevTests` mit echten Werkzeugen,
   - `W4GameTests` mit `node --check`.

## Gesamtempfehlung

Alle sechs vorgeschlagenen Pakete (A1, A2, B1, B2, C1, C2) sind reine Core- oder Core-plus-UI-Änderungen auf vorhandenen Klassen. Sie brauchen keine neue Abhängigkeit und keinen Engine- oder Netzzugang. Mit xUnit sind sie deterministisch prüfbar.

C2 und jede Änderung am Shop- und Lagerverhalten setzen vorher ein Decision Delta voraus, weil bestehende Tests das Altverhalten absichern.
