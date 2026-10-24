# TRENDO OS – VERBESSERUNG 42
## Universal Studio Prompt-to-Action GODMODE – verbindlicher Claude-Code-Auftrag

**Auftraggeber:** Trendo OS  
**Ziel-Repository:** `manuelbrandner85/Trendo-OS`  
**Stand der Beauftragung:** 22.10.2026  
**Status:** Verbesserung 42 einschließlich aller 20 Funktionen **JA – zur Implementierung freigegeben**. Dies ist **kein Nachweis**, dass sie bereits implementiert sind.

> **Arbeitsbefehl an Claude Code:** Implementiere Verbesserung 42 in der **bestehenden** Trendo-OS-Codebasis. Untersuche zuerst den tatsächlichen Repository-Stand und passe sämtliche Namen, Dateipfade, Schnittstellen und Implementierungen an den real gefundenen Code an. Erfinde weder vorhandene Komponenten noch erfolgreiche Testergebnisse. Kein Rewrite des Betriebssystems, keine zweite KI-Engine, keine Umgehung bestehender Freigaben. Liefere funktionsfähigen, getesteten Code statt einer bloßen Präsentation oder Demo mit vorgetäuschten Ergebnissen.

## 1. Nicht verhandelbare Projektregeln

1. **Bestehende Architektur erhalten:** Vorhandene **54 Studioeinträge** im `StudioCatalog` und bestehende UI-/Navigations-, Projekt-, Sicherheits-, Dateiverwaltungs- und Persistenzmechanismen nutzen. Falls Anzahl oder Namen im aktuellen Branch abweichen, Abweichung dokumentieren; nichts blind auf 54 hardcoden.
2. **Wiederverwendung vor Neubau:** Nachweislich vorhandene `PromptLibrary`, `Intel.AskAsync`, Briefdatenmodelle, `StudioCatalog`, KI-/Blender-/Unreal-Adapter, Projektisolation und vorhandene QA-/Forensikmechanismen zuerst finden und wiederverwenden. Falls ein Symbol fehlt oder anders heißt: realen Gegenpart benennen und adaptieren, nicht eine vermeintliche Implementierung vortäuschen.
3. **Lokale KI zuerst:** Bereits eingestelltes lokales KI-System verwenden. Cloud-/Drittanbieter nur nach expliziter, widerrufbarer Nutzererlaubnis und transparenter Offenlegung von Kosten, Daten und Zweck. Keine stillen Netzwerk-Fallbacks.
4. **Menschliche Kontrolle:** Versand, Zahlung, Veröffentlichung, Kontenänderungen, Sicherheitskonfiguration, Boot-/Systemeingriffe, Gerätebefehle und Maschinensteuerung werden **nie** durch bloße Prompt-Absicht freigegeben. Vor dem konkreten Seiteneffekt ist eine zu Ziel, Inhalt, Umfang und Gültigkeitsdauer passende menschliche Bestätigung nötig.
5. **Deterministische Fachprüfung:** Technische Berechnungen, Einheiten, Konstruktionswerte, Statik, Finanzrechnungen, Formeln, Geometrie, Prüfwerte usw. müssen – soweit fachlich möglich – mit verifizierbaren deterministischen Fachwerkzeugen berechnet und geprüft werden. KI darf Annahmen vorschlagen, aber keine unbelegten Mess-/Rechenergebnisse als Fakten ausgeben.
6. **Keine Phantom-Ergebnisse:** Niemals „fertige Datei“, „gerendertes Video“, „gesendete E-Mail“, „gebautes Modell“, „ausgeführt“, „getestet“ oder „veröffentlicht“ melden, wenn die reale Aktion, Datei bzw. QA nicht nachgewiesen ist. Ergebnisstatus unterscheidet `planned`, `awaiting_input`, `awaiting_approval`, `running`, `produced`, `verified`, `failed`, `cancelled`, `unsupported`.
7. **Projektisolation und Datenschutz:** Jeder Auftrag bleibt im expliziten Projekt-/Studio-Kontext. Pfade kanonisieren, relative/absolute Pfadfluchten und Symlink-Umgehungen blockieren, geheimhaltungsbedürftige Inhalte minimieren, Berechtigungen serverseitig prüfen.
8. **Bestandsentscheidungen respektieren:** Verbesserungen **23, 25, 34 sowie Language Studio 2.0 bleiben NEIN**. Verbesserung **35 bleibt OFFEN** und darf nicht stillschweigend aktiviert werden. Alle bisherigen bestätigten Module, ausdrücklich **41**, bleiben bestehen und dürfen nicht regressieren.
9. **120-Stunden-Forensikregel unverändert:** Die im Projekt bereits vereinbarte 120-Stunden-Forensikregel einschließlich ihrer realen Speicher-, Zugriffsschutz-, Lösch- und Ablaufsemantik aus vorhandenem Code/Projektvorgaben übernehmen. **Nicht** eigenmächtig als andere Frist interpretieren, verkürzen oder ausdehnen. Falls Implementierung oder genaue Semantik unklar sind, als Blocker kennzeichnen und unverändert lassen, bis geklärt.
10. **Keine pauschale Aktivierung gefährlicher Fähigkeiten:** Adapter können implementiert, aber risikoreiche Ausführung bleibt bei fehlendem Berechtigungs-/Freigabe-Backend deaktiviert (`unsupported` oder `awaiting_approval`).

## 2. Verbindliche Zielkette

`Nutzerprompt → Studioauflösung → strukturierter validierter Auftrag → gezielte Rückfragen bei tatsächlich fehlenden Angaben → Policy-/Capability-Prüfung → Auftragsplanung → menschliche Freigabe falls erforderlich → echter fachspezifischer Studio-Adapter → persistiertes, bearbeitbares Artefakt → deterministische und studienspezifische QA → Vorschau → Änderungsrunden → Export/Datei bzw. gesondert freigegebene Aktion`

**Kein Übergang darf simuliert werden.** Jede Stufe hat maschinenlesbare Status-, Fehler- und Provenienzinformationen. Prompt-Text ist keine Autorität zur Umgehung der Rechteprüfung.

## 3. Die 20 obligatorischen Funktionsbereiche

1. **Universal Prompt Entry:** Einheitliche Eingabe pro Studio mit Anhängen, Sprache, Kontext und verlustfreier Verlaufshistorie.
2. **Studio Context & Routing:** Zuordnung zum aktuellen Studio oder bewusstes Routing auf ein anderes Studio, stets mit sichtbarer Begründung.
3. **Intent & Action Parser:** Strukturiertes, versioniertes Auftragsobjekt statt freier, unkontrollierter Tool-Aufrufe.
4. **Schema Validation:** Studio-, Aktions- und Dateiformat-spezifische Validierung mit verständlichen Fehlermeldungen.
5. **Smart Clarifications:** Nur fehlende, entscheidungsrelevante Daten nachfragen; Teilantworten fortsetzen können.
6. **PromptLibrary Integration:** Bestehende Promptvorlagen und Fachinstruktionen referenzieren und versioniert anwenden.
7. **Existing AI Orchestration:** `Intel.AskAsync` bzw. reale vorhandene KI-Abstraktion verwenden; keine zweite Model-Routing-/Conversation-Engine.
8. **Local-First Provider Policy:** Anbieterberechtigungen, Datenschutz, Kostenhinweis, explizite Einwilligung, Offline-Verhalten und Widerruf.
9. **Studio Capability Registry:** Für jeden bestehenden Studioeintrag eindeutige unterstützte Aktionen, Adapter, Outputtypen und Capability-Level deklarieren.
10. **Adapter Execution Layer:** Echte typisierte Adapter und ausführbare Aktionen, mit Cancellation, Timeouts, Fehlerbehandlung und Ressourcenlimits.
11. **Editable Artifact Workspace:** Persistierte, editierbare Zwischen- und Endartefakte mit Projektbezug und Wiederaufnahme.
12. **Live Progress & Evidence:** Wahrheitsgemäßer Fortschritt, Schritte, Tool-Ergebnisse, Protokolle und Dateinachweise, keine erfundenen Prozente.
13. **Deterministic Verification Hub:** Fachrechner/Validatoren mit Einheiten, Eingangsannahmen, Versionsstand und reproduzierbarem Prüfergebnis.
14. **Specialized QA:** Fachspezifische Lint-, Schema-, Render-, Dokument-, Laufzeit-, Geometrie-, Medien- oder Formatprüfungen.
15. **Preview Engine:** Echte Vorschau aus erstelltem Artefakt (oder sichtbare Kennzeichnung, wenn Vorschau unmöglich).
16. **Revision Loop:** Gezielt änderbare Artefakte, Diff/Versionen, Undo/Redo nach realer Unterstützung, QA nach Änderungen erneut ausführen.
17. **Export & File Handoff:** Wirklich existierende Dateien, prüfbare Pfade/Checksummen, Formate, Importierbarkeit und Download-/Öffnen-Aktion.
18. **Human Approval Gateway:** Aktionsgebundene Freigaben für Senden, Bezahlen, Publizieren, Geräte/Maschinen, Sicherheit, Boot und andere Seiteneffekte; Ablehnung/Timeout sicher behandeln.
19. **Project Isolation, Security & Forensics:** Sandbox, Berechtigungen, Redaction, Protokolle und die bestehende 120-Stunden-Forensikregel ohne Regression.
20. **Testing, Telemetry & Recovery:** Unit-, Integrations-, E2E-, Sicherheits-, Abbruch-/Recovery-Tests und nachvollziehbare Betriebsdiagnose ohne sensitive Datenlecks.

## 4. Architekturvertrag (am Codebestand ausrichten)

Nutze vorhandene Patterns und DI-Lifecycle der Anwendung. Die folgenden Typen sind **semantische Soll-Schnittstellen**, keine Behauptung über bestehende Klassennamen:

```text
StudioPromptRequest
  requestId, projectId, studioId, prompt, attachments[], locale,
  requestedAction?, privacyMode, provenance
StudioActionSpec
  schemaVersion, studioId, actionId, parameters{}, requiredInputs[],
  outputKinds[], sideEffectClass, capabilityRequirements[]
StudioExecutionContext
  projectWorkspace, userIdentity, effectivePermissions,
  providerConsent, cancellationToken, resourceBudget
StudioActionPlan
  normalizedRequest, validationFindings[], clarificationQuestions[],
  steps[], approvalRequirements[], estimatedResources?
StudioArtifact
  artifactId, projectId, format, filePath, editability,
  producer, dependencies[], sha256?, version, previewRef?, qaReportRef?
StudioExecutionResult
  status, stepEvidence[], artifacts[], errors[], warnings[],
  approvals[], qaResults[], timestamps
```

Konzipiere eine **gemeinsame Workflow-Orchestrierung** mit `IStudioActionAdapter` (oder existierendem Äquivalent), `IStudioCapabilityProvider`, `IArtifactValidator`, `IApprovalService` und `IArtifactStore`. Adapter müssen Fähigkeiten explizit deklarieren; fehlen sie, darf der Auftrag **nicht** erfolgreich enden. Vorhandene Services bevorzugen, keine parallelen Datenbanken, Promptstores oder KI-Provider-Manager einführen.

**Persistenter Zustandsautomat:** `Draft → Validating → NeedsClarification | Planned → NeedsApproval | Executing → Produced → QA → Verified | RevisionRequested | Failed | Unsupported`; Abbruch von ausführbaren Zuständen aus ermöglichen. Freigabestatus muss vor jedem risikoreichen Side Effect **erneut** durch vertrauenswürdigen Code geprüft werden (TOCTOU vermeiden). Retries dürfen externen Seiteneffekt nicht doppelt ausführen; Idempotenzschlüssel und deduplizierte Aktionen.

**Security Boundary:** Das Modell liefert Daten bzw. vorgeschlagene Aktionen, keine Shell-, Browser-, Git-, Zahlungs-, Geräte- oder Netzwerkprivilegien. Tool-Aufrufe nur durch registrierte, parametergeprüfte und autorisierte Fachadapter. Externe Dokumente/Prompts sind untrusted; Prompt Injection darf keine Freigabe ersetzen.

## 5. Abdeckung aller Studioeinträge

1. Enumeriere **alle im aktuellen `StudioCatalog` tatsächlich gefundenen Einträge** mit stabiler `studioId`, UI-Titel, Kategorie, existierenden Services, vorgeschlagenen Aktionen, erlaubten Eingaben, realem Adapter und Outputformaten.
2. Erzeuge daraus `studio-capability-matrix.md` als **vollständige, überprüfbare Abdeckungsmatrix**. Zieleinträge sind nach Projektvorgabe 54; stimmt der Code nicht überein, melde die Differenz und wahre die echten IDs.
3. **Kein blindes „alle 54 funktionsfähig“.** Je Studio Capability-Reife explizit ausweisen: `ready`, `partial`, `blocked`, `not_implemented`; `ready` nur mit real ausgeführtem End-to-End-Test.
4. Baue die gemeinsame Pipeline so, dass alle Studios Prompt-Erfassung, Validierung, Status, Rückfragen, Vorschau/Artefaktlinks (soweit zutreffend) und Fehlermeldungen verwenden; fachspezifische Ausführung nur dort freischalten, wo ein realer Adapter vorhanden ist.
5. Keine 54 duplizierten KI-Prompts als Ersatz für funktionierende Studio-Adapter. Profile dürfen Daten/Regeln enthalten, aber der gemeinsame Kern bleibt geteilt.

## 6. Priorisierte vertikale Implementierung

**P0 – Briefstudio**  
Vorhandene Briefdatenmodelle wiederverwenden. Empfänger/Absender, Betreff, Form, Anrede, Sprache, Brieftext, Datum, Unterschriftenplatz, Layout prüfen; strukturierter editierbarer Brief, echte Vorschau, PDF/DOCX-Export **nur wenn im System nachweislich unterstützt** bzw. durch überprüften Exportadapter. Test: Brief erzeugen, ändern, speichern, neu öffnen und Export auf Dateiexistenz und Inhalt prüfen.

**P0 – Office**  
Dokumente, Tabellen, Präsentationen nur mit echten vorhandenen bzw. integrierten Bibliotheken; editierbare Quelldateien; Formeln über Tabellen-Engine evaluieren, keine frei erfundenen Ergebnisse. Re-Open-Tests, Formatprüfungen, Formeln und Export-Checks.

**P0 – E-Mail**  
Entwurf, Empfänger, Betreff, Anhänge, Vorschau; keine Nachricht ohne konkrete Sendefreigabe verschicken. Versandprovider/Konto nur nach verifizierter vorhandener Integration und Berechtigung; nachweisbare Message-ID oder präziser Fehlerstatus. Idempotenz.

**P0 – Business**  
Kunden-/Angebots-/Rechnungs-/Analyseartefakte nur mit belastbaren Datenquellen und validen Rechenregeln; Zahlungs- und steuerrelevante Beträge deterministisch mit Decimal/Einheiten/Währungen; keine ungeprüfte Rechts-/Steuerberatung, keine Rechnung als „gebucht“ markieren ohne tatsächliche Transaktion. Zahlungsaktionen extra freigeben.

**P1 – Software**  
Projektgebundene Quelltextänderungen über diffbasierte Vorschläge, Tests, Build und Review; kein eigenmächtiger Push/Merge/Deployment. Kontrollierte Workspaces, Syntax-/Buildprüfungen, Git-Status, keine Überschreibung fremder Arbeit.

**P1 – 3D / Blender**  
Vorhandene Blender-Adapter nutzen, modellierbare Szenendatei erzeugen, Geometrie/Materialien/Koordinaten/Skalen prüfen, Blender-Protokolle und gerenderte Vorschau nur bei tatsächlichem Lauf ausgeben. Kein „fotorealistisch“ ohne messbare Prüfung bzw. Nutzerabnahme.

**P1 – Games / Unreal**  
Bestehende Unreal-Schnittstelle und Projektisolation verwenden; Assets importieren, tatsächliche Projektänderungen dokumentieren; automatisierte Build-/Asset-Validierung und optional Editor-/Playtest-Nachweis, soweit verfügbar. Keine AAA+- oder Spiel-fertig-Behauptungen ohne reale Qualitäts- und Funktionsabnahme.

**P1 – Medien**  
Bild, Audio, Video über echte lokale Werkzeuge/Adapter; Eingangslizenzen, Render-/Transcode-Logs, Format, Laufzeit, Abspielbarkeit und Artefaktexistenz prüfen; kein fingierter Renderstatus.

**P2 – restliche katalogisierte Studios**  
Profile, Schemas und Adapter nach Fachgruppe schrittweise ergänzen. Ingenieurwesen, Elektronik, Wissenschaft, CNC, Transport, Gesundheit o. ä. nur mit geeigneten Fachprüfern, nachvollziehbaren Modellannahmen und klarer Trennung von Entwurf und sicherheitskritischer physischer Ausführung. Ohne geeignete Prüfmittel: `blocked`/`partial`, keine unkontrollierten Maschinenbefehle.

## 7. Sicherheits- und Freigabematrix

| Operation | Vor Ausführung erforderlich | Ohne Freigabe |
|---|---|---|
| Nur lokaler Entwurf / Vorschau / validierte Berechnung | Normale Projektberechtigung | Darf gemäß Projektpolicies laufen |
| E-Mail, Messenger, Kontaktaufnahme | Empfänger, Inhalt, Anhänge, Versandkonto und einmalige Sendefreigabe | Entwurf halten |
| Zahlung, Bestellung, finanzielle Buchung | Betrag, Währung, Empfänger, Konto, Transaktionsdetails und explizite Aktionfreigabe | Keine Transaktion |
| Veröffentlichung / Upload / Deployment / Git-Merge | Zielsystem, Sichtbarkeit, konkrete Änderung und Freigabe | Lokal bleiben |
| Löschen, Datenexport, Rechteänderung, Sicherheit | Exakte Ressource, Folgen und erhöhte Bestätigung | Blockieren |
| Boot, Treiber, OS-Konfiguration, Geräte, Robotik, CNC, Fahrzeug, Maschine | Eignungs-/Interlock-Prüfung, menschliche Freigabe und vorhandener sicherer Controller | Keine physische Aktion |
| Externer KI-Anbieter | Informierte opt-in-Einwilligung, Datenumfang, Kosteninformation | Nur lokal/unsupported |

**Wichtig:** Ein Dialogfeld allein ist kein Sicherheitsmechanismus. Freigaben sind einmalig bzw. eng begrenzt, rollen-/projektgebunden, serverseitig verifiziert, mit Ablauf, Widerruf und Auditspur. Keine Dauerfreigabe aus einem einzigen vagen „Ja“ ableiten.

## 8. UI-/UX-Vorgaben für Trendo OS

- Bestehende Avalonia-/Desktop-Navigation und visuellen Stil integrieren, keine eigenständige zweite App.
- Gleicher Ablauf in allen Studios: Promptfeld, aktuelle Studio-Fähigkeiten, Auftragsübersicht, Rückfragen, Freigabeansicht, Fortschritt, editierbares Ergebnis, QA-Befunde, Vorschau, Datei/Export.
- Frei lesbare Zustände `Noch nicht gestartet`, `Rückfrage`, `Freigabe nötig`, `Wird wirklich ausgeführt`, `Datei erstellt`, `QA bestanden`, `Fehlgeschlagen`, `Nicht unterstützt`.
- Keine blockierende UI; Cancel/Retry bei sicheren Aktionen, verständliche Fehlermeldungen, Tastaturbedienung, Screenreader-Labels, ausreichend große Interaktionsziele.
- Fähigkeit pro Studio transparent markieren. Nicht implementierte Aktionen nicht als funktionsfähige Schaltflächen darstellen.
- Keine realitätsfernen Fortschrittsbalken: nur messbarer Fortschritt oder indeterminierter Aktivitätsindikator.

## 9. Implementierungsphasen mit konkreten Ergebnissen

### Phase A – Pflicht-Audit, bevor Code geändert wird
- Repo/Branch/Commit-SHA dokumentieren; Build-Anweisungen, Tests, CI, Architektur, `StudioCatalog`, `PromptLibrary`, `Intel.AskAsync`, Briefmodelle, Blender/Unreal, Projektisolation und Forensikregeln suchen.
- Ausgangszustand builden und bekannte Fehler festhalten.
- `docs/improvement-42/repository-audit.md`, `studio-capability-matrix.md`, `architecture-decision.md` erstellen.
- Entscheidungsmatrix prüfen: 23/25/34/Language Studio 2.0 = NEIN; 35 = offen; 41 und alle JA bleiben erhalten.

### Phase B – Gemeinsamer Engine-neutrale Workflow-Kern
- Schemas, Validation, Statusautomat, Clarification, Capability Registry, Artifact Persistence, Cancellation, Evidence, Approval-Gateway und QA-Pipeline implementieren.
- Bestehendes KI-System nur per Adapter/Service nutzen. Migrationsarme Integration und rückwärtskompatible Schnittstellen bevorzugen.
- Persistenzänderungen versioniert mit Rollback-/Migrationspfad.

### Phase C – Acht priorisierte Studiogruppen
- Briefstudio → Office → E-Mail → Business → Software → 3D → Games → Medien.
- Pro Gruppe mindestens einen echten vollständigen Happy-Path und einen Failure-/Revision-/Approval-Path nachweisen; keine reine Mock-Abnahme.
- Bei fehlender externer App realen `unsupported`-Fall statt Fake-Ergebnis zeigen.

### Phase D – Vollständige Studioabdeckung und Härtung
- Alle katalogisierten Einträge in gemeinsamer UX und Matrix registrieren.
- Restliche fachspezifische Adapter nach vorhandenen Abhängigkeiten umsetzen; verbleibende Blocker namentlich dokumentieren.
- Sicherheits-, Berechtigungs-, Offline-, Kosten-, Projektisolations- und Forensik-Regressionssuite ausführen.

### Phase E – Übergabe
- Änderungen auf separatem Feature-Branch, nachvollziehbare Commits, PR mit Screenshots/Evidenzen sowie Test-/Build-Ausgaben.
- Keine unaufgeforderte Veröffentlichung/Deployment, kein direkter Merge ohne Freigabe.
- Bedienungsanleitung, Entwickleranleitung, Capability-Matrix, offene Punkte und Abnahmeprotokoll mitliefern.

## 10. Obligatorische Prüffälle

1. Brief erzeugen → speichern → ändern → wieder öffnen → echte Exportdatei prüfen.
2. Office-Tabelle mit bekannten Testdaten/Formeln: deterministisch erwartete Ergebnisse stimmen überein.
3. E-Mail ohne Sendefreigabe: exakt **null** versendete Nachrichten; mit Freigabe exakt eine und belegbar.
4. Cloudprovider ohne opt-in: kein Cloud-Request; verweigerte/entzogene Einwilligung bleibt wirksam.
5. Widersprüchlicher oder unvollständiger Auftrag führt zu gezielter Rückfrage, nicht zu geratenem Ergebnis.
6. Studio ohne Adapter: `unsupported`, keine Phantom-Datei, keine falsche Erfolgsmeldung.
7. Benutzer startet zwei Projekte: keine Artefakt-/Kontext-/Secret-Übertragung zwischen Workspaces.
8. Abbruch, Crash, Neustart und Retry: konsistenter wiederherstellbarer Zustand; kein doppelter Side Effect.
9. Manipulierter Anhang mit Prompt Injection umgeht weder Berechtigungen noch Freigaben.
10. Technische Berechnung: dokumentierte Einheiten, Grenzfälle, reproduzierbare deterministische Werte; KI-Abweichungen führen zu QA-Fehler.
11. Blender-/Unreal-Test: nur bei real verfügbarer Toolchain; Datei und Log nachweisen, sonst sauber `blocked`.
12. Änderungen an bestehenden Modulen 41 sowie 23/25/34/Language Studio 2.0/35 auf Regression bzw. unerlaubte Aktivierung prüfen.
13. 120-Stunden-Forensikregel anhand vorhandener Spezifikation inkl. Zugriff, Ablauf und Löschverhalten testen; keine selbst erfundene Semantik.
14. Risikoaktionen Boot, Maschine, Sicherheit, Zahlung, Publikation, Versand ohne Freigabe sicher blockieren.
15. Capability-Matrix muss jede echte `StudioCatalog`-ID genau einmal enthalten, keine Phantom-IDs und keine fehlenden IDs.

**Abnahmeprinzip:** Jeder Test erhält `passed`, `failed`, `blocked` oder `not_run`, reproduzierbaren Befehl, Testumgebung und Nachweise. `blocked` ist **nicht** `passed`.

## 11. Konkrete Liefergegenstände

- Produktionscode auf einem dedizierten Branch für Verbesserung 42, ohne bestehende Systeme auszutauschen.
- `docs/improvement-42/repository-audit.md`
- `docs/improvement-42/architecture-decision.md`
- `docs/improvement-42/studio-capability-matrix.md`
- `docs/improvement-42/security-approvals.md`
- `docs/improvement-42/test-and-evidence-report.md`
- `docs/improvement-42/user-guide.md`
- Nötige Migrationen, Konfiguration, verifizierte Tools/Abhängigkeiten, Unit-/Integrations-/E2E-Tests.
- PR/Änderungsübersicht mit tatsächlichen Pfaden, Commit-SHA, Build-/Testergebnissen, screenshots/evidence nur bei realer Erzeugung.
- Restblocker mit klarer Ursache, Auswirkungen und nächstem konkreten Entwicklungsschritt.

## 12. Fertigdefinition (Definition of Done)

Verbesserung 42 darf als **vollständig implementiert** bezeichnet werden, wenn **alle** folgenden Aussagen durch Artefakte und Tests belegbar sind:

- Ein gemeinsamer Prompt-to-Action-Workflow ist in die bestehende UI integriert und benutzt bestehende KI-/Prompt-/Projektservices statt Duplikaten.
- Alle tatsächlich vorhandenen Studioeinträge sind vollständig inventarisiert, profiliert und in der Matrix erfasst.
- Die acht priorisierten Studiogruppen verfügen über reale, verwendbare Fachadapter mit echter End-to-End-Ausführung und editierbarem Ergebnis, soweit die benötigten Tools installiert und zulässig sind.
- Jede als `ready` markierte Studioaktion besteht Validierung, QA, Vorschau und Artefaktprüfung.
- Verbleibende Studioaktionen stehen transparent auf `partial`/`blocked`/`not_implemented`; keine Gesamtfertigmeldung solange geforderte Funktionen noch fehlen.
- Lokale KI wird bevorzugt; kein Cloudzugriff ohne Zustimmung; risikoreiche Seiteneffekte sind technisch durch Freigaben abgesichert.
- Deterministische Fachprüfungen und Testnachweise liegen vor; keine erfundenen Ergebnisse.
- Bestehende Entscheidungen/Module und die 120-Stunden-Forensikregel bestehen die Regressionstests.
- Build, Tests und PR-Übergabedokumentation sind nachvollziehbar; bekannte Blocker offen ausgewiesen.

## 13. Exakte Ausgabeanforderung an Claude Code

Starte mit einem knappen **Ist-Stand-Audit mit echten Repository-Pfaden und Commit-SHA**, anschließend implementiere den Code inkrementell. Zeige nach jeder Phase:

```text
Phase:
Geprüfter Branch/Commit:
Reale geänderte Dateien:
Wiederverwendete vorhandene Komponenten:
Echte funktionierende Aktionen:
Blockierte/nicht implementierte Aktionen:
Sicherheits-/Freigabeprüfung:
Build- und Testbefehle:
Tatsächliches Testergebnis / Logs / Artefaktpfade:
Bekannte Fehler / nächste Phase:
```

**Beginne mit Phase A und setze danach Phase B–E um, soweit Codezugriff, Entwicklungsumgebung und Werkzeuge dies tatsächlich zulassen.** Frage nur bei echten fachlichen oder sicherheitsrelevanten Blockern; niemals künstlich stoppen, weil eine einzelne Pfad- oder Klassenbezeichnung vom Auftrag abweicht. Dokumentiere jede Abweichung ehrlich. Keine Behauptung, die Software sei komplett fertig, bevor die obige Definition of Done tatsächlich erfüllt ist.

---
**Ende des autorisierten Build-Auftrags für Verbesserung 42.**
