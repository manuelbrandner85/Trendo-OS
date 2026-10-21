# TRENDO OS — SYSTEMBIBEL v2.0 (Anforderungen und Architekturziel)

**Stand: 2026-10-21 · Status: Produktionsvorbereitung / Plan, nicht implementiert**

## 1. Identität und Ziel
**Trendo OS** soll ein einheitliches, lokales, KI-unterstütztes Arbeits- und Produktionssystem für Desktop, Softwareentwicklung, Game Development, 3D, Medien, Office, Kommunikation, Recherche, Technik und Spezialstudios sein. Der KI-Assistent heißt **Trendo**. **Grok Build** wird als vom Nutzer autorisiertes Produktionswerkzeug auf tatsächliche Dateien, Builds, Blender, Unreal, Office- und Browserintegrationen angesetzt; Vorschläge und Testergebnisse müssen voneinander unterschieden werden. Als Qualitätsmerkmal gelten *reale, prüfbare Ergebnisse*, nicht scheinbar aktive Dashboards.

Trendo OS ist NICHT mit einem neu geschriebenen OS-Kernel gleichzusetzen: Die bootbare Edition basiert auf kompatiblem Linux; unter Windows ist Trendo eine eigenständige App mit systemgerechten Rechten; in der Raspberry-Pi-Ausgabe ein leichter Koordinator mit einem PC-Rechenknoten.

### Produktprinzipien
- **Ein Produkt, drei Editionen.** Gemeinsamer fachlicher Kern, editionsspezifische Betriebssystemadapter.
- **Ready-to-Run zuerst.** Im ausgelieferten Paket vorhandene, kompatible und legal mitlieferbare Werkzeuge sollen ab Erststart funktionieren. Fehlende proprietäre Komponenten korrekt als optional / separat installieren markieren.
- **Lokaler Betrieb zuerst.** Basisfunktionen offline ohne zusätzliche kostenpflichtige KI-API; optionale Internetdienste nur mit Berechtigung, Datums-/Quellenhinweisen und Lizenzprüfung.
- **Datensparsam und projektspezifisch.** Jedes Projekt hat eine eigene isolierte Wissens-, Datei- und Agentendomäne.
- **Menschen entscheiden.** JA/NEIN als verbindliches Decision Registry; nur ausdrücklich angenommene Vorschläge dürfen produktiv wirksam werden.
- **Wahrheitsgemäße Statusmeldungen.** Geplant, gebaut, getestet, abgenommen und veröffentlicht sind getrennte Stati.

## 2. Bestätigte Editionsmodelle
### Edition 1 — Raspberry Pi 4 (8 GB) + Windows-PC
Pi ist Steuer- und Koordinationsgerät; Windows-PC stellt CPU/RAM/GPU und installierte Windows-Anwendungen für freigegebene Aufgaben bereit. Kein nativer Start von Windows-x64-Programmen auf ARM-Pi versprechen. Netzwerkverbindung und Rechte können fehlen: entsprechende Degradation und Offlineverhalten vorsehen.

### Edition 2 — bootbares USB-/SSD-Trendo Full Studio / Lite
Eigenständig von geeigneter externer SSD/USB unter Linux booten, **ohne Pi**. Linux-Desktop mit Trendo-Kern, persistentem und soweit kompatibel verschlüsseltem Speicher, erlaubten internen/externalen Laufwerkszugriffen und vorinstallierten legalen Programmen. UEFI, Secure Boot, BitLocker, vorhandenes Windows und Hardwarekompatibilität schützen. Ein Live-System ohne Persistenz genügt nicht für Full Studio. Nutzerpräferenz: **Full Studio auf externer SSD**.

### Edition 3 — native Windows-App
Trendo läuft als eigenständige Anwendung auf einem Windows-PC **ohne Pi**, verwendet vorhandene Windows-Apps, passende portable Programme, Hardware und Dateien innerhalb erteilter Berechtigungen. **Nicht** die Windows Shell, Taskleiste, Treiber, Registrierungsdatenbank oder Startoptionen ohne ausdrückliche Zustimmung ersetzen oder ändern.

## 3. Systemschichten
1. **Trendo UI:** hochwertige einheitliche Oberflächen, Startmenü/Launcher, Workspaces, Desktop-Widgets, Status, Accessibility, adaptive Last.
2. **Trendo Core Service:** Projektverwaltung, Einstellungen, Edition-Erkennung, Task Registry, Capability Contracts, Storage, Workflow Scheduler.
3. **Governance & Security Broker:** RBAC/ACL, sichere Secrets, zeitlich/sachlich begrenzte Agentenfreigaben, Audit, Netzwerk-/Geräterechtkontrolle, Sicherheitsregeln.
4. **Memory & Knowledge:** versionierte bestätigte Entscheidungen, getrennte Projekt-RAG-/Suchindizes, Quellen, Timeline, Gültigkeit, Token-/Caching-Regeln.
5. **Automation & Agents:** Trendo als Auftraggeber, Grok Build als explizit autorisierter Ausführer; dokumentierte Tasks und Subagents, reale Status- und Testnachweise.
6. **Studio SDK & Adapters:** versionierte Plugin-Schnittstellen, Software-Adapter für Blender/Unreal/Office/Browser etc., keine unkontrollierten Studio-zu-Studio-Zugriffe.
7. **OS adapters:** Windows, Linux, Raspberry Pi+Windows-Brücke; Hardware, Prozesse, Audio, Kamera, Netzwerke, Dateisystem, Notifications, Paketverwaltung.
8. **Build/QA/Release:** CI, signierbare Pakete, Lizenzinventar, SBOM, Abnahmetests, Rollback.

## 4. Bestätigte grundlegende Hauptstudios und technische Inhalte
Die folgenden Funktionsfamilien sind als **Anforderungen** bestätigt (nicht als gebaut). Maßgebliche Detailentscheidungen bleiben durch das Decision Registry zu erhalten.

- **Intelligent Desktop OS:** Universal Smart Workspace, Explorer, Fenster-/Multitasking, Startmenü, später umfassend erweitertes Adaptive Interface mit 25 bestätigten Funktionen.
- **Trendo Intelligence Core:** Taskplanung, nachvollziehbare Vorschläge, Agentensteuerung mit Benutzerfreigabe, lokal soweit möglich.
- **AI Agent Command Center:** ursprünglich 12 Agentenrollen, projektbezogene Trennung und tatsächliche Grok Build Jobs.
- **Pi/PC/Android Fusion Engine:** Remote-Status, Edition-Trennung, lokale Rechnerressourcen, Handy-Begleiter nur mit zulässigen Funktionen.
- **Ultimate 3D Creation Studio 2.0 (20):** 3D-Regie, Multiview-/orthographische Referenzen, echte editierbare Blender-Geometrie, Anatomie, Sculpt, Retopologie, Rig/Animation/Deformation, PBR/UV/UDIM, Stoff-/Physik-/Groom, Hard Surface/Geometry Nodes, Turntable, LOD, Performance, echte QA und verifizierter Unreal-/Godot-Transfer, Asset Vault.
- **Ultimate Game Development Studio 2.0 (20):** Projekt-/Game Bible, Story/Quests/Abhängigkeiten, Welt/NPC/Combat/Bosse, Animation/Cinematics/Audio/UI, Progression, Unreal-/Godot-Architektur, Grok-Build-Implementierung, Physik/Fahrzeuge, Saves/Migration, Multiplayer optional, Performance, echte Builds/Tests/Regressionen/DLC. Erst Vertical Slice, dann breiter Ausbau.
- **Ultimate App & Software Development Studio 2.0 (20):** Anforderungen, UI/UX, Frontend/Backend, DB/Migration, Auth/Rollen/Mandanten, erlaubte Integrationen/Payments, Grok-Build-Multiagenten, sichere Änderungen vorhandener Repos, Offline/Sync, Security, Tests, Debugging, Installer, Performance, Übergabe.
- **Ultimate Creative & Media Studio 2.0 (20):** Branding/Vektor/RAW-Bild, Videoregie/Schnitt/Color/VFX, Animation, Sprache/Audio/Musik/Mix, Rechte, editierbare Projekte, Export & echte QA. Geeignete Open-Source-Werkzeuge wie Krita/Inkscape/Blender/Audacity/Kdenlive/FFmpeg prüfen.
- **Vision & Robotics Studio 2.0 (20):** Kamera/Kalibrierung, Objekterkennung, Tracking, OCR, RGBD/SLAM/Sensorfusion, Simulationszwillinge, ROS2/Gazebo, Bewegungsplanung ausschließlich kontrolliert, ROS2-Security, echte Tests. **Keine autonom gefährlichen realen Aktoren, Bremssysteme oder Motorstarts; Simulation vor Hardware.**
- **Knowledge, Memory & Self-Improvement Core 2.0 (20):** getrennte Projektgedächtnisse, bestätigte Decisions, Quellen, Timeline, Suche, Knowledge Graph, Widersprüche/Verfallsstatus, Token/Delta-Kontext/Caching, Agentenwissen, verschlüsselte Sync, Lessons Learned, echte Evaluierung; Selbstverbesserungen nur vorgeschlagen und nach Zustimmung getestet/übernommen; Datenschutz/Retention/Audit.
- **Ultimate Prompt & Masterprompt GODMODE 2.0:** genehmigte 50 Funktionen und 40 Hauptkategorien; promptbezogene Recherche, Design, Qualitätsprüfung, Vergleich, Export, Reuse und niedriger Tokenverbrauch.

### Zusatz-/Spezialstudios mit bestätigten 2.0-Erweiterungen
Architektur & Interior, Electronics & PCB, Engineering & Simulation, VR/AR/MR, Science & Research, Education & Training, Fashion & Product, 3D Scanning & Reconstruction, Digital Twin, Business & Finance, Cybersecurity & Forensics, Maps & Geospatial, Manufacturing & CNC, Broadcast & Streaming, Transport & Vehicle. Sie werden als **optionale Plugins mit echten Geräte-/Plattformvoraussetzungen** integriert.

**Ausdrückliche Ablehnung:** Vollständige Erweiterung **Language & Localization Studio 2.0** wurde mit NEIN abgelehnt. Das bestehende Basisstudio bleibt; **die spezifische später bestätigte lokale deutsche Übersetzung im Deep-/Dark-Web-Lesestudio ist zulässig**, ohne die abgelehnte allgemeine 2.0-Erweiterung zu reaktivieren.

### Übergreifende bestätigte Erweiterungen
Software-Rekonstruktion nur legal im Clean-Room-Ansatz, Free AI Discovery & Fallback, Universal Creation & Productivity Suite, Ressourcen-/Studio-Manager, GitHub Open-Source-Integration, Free Asset Discovery, Mobile OS Bridge, Universal Studio Fusion & Autonomous Production, Zero-Touch Setup & Self-Healing, Visual Builder & No-Code, Extension Marketplace & Evolution Hub. Jedes Modul prüft Rechte, Lizenzen, Versionsstand und echte technische Unterstützung.

## 5. Bestätigte neuere Trendo-OS-Systemmodule
- **Triple-Mode OS GODMODE (20):** Linux-bootbares System, native Windows-App, Pi/PC-Fusion, gemeinsame UI/Kern/Backups/Security/QA.
- **Ultimate Desktop, Utilities & Media Center (25):** OS-Uhr/Zeit/Zeitzonen/Wecker, Widgets, Suche, Kalender/ICS/CalDAV, Reminder, lizenzkonforme Wetterdienste, RSS/Atom-Nachrichten samt Quelle/Datum, Trendo-Tagesbriefing, Offlinecache, Audio-/Videoplayer, Systemmixer, Benachrichtigungen, Quick Settings, Screenshot/Record mit Zustimmung, Vorschau, QA.
- **Ultimate Browser, Internet & Download Center (25):** installierter Chrome unter Windows, Chromium/optionales reguläres Chrome unter Linux, Tabs/Bookmarks/Suche, sichere Downloads/Upload/PDF/Web-Apps, getrennte Browserprofile, autorisierte Grok Build-in-Chrome- bzw. Playwright/DevTools-Integration, keine ungefragten externen Aktionen.
- **Ultimate App, Installation & System Control Center (25):** App-Katalog, Windows/Linux-Paketmanager, Installieren/Updates/Deinstallation nach Freigabe, CPU/GPU/RAM/Prozesse/Autostart/Treiber/Storage, Recovery/Diagnose/QA.
- **Ultimate Network, File Sharing & Remote Workspace (25):** LAN/WLAN, SMB/NFS/SFTP/NAS, Sync, abgesicherte Remote-Desktops/SSH/VPN, mobile Ansichten und Freigaben, Permissions/MFA, Monitoring, keine heimliche Fernsteuerung.
- **Ultimate Hardware & Device Recognition (20):** Bootscan, Hotplug, PCIe/USB/Bluetooth/Displays/Audio/Kameras/Drucker/Input/Android/Sensoren, Treiberdiagnose, Health/QA, keine ungefragten Firmware- oder Treiberänderungen.
- **Ultimate Deep Web & Dark Web READ-ONLY Research Studio (20):** LEGALER Lesemodus, geeigneter Standardbrowser oder offizieller Tor Browser für `.onion`, Forschungskategorien/Quellen, isolierte ephemere Sitzung, keine Käufe/Zahlungen/Formulare/Uploads/Downloads/Posts, keine unkontrollierte KI-Browserautomation; lokale deutsche Übersetzung ohne externe Seitenexfiltration, mit gesonderter Leseansicht falls im Tor Browser unsicher; Webseitenanzeige benötigt kurzfristigen Datenabruf.
- **Ultimate Communication, Mail & Collaboration (25):** E-Mail/IMAP/SMTP/OAuth, Kontakte/CardDAV, Messenger nur mit offiziellen Schnittstellen/Matrix, Konferenzen (z.B. Jitsi), Entwürfe, nichtautonomer Versand/Anruf/Sharing, Spam/Phishing, QA. Strikte Isolierung vom Dark-Web-Studio.
- **Ultimate Office, Documents & Productivity (25):** DOCX/ODT, XLSX/ODS/CSV-Formeln, PPTX/ODP, PDF/OCR/Scans/Signaturen mit Freigabe/Print, Dokumentenversionen, Vorlagen, echte Export-/Layout-/Formeltests; Lizenzbedingungen von LibreOffice/ONLYOFFICE/Tesseract etc. berücksichtigen.
- **Ultimate Ready-to-Run & Software Integration (25):** legale vorinstallierte Essentials; Full Studio auf externer SSD bevorzugt; Lite USB und Windows-App; bei fehlenden Werkzeugen Wahl (a) portabel in Trendo wo möglich (b) Installation auf PC nach Zustimmung (c) bereits vorhandene App anbinden; Manifest/Lizenz/Updates/Checks/Test.
- **Ultimate Adaptive Interface, Accessibility & Personalization (25):** Dark/Light/High-Contrast, Effects/Reduced Motion, HiDPI/Multi-Monitor, Workspace/Taskbar/Startmenü/Window Snap/Widgets/Sessions, Accessibility/Zoom/Screenreader/Touch/Sprache optional lokal, Benutzerprofile/Backup, Performance/Undo, echte UI-Tests.
- **Ultimate Universal Automation & Workflow Engine (20):** No-Code/Flows/Scheduler/Event Trigger/Conditions, Studio- und Grok-Build-Jobs, Rechte/Secrets/Approvals, Offlinequeue/Retries/Dedup/Checkpoint/Recovery/QA.
- **Ultimate System Architecture, Integration & Release Quality (25):** Systemarchitektur, Anforderungsregister, Core/Adapter, Isolation, Agenten, sichere Daten/Event Bus, drei Build-Ziele, Ready-to-Run/Compliance/SBOM, Forensik-TTL, CI/QA/Recovery, Release-Artefakte.
- **Ultimate Online Business Creation & Innovation Studio GODMODE (30):** Zwei verbindlich bestätigte Modi: **A** bestehende Online-Geschäftsmodelle recherchieren, prüfen, verbessern und vollständig individuell umsetzen; **B** neue Geschäftsideen entwickeln, Ähnlichkeiten/Wettbewerb und Machbarkeit prüfen, Hypothesen validieren und erst nach ausdrücklicher Nutzungs-/Entwicklungsfreigabe realisieren. Umfasst Markt- und Nischenanalyse, Wirtschaftlichkeitsmodelle, Business Blueprint, Marke/UX, Rechts-/Steuer-Prüfbedarf, Journey, echten Grok-Build-Full-Stack mit Datenbank, Auth/Mandanten, SaaS/Shop/Abo/Marktplatz, Sandbox-Payments/Billing, CRM/Admin, Fulfillment/White-Label, SEO/Funnel, Support, Analytics, Finanzcontrolling, Compliance, echte End-to-End-QA, Hosting/Domain/Deployment mit separater Freigabe, Monitoring/Backup sowie quelloffene dokumentierte Abnahme. **Keine Umsatz- oder absolute Neuheitsgarantie, keine eigenständigen Käufe, externen Nachrichten, Live-Zahlungen oder Veröffentlichungen.** Maßgebliches Detailregister: `STUDIO_ONLINE_BUSINESS_30.md` / `BUSINESS_REQUIREMENTS_30.csv`.

- **Ultimate Letter, Business & Legal Correspondence Studio GODMODE (25):** eigenständiges genehmigtes Briefstudio für private und geschäftliche Schreiben, Behörden/Ämter, Gerichte, Anwaltskommunikation, Versicherungen und andere rechtliche Korrespondenz. KI-Briefdirektor, DIN-5008-orientierte Layouts/Vorlagen, Ton, OCR-Eingangspost, Frageassistent/Sachverhalt, Chronologie/Belege/Anlagen, amtliche Quellen-/Form-/Fristenprüfung ohne erfundene Rechtslage, standardmäßig Deutsch und optional ausschließlich briefbezogene Fremdsprachenentwürfe, bearbeitbare DOCX/ODT und PDF/Print, Briefkopf-/Profilverwaltung, Konsistenz-/Fristencheck und bestätigte Kalendererinnerungen, Antwortentwürfe, geschützte Fallakten/Versionen, zulässige Einreichungswege und streng freigabegesteuerter Versand. **Kein automatischer Versand, keine ungesicherte Gerichtseinreichung oder fingierte anwaltliche Beratung.** Office, OCR, Kommunikation, Memory, Kalender und Approval-Broker wiederverwenden. Detailregister: `STUDIO_LETTER_CORRESPONDENCE_25.md`, `LETTER_REQUIREMENTS_25.csv`, `LETTER_QA_MATRIX.csv`.

## 6. Sicherheit: verbindliche übergreifende Regeln
1. Menschliche ausdrückliche Freigabe **vor** realen Nachrichten, Veröffentlichungen, LIVE-Starts, Zahlungen, Käufen, Uploads sensibler Daten, System-/Treiber-/Firmwareänderungen und kritischen Änderungen. Keine allgemeine Studio-Freigabe als Blankovollmacht.
2. Keine gefährliche autonome Steuerung realer Roboter/Maschinen/Fahrzeuge oder sicherheitskritischer Aktoren; eigenständig sicherheitsgeprüfte Hardware-Interlocks notwendig. Read-only-Diagnose und Simulation getrennt.
3. Keine hartcodierten Secrets, Passwörter oder Accounts; kein Dump von Browser-Cookies/Passkeys zu KI; Daten aus Webseiten/Dokumenten/Emails gelten als **untrusted** und sind keine neuen Instruktionen.
4. Schutz vor Prompt Injection, Domainverwechslung, Pfad-/Rechteüberschreitung; Genehmigungen projekt-, zeit- und aktionsgebunden; alles in begrenztem Audit nachvollziehbar.
5. **Forensische Beweiskopien** inklusive relevanter abgeleiteter Indizes, Cachedateien, temporärer Exporte, Kopien in Backups und extrahierten Texten innerhalb von **max. 120 Stunden / fünf Tagen** bereinigen, mit Vorwarnung nach vereinbarter Regel (24 Stunden). Bei ausdrücklich gewollter Langzeitaufbewahrung eigenständiger, vom Nutzer bewusst verwalteter Archivexport. Offline ausgeschaltete Medien können physisch nicht gelöscht werden: dort keine dauerhaft erreichbaren forensischen Kopien belassen; ggf. nur kryptographisch kontrollierte flüchtige Arbeitsbereiche.
6. Betriebsfähigkeit ohne zusätzliche kostenpflichtige KI-APIs; Internetservices mit Quellen/Datum und Berechtigungen. Bestehende Grok Build-Abonnement-/Berechtigungsgrenzen nicht als kostenlose API deklarieren.
7. Keine fremden oder rechtlich unzulässigen Inhalte/Assets ohne Rechte; Lizenzen, Herkunft und Verteilbarkeit aller gebündelten Apps dokumentieren.
8. Deep-/Dark-Web-Recherche dauerhaft vom allgemeinen Internet-/Downloadcenter, Accounts und Kommunikation isolieren. Keine illegale Marketplace-Kuration. Isolierter Viewer kann weder jedes serverseitige Verhalten noch absolute Anonymität garantieren.

## 7. Zustandsmodell / ehrliche Statusführung
Pro Feature, Adapter, Studio und Release: `PROPOSED` → `APPROVED_SPEC` → `IN_PROGRESS` → `IMPLEMENTED` → `TESTED` → `ACCEPTED` → `RELEASED`. `FAILED` und `BLOCKED` mit Grund und Nachweis; Statusübergänge nur bei tatsächlichen Artefakten/Tests. Eine bloße Programmierabsicht oder Agentenmeldung erfüllt nicht `IMPLEMENTED`/`TESTED`.

## 8. Lizenz- und Installationsbedingungen
- Chromium/Firefox-/Linux-basierte Software nur nach Manifestprüfung, aktuellen Lizenzen, Versionen, Markenrecht, Sicherheitsupdates und Hardwaretest in Pakete aufnehmen.
- Proprietäres Google Chrome / Grok Build / Unreal Engine / Microsoft Office nicht ohne zulässige Redistributionsrechte als Binärdateien beiliegen lassen; offizielle Installations-/Anmeldepfade einsetzen.
- Gute Basiswerkzeuge (Kandidaten, nicht abschließend geprüft): LibreOffice, mpv/libVLC, FFmpeg, Blender, Krita, Inkscape, Audacity, Kdenlive, Git, Python, Tesseract OCR, OpenSSH, WireGuard, geeignete PDF-Werkzeuge. Paketversionen vor Umsetzung verifizieren.
- USB/SSD-Edition benötigt ausreichend schnellen persistenten Speicher, getrennte System-/Datenpartitionen, geeignete Recovery und explizites Nutzer-Consent beim Einrichten des Zielmediums.

## 9. Definition of Done für Trendo OS (gesamtes System)
Ein Release ist erst abnahmebereit, wenn (a) installierbare Dateien und dokumentierte Hashes existieren, (b) jede zugesagte Edition auf passender realer Hardware tatsächlich startet, (c) Kernworkflow / Login / Projektisolierung / Genehmigungen funktionieren, (d) die gewählte Ready-to-Run-Software echt startet, (e) kritische Sicherheitstests bestehen, (f) Backup/Restore erfolgreich getestet sind, (g) Testergebnisse und offene Einschränkungen pro Edition dokumentiert sind, (h) Nutzer Release ausdrücklich freigibt. Zu aufwendige Studios dürfen **sichtbar als noch nicht abgenommen** gekennzeichnet sein, aber nicht fälschlich als fertig ausgeliefert werden.

## 10. Online Business Creation & Innovation Studio — bestätigter neuer Umfang
**Entscheidungsstand:** `APPROVED_SPEC`, 30 / 30 Funktionen, **beide Modi JA**. Noch kein Quellcode/Build/QA dieses Studios vorhanden.

### Modus A: bewährtes Online-Geschäft neu entwickeln
Wähle einen Geschäftsmodelltyp (etwa SaaS, Shop, digitale Produkte, Abo, Mitgliederbereich, Marktplatz, Buchungs-/Kursplattform), ermittle öffentlich belegbare Marktdaten und Konkurrenten mit Quelle/Datum, formuliere Kundennutzen, wirtschaftliche Annahmen, vollständige Rollen/Journeys, Produkt- und Servicegrenzen. Vorschläge brauchen ein Projekt-spezifisches `business_case.md`, realistisches Szenariomodell und eine Nutzerfreigabe vor der Produktion.

### Modus B: neuartige Online-Geschäftsideen entwickeln
Identifiziere ein überprüfbares Problem, entwickle alternative Lösungsmodelle, recherchiere ähnliche existierende Angebote, kennzeichne Neuheitsunsicherheit, validiere Hypothesen mit ausdrücklich freigegebenen Prototypen/Nutzertests und dokumentiere `GO`, `NO_GO` oder `NEEDS_EVIDENCE` mit Nutzerentscheidung. Ohne Freigabe keine Produktentwicklung auf Verdacht.

### Gemeinsamer Produktionsauftrag (nur freigegebenes konkretes Geschäftsprojekt)
Nach Entscheidung: Marke/UX, Anforderungsanalyse, rechtliche Prüf- und Zuständigkeitsliste, echten Frontend-/Backend-/Datenbankcode, Auth/RBAC/Mandanten, bedarfsabhängige Shop-/SaaS-/Abo-/Marktplatzfunktionen, Payment-Sandbox und zustimmungspflichtigen Live-Gateway, Rechnungs-/CRM-/Adminfunktionen, Service-Fulfillment, Dokumentation, SEO, datenschutzgerechte Analytics, Backups/Monitoring und reale End-to-End-Tests implementieren. Der konkrete Funktionsumfang variiert je Geschäftsmodell; *nicht* jedes Geschäft muss Shop, Abos und Marktplatz gleichzeitig enthalten.

### Verbindliche Entscheidungsgates
`DISCOVERY` → `MODEL_DEFINED` → `EVIDENCE_REVIEW` → **`GO_NO_GO_APPROVAL`** → `SPEC_LOCK` → **`BUILD_APPROVAL`** → `IMPLEMENTED` → `TESTED` → `COMPLIANCE_SIGNOFF` → **`DEPLOY_APPROVAL`** → `ACCEPTED` → `RELEASED`.

**Keine autonomen Außenwirkungen:** Keine Käufe, Domain- oder Hostingbestellungen, echten Zahlungen, Verkaufsstarts, Kundenkontakte, rechtsverbindlichen Erklärungen, Marketingpublikationen, Live-Schaltungen oder geheimen Datenübertragungen ohne konkrete Nutzerfreigabe. Providerverifizierung, Gewerbe-/Steuerpflichten und gesetzlich erforderliche Prüfungen sind nicht durch eine KI-Zusage ersetzt. Dokumentiere Kosten, Hosting, Lizenzen und Drittanbieter-Abhängigkeiten; kein weiterer kostenpflichtiger KI-API-Zwang.

**Sicherheit/Isolation:** Produktives Online Business nutzt nur genehmigte reguläre Browser-/Kommunikations-/Payment-Schnittstellen und niemals das Deep-/Dark-Web READ-ONLY-Studio. Payments zunächst im Sandboxmodus, niemals mit echten Zahlungsinstrumenten zu Testzwecken. Projekt- und Mandantenrechte in API UND Datenbank erzwingen. Die gesonderte forensische Beweiskopien-Frist von 120 Stunden gilt auch für abgeleitete Kopien, falls das Business Studio solche verarbeitet; reguläre rechtlich aufbewahrungspflichtige Geschäftsbelege haben eigenständige Aufbewahrungsregeln und dürfen nicht mit der forensischen Kurzzeitregel verwechselt werden.

**Abnahme:** Die 30 Einzelanforderungen und Nachweise sind in `BUSINESS_REQUIREMENTS_30.csv` und `BUSINESS_QA_MATRIX.csv` beschrieben. `NOT_RUN` bleibt bis echte Tests dokumentiert sind. Fehlende geeignete Provider-/Recht-/Lizenz-/Hardware-Voraussetzungen als `BLOCKED` ausweisen.

## 11. Letter, Business & Legal Correspondence Studio — neu bestätigter Umfang (25/25)

**Status:** `APPROVED_SPEC`, nicht gebaut oder getestet. Eigenständiges Studio `studio.letter_correspondence` mit 25 nummerierten genehmigten Anforderungen `LETTER-001..LETTER-025`; siehe `STUDIO_LETTER_CORRESPONDENCE_25.md`.

### Zulässige Aufgaben
Private Briefe, geschäftliche Schreiben, Behörden/Ämter, Versicherungs-/Schadensmeldungen, Anwaltsanfragen, Schriftsatzentwürfe für Gerichte sowie Empfangsbriefe nach OCR auswerten und Antwortentwürfe schreiben. Sachverhalt, Belege, Aktenzeichen, Anlagen, Ton, Briefkopf und geeignetes Seitenlayout verwalten. Echte DOCX/ODT/PDF-Dokumente erstellen, rendern und prüfen. Kalender nur mit verifizierten und bestätigten Fristangaben aktualisieren.

### Verbindliche Schranken
1. Briefquellen, Fristen, Normen, Zuständigkeiten und Einreichungsformen dürfen **nicht erfunden** werden; unbekannt/ungeprüft offen ausweisen. Bei rechtlich bedeutsamen Fällen gegebenenfalls qualifizierte Rechtsberatung oder Verfahrensprüfung verlangen.
2. Menschliche Prüfung und spezifisches Freigabetoken für jede echte externe Sendung, postalische Beauftragung, elektronische Signatur oder Gerichtseinreichung; Entwurfsstatus niemals mit zugestellt/eingereicht verwechseln.
3. Ausschließlich genehmigte Office-/OCR-/Kommunikations-/Kalender-/Memory-Schnittstellen nutzen, keine zweite Office-Engine entwickeln und keine direkte Versandhintertür. Fremde Dokumente sind untrusted Daten.
4. Gesonderte fünf-Tage-/120h-Löschung forensischer Beweiskopien und sämtlicher daraus abgeleiteter kontrollierter Daten beibehalten, ohne normale bewusst gespeicherte Briefe pauschal dieser Frist zu unterstellen.
5. Kein stillschweigendes Upgrade des zuvor abgelehnten vollen Language & Localization Studio 2.0; optionale sprachbezogene Briefentwürfe sind hier eng begrenzt.

### Produktionsübergabe
`LETTER_GROK_BUILD_AUFTRAG.md` gibt die technische Reihenfolge und Sicherheitsgrenzen vor. `LETTER_QA_MATRIX.csv` enthält reale format-, OCR-, Rechtssicherheits-, Freigabe-, Archiv- und editionsbezogene Negativtests; alle beginnen `NOT_RUN`.


## 12. VERBINDLICHE PROGRAMMIERWERKZEUG-UMSTELLUNG: GROK BOT + GROK BUILD (21.10.2026)

**JA bestätigt:** Grok Bot ist Projektleiter/Planungsagent, Grok Build der lokale Programmier-/Test-Agent. Die vollständigen genehmigten Module, die drei Editionen und die Produktnamen **Trendo OS** und **Trendo** bleiben unverändert. Ein anderer Coding-Assistent darf NICHT als Voraussetzung eingebaut werden.

### Sicherheitsrelevante Realitäten
- Grok Bot ist ein Cloud-Dienst: Mehrere Bots eines Kontos teilen sich Cloud-Computer, Dateien, Browser-Cookies und zugängliche Konten. **Nicht** als isolierte separate Projektumgebungen ansehen. Sensible Daten lokal behalten; forensische Beweiskopien GAR NICHT in die Cloud synchronisieren.
- Grok Build ist als lokaler Programmieragent mit CLI, Terminal, Subagents, `AGENTS.md`, MCP und Skripting vorgesehen. Modellzugriff/Account, konkrete Kosten, Plattformkompatibilität und verfügbare Rechte müssen real geprüft werden. Ein eigenständiges zusätzliches kostenpflichtiges KI-API-Abo darf im fertigen Trendo OS keine Pflicht sein.
- Grok Bot kann auf geeigneten Endgeräten unter ausdrücklicher Freigabe lokale Ausführungen veranlassen; eine automatische dauerhafte Grok-Bot-zu-Grok-Build-Steuerung ist NICHT schon durch den Namen garantiert. Bis ein sicherer Taskbroker mit echten Tests existiert, manuell Übergabedateien nutzen.
- Blender- und Unreal-Anbindungen werden über verifizierte lokale Programm-/Python-Schnittstellen oder geprüfte MCP-Server mit Capability-Discovery, Benutzerrechten, QA und Versionsprüfung umgesetzt. Keine erfundenen Toolnamen oder behaupteten 3D-/UE5-Ergebnisse.
- Die frühere Coding-Agent-Abhängigkeit ist ABGELÖST. **Keinerlei Rückfallzwang** oder unautorisierte Fremdagent-Ausführung.

### Technische Unterlagen
`GROK_BOT_PROJEKTLEITER_PROMPT.md`, `GROK_BUILD_MASTERPROMPT.md`, `AGENTS.md`, `GROK_ARCHITEKTUR_AGENTEN_MCP.md`, `GROK_BUILD_LOCAL_SETUP.md`, `GROK_MIGRATION.md`, `GROK_QA_MATRIX.csv` sind verbindliche Ergänzungen.

### Freigabe- und Produktionsstatus
Das Grok-Umstellungs-Dokumentationspaket ist erstellt. **Trendo OS, Grok-Lokalinstallation, Blender/Unreal-MCP-Anbindungen und Releases wurden hier nicht implementiert oder getestet.** Alle Testwerte bleiben `NOT_RUN` bis zum tatsächlichen Durchlauf.
