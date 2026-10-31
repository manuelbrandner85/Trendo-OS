# T-0248 – Shop: Bestand bei Lieferung, Umsatz bei Zahlung (105-05/105-22)

- Freigabe: Manuel im Chat am 31.10.2026 (Entscheidung C2 „Ja, wie 105“, Auftrag „Shop-Buchung (105)“)
- Branch: `claude/T-0248-shop-buchung` · Builder 11-web-business-commerce · QA 19-qa-verification
- Ziel: Vormerkung statt Sofortabgang, Lieferung mit Lagerbewegung, Rechnung ohne Buchung, Zahlung mit Referenz (idempotent, Teilzahlung, Überzahlung abgewiesen), Storno mit Gegenbuchung, Altdaten unverändert; bestehender Test bewusst angepasst. Kein CRM-Ausbau (IMP-86 NEIN).
