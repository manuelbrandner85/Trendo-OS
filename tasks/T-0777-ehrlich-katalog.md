# T-0777: Stille Tests ehrlich, Katalog aus Belegen, Umzug zweier Studios

**Ziel:** Querschnitt Q4 und Q5 aus `docs/audit/2026-11-26-neubewertung/restliste.md` umsetzen, Tagesplaner und Recherche mit Quellen nach KI & Automatik umziehen und die Nutzerentscheidung vom 26.11.2026 festhalten (Etappe 0.9.112, freigegeben am 26.11.2026).
**Branch:** claude/T-0777-ehrlich-katalog
**Dateien/Bereich:** stille Tests aus `BERICHT.md` 3.7 (App- und Kern-Tests), `src/Trendo.Core/Programs/ProgramCatalog.cs`, `tools/Trendo.Render/Program.cs` (Katalog-Erzeuger), SHA-Listen in `reports/T-0698`, `T-0703`, `T-0707`, `T-0723`, `src/Trendo.App/StudioCatalog.cs`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `docs/improvement-42/studio-capability-matrix.md`, `docs/audit/2026-11-26-neubewertung/fehlende-studios.md`.
**Fertig, wenn:**
- [ ] Echtlauf-Tests ohne Voraussetzung melden „übersprungen“ (App `Assert.Skip`, Kern `Ueberspringen.Weil`), Netz-Tests schlucken keine Verbindungsfehler; eine Quelltext-Prüfung hält das fest (rot auf altem Stand).
- [ ] Der Katalog-Erzeuger nimmt den Teststand aus Belegen: Register nur mit gültigem Rohbeleg (SHA-256), dazu der eingelesene TRX-Lauf; PC_VERIFIED für SYS-04, SYS-07, CORE-04, X-08 zurückgenommen.
- [ ] SHA-Listen der vier Belegordner passen wieder zum eingecheckten Inhalt.
- [ ] Tagesplaner und Recherche mit Quellen stehen unter KI & Automatik, Übersicht im Bild geprüft.
- [ ] Nutzerentscheidung in `fehlende-studios.md` nachgetragen (51 fehlen, erste Etappe Wissen & Lernen).
- [ ] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung).
**Nicht tun:** `requirements.yaml` ändern (Sperre in `.claude/settings.json`), Katalog endgültig erzeugen (macht die Hauptsitzung mit TRX), echte Trendo-Installation anfassen, nach main pushen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0777-ehrlich-katalog.bericht.md`.
