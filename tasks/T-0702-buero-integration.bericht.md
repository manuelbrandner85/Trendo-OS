# Bericht T-0702: Büro-Etappe zusammengeführt, an die Design-Regeln angepasst, Statusliste ehrlich

**Auftrag:** `tasks/T-0702-buero-integration.md` · Freigaben im Chat 17.11.2026: „Büro-Etappe mit aufnehmen und ausbauen“, „requirements-delta übernehmen“.
**Branch:** `claude/T-0702-buero-integration` (Basis `claude/T-0685-b1-integration` @ 0262de00b, Merge von `claude/T-0674-buero-etappe` @ cc6f84484) · Arbeitskopie: eigener Temp-Ordner, nach dem Push gelöscht.

## 1. Zusammenführen
- Konflikte: `docs/programme/KATALOG.md`, `docs/programme/katalog.json` (neu erzeugt, nicht gemischt), `installer/neuerungen/0.9.103.md` (beide Inhalte in einer Notiz: erst „Deine Arbeit ist jetzt besser geschützt …“, dann „Sieben neue Studios unter Arbeiten & Büro“), `tests/Trendo.App.Tests/Daten/einfach-vorher-ausgeblendet.txt` (beide Blöcke behalten).
- Ohne Konflikt, geprüft: `MainWindow.W6.cs` (Init-Aufrufe beider Seiten: … `InitKuenstlerstudio(); InitBueroStudios(); InitDeckDesign(); InitDocScan(); InitPdfWerkstatt(); InitFormulare();`), `IntelligencePromptModel.cs` (7 neue Adapter angehängt, alle bestehenden erhalten), `.gitattributes` (Integrationsstand unverändert gültig), `CapabilityCatalog.cs`, `StudioKnowledge.cs`, `StudioCatalog.cs`, `StudioIcons.cs`, Zähl-Tests.
- Katalog: `dotnet build tools/Trendo.Render -c Release` + `TRENDO_KATALOG=. dotnet run … --no-build` → „Programmkatalog: 125 Programme“. Behalten: `katalog.json`, `KATALOG.md`, die sieben neuen Paketordner; 1 298 übrige erzeugte Dateien zurückgesetzt.
- Matrix: `TRENDO_UPDATE_MATRIX=1` mit `PromptActionTests` → 88/88 grün, `docs/improvement-42/studio-capability-matrix.md` unverändert gegenüber dem Merge-Stand (stimmt).
- Zahlen aus den Testläufen: 83 Studios im Katalog, 61 in der Übersicht, 110 Adapter, 80 „teilweise“ (`PromptBarTests`, `StudioFolds_Ui`, `PromptActionTests`, QA-015 `list.Count == 83`) – alle grün. `ActionId`s eindeutig (Prüfung in `PromptActionTests` grün).

## 2. Die sieben Studios an die neuen Regeln
**Erster Schritt (`ProgramSpec.FirstStep`)** – Knopf mittig auf der leeren Arbeitsfläche, löst die echte Funktion aus:
| Programm | Knopf | Wann sichtbar | Was passiert |
|---|---|---|---|
| Bewerbung & Lebenslauf | Namen eintragen | Name leer, keine Station, keine Ausbildung | Ansicht „Lebenslauf“, Namensfeld im Fokus |
| Karten & Einladungen | Text anpassen | „Einfach“, Eigenschaften eingeklappt, Vorlage unverändert | Eigenschaften (Texte, Farben) aufklappen |
| Verträge-Ordner | Ersten Vertrag eintragen | keine Verträge | neuer Vertrag + Eigenschaften |
| PDF-Werkstatt | Dateien hinzufügen … | keine Dateien, keine Ergebnisse | Dateiauswahl |
| Formulare ausfüllen | Formular öffnen … | kein Formular | Dateiauswahl |
| Präsentationen gestalten | Stichpunkte schreiben | Gliederung = Platzhalter der leeren Vorlage | Gliederungsfeld im Fokus, Text markiert |
| Dokumente scannen | Fotos hinzufügen … | keine Seite | Dateiauswahl |
Neu in der Hülle (`MainWindow.ProgramShell.cs`): `ProgramFirstFocus(d, feld)` / `ProgramFirstDone(d)` für erste Schritte „in ein Feld schreiben“ (Karte verschwindet, Feld bekommt den Fokus). Bilder geprüft (Render 1600 × 1000, „Einfach“): Verträge, Bewerbung, Präsentationen, Scannen – Karte mittig, ein goldener Knopf im Programm.

**Gold-Regel:** `ProgramHeadDesign_Ui` („V-03: je Ansicht höchstens ein goldener Knopf …“) grün über alle Hüllen-Programme inkl. der sieben. Dabei zwei Merge-Fehler gefunden und behoben:
- **V-01 Verträge-Ordner:** Arbeitsfläche begann bei 318 px (Grenze 300). Ursache: `PathFields.Classify` hielt die Auftragszeile wegen „Ordner“ im Programmnamen („Schreib hier, was in „Verträge-Ordner“ …“) für ein Ordnerfeld und hängte „Ordner wählen …“ an – die Zeile brach um. Fix: `auftrag-prompt` ist Freitext (`src/Trendo.Core/Shell/PathFields.cs`). Rot-Grün: neue Zeile in `T0201_PathFields` war rot (1 von 38), danach 38/38 grün.
- **V-04 Dokumente scannen:** im leeren Scan war kein Werkzeug markiert. Jetzt gilt der Modus ohne Seite für die nächsten Seiten (`ScanSetModeAll`), markiert ist der aktuelle Modus.

**W0b-01 „Keine Fachwörter“:** Ursache aus der Büro-Etappe: „Laufzeit“ im Verträge-Ordner (Meldung, Fehlermeldung, Überschrift). Ersetzt durch „Vertragsdauer“/„Mindestdauer“ („Vertragsdauer und Kündigungsfrist“). Auf dem Integrationszweig war der Test zusätzlich durch zwei fremde Stellen rot – ebenfalls mit Alltagswörtern ersetzt (Test unverändert): Geld/Kredit „Laufzeit (Monate)“ → „Dauer (Monate)“ (4 Texte), Code-Editor „lädt .NET die Laufzeit“ → „lädt .NET noch Bestandteile nach“. W0b_Look 3/3 grün.

**Einfach-Liste:** `UebersichtEinfach_Ui` im Bericht-Modus: in den sieben Büro-Studios fehlt nichts. Gefunden wurden 36 Zeilen aus anderen Paketen (Comic, Podcast: Eigenschaften in „Einfach“ eingeklappt → „Mehr Werkzeuge“; Geld: DATEV, Steuersätze, Kredit-Feinheiten erst ab „Erweitert“, im Code geprüft: `level >= UserLevel.Erweitert`). Mit Ersatzweg-Kommentar eingetragen; `Simple_keeps_every_function` danach grün.

## 3. Statusliste (`requirements.yaml`)
- 37 Zurückstufungen genau nach `requirements-delta.md`: `test_status: PC_VERIFIED → NOT_RUN`; `evidence: docs/audit/2026-11-16-profi-audit/requirements-delta.md` mit `evidence_sha256`; alter Beleg erhalten als `evidence_previous` (+ `evidence_previous_sha256`); Grund aus dem Delta in `note`. CORE-09, SPEC-04, SPEC-16 zusätzlich als „Studio entfernt (StudioCatalog.Removed)“ gekennzeichnet.
- 2 Hochstufungen: STUDIO-MUSIK-01 und STUDIO-IMAGEAI-01 `NOT_RUN → PC_VERIFIED` mit `reports/echtlauf/2026-11-16/acestep.log` bzw. `sdcli.log` + SHA-256 (Rohdatei-Prüfsumme in `SHA256SUMS` abgeglichen); `implementation_status` bleibt IN_PROGRESS, Hinweis „ein Fachlauf, nicht jede Funktion“.
- Kaputte Umlaute (3 Zeilen aus der Büro-Etappe: STUDIO-PDFTOOLS-01, STUDIO-FORMS-01) repariert: „Büro“, „„Ja, weiter““, „Formulare ausfüllen“.
- Sieben neue Studios: alle IN_PROGRESS / NOT_RUN (kein Echtlauf-Beleg für die Studios selbst; Tesseract/LibreOffice-Echtläufe belegen Werkzeuge, nicht diese Studios).
- QA-017, QA-019, Codesignatur unverändert offen.
- **Zahlen (Abschnitt `modules`):** vorher PC_VERIFIED 53 / NOT_RUN 30 → nachher PC_VERIFIED 18 / NOT_RUN 65. Gesamte Datei: PC_VERIFIED 83 → 48, NOT_RUN 251 → 286, TESTED 25 → 25.
- `EvidenceTests` (Belege mit Prüfsumme gültig, >100 Paare) grün.

## 4. Prüfungen (nur gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (`dotnet test … --filter`): BueroTests, DeckDesignTests, DocScanTests, FormulareTests, PdfWerkstattTests, VorbildAnalysenTests, EvidenceTests, DecisionRegisterTests, QA015_StudioStatus, PromptActionTests, T0201_PathFields, Bauplan → **195/195 bestanden**; nach Katalog-Neuerzeugung VorbildAnalysen + PromptAction + Evidence erneut **92/92**.
- App (`Trendo.App.Tests.exe -class …`): UebersichtEinfach_Ui, W0b_Look, ProgramHeadDesign_Ui, BueroStudios_Ui, DeckDesign_Ui, DocScan_Ui, Formulare_Ui, PdfWerkstatt_Ui, PromptBarTests, StudioFolds_Ui, StudioIconTests, StudioBilder → **51 Tests, 50 bestanden, 1 rot**: „V10 Übersicht: höchstens 40 Knöpfe“ – `3d: 43 Knöpfe` (3D-Modelle, CORE-05; nicht Büro, nicht durch diesen Merge berührt).
- Paket-Tests `python -m unittest discover -s tests/claude_handoff`: 11 Tests, 1 rot – `test_keine_funktion_verschwunden`: `FN-feld-fin-csv`, `FN-knopf-fin-import` fehlen. Nachgewiesen: auf dem Integrationszweig (HEAD) gibt es `fin-csv`/`fin-import` nicht mehr, auf dem Büro-Zweig noch (`MainWindow.W5Finance.cs`) – vom Geld-Paket T-0694 entfernt, nicht von T-0702.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: **NICHT GETESTET** in diesem Paket (nur Zusammenführung; die Büro-Tests der Etappe laufen grün).
2. Fremddatei/Export im Fremdprogramm: **NICHT GETESTET** (kein Beleg unter `reports/T-0702/`).
3. Absturz ≤ 30 s: **TEILWEISE** – die sieben Programme nutzen die Hülle mit Autosicherung (20 s); kein eigener Abbruch-Test hier.
4. Claude/KI-Fachaufgabe: **NICHT GETESTET**.
5. Klick bis Datei: **TEILWEISE** – BueroStudios_Ui, PdfWerkstatt_Ui, Formulare_Ui, DeckDesign_Ui, DocScan_Ui grün (Werkzeuge teils ersetzt, siehe deren Tests); erster Schritt nur über die Gold-/Kopf-Tests geprüft, kein eigener Klicktest je neuem ersten Schritt.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Einfach/Erweitert über UebersichtEinfach_Ui und ProgramHeadDesign_Ui; Rechtsklick/Ziehen nicht geprüft.

## Was NICHT geprüft wurde
Volltest aller drei Testprojekte; Api-Tests; eigener Klicktest für jeden der sieben neuen ersten Schritte; Echtläufe der Büro-Studios mit Fremdprogrammen; Bilder von Karten, PDF-Werkstatt und Formulare (nur Verträge, Bewerbung, Präsentationen, Scannen gerendert).

## Offene Punkte
- `UebersichtEinfach_Ui.Every_view_stays_clear`: `3d` hat in „Einfach“ 43 Knöpfe (Grenze 40) – Paket 3D/CAD.
- `tests/claude_handoff` Funktions-Baseline: `fin-csv`/`fin-import` fehlen seit dem Geld-Paket – Nutzerentscheidung oder Baseline nachziehen.
- Ausbau der sechs „teilweise“ Büro-Studios (direkte Bearbeitung statt Formular) – späteres Paket.
- `CapabilityCatalog`/`StudioKnowledge` (Core) nennen für Verträge noch „Laufzeit“ (nicht in der Oberfläche geprüft, nicht vom Test erfasst) – bewusst nicht geändert, um die Matrix nicht neu erzeugen zu müssen.

## Status
Zusammengeführt, gezielte Tests grün bis auf zwei nachgewiesen fremde rote Tests; gepusht nur auf `claude/T-0702-buero-integration`.
