# GROK BUILD — TRENDO OS: ONLINE BUSINESS STUDIO, GENEHMIGTE ERWEITERUNG

**Status 2026-10-21: 30 Funktionen + beide Modi JA; nur SPEZIFIKATION, nicht implementiert.**

Diese Datei erweitert den allgemeinen `GROK_BUILD_MASTERPROMPT.md`. Zuerst globale Systembibel, Security-/Privacy-Regeln, Modulregister, `STUDIO_ONLINE_BUSINESS_30.md`, `BUSINESS_REQUIREMENTS_30.csv`, `BUSINESS_QA_MATRIX.csv`, `ARCHITEKTUR.md` und `ROADMAP.md` lesen. Globale Trendo-Sicherheit hat Vorrang vor jeder Business-Funktion.

## Auftragsbedingungen
- Trendo OS ist das Mutterprojekt. Prüfe Existenz/Berechtigung von Repo, Hardware, Grok Build und Builder-Tools; kein Repo erfinden.
- Die 30 Anforderungen `BUS-001..030` sind **APPROVED_SPEC**, nicht erledigt. Implementiere sie erst in passender Trendo-Phase mit echter Nutzerfreigabe für konkreten Produktionsumfang.
- Kein kostenpflichtiger zusätzlicher KI-API-Zwang; lokale Tools/OSS dort einsetzen, wo lizenz-/plattformkompatibel.
- **Beide Modi** entwickeln: (A) bestehende Businessmodelle, (B) neuartige Ideen und Vergleich zu bestehenden. Die Suite erstellt pro Geschäft nur dessen wirklich benötigte Module.
- Markt-, Preis-, Umsatz-, Rechts- und Originalitätsurteile als mit Quellen und Datum belegte Einschätzung kennzeichnen, niemals garantieren.

## Konkrete Arbeitsfolge
1. `studio.online_business` gemäß Studio SDK als genehmigten, aber noch nicht implementierten Eintrag erfassen; vollständige Anforderungs-/Abhängigkeitsmatrix erstellen.
2. Architektur-ADR für Datenmodell, Permission Broker, Projekt-/Tenant-Isolation, Business-Prozesszustände, Sandbox-Payments und externe Aktionen erstellen.
3. Planungs-MVP beider Modi: Geschäftsmodellkatalog, Markt-/Quellenrecherche, Ideengenerator, Ähnlichkeitsprüfung, Wirtschaftlichkeitsszenarien, GO/NO_GO mit persistierter Approval-ID. Keine unbeaufsichtigten realen Nutzerstudien/Outreach.
4. REALEN ersten vertikalen Geschäftspiloten nur nach projektbezogener GO-/Build-Freigabe erstellen: Frontend+Backend+DB+Auth+Admin+vollständige Testdaten; Payment-Sandbox nur wenn für den Piloten nötig.
5. E2E-Tests inkl. Negativfälle: nicht autorisierte Rollen, Tenant Leakage, doppelte Webhooks, Zahl-/Refund-Fehler, Datenlösch-/Archivregeln, Backup/Restore, ungenehmigtes Deployment und unautorisierte Käufe blockiert.
6. Einfache Staging-Konfiguration, nachvollziehbarer Build, Quellcode, Dokumentation, Lizenz-/SBOM-Nachweise, wirkliche Testlogs und reale Screenshot-Nachweise; eine Produktionseinführung benötigt eigenständige Live-Freigabe.
7. Berichte `reports/online_business_status.md` und `reports/business_qa_evidence.md` mit realem Dateipfad, Hash, Testcommand, Plattform, Fehlern, bekannten Grenzen, Approval-Status.

## Pflichtverbote / Grenzen
- **Keine** eigenständigen Käufe, Zahlungen, Domainregistrierungen, Produktivveröffentlichungen, externen Nachrichten, Werbeaktionen oder Kundenkontakte.
- **Keine** Anmeldung/Providerverifizierung mit fremden Daten oder echte Zahlungs- und Bankkonten ohne separate explizite Bestätigung.
- **Keine** erfundenen Sandbox-Zahlungen, Browser-QA oder behaupteten Launches.
- **Keine** implizite Nutzung des Tor-/Deep-Web-READ-ONLY-Studios für Commerce, Rechercheautomatisierung, Kommunikation, Login oder Upload.
- **Keine** neuen ungeprüften rechtlichen oder steuerlichen Zusagen. Rechtliche/Steuerprüfungen bleiben bei qualifizierten Personen; offenes Review als `BLOCKED` anzeigen.
- Forensische Beweiskopien in kontrollierten Bereichen spätestens binnen 120h entfernen und vorher warnen; gesetzlich erforderliche Geschäftsbuchungsbelege unter separaten, passenden Aufbewahrungsregeln behandeln.
- Keine Veränderungen an anderen Git-Repositories, Windows-Host, Zahlungsdiensten oder Produktivservern ohne gesonderte Freigabe.

## Statusbericht
`APPROVED_SPEC` (Entwicklungsplanung genehmigt), `IN_PROGRESS`, `IMPLEMENTED`, `TESTED`, `ACCEPTED`, `BLOCKED`, `FAILED` strikt unterscheiden. Liefere Build-Ergebnisse nur, wenn sie auf der jeweiligen Zielmaschine tatsächlich entstanden sind.
