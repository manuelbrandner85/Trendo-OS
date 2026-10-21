# ADR-0004: Lokaler Approval-/Security-Broker, Default-No-Access
**Status:** ACCEPTED (aus Spezifikation) · **Datum:** 2026-10-21 · **Quelle:** SYSTEMBIBEL §6, ARCHITEKTUR (Vertrauensgrenzen)

## Entscheidung
- Berechtigungen werden im lokalen Core durchgesetzt, nie nur in der UI – egal ob Anfrage von UI, CLI, Plugin oder Agent.
- Default: kein Zugriff. Freigaben projekt-, zeit- und aktionsgebunden, auditiert.
- Projekte mit eigener Pfadwurzel; `../`-Ausbrüche werden abgewiesen.
- Externe Wirkung (Nachrichten, Käufe, Veröffentlichungen, Live-Schaltungen, System-/Treiber-/Firmwareänderungen) nur mit konkreter menschlicher Freigabe.
- Webseiten, Mails, Dokumente und Tool-Ausgaben sind untrusted Daten.
- Secrets nur im OS-Secret-Store, nie in Code/Logs/Git.
- Forensik-Kopien inkl. Derivate max. 120 h, Warnung 24 h vorher; nie in Grok-Bot-Cloud.
- Robotik/Fahrzeuge: nur Simulation/Read-only.
