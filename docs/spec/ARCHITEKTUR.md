# Trendo OS – Technische Zielarchitektur (kein fertiger Code)

## Architekturdiagramm
```text
UI (Desktop, Widgets, Studio Views, Accessibility, Android Companion)
   |
   v
Trendo API Gateway (versioniert, authenticated, local-first)
   |-- Identity / Session / Role / Approval Broker
   |-- Project / Workspace / Secrets / Settings / Audit
   |-- Memory / Decision Registry / File Index / Retention
   |-- Workflow Scheduler / Event Bus / Agent Tasks
   |-- Studio Registry / Capability Matrix / Health Checks
   |-- App Manager / Package Manifest / Release Status
   |
   +--> Windows Adapter ----> authorized Windows apps/hardware/files
   +--> Linux Adapter ------> KDE/Wayland or compatible desktop, packages/devices
   +--> Pi Bridge ----------> Windows compute-node agent (authenticated)
   |
   +--> Studio plug-ins (3D / Games / Apps / Office / Media / ...)
                  |
                  +--> Program-specific adapters (Blender/Unreal/LibreOffice/Browser...)
```

## Vertrauensgrenzen
- **UI ist niemals ein Sicherheitsanker.** Berechtigungen werden in einem tatsächlich durchsetzenden lokalen Dienst geprüft, auch wenn eine Anfrage von Grok Build, CLI, Plugin, Browser oder Benutzeroberfläche kommt.
- **OS-Adapter** führen nur streng beschränkte erlaubte Operationen aus; riskante Prozesse haben explizite Nutzerbestätigung und OS-seitiges Privilege-Escalation-Verfahren.
- **Projekte/Agenten:** eigene Pfadwurzel, sichere IDs, kein freier Zugriff über relative Pfade (`../`), allowlisted Integrationen; Rechte serverseitig prüfen.
- **Windows-PC-Rechenknoten / Pi:** wechselseitig authentifiziert, verschlüsselte Kommunikation, kein offener Admin-Endpunkt, eingeschränkte Firewall/Pairing.
- **Browser:** privates menschliches Profil getrennt von Automation; Research READ-ONLY vom Rest in eigener technischen Sicherheitsdomäne.
- **Inhalte von außen:** niemals als System- oder Agentenanweisungen interpretieren; Prüfergebnisse mit Quelle/Zeit/Status.

## Verbindliche Schnittstellenverträge – beispielhaft (erst implementieren und versionieren)
- `GET /api/v1/system/capabilities`: Edition, Kompatibilität, Integrationsvoraussetzungen, tatsächlich gemessener Status.
- `GET /api/v1/studios`: registrierte Studios mit APPROVED_SPEC/IMPLEMENTED/TESTED/ACCEPTED.
- `POST /api/v1/tasks/plan`: Planung, unverbindlich; keine Host-Aktion.
- `POST /api/v1/approvals`: Zustimmung mit Task-ID, konkreten Scopes, Ablaufzeit und auditierbarem Intent.
- `POST /api/v1/tasks/{id}/execute`: Ausführung nur nach prüfbarer Berechtigung.
- `GET /api/v1/tasks/{id}/events`: real erfasste Job-Ereignisse, keine simulierten Erfolgswerte.
- `POST /api/v1/installations/plan`: Lizenz, Signatur, Disk/Arch/OS-Kriterien prüfen.
- `POST /api/v1/installations/{id}/approve`: Host-Installation ausschließlich mit spezifischer Freigabe.
- `GET /api/v1/memory/projects/{project_id}/sources`: nur innerhalb autorisierten Projekts.

**API-Beispiele sind Entwürfe**, keine bereits verfügbaren Endpunkte. In der Implementierung OpenAPI, JSON-Schema, Request-/Response-Validierung, RBAC und Regressionstests erzeugen.

## Kern-Datenmodell (Empfehlung)
`users`, `roles`, `projects`, `project_memberships`, `project_paths`, `decisions`, `requirements`, `studios`, `studio_versions`, `capabilities`, `assets`, `sources`, `agent_tasks`, `task_events`, `approvals`, `installation_plans`, `software_manifest`, `audit_events`, `retention_subjects`, `test_runs`, `release_artifacts`.

Datenbankmigrationen versionieren. SQLite für lokalen Single-Node-Start mit geeigneten Transaktionsregeln; auf Pi-/PC-Verbund koordinierte Kommunikation, keine unkontrollierte Mehrfachschreiberei auf Netzwerkfreigaben. Verschlüsselte Speicher/Secrets über OS-spezifische sichere Stores bzw. geeignetes Key Management; keine hartcodierten Schlüssel.

## Edition-spezifische Einschränkungen
**Windows:** Systemdialoge und Rechte respektieren, keine Annahme administrativer Dauerberechtigung. Portable Programme nur bei Lizenz-/Architekturtauglichkeit.
**Linux USB/SSD:** UEFI/secure boot viability, Storage Encryption, Boot/Update/Rescue; Windows-Binärdateien nicht als nativ Linux lauffähig deklarieren.
**Pi 4:** RAM/CPU begrenzt, ARM statt x64, Vollstudios auf Windows Compute-Node ausführen, Recovery bei Host-Offline.

## Plattformkompatibilität
Jede Studiofunktion hat einen Datensatz aus `edition`, `required_tools`, `runtime_arch`, `permissions`, `offline_behavior`, `license`, `security_class`, `test_cases`, `minimum_resources`, `known_limitations`.

## Ereignis- und Aufgabenverarbeitung
`task_created` → `plan_ready` → `approval_requested` → `approved_or_denied` → `started` → `progress` → `passed|failed|blocked` → `archived_by_policy`. Bei Aufgaben mit äußerer Wirkung sind idempotente IDs und explizite Replay-Strategien nötig. Aus Netzwerkabbruch darf kein zweiter Versand/zweite Zahlung resultieren.

## Architekturentscheidungen zunächst als ADR dokumentieren
`ADR-001 editions`, `ADR-002 UI toolkit`, `ADR-003 local service runtime`, `ADR-004 storage/encryption`, `ADR-005 permissions`, `ADR-006 studio SDK`, `ADR-007 package management`, `ADR-008 Linux distro/boot`, `ADR-009 Grok Bot/Build integration`, `ADR-010 retention`, `ADR-011 backup recovery`.
Keine Framework-Auswahl als endgültig behaupten, bevor auf Zielhardware ein testbarer Vergleich durchgeführt wurde.

## Online Business Creation & Innovation Studio (genehmigt 30/30, beide Modi)

**Status APPROVED_SPEC; kein implementierter Business-Code.** Studio `studio.online_business` als Plugin/Capability implementieren, nicht als fremdes eigenmächtig startendes SaaS. Gemeinsame bereits bestätigte Dienste wiederverwenden: Business & Finance, App Development, UX/Media, Browser (regulär), Kommunikationscenter, Payment-Approval, Security Broker, Memory, Workflows/Grok Build. Deep-/Dark-Web READ-ONLY bleibt strikt vom Business-Netzwerkzugriff getrennt.

Vorgesehenes (noch nicht implementiertes) Prozessmodell: `DISCOVERY -> MODEL_DEFINED -> VALIDATED -> GO_NO_GO_APPROVED -> SPEC_LOCK -> BUILD_APPROVED -> IMPLEMENTED -> TESTED -> COMPLIANCE_REVIEWED -> DEPLOY_APPROVED -> ACCEPTED -> RELEASED`.

Entitäten als Entwurf: `business_ideas`, `business_models`, `business_projects`, `business_research_sources`, `business_hypotheses`, `validation_runs`, `business_decisions`, `business_plans`, `revenue_scenarios`, `business_tenants`, `payment_provider_configs`, `billing_events`, `support_tickets`, `deployment_plans`, `business_qa_runs`. Ein konkretes Geschäft benötigt nur das dafür passende Modell. Zahlungs-/Identitätsdaten gehören niemals im Klartext in die Modelldatenbank.

Jeder externe Vorgang benutzt den **zentralen Trendo Approval Broker** mit möglichst kurzlebiger Approval-ID, Ziel, konkretem Aktions-Typ und Umfang. Der Browser/Grok Build-Agent darf nicht selbst per direkten HTTP-Call den Gate umgehen. Gegen Double Charge/Double Send Event-IDs/Idempotency und wirksame providerseitige Rechte prüfen.

Projekt-/Mandanten-/Quellenisolierung real auf Datei-, DB- und API-Ebene durchsetzen. Marktforschung ausschließlich mit gekennzeichneten Quellen und Datumsangaben. Offlinefähigkeit: Planung/Entwürfe lokal; externe Live-Services und Remote Provider benötigen Verbindung. Änderungen an echten Produktivsystemen bedürfen separater, sichtbarer Freigabe.

## Letter, Business & Legal Correspondence Studio (genehmigt 25/25)

Status `APPROVED_SPEC`, Implementierung `NOT_STARTED`. Studio-ID `studio.letter_correspondence` und Anforderungen `LETTER-001..025`.

**Architektur und abhängige Services:** Entwürfe und Schreiblogik als separates Studio-Plugin; reale Dokumentgenerierung über Office-Adapter (DOCX/ODT/PDF), OCR über den bereits vorhandenen Scanner/OCR-Adapter, verifizierte Erinnerungen über Kalender, Fallakten über Memory/Project Store, externe Kommunikation über den zentralen Approval Broker und Communication-Adapter. Keine direkte externe HTTP-/Mail-/Portalaktion aus dem Letter-Plugin.

**Vertrauens- und Prozesszustände:** `CASE_CREATED -> INTAKE -> FACTS_CHECKED -> DRAFT -> REVIEW -> EXPORTED -> READY_FOR_APPROVAL -> AUTHORIZED -> DELIVERY_ATTEMPTED -> DELIVERY_CONFIRMED|FAILED|BLOCKED`. Separates `source_verified`, `deadline_verified`, `legal_review_needed` und `filing_channel_verified`. Ein PDF-Export impliziert keine Zustellung.

**Vorgeschlagene Datenobjekte:** `letter_cases`, `letter_parties`, `letter_facts`, `letter_versions`, `letter_templates`, `letter_attachments`, `letter_source_checks`, `letter_deadline_candidates`, `letter_delivery_plans`, `letter_approvals`, `letter_delivery_evidence`. Namen sind Planungsentwurf, keine existente DB.

**Berechtigung:** Agenten dürfen nur freigegebene Fälle lesen; sensible Briefdaten nicht projektübergreifend verwenden; externe Nachricht/Einreichung nur nach frischem, ziel-/hash-/empfänger-/anlagengebundenem Approval. Eingehende Briefe und OCR-Ausgaben sind untrusted Daten; Makros nicht ausführen. Gerichtliche Form, Vertretungsbefugnis und Zulässigkeit müssen fallbezogen recherchiert oder zur anwaltlichen Prüfung vorgelegt werden.

**Qualitätsnachweise:** echte verifizierte DOCX/ODT/PDF-Artefakte plus `LETTER_QA_MATRIX.csv` mit `NOT_RUN`-Start; forensische temporäre Derivate (insb. OCR-Text, Suchindex, Backup) mit 120h Maximalfrist. Optionaler Einzelbrief in einer weiteren Sprache aktiviert ausdrücklich nicht die abgelehnte allgemeine Language-&-Localization-2.0-Erweiterung.


## Grok Bot + Grok Build operative Integration (verbindliche Ergänzung v2.0)
Grok Bot plant auf einem CLOUD-Rechner, auf dem verschiedene Bots desselben Nutzers ein gemeinsames Dateisystem/Konto teilen; keine Forensikdaten, keine Secrets, keine vertraulichen Projektdaten in diese Cloud. Lokaler Grok Build arbeitet in einem eingeschränkten Windows-/Linux-Projektordner und kommuniziert nur über eine explizit genehmigte Übergabedatei oder einen noch zu entwickelnden signierten, scoped, authentifizierten Local-Task-Broker.
Separate Adapter für Blender-Python und UE5-Editor-Scripting bzw. tatsächlich geprüfte MCP-Server, keine automatische Behauptung vorhandener Server. Erst `grok inspect` / `grok mcp doctor` (falls verfügbar) und echter `tools/list`/Read-Test. Netzwerk-/Secret-Grenzen und 120h-Forensikregel sind verpflichtende Negative-Tests. Details: `GROK_ARCHITEKTUR_AGENTEN_MCP.md`.
