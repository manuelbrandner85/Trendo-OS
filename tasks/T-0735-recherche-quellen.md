# T-0735: Recherche mit Quellen + Sicher recherchieren + Freie Bilder & Klänge

**Ziel:** Restliste Rang 5, 6 und 9 (docs/audit/2026-11-16-profi-audit/d7-restliste.md) einlösen: STUDIO-CITE-01 mit RIS/CSL-JSON und DOI-Abruf, SYS-08 mit Suche und behaltbarer Sammlung, X-07 mit echtem Laden nach Freigabe.
**Branch:** claude/T-0735-recherche-quellen
**Dateien/Bereich:** CiteKit (Core/Kreativ), ResearchDesk (Core/W3), FreeAssets und X-07-Adapter (Core), Recherche-/Quellen-/Assets-Teile der App; Tests dazu; Beleg `reports/T-0735/`.
**Fertig, wenn:**
- [x] STUDIO-CITE-01: Export RIS und CSL-JSON offline, Import RIS (BibTeX bleibt), DOI-Abruf über die öffentliche Crossref-Schnittstelle mit sichtbarer Herkunft an der Quelle
- [x] SYS-08: Suchquelle mit Trefferliste (Wikipedia-Schnittstelle, DuckDuckGo-HTML), Herkunft an jedem Treffer, keine .onion-/Heimnetz-Treffer; „Sammlung behalten“ statt Löschen bei jedem Start (Standard bleibt flüchtig)
- [x] X-07: KI-Auftrag „Quelle holen“ lädt nach Freigabe (ApprovalGate) wirklich herunter, Lizenz- und Quellenbeleg je Datei; der Knopf „Laden“ im Medienbrowser schreibt denselben Beleg
- [x] Netz nur über vorhandene Wege (`ReadOnlyWeb` in der App, `PromptServices.ReadBytes` im Kern) – kein HttpClient im Kern (QA-019)
- [x] Echtbeleg eines echten Abrufs unter `reports/T-0735/` mit Rohlog und SHA-256
- [ ] alle drei Testprojekte grün – Volltest macht die Hauptsitzung am Schluss; hier nur die berührten Testklassen
**Nicht tun:** kein Volltest, kein Merge, kein Release, kein Darknet-Abruf, keine Anmeldung bei Diensten, kein neuer Netz-Client im Kern.
**Am Ende:** Branch pushen, Bericht `tasks/T-0735-recherche-quellen.bericht.md`.
