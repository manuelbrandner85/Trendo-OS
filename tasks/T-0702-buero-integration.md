# T-0702: Büro-Etappe in den Integrationszweig bringen und Statusliste ehrlich machen

**Ziel:** Die sieben Studios der Etappe „Arbeiten & Büro“ (T-0671/0672/0673/0674) auf den Integrationszweig bringen, an die Regeln aus Design-Paket 2 anpassen und `requirements.yaml` nach `docs/audit/2026-11-16-profi-audit/requirements-delta.md` ehrlich setzen.
**Freigaben (Chat, 17.11.2026):** „Büro-Etappe mit aufnehmen und ausbauen“, „requirements-delta übernehmen“.
**Branch:** claude/T-0702-buero-integration (Basis: claude/T-0685-b1-integration, Merge von claude/T-0674-buero-etappe)
**Dateien/Bereich:** Merge-Konflikte (Katalog, Einfach-Liste, Neuerungen 0.9.103), die sieben Büro-Programme (`MainWindow.Bewerbung/Karten/Vertraege/PdfWerkstatt/Formulare/DeckDesign/DocScan.cs`), Programm-Hülle (kleiner Helfer für den ersten Schritt), `requirements.yaml`, Fachwort-Test W0b-01, Zähl- und Katalogtests.
**Fertig, wenn:**
- [x] Merge ohne Konfliktmarker; Katalog und Matrix neu erzeugt (nicht von Hand gemischt)
- [x] Die sieben Studios haben einen ersten Schritt (`ProgramSpec.FirstStep`) und je Ansicht genau einen goldenen Knopf (`ProgramHeadDesign_Ui`)
- [x] W0b-01 grün durch Alltagswörter, nicht durch einen schwächeren Test
- [x] 37 Zurückstufungen und 2 Hochstufungen mit Beleg-Verweis in `evidence`, kaputte Umlaute repariert
- [x] gezielte Testklassen grün (Zahlen im Bericht); kein Volltest (macht die Hauptsitzung)
**Nicht tun:** Inhaltlicher Ausbau der Büro-Studios (direkte Bearbeitung statt Formular) – eigenes Paket. QA-017, QA-019, Codesignatur bleiben offen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0702-buero-integration.bericht.md`.
