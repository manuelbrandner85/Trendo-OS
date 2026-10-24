# Trendo OS – Entwicklungsreihenfolge / Milestones

**Alle Phasen sind ZIELE.** Initialer echter Fortschrittsstatus: `NOT_STARTED`. Ein Release wird nicht anhand einer Liste von Projektdateien als fertig eingestuft.

## P0 – Repository & Requirements Lock
- Repository/Startzustand lesen, Zugriffsrechte verifizieren und Backup erstellen; keine blinden destructive writes.
- MODULE_REGISTER und Systembibel in maschinenlesbare `requirements.yaml` mit stabilen IDs importieren; explizite JA/NEIN-Entscheidungen erhalten.
- Architektur-ADRs, Windows-/Linux-/Pi-Target-Matrix, unterstützte Hardwaredefinition, Risiko- und Lizenzregister erstellen.
- **Exit:** überprüfte Spezifikation, Nutzerabnahme und konsistenter Implementierungsplan.

## P1 – Windows Kern-Vertical-Slice (früh testbar)
- Minimaler Trendo-Core-Service + native UI + lokaler Launcher + Projekteinstellungen + lokaler Event-Log.
- Benutzer-/Projektisolation, rollenbasierte Erlaubnisse, Approval-UI, Secret Store mit Testkonten.
- Studio-Registry / Capability-Einträge mit ehrlich angezeigtem Status.
- Echte Integrationen für ein sehr kleines Werkzeugset (z.B. installierten Browser starten, Dateiexplorer lesen, autorisierten Prozess starten).
- **Exit:** unter Windows gebauter Installer, tatsächlicher Erststart, Demo-Workflow, Security-Negativtests, Logs.

## P2 – Linux USB/SSD Boot & Ready-to-Run Essentials
- Linux-Basis und passendes Desktopframework auswählen; reproduzierbare Image-Erstellung, UEFI-/Secure-Boot-Pfad planen.
- Persistenz/Encryption/Backup/Recovery, restriktiver Zugriff auf Windows-interne Laufwerke, First-Boot, Device Detection.
- Lizensiertes Essentials-Bundle (Dateimanager, Chromium oder geeigneter Browser, Office, Player, PDF) erstellen und starten.
- **Exit:** bootet auf echter unterstützter x86-64-Hardware von externem Medium, Dateien erhalten, Programme funktionieren, keine Beschädigung interner Windows-Installation.

## P3 – Pi/Windows Bridge
- Schlanker Trendo-Agent auf Raspberry Pi 4 8GB; Windows PC Agent lokal authentifiziert und auftragsbezogen.
- Heartbeats, offline/reconnect, scoped calls, keinerlei allgemeine OS-Adminfernsteuerung.
- **Exit:** tatsächlich verbundener Pi+PC Test, freigegebene Rechenaufgabe auf Windows, saubere Fehlerbehandlung.

## P4 – Funktionsfamilien nach Abhängigkeit integrieren
Reihenfolge als Empfehlung, unter Berücksichtigung aller bestätigten Studios:
1. Files / Browser / Downloads / Install Manager / Hardware / Network.
2. Desktop Widgets / Media / Accessibility / Calendar / News / Mail / Office.
3. Memory/Prompt/Workflow/Agents und lokaler freigabegesteuerter Trendo-Assistant.
4. Creative/3D/Unreal-/Godot-/App Development Integrationen mit echten vertikalen Workflows.
5. Erweiterte Studios nach Hardware/Berechtigung/Feasibility: Engineering, Scanning, CNC, Robotics, XR, Broadcast usw.
6. Separates isoliertes Dark-Web READ-ONLY Research Studio mit Tor-konformer Sicherheitsprüfung und lokaler deutscher Leseübersetzung; nie Kommunikations-/Upload-Scopes.
7. **Ultimate Online Business Creation & Innovation Studio (30, beide Modi, JA):** zunächst zwei Planungs-Vertical-Slices (bewährtes Modell + Innovation, Ähnlichkeit, realistische Finanzannahmen, GO/NO_GO), dann nach separater Business-Projektfreigabe realen Full-Stack-Piloten mit Auth, Admin, bei Bedarf Test-Payments, echte E2E-/Compliance-/Lizenztests. **Kein Business Launch ohne gesonderte Freigabe.**
8. **Ultimate Letter, Business & Legal Correspondence Studio (25, JA):** Nachdem Writer/Office, OCR, Kalender, Memory, Communication und zentraler Approval Broker wirklich verfügbar sind, erst einen sicheren Brief-Vertical-Slice ohne Versand bauen (Sachverhalt → echte DOCX/ODT/PDF-Ausgabe), danach Behörden-/Versicherungs-/Anwalts-/Gerichtsentwürfe, Anlage- und Fristenwarnungen, Freigabe- und Versandnegativtests, isolierte Fallakten, echte plattformbezogene QA. **Keine Einreichung ohne konkrete separate Freigabe, kein Erfinden von Rechtsgrundlagen/Fristen.**
- **Exit pro Studio:** Mindestfunktion startet; Adapter meldet reale Zustände; mindestens ein Integrationstest bestanden; Versions-/Lizenz-/Datenschutzstand erfasst.

### Spezifischer Nachweis für Business-Studio
- `BUS-001..030` mit `APPROVED_SPEC` in `BUSINESS_REQUIREMENTS_30.csv` übernehmen; nicht als BUILT markieren.
- Beide Geschäftsmodi und die Project-Gates G0–G9 im UI/Backend demonstrieren.
- Modellabhängiger Echt-Build auf geeigneter Hardware; Sandbox-Payments und QA nur wenn tatsächlich gebraucht/getestet.
- Im Business-Testkatalog `BUSINESS_QA_MATRIX.csv` beginnen alle Tests mit `NOT_RUN`.

### Spezifischer Nachweis für Letter-Studio
- `LETTER-001..025` aus `LETTER_REQUIREMENTS_25.csv` als `APPROVED_SPEC`/`NOT_STARTED` übernehmen.
- Abhängigkeiten Office, OCR, Calendar, Memory, Communication und Approval Broker tatsächlich prüfen.
- Echte DOCX-/ODT-/PDF-Öffnung und Layout-Tests, OCR-Unsicherheit, Quellen-/Fristenwarnung, Briefkopf-/Projektisolation und explizites Sendegate vorweisen.
- `LETTER_QA_MATRIX.csv` startet durchgehend `NOT_RUN`. Nur Testläufe mit Logs dürfen Status ändern.

## P5 – Security, Integration, Recovery
- Permission bypass / Prompt Injection / Projektpfad / Netzwerk-/Browser-Profiltests.
- Forensik-TTL automatisiert in kontrollierter Laufzeit nachweisbar getestet; kein Offline-USB-Scheinversprechen.
- Backup, Restore, Safe Updates & Rollback. Signierte Release-Verfahren; SBOM.
- **Exit:** keine kritischen offenen Sicherheitsfehler, erfolgreiche echte Recovery und protokollierte Testergebnisse.

## P6 – Release Candidates
- Windows installer; USB Full Studio image; Pi+PC package. Je Edition eigene Hashwerte, Release Notes, Lizenz-/SBOM, User Docs, relevante Tests.
- Distribution und eventuell kommerzielle Lizenzrechte separat prüfen.
- **Exit:** explizite Benutzerabnahme. Offen gebliebene Studios nicht als 'Fertig' kennzeichnen.

## Priorisierung & Zeitplanung
Keine nicht belastbaren Zeit- oder Kostenversprechen. Erst P0/P1 prototypisch messen, dann Ressourcen, Hardwarekosten, laufende Dienste, Größe externer SSD und Lizenzrisiken mit echten Daten schätzen. Ein Full-Studio-System mit allen Studios wird eine erhebliche mehrphasige Entwicklung sein.


## v2.0 – Claude-Code-Umstellung in der Produktionsreihenfolge
- **P0-A Claude-Code-Inventur:** Offiziell installierte lokale Claude-Code-Version, Konto, Konfiguration, Betriebssystem, Projektpfad und Git-Zugriff tatsächlich nachweisen. Ohne lokalen Zugriff `BLOCKED`.
- **P0-B Task-Übergabe:** Claude Code erstellt scope-begrenzte Projektaufträge; zunächst manuelle `.md`-Übergabe, später nur nach realer Auth-/Safety-QA ein lokaler Taskbroker.
- **P1-C Coding:** Windows-Core-Vertical-Slice unter Claude Code mit `CLAUDE.md`; keine erfundenen Build-/Run-Ergebnisse.
- **P4-D 3D/Game:** Blender- und UE5-Adapter getrennt entwickeln und auf real installierten Programmen mit kontrollierten Testprojekten prüfen.
- **P5-E Security:** Gemeinsame Cloud-Dienste, Tool-Prompt-Injection, Default-No-Access und 120h-Forensik als Pflicht-Negativtests ergänzen.
- **P6-F QA:** `docs/claude/QA_RELEASE_MATRIX.md` vollständig mit tatsächlichen Nachweisen abarbeiten, sofern für jeweilige Edition relevant.
