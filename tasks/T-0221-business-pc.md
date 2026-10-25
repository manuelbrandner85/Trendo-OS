# T-0221: BUS-001 bis BUS-030 am Windows-PC prüfen (C2)

**Ziel:** Die 30 Business-Anforderungen (bisher nur `TESTED_BOX` auf der Linux-Box) am Windows-PC mit Rohlog und SHA-256 nachweisen.
**Branch:** claude/T-02xx (Sammelbranch)
**Auftrag (Manuel, 2026-10-25):** „Mach mit C2 weiter: BUS-001 bis BUS-030 am Windows-PC prüfen, mit Rohlog und SHA-256.“
**Dateien/Bereich:** `reports/c2-business/`, `requirements.yaml` (nur `test_status` und `evidence` der 30 BUS-Einträge), fehlender BUS-001-Test, Bericht.
**Fertig, wenn:**
- [x] Jede BUS-Nummer hat mindestens einen benannten Test, der am PC (Release) besteht.
- [x] Rohprotokolle (`.trx`) ohne Rechner- und Benutzernamen, SHA-256, Übersicht je BUS-Nummer.
- [x] `test_status: PC_VERIFIED` mit `evidence` nur, wenn die Übersicht „bestanden“ zeigt.
- [x] alle drei Testprojekte grün (Zahlen in den Bericht)
**Nicht tun:** keine externen Dienste ansprechen (Zahlung, Domain, Veröffentlichen, Versand), `implementation_status` nicht ändern.
