# Audit V106–V108 gegen das Repo (HEAD 7e1b863) – Zusammenfassung

Read-only-Audit. Im Repo wurde nichts geändert, gebaut oder getestet. Die Belege stammen aus Read/Grep auf `src/` und `tests/`. Die Testnamen sind DisplayNames aus den Testdateien.
Der letzte volle Testlauf war am 30.10.2026 auf Commit c062bb3 grün (Api 15/15, Core 845, App 432). Das gilt **nicht** als PC_VERIFIED und belegt keine einzige V106–V108-Abnahme: Kein Test im Repo prüft die Abnahmekriterien dieser Specs.
Detailtabelle: `V106-108.csv` mit 150 Zeilen.

Strenges Bewertungsprinzip: IMPLEMENTIERT_* wird nur vergeben, wenn der Funktionsvertrag der Spec im Kern erfüllt ist. Ein vorhandener Baustein allein reicht dafür nicht, er ergibt TEILWEISE. Keine der 150 Funktionen erfüllt ihren Vertrag vollständig.

| Verbesserung | NUR_SPEZIFIZIERT | TEILWEISE | IMPLEMENTIERT_MIT_TEST | IMPLEMENTIERT_OHNE_TEST |
|---|---:|---:|---:|---:|
| 106 Communication/Collaboration | 23 | 27 | 0 | 0 |
| 107 System/Security/Device Fusion | 17 | 33 | 0 | 0 |
| 108 Knowledge/Research/Education/Publishing | 26 | 24 | 0 | 0 |
| **Summe** | **66** | **84** | **0** | **0** |

Fast vollständig, aber trotzdem TEILWEISE: V107-19 Not-Stopp, V107-20 Forensik, V107-26 Android, 108-12 sicherer Lesemodus, 108-26 Statistikkern und 108-33 Karteikarten.

---

## V106 – Mail, Kalender, Kontakte, Projekte, Zusammenarbeit

**Basis im Repo:** `MainWindow.Pim.cs` (ein Konto, IMAP nur lesend, Senden nach Bestätigung, Monats-/Wochen-/Tagesansicht, Aufgaben, Regeln), `W3/MailProtocol.cs`, `W3/Mail.cs` (Kontakte, vCard 3.0, EML), `Sys/Sys02.cs` (`Calendar`, `Ics`, `Repeat`, `TodoItem`), `CreationSuite.cs` + `MainWindow.W4Suite.cs` (Vorhaben: Liste, Kanban, Gantt light), `W3/Sharing.cs` + `ShareServer.cs` (LAN-HTTP mit Token), `Rbac.cs`, `AuditLog.cs`, `Backup.cs`.

**Wichtigste Lücken:**
1. Es gibt kein Mehrkonto, kein OAuth2/XOAUTH2, keine Ordner/Flags/UIDVALIDITY und keine Outbox mit UNCERTAIN-Zustand (MAIL-01/02/03/07).
2. Der Kalender hat keine Zeitzonen: `CalEvent` nutzt ein lokales `DateTime`. Es fehlen außerdem EXDATE/RECURRENCE-ID, CalDAV, iTIP und Free/Busy (CAL-13/14/17/18/19).
3. Kontakte sind nur ein flacher `Contact`-Record. Gruppen, Dubletten, CardDAV und eine Feld-ACL fehlen (CNT-23/25/26/27).
4. Es gibt zwei getrennte Aufgabenmodelle: `CreationSuite.Step` (ID = Zeilennummer) und `Sys02.TodoItem`. Abhängigkeiten, WIP und wiederkehrende Aufgaben fehlen (TASK-32/33/34/37).
5. Zusammenarbeit läuft nur über globale Rollen (Guest/User/Owner). Projekt-ACL, Konflikterkennung, Locking und Sync-Journal fehlen (COL-41/44/45/47).

**Konflikte und Sperren:**
- CNT-28 (Kontaktaktivitäten) und CNT-22 bergen ein hohes Risiko, dass daraus ein CRM bzw. Service-Desk wird. **86 = NEIN** (`decisions.yaml` IMP-86 REJECTED) muss hart gehalten werden.
- MAIL-09: Die Bestätigung beim Senden (`AskSend`/`ConfirmSend`) existiert, läuft aber **nicht** über `ApprovalGate`. Ausbauen ja, aufweichen nein.
- MAIL-07, CAL-18, CNT-23: Es darf keinen automatischen Retry, keinen Einladungs- und keinen Massenversand ohne Einzelbestätigung geben.
- COL-42: Der ShareServer ist nur HTTP und nur LAN. Externe Freigaben brauchen ein Security-Gate.

**Erste Arbeitspakete:**
- **CAL-19 Free/Busy:** Reine Berechnung in `src/Trendo.Core/Sys/Sys02.cs` auf Basis von `Calendar.Occurrences`. Neuer Test in `tests/Trendo.Core.Tests/PimTests.cs` für überlappende und ganztägige Termine. Kein Netz und keine UI nötig.
- **CAL-20 Dedup und Referenz „Termin aus Mail“:** Die Quelle (Message-UID) wird im `CalEvent` bzw. in einem Referenzfeld gespeichert, und `MainWindow.Pim.cs` (Buttons `inbox-cal-N`) erzeugt den Termin genau einmal. Test in `PimUiTests.cs`. Dabei auf eine abwärtskompatible JSON-Migration achten.

---

## V107 – Explorer, Security Center, Device Studio, Backup, System Monitor

**Basis im Repo:** `Sys/Sys02.cs` (`FileAnalysis`, `Archives`, `Trash`, `Renamer`, `ResourceWarn`), `Sys/Sys03.cs` (`VerifyHash`, `Quarantine`, Defender-Auswertung), `W3/Security.cs` (`Checks`/`ScanFolder`/`Mask`), `ApprovalGate.cs` (`Halt`/`Resume`), `AuditLog.cs` (`Verify`/`FirstBroken`), `W3/Forensics.cs` (`MaxLife` = 120 h), `W3/Devices.cs`, `W7/BluetoothLink.cs`, `W7/Fusion.cs` (`TripleMode`/`FusionStore`/`MobileBridge`), `Trendo.Companion`, `Backup.cs` (`Create`/`Preview`/`Restore`), `SystemMetrics.cs`, `Utilities.cs` (`ResourceMonitor`), `W3/SelfHealing.cs`.

**Wichtigste Lücken:**
1. **Es gibt keinen Explorer.** `SystemPromptAdapter` meldet „Explorer … nicht angeschlossen“. Navigation, Tabs, Mehrfachauswahl und eine sichere Kopier-Warteschlange fehlen vollständig (V107-01 bis 04).
2. Das Backup ist ein Voll-ZIP des DataRoot: nicht inkrementell, nicht verschlüsselt, ohne Integritätsprüfung, ohne Einzeldatei-Restore und ohne DR-Probelauf (V107-32/33/36/38/40).
3. Dem Security Center fehlt ein UNKNOWN-Zustand (nur Gut/Hinweis/Risiko). Es gibt keinen Listener-/Portüberblick und keine Authenticode-Signaturprüfung bei Installationen (V107-11/15/17).
4. Für den Monitor fehlen Historie, Sensoren, Prozess-CPU/PID-Schlüssel und eigene Schwellwertregeln (V107-42/46/47/48). `NetworkSpeed` hat keinen Test.
5. `Archives.Unpack` überschreibt Dateien (`ExtractToFile(t, true)`), `Archives.Pack` löscht ein vorhandenes Ziel. Beides widerspricht V107-09 „nie ohne Konfliktdialog überschreiben“. Bomben- und Größenlimits fehlen.

**Konflikte und Sperren:**
- V107-20: Die **120-h-Regel ist unveränderlich** (`ForensicsLab.MaxLife`, `Create` wirft bei mehr als 120 h). Bei jedem Ausbau darf das nicht angefasst werden.
- V107-22/23/46/50 berühren Treiber, Datenträger, Registry und Dienste. Das ist laut CLAUDE.md verboten, deshalb sind nur read-only Adapter möglich.
- V107-17: GRUNDSATZ-01 (installierbar nur nach Zustimmung, aus offiziellen Quellen) muss eingehalten werden.
- 86, 97 und SPEC-07 sind nicht berührt.

**Erste Arbeitspakete:**
- **V107-09 Härtung von Archives.Unpack:** In `src/Trendo.Core/Sys/Sys02.cs` Limits für Gesamtgröße, Eintragszahl und Kompressionsverhältnis ergänzen. Bei vorhandenen Zieldateien abbrechen bzw. einen Konflikt melden statt still zu überschreiben. Test erweitern in `tests/Trendo.Core.Tests/Sys02Tests.cs` (Zip-Bombe, vorhandene Datei, ../).
- **V107-04 FileOperation-Kopierdienst (Vorschlag der Spec selbst):** Neuer kleiner Core-Dienst neben `Sys02.cs`, der über eine temporäre Datei kopiert, die Hash-Länge prüft, atomar umbenennt und bei Abbruch das Original unberührt lässt. Komplett mit Temp-Ordnern testbar. UI folgt später.

---

## V108 – Wissen, Research, Data Science, Learning, Publishing

**Basis im Repo:** `W2/Knowledge.cs` + `MainWindow.Wissen.cs`, `W3/Research.cs` + `ReadOnlyWeb.cs` (SSRF-Schutz, keine Auto-Redirects, `MaxBytes`), `W6/Science.cs`/`Science2.cs` (Welch, gepaarter t-Test, Chi², Konfidenzintervall, Regression, SVG), `Office/SheetDoc`/`SheetFiles`/`SheetExtras`, `W6/Learning.cs` (`LearnBox` SM-2, injizierbare Uhr), `Office/TextDoc`/`TextPdf`/`TextTools`/`Spelling.cs`.

**Wichtigste Lücken:**
1. Es gibt kein Quellen-/Claim-Modell (`SourceRecord`/`ClaimRecord`) und keine persistenten Rechercheprojekte. `ResearchDesk` ist absichtlich flüchtig. Betroffen sind 108-06/11/13/14/15.
2. Wissen ist reiner Text ohne Sammlungen, Links, Graph und Revisionen (108-01/02/03/05/08).
3. Ein Kurs-/Lektions-/Prüfungsmodell fehlt (108-31/32/35). Es gibt nur Kartenstapel.
4. Publishing hat kein Buchprojekt, keinen EPUB-Writer und keinen Preflight (108-41/47/48/50).
5. Data Science hat keine Pipeline/DAG, keinen Join und keine Notebooks (108-24/27/28). Die Rechenkerne sind dagegen gut getestet.

**Konflikte und Sperren:**
- **Language Studio 2.0 = NEIN** (`decisions.yaml` LANG-2.0 REJECTED). **SPEC-07 = REJECTED**, nur Basisfunktionen und eng begrenzte Übersetzung sind zulässig. Learning 31–40 darf deshalb nicht zum Sprach-/Lokalisierungsstudio ausgebaut werden. Vokabelkarten in `LearnBox` sind Bestand. Hinweis: `StudioCatalog.cs` führt `SPEC-07` „Texte übersetzen“, das entspricht der zulässigen Basis laut `decisions.yaml`-Notiz.
- 108-49: `MainWindow.TextPro.cs` `TextAiRewrite` ersetzt den Text sofort (Undo ist möglich). Die Spec verlangt aber „nur als Diff, keine automatische Überschreibung“. Das ist eine Abweichung, die bei der Umsetzung angepasst werden müsste.
- 108-09/29/37: Es darf keinen zweiten KI-Kern geben. Alles muss über `IntelligenceCore`/`Intel.AskAsync` laufen.
- 108-47: EPUBCheck nur nach Installationsfreigabe (GRUNDSATZ-01).
- 108-32 hängt an Verbesserung 101 (Media-Assets). Das wurde hier nicht geprüft.

**Erste Arbeitspakete:**
- **T-108-002 SourceRecord/ClaimRecord (108-06):** Ein reines Core-Datenmodell, etwa als Erweiterung neben `src/Trendo.Core/W2/Knowledge.cs`, mit Verdict UNVERIFIED/SUPPORTED/…. Regel: Ein Claim ohne Locator kann nie SUPPORTED werden. Unit-Tests in `tests/Trendo.Core.Tests/W2Tests.cs`.
- **108-26 Statistik-Referenztests:** Für `Stats`/`StatsPlus` (`src/Trendo.Core/W6/Science.cs`, `Science2.cs`) 12 feste Referenzdatensätze mit bekannten Ergebnissen und Toleranz in `tests/Trendo.Core.Tests/W6Tests.cs`. Optional ein `AnalysisResult`-Record. Das ist rein additiv und risikoarm.

---

## Fehlende Quellen und Lücken der Prüfung
- **106, 107 und 108 sind nicht registriert:** `docs/spec/decisions.yaml` endet bei IMP-97, `requirements.yaml` enthält kein IMP-98…113. Die JA-Freigaben aus den Specs müssten per Decision-Delta (nur Manuel) nachgetragen werden.
- Die Specs geben **keine Priorität je Funktion** an, nur Phasen. Die Spalte `prioritaet_spec` nennt deshalb die Phase.
- Die Agentennummern der Specs beziehen sich auf `docs/claude/AGENTEN_MATRIX.md`. Die Datei existiert, wurde aber nicht inhaltlich abgeglichen. Die Agenten-Dateinamen stimmen mit `.claude/agents/` überein (01–20).
- Nicht geprüft: Laufzeitverhalten, UI-Rendering, `Trendo.Render`-Screenshots, Linux/Pi, die Verbesserungen 101 und 106 als Abhängigkeit von 108-32/38, sowie der Inhalt von `tests/claude_handoff`.
- Die Spec-Annahme „`MainWindow.Spec05.cs`/`Spec06.cs`“ für Data Science/Learning ist bestätigt. Die Spec-Angabe „`StudyPromptAdapter`“ wurde nur als Datei gefunden, nicht inhaltlich geprüft.
