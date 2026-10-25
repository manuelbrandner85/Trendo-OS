# C2: BUS-001 bis BUS-030 am Windows-PC geprüft

**Lauf:** 2026-10-25 auf Manuels Windows-PC (Windows 11, Ryzen 7 5700X), Konfiguration Release, Stand `main` nach T-0213 plus neuer BUS-001-Test.
**Filter:** `DisplayName~BUS-` in `tests/Trendo.Core.Tests` und `tests/Trendo.App.Tests`. Testnamen mit mehreren Nummern (z. B. „BUS-006/007/008/009“) zählen für jede genannte Nummer.
**Rohprotokolle:** `bus-core.trx` und `bus-app.trx`. Rechner- und Benutzername sind durch `<pc>`/`<user>` ersetzt. SHA-256 in `SHA256SUMS`.

**Zähler aus den .trx:** Core 15 gesamt, 15 bestanden, 0 fehlgeschlagen · App 5 gesamt, 5 bestanden, 0 fehlgeschlagen.

| Anforderung | Titel | Core-Tests | Oberflächen-Tests | Ergebnis |
|---|---|---:|---:|---|
| BUS-001 | Business Discovery Director | 1 | 1 | bestanden |
| BUS-002 | Proven Business Model Library | 1 | 0 | bestanden |
| BUS-003 | Competitor & Market Intelligence | 1 | 1 | bestanden |
| BUS-004 | Customer Problem & Niche Discovery | 1 | 1 | bestanden |
| BUS-005 | Realistic Opportunity & Profitability Score | 1 | 1 | bestanden |
| BUS-006 | Business Innovation Engine | 1 | 0 | bestanden |
| BUS-007 | Cross-Industry Business Fusion | 1 | 0 | bestanden |
| BUS-008 | Originality & Similarity Research | 1 | 0 | bestanden |
| BUS-009 | Market Validation & Prototype Laboratory | 1 | 1 | bestanden |
| BUS-010 | Innovation Go / No-Go Decision Brain | 1 | 1 | bestanden |
| BUS-011 | Business Blueprint Architect | 1 | 1 | bestanden |
| BUS-012 | Revenue, Pricing & Subscription Designer | 1 | 0 | bestanden |
| BUS-013 | Brand Identity & Premium UX Director | 1 | 1 | bestanden |
| BUS-014 | Business Requirements & Legal Feasibility Planner | 1 | 0 | bestanden |
| BUS-015 | Customer Journey & Process Architect | 1 | 1 | bestanden |
| BUS-016 | Claude Code Full-Stack Business Builder | 1 | 1 | bestanden |
| BUS-017 | Accounts, Membership & Multi-Tenant Engine | 1 | 1 | bestanden |
| BUS-018 | Payments, Billing & Subscription Integration | 1 | 1 | bestanden |
| BUS-019 | Business Admin, CRM & Operations Center | 1 | 1 | bestanden |
| BUS-020 | Product Delivery, Supplier & White-Label Engine | 1 | 1 | bestanden |
| BUS-021 | SEO, Content & Visibility Director | 1 | 1 | bestanden |
| BUS-022 | Customer Acquisition & Sales Funnel Studio | 1 | 1 | bestanden |
| BUS-023 | Customer Support & Service Center | 1 | 1 | bestanden |
| BUS-024 | Business Analytics & Conversion Intelligence | 1 | 0 | bestanden |
| BUS-025 | Finance, Cost & Profitability Dashboard | 1 | 0 | bestanden |
| BUS-026 | Business Compliance, Privacy & Rights Gate | 1 | 1 | bestanden |
| BUS-027 | Real Security, Payments & Business Process QA | 1 | 0 | bestanden |
| BUS-028 | Hosting, Domain & Deployment Director | 1 | 1 | bestanden |
| BUS-029 | Business Monitoring, Backup & Evolution Engine | 1 | 1 | bestanden |
| BUS-030 | Ready-to-Business Production & Handover Gate | 1 | 1 | bestanden |

## Testnamen je Protokoll

- Core · Passed · BUS-001: Idee erfassen – alle Angaben bleiben nach Speichern erhalten, Ändern ersetzt, Löschen entfernt
- Core · Passed · BUS-002/011/014: 10 Modelle, Bauplan enthält Zahlen, Entscheidung und rechtliche Schritte; Ideen speichern
- Core · Passed · BUS-003: Preisspanne, Median, eigene Position, gemeinsame Schwächen als Marktlücke
- Core · Passed · BUS-004: Nischen nach Schmerz × Häufigkeit, gleiche Nennungen zusammengefasst
- Core · Passed · BUS-005/012: Marge, Gewinn, Gewinnschwelle und Stundenlohn richtig gerechnet
- Core · Passed · BUS-006/007/008/009: Varianten, Branchen-Mix, Eigenständigkeit und Testauswertung
- Core · Passed · BUS-010: Gute Idee → Starten; Verlust je Verkauf → Nicht starten; vage Idee → Überarbeiten
- Core · Passed · BUS-013: Namensprüfung, Farbpalette (Gegenfarbe stimmt), Kontrast nach WCAG, CSS
- Core · Passed · BUS-015/021/022: Kundenweg mit Lücken und Ablaufdiagramm, Suchmaschinen-Prüfung, Trichter mit Engpass
- Core · Passed · BUS-016/026/027: Webseite mit 5 Seiten, Pflichtangaben erfüllt, Qualität fehlerfrei; Mängel werden erkannt
- Core · Passed · BUS-017/018: Monatsumsatz, Rechte, Teams getrennt; IBAN-Prüfung, GiroCode nach EPC, Gebühren
- Core · Passed · BUS-019/020/023: Fällige Kontakte, Angebotswert, Nachbestellen, Anfragen-Fristen und FAQ
- Core · Passed · BUS-024/025: Zahlen-CSV, Trend 7 gegen 7 Tage, Plan gegen echte Buchungen
- Core · Passed · BUS-028/029: Paket mit Anleitung und Pages-Ablauf, Domain geprüft; Sicherung behält 10, Wiederherstellen
- Core · Passed · BUS-030: Startklar erst, wenn Bewertung, Markttest, Webseite, Recht, Qualität, Paket und Sicherung passen
- App · Passed · BUS-001/005/010/011 (UI): Idee eingeben, bewerten, Bauplan speichern; Studio-Kachel öffnet Business
- App · Passed · BUS-003/004/009 (UI): Mitbewerber, Problem und Test erfassen – Auswertung erscheint
- App · Passed · BUS-013/015/021/022 (UI): Name bewertet, Kundenweg-Schritt, SEO-Titel, Trichter-Ergebnis
- App · Passed · BUS-016/026/028/029/030 (UI): Angaben, Webseite bauen, Prüfung grün, Paket, Sicherung
- App · Passed · BUS-017/018/019/020/023 (UI): Kontakt fällig, Mitglied mit Tarif, GiroCode, Nachbestellen, Anfrage lösen

## Neu in diesem Auftrag
- BUS-001 (Idee erfassen) hatte keinen Test mit seiner Nummer. Neu ist der Core-Test „BUS-001: Idee erfassen – alle Angaben bleiben nach Speichern erhalten …“.
- Der bestehende Oberflächen-Test heißt jetzt „BUS-001/005/010/011 (UI): …“, weil er genau diese Funktionen prüft.

## Grenzen
- Geprüft wird die Logik und Bedienung von Trendo, headless am PC. Externe Dienste werden nicht angesprochen, sie bleiben aus oder freigabepflichtig: echte Zahlung, Domainkauf, Veröffentlichen, E-Mail-Versand.
- 9 Anforderungen haben nur Core-Tests und keinen eigenen Oberflächen-Test: BUS-002, 006, 007, 008, 012, 014, 024, 025, 027.
