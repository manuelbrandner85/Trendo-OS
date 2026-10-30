# Audit Verbesserung 109 und 110 gegen das Repo (HEAD 7e1b863)

Stand: 30.10.2026 · Rolle: Requirements/Audit (02) und QA (19) · nur gelesen, nichts gebaut oder getestet.
Detailtabelle: `V109-110.csv` (100 Zeilen, je Funktion Code- und Testbelege).

**Bewertungsregel:** IMPLEMENTIERT_* wird nur vergeben, wenn die Funktion, wie die Spec sie beschreibt, vorhanden ist. Beide Specs verlangen für jede Funktion eine native ONE-Workspace-Oberfläche (ProgramSpec), Save/Reopen und Negativtests. Im Repo sind über `RegisterProgram` nur zwei Programme registriert: `src/Trendo.App/MainWindow.ProgGame.cs` und `MainWindow.ProgMode.cs`. Deshalb bekommt keine der 100 Funktionen den Status IMPLEMENTIERT. Ist vorhandene Fachlogik mit Test belegt, steht TEILWEISE. Die Lücken sind in der CSV genannt.

**Testbelege:** Die C#-Tests (Core/App/Api) waren am 30.10.2026 auf c062bb3 grün. Das ist nicht PC_VERIFIED. Die Python-Tests in `tests/claude_handoff/test_package.py` gehören nicht zu diesem belegten Lauf. Ihr Status ist deshalb offen.

## Verbesserung 109 – Robotics, IoT, Geospatial & Digital Twin (50 Funktionen)

| Status | Anzahl |
|---|---|
| NUR_SPEZIFIZIERT | 24 |
| TEILWEISE | 26 |
| IMPLEMENTIERT_MIT_TEST | 0 |
| IMPLEMENTIERT_OHNE_TEST | 0 |

Verteilung: In Robotik (F03, F05–F09), GIS (F22, F23, F25, F29), Digital Twin (F13, F17, F19, F31–F33, F35–F37) und Vision (F41–F45, F47, F48) gibt es Fachlogik mit Tests aus CORE-09, SPEC-10 und SPEC-13. IoT/Smart Home (F11, F12, F14–F16, F18, F20) ist praktisch nur spezifiziert.

**Die wichtigsten Lücken**
1. **Es gibt keine fünf Studios.** CORE-09, SPEC-10 und SPEC-13 sind Formular-Werkzeuglisten (`FachTool` in `src/Trendo.App/MainWindow.Core09.cs`, `Spec10.cs`, `Spec13.cs`), keine ProgramSpec-Workspaces. Es fehlen Datenmodelle wie RobotCell, ToolModel, GIS-Layer und Topologie.
2. **Die Live-Robotik ist nur teilweise abgesichert.** `RobotDrive` (`src/Trendo.Core/W6/RobotDrive.cs`) verlangt eine ApprovalGate-Freigabe und hat einen Not-Aus. Es fehlen Geräte-Allowlist, TLS, Controller-ACK, Watchdog/Dead-Man und ein Test für Netzverlust (V109-F09). `TcpMotionLink` ist reines TCP.
3. **Die IoT-Protokolle fehlen.** Es gibt keinen allgemeinen MQTT-5-Adapter und weder OPC-UA noch Home-Assistant. Vorhanden ist nur ein druckerspezifischer MQTT-3.1.1-Codec in `src/Trendo.Core/Print3D/BambuLan.cs` (V109-F12/F18). Der Aktor-Editor (F14) fehlt ganz.
4. **Datenlücken werden falsch behandelt.** `TwinPlus.EnergyKwh` und `TwinPlus.HoursAbove` (`src/Trendo.Core/W6/DigitalTwin2.cs`) rechnen über Messlücken hinweg. Das widerspricht QA-017 und QA-035 (V109-F17/F35). Messwerte (`Reading`) haben weder Quelle noch Qualitätsflag (F13/F32).
5. **Messen ist ohne gültige Kalibrierung möglich.** `Vision.PixelToTable` liefert mm aus einer angenommenen Tischgröße, auch ohne gültige Kalibrierung. Ein Status NICHT_MESSBAR und eine Unsicherheitsangabe fehlen (V109-F45).

**Konflikte und Sperren**
- **Gefährliche Realwelt-Aktoren** (F09, F14, F15, F48): Sie brauchen eine eindeutige Nutzerfreigabe und eine Fachkraft. Ein Test in Simulation oder DryRun ist kein Live-Test, und der Software-Not-Stopp ist keine zertifizierte Schutzfunktion.
- **Keine zweiten Kerne:**
  - F15/F39: kein zweiter Workflow-Scheduler, nur `WorkflowEngine` erweitern.
  - F11: kein zweiter Fusion-Kern neben `FusionStore`.
  - F13/F31: keine zweite Telemetrie-DB neben `TwinHub`.
  - F02/F27/F34: kein zweiter 3D-Kern neben `Mesh3D`.
- **Keine Berührung** mit 86 NEIN, 97 OFFEN oder SPEC-07. 114+ ist nicht betroffen.
- **Widerspruch in der Quelle:** Die Schlusszeile von `VERBESSERUNG_109_GODMODE.md` (Z. 640) sagt, Verbesserung 110 sei „separat vorgeschlagen und nicht Teil dieser Freigabe“. Die 110-Datei selbst trägt aber „JA / APPROVED_SPEC durch Nutzer“. Das muss Nutzer oder Agent 02 klären.

**Fehlende Quellen**
- `docs/spec/decisions.yaml` endet bei IMP-97. In `requirements.yaml` gibt es keinen Eintrag für 109 oder 110. Die Freigabe ist also nicht im Register.
- Die Spec nennt keine Priorität je Funktion. In der CSV steht stattdessen die Phase aus Spec §12 (P2-Liste: 01–08, 11–18, 21–28, 31–38, 41–48).
- Die in der Spec genannte `Scene3D` gibt es als Klasse nicht. Vorhanden sind `src/Trendo.Core/Mesh3D.cs` und `MainWindow.W4Studio3D.cs`.

**Vorschläge für erste Arbeitspakete** (klein, gut testbar)
1. **Lückenfeste Energie- und Betriebsstunden** (V109-F17/F35, QA-017/QA-035)
   - `TwinPlus.EnergyKwh` und `HoursAbove` in `src/Trendo.Core/W6/DigitalTwin2.cs` bekommen einen optionalen Parameter `maxGap`. Abstände über dieser Grenze zählen als Lücke und werden gemeldet.
   - Neue xUnit-Fälle in `tests/Trendo.Core.Tests/W6Tests.cs`:
     - 100 W für 1 h ergibt 0,1 kWh.
     - Bei einer Lücke gibt es keinen erfundenen Wert.
     - 100 h Laufzeit mit 20 h Ausfall ergeben die erklärte Stundenzahl.
   - Bestehende Tests F13/F20 dürfen sich nicht ändern (Default ohne Grenze).
   - Agenten: 05-core-api, QA 19.
2. **Robotik-Negativtests ohne Hardware** (V109-F09, Teil von QA-009)
   - In `tests/Trendo.Core.Tests/CameraRobotTests.cs` mit `MemoryMotionLink` ergänzen:
     - abgelaufene Freigabe (ApprovalGate mit FakeTimeProvider)
     - Link geschlossen, Not-Aus während einer mehrzeiligen Fahrt
   - Erwartet wird jedes Mal: keine weitere Zeile gesendet.
   - Code nur ändern, falls ein Test rot wird.
   - Agenten: 05-core-api, QA 03 und 19.

## Verbesserung 110 – AI Agent Command & Autonomous Production (50 Funktionen)

| Status | Anzahl |
|---|---|
| NUR_SPEZIFIZIERT | 23 |
| TEILWEISE | 27 |
| IMPLEMENTIERT_MIT_TEST | 0 |
| IMPLEMENTIERT_OHNE_TEST | 0 |

**Die wichtigsten Lücken**
1. **Das Jobregister ist nicht mit der App verbunden.**
   - `tools/trendo_ops.py` (SQLite `jobs`/`transitions`, TRANS-Tabelle, `run --execute`, `worktree`) ist die einzige Job-Wahrheitsquelle.
   - Es gibt keine C#-Anbindung (kein JobGateway). Jobs haben kein `projectId` und speichern weder PID noch Abbruch (V110-03/04/14/18).
   - Der Not-Stopp (`ApprovalGate.Halt`) erreicht laufende Claude-CLI-Prozesse nicht (V110-07).
2. **Status und Evidenz werden technisch nicht kontrolliert.**
   - Es gibt weder TaskCard, HandoffEnvelope, ArtifactManifest noch JobEvidence (V110-02/12/16/20/41/43).
   - Builder und Reviewer sind nur per Agentendefinition getrennt, nicht technisch.
3. **Die WorkflowEngine ist linear.** `src/Trendo.Core/W2/Workflows.cs` hat keine Bedingungen, keinen Datenfluss, keine Retry/Kompensation und keine Checkpoints (V110-23/24/27/28). Ohne Lease und Idempotenzschlüssel kann `RunDueAsync` bei parallelen Ticks doppelt starten (V110-25).
4. **Ressourcen, Kosten und Benchmarks fehlen.** Es gibt keine Queue-Slots, kein ConcurrencyBudget, keine Usage-Erfassung und keinen Benchmark (V110-06/08/35/40). Vorhanden ist nur die Messbasis `src/Trendo.Core/SystemMetrics.cs`.
5. **Freigabe und Pipeline sind nur Dokumente.** Release-Gate, Build-Matrix, Golden-Files und E2E für AION/GENESIS existieren nur als Doku oder Skill (V110-42/46/47/50).

**Konflikte und Sperren** (besonders genau geprüft)
- **Kein zweites Agenten-Laufzeitsystem.** `trendo_ops.py run/worktree` wird gekapselt, nicht neu gebaut (V110-14/13). Die 12 internen `AgentCenter`-Rollen (`src/Trendo.Core/W2/Agents.cs`) dürfen nicht mit den 20 Claude-Subagenten gleichgesetzt werden (V110-11).
- **Kein zweiter Workflow-Scheduler.** Canvas, Trigger und Lease bleiben auf `WorkflowEngine` (V110-06/21/22/25). Für den Planer (V110-06) wird das SQLite-Jobregister per Migration erweitert. Eine parallele SQLite-Datei ist verboten (Spec §8.1).
- **Kein zweiter KI-Kern.** `IntelligenceCore`, `KiLibrary`/`KiLibraryStore` und `KiBackends` werden erweitert (V110-31/34/36).
- **Kein zweiter Sicherheitsbroker.** Nur `ApprovalGate` und `Rbac` werden erweitert (V110-07/19/29/49). Der Hook bleibt Defense-in-Depth.
- **Kein zweites Projektregister.** Es bleibt bei `ProjectStore` (V110-03).
- **Kostenpflichtige KI-APIs:**
  - V110-08/36/40 dürfen sie nicht voraussetzen.
  - Die vorhandene Ersatz-KI (T-0225, `KiCloudUiTests`) springt ein, wenn die lokale KI schweigt. Für den Modus „nur lokal“ (V110-34/36) muss das hart abschaltbar sein, sonst ist es ein stiller Fallback.
- **AION/GENESIS** (V110-03/47) nicht fest ins Produkt schreiben, sondern generisch über `ProjectStore` lösen.
- **Unberührt:** 86 NEIN, 97 OFFEN und SPEC-07 abgelehnt sind nicht betroffen. 11-web-business-commerce wird nicht zur Aktivierung von 86 genutzt. 114+ bleibt pausiert.
- **Abhängigkeiten von Verbesserung 99 (REA, V110-38) und 103 (V110-47):** Deren Stand hier nicht geprüft.

**Fehlende Quellen**
- 110 steht nicht im Register (`docs/spec/decisions.yaml` endet bei IMP-97). Dazu kommt der oben genannte Widerspruch zur Schlusszeile der 109-Datei.
- Die Spec nennt den Baum `c062bb3`, HEAD ist `7e1b863`.
- `.trendo-local/jobs.sqlite3` ist privat und wurde nicht gelesen. Deshalb gibt es keinen Beleg für echte Jobläufe.
- Für `tools/trendo_dashboard.py` und für `trendo_ops.run/worktree` gibt es keine Tests.

**Vorschläge für erste Arbeitspakete** (klein, gut testbar)
1. **Doppelstartschutz für Abläufe** (V110-25)
   - In `src/Trendo.Core/W2/Workflows.cs` `RunDueAsync` mit einem Lock oder Lease pro Workflow-Id absichern, nach dem Muster von `EffectLedger.Claim` in `src/Trendo.Core/PromptAction/RunStore.cs`.
   - Fälligkeit (`LastRun`) atomar setzen.
   - Test in `tests/Trendo.Core.Tests/W2Tests.cs`: Zwei parallele `RunDueAsync(now)` auf denselben Intervall-Ablauf ergeben genau einen `WfRun` mit `DryRun=false`.
   - Agenten: 08-agent-workflow, QA 19.
2. **Verwaiste Jobs erkennen und Ereignisse redigieren** (V110-18/05)
   - In `tools/trendo_ops.py` eine Funktion ergänzen, die RUNNING-Jobs ohne lebenden Prozess beim Start auf INTERRUPTED setzt. Dazu `pid` und `started` per Migration ergänzen, mit Backup.
   - `event()`-Details auf schlüsselähnliche Muster maskieren.
   - Tests in `tests/claude_handoff/test_package.py`:
     - Ein RUNNING-Job mit nicht existenter PID wird INTERRUPTED, nie SUCCEEDED.
     - Ein künstlicher API-Key im Titel erscheint nicht in `events.jsonl`.
   - Agenten: 08-agent-workflow und 06-data-memory, QA 03 und 19.
