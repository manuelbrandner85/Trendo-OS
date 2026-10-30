# Audit Verbesserungen 98–102 gegen Repo-Stand HEAD 7e1b863

Rolle: Requirements- und Audit-Agent (02-requirements-decisions / 19-qa-verification). Stand: 30.10.2026.
Methode: Ich habe nur gelesen und gesucht (Grep/Read). Es lief kein Build und kein Test. „Test vorhanden“ heißt: Der Test lag im letzten Volltest (30.10.2026, c062bb3, grün). Das ist **kein PC_VERIFIED**. Einige Office-Abgleichstests laufen nur, wenn LibreOffice da ist („nur mit LibreOffice“). OCR-Tests brauchen ein installiertes Tesseract. Ohne diese Programme sind die Tests möglicherweise übersprungen worden.
Detailtabelle: `V098-102.csv` (262 Zeilen, Trennzeichen `;`).

## Gesamtbefund vorab

- **IMP-98 bis IMP-102 fehlen in `docs/spec/decisions.yaml` und `requirements.yaml`.** Das Register endet bei IMP-97. `DecisionRegisterTests` prüft „Verbesserungen 13–97“. Für die Aufnahme braucht es ein Decision-Delta (laut Datei-Kopf ändert „nur Manuel“ `decision`) und eine Anpassung dieses Tests. Die Statusführung (`StatusRules`, `Progress.GermanStatus`) kennt `APPROVED_SPEC` bisher nicht.
- Der Programmrahmen `ProgramShell`/`ProgramDocument` ist solide und getestet, wird aber **nur von zwei Programmen** genutzt (`MainWindow.ProgGame.cs`, `MainWindow.ProgMode.cs`). Text, Tabelle, Präsentation, PDF, CAD und TV sind eigene Views, die den Rahmen nicht verwenden. Viele Anforderungen sind deshalb nur „TEILWEISE“ erfüllt.

## Verbesserung 98 – Native Program GODMODE + IPTV Zero-Config

| Teil | IMPLEMENTIERT_MIT_TEST | TEILWEISE | NUR_SPEZIFIZIERT | IMPLEMENTIERT_OHNE_TEST |
|---|---|---|---|---|
| V98-F01…F50 | 9 | 39 | 2 | 0 |
| V98-TV-01…TV-11 (Zero-Config, IDs von mir vergeben) | 0 | 7 | 4 | 0 |

**Wichtigste Lücken**
1. **Xtream-Passwort liegt im Klartext.** `MainWindow.Tv.cs` `TvAddSource` speichert `Xtream.PlaylistUrl(server,user,pass)` als `TvSource.Url` im TvLibrary-Zustand auf der Platte. Die Spec verlangt einen verschlüsselten Speicher. `ISecretStore`/`WindowsCredentialStore` in `src/Trendo.Core/Secrets.cs` gibt es bereits. (V98-TV-06/09, Risiko hoch)
2. **Zero-Config fehlt ganz.** Der Code sagt ausdrücklich „Trendo bringt keine Sender mit“ (`MainWindow.Tv.cs` ~Z. 449, ebenso `docs/improvement-42/studio-capability-matrix.md`). Es gibt kein Quellen-/Lizenzregister und keine Mediathek-Anbindung (TV-01/02/04).
3. **Office-Programme haben kein Autosave und keine Wiederherstellung.** In `MainWindow.Text.cs`, `Tabelle.cs` und `Praesentation.cs` habe ich nichts gefunden. Nur ProgramShell-Programme sichern automatisch (F14/F15).
4. `.tdoc` schreibt `version=1`, `Load` wertet die Version aber nicht aus. Es gibt keine Migration (F10). Das Undo arbeitet mit Snapshots und eignet sich nicht für große Medien- oder CAD-Daten (F16).
5. Es gibt keine zentrale `ProgramCommand`-Registry (die Palette durchsucht den Button-Baum), keine speicherbaren Layouts (F38), keine Drop-Tests (F08) und kein Einheiten-/Farbmodell über Studios hinweg (F47).

**Konflikte/Widersprüche**
- TV-01 widerspricht dem dokumentierten Ist-Verhalten „bringt keine Sender mit“. Das muss als Entscheidungs-Delta festgehalten werden und darf nicht still geändert werden.
- F10 und TV-06 nennen „Migration“. Gemeint sein darf nur die Migration des eigenen Dokumentschemas bzw. der eigenen TV-Quelle. Ein Datenumzug wäre IMP-25 (Smart Migration, **NEIN**).
- F20/F44 (Job- und Recovery-Status) dürfen keinen zweiten Scheduler und keine zweite Job-Engine erzeugen. F35/F48 dürfen keine zweite Registry oder Statusmaschine neben `StudioCatalog`/`CapabilityCatalog`/`StatusRules` aufbauen.
- Die 120-h-Regel ist in `CLAUDE.md` korrekt enthalten (inkl. 24-h-Warnung, `ForensicsLab.MaxLife`). Die Spec sagt dazu nichts Abweichendes.

**Fehlende Quellen**
- Die Spec verweist auf `IPTV_ZERO_CONFIGURATION_REQUIREMENTS.md`. Vorhanden ist nur `altbestand/V98_IPTV_ZERO_CONFIGURATION_REQUIREMENTS.md`, ich habe diese genutzt.
- Die TV-Anforderungen haben in der Quelle keine IDs. Ich habe TV-01…TV-11 vergeben (10 Setup-Punkte und die Standardoberfläche).

**Erste Arbeitspakete (klein, gut testbar)**
- **AP-98-1: Xtream-Zugang in den Secret-Store verlagern.** In `TvAddSource`/`TvRefreshSource` (`src/Trendo.App/MainWindow.Tv.cs`) und `TvLibrary.AddSource` (`src/Trendo.Core/Tv/IpTv.cs`) nur Server und Benutzer speichern, das Passwort über `ISecretStore` (`src/Trendo.Core/Secrets.cs`) ablegen. Bestehende `get.php`-URLs beim Laden migrieren. Ein Negativtest in `tests/Trendo.Core.Tests/IpTvTests.cs` prüft: Die State-Datei enthält kein Passwort. Agent 03 baut, Agent 19 prüft.
- **AP-98-2: Schema-Version in `ProgramDocument.Load` auswerten.** Unbekannte höhere Version wird verständlich abgelehnt, für v1 gibt es einen Migrations-Hook. Tests in `tests/Trendo.Core.Tests/ProgramDocumentTests.cs`. Agent 06 baut.

## Verbesserung 99 – REA Professional Reconstruction

| IMPLEMENTIERT_MIT_TEST | TEILWEISE | NUR_SPEZIFIZIERT | IMPLEMENTIERT_OHNE_TEST |
|---|---|---|---|
| 2 (REA-45, REA-49) | 35 | 13 | 0 |

**Wichtigste Lücken**
1. `CleanRoom` (`src/Trendo.Core/CleanRoom.cs`) hat Rollen-Ordner, ein Tagebuch, einen Shingle-Vergleich und einen Bericht. Es fehlen **strukturierte Datensätze** (Referenz/Version/Nutzungsrecht, `evidence_status`, Featurevertrag) und ein BLOCKED-Status (REA-01/02/05/09).
2. Die Rollentrennung ist nur organisatorisch umgesetzt (Ordner). Es gibt keine technische Zugriffssperre (REA-10/23).
3. Paritäts-Dashboard und Paritätsformel fehlen (REA-41/42). Visuelle Regression mit Baselines fehlt (REA-34).
4. Symlink- und Prompt-Injection-Negativtests fehlen (REA-38). Golden-Outputs im Repo fehlen, die LibreOffice-Abgleiche laufen nur bedingt (REA-32).
5. Viele Punkte sind reine Prozessanforderungen (REA-21/23/24/40/44). Im Code sind sie nicht prüfbar, ich habe sie trotzdem ehrlich als NUR_SPEZIFIZIERT/TEILWEISE geführt.

**Konflikte**
- REA-44 „Autonomous Task Director“: Im Produkt gibt es schon `src/Trendo.Core/W2/Agents.cs` (CORE-03), dazu kommen die 20 Entwicklungsagenten in `.claude/agents/`. Keine zweite Agentenlaufzeit bauen. Die Abgrenzung zwischen Produkt- und Entwicklungsagenten muss geklärt werden.
- Die Programmfamilien-Tabelle in Spec §9 nennt bei Business „CRM“. Das berührt **IMP-86 (CRM/Service-Desk) NEIN**. Erlaubt sind nur die bisher bestätigten CRM-/Kommunikationsfunktionen, kein eigenständiges CRM.
- REA-38 (Security-Negativtests) ist erlaubt, darf aber nicht in Richtung Antivirus wachsen (IMP-34 NEIN).
- Die Spec hält fest, dass `CleanRoom.Compare` kein Freigabe-Gate sein darf. Der Code hält das ein (nur Bericht).

**Fehlende Quellen**
- Keine. Alle genannten Dateien sind vorhanden (CleanRoom.cs, MainWindow.W5CleanRoom.cs, NACHBAU_FAHRPLAN.md, STUDIO_VORSCHLAEGE_2026-10-27.md).

**Erste Arbeitspakete**
- **AP-99-1: Paritätsformel als reine Funktion.** Zum Beispiel `ParityScore(observed, verified, inferred, unknown)` in `src/Trendo.Core/CleanRoom.cs`, Unbekannte werden separat ausgewiesen. Unit-Tests in `tests/Trendo.Core.Tests/W5CleanRoomTests.cs` (REA-42). Agent 05 baut, Agent 19 prüft.
- **AP-99-2: `CleanRoom.Create` um Pflichtangaben erweitern.** Neu sind Version, Nutzungsrecht und Analyseart (`referenz.json`). Fehlen sie, meldet `Report` BLOCKED (REA-01/02). Bestehende Tests erweitern.

## Verbesserung 100 – Office Native Suite

| IMPLEMENTIERT_MIT_TEST | TEILWEISE | NUR_SPEZIFIZIERT | IMPLEMENTIERT_OHNE_TEST |
|---|---|---|---|
| 8 | 38 | 4 (O100-26 Pivot, 37 Folien-QA, 46 PDF-Inhaltskorrektur, 48 PDF/A) | 0 |

Office ist der am besten abgedeckte Bereich. Text (T-0223/T-0230), Tabelle (T-0228), Präsentation (T-0229) und PDF (T-0223 P1–P6) haben viele echte Roundtrip-Tests.

**Wichtigste Lücken**
1. **Kein Autosave und keine Crash-Recovery** in Text, Tabelle und Präsentation (O100-05, Risiko hoch).
2. **Keine Sicherheits-Negativtests für Office-Container.** ZIP-Bomb-, XXE- und Makro-Tests (OFF100-SEC-01) fehlen, Abwehrcode im Office-Bereich habe ich nicht gefunden (O100-29/50).
3. Format-Verlustberichte fehlen (FULL/PARTIAL/READ_ONLY/UNSUPPORTED) für DOCX/ODT/PPTX/ODP (O100-19/38).
4. Pivot/Zielwertsuche fehlen, ebenso die Folien-Layout-QA. PDF-Signaturprüfung, Flatten, Stempel/Freihand und PDF/A fehlen.
5. Für die Tabelle gibt es keinen 50.000-Zellen-Lasttest und keine Virtualisierung im Code (O100-21). Für PDF fehlen die Miniaturleiste und ein Test mit 100 Seiten (O100-41).

**Konflikte**
- O100-18 (Sprache, Silbentrennung, Sprachwahl) darf nur die Dokumentsprache betreffen. Ein Ausbau zum Lokalisierungs-Studio wäre **LANG-2.0 / SPEC-07-Ausbau (NEIN)**.
- O100-16 „Autorenkennung“ nur lokal. Mehrbenutzer-Identität wäre IMP-23 (NEIN).
- Überschneidung mit IMP-45 (Office Studio, APPROVED). 100 ist ein Ausbau davon, kein zweiter Office-Kern.
- Die Spec sagt „101 noch nicht bestätigt“. Die 101-Spec meldet später JA. Das ist eine zeitliche Abfolge, kein Widerspruch.

**Fehlende Quellen**
- Keine. Die Spec weist selbst darauf hin, dass `STUDIO_VORSCHLAEGE_2026-10-27.md` teilweise veraltet ist (Präsentation „neu“). Der Code bestätigt das: `MainWindow.Praesentation.cs` und `SlideFiles.cs` sind vorhanden.

**Erste Arbeitspakete**
- **AP-100-1: Folien-Layout-QA als reine Core-Prüfung** auf `SlideDoc` (`src/Trendo.Core/Office/SlideDoc.cs`). Sie meldet Überlappung, Schrift unter Mindestgröße, Objekte außerhalb der Folie, fehlende Alt-Texte und fehlende Bilddatei. Test: ein defektes Deck ergibt mindestens 5 Befunde, neu in `tests/Trendo.Core.Tests/SlideTests.cs` (O100-37). Agent 10 baut, Agent 19 prüft.
- **AP-100-2: Office-Container-Schutz.** Grenzen für Eintragszahl und entpackte Größe sowie `DtdProcessing.Prohibit` beim Öffnen von DOCX/XLSX/PPTX (`DocxFormat.cs`, `SheetFiles.cs`, `SlideRead.cs`). Negativtests mit Bomben-Fixture in `tests/fixtures/`. Agent 03 baut und prüft.

## Verbesserung 101 – Media, Design & Broadcasting

| IMPLEMENTIERT_MIT_TEST | TEILWEISE | NUR_SPEZIFIZIERT | IMPLEMENTIERT_OHNE_TEST |
|---|---|---|---|
| 1 (101-L09 Livestream mit Freigabe) | 15 | 34 | 0 |

**Wichtigste Lücken**
1. **Trendo Grafik (G01–G10) gibt es nicht.** Es gibt keinen Vektoreditor, `SvgLite.cs` zeigt nur eigene einfache SVGs an. Inkscape steht nur als externes Programm in `Dependencies.cs`.
2. **Trendo Audio (A01–A09) gibt es nicht.** Kein MIDI, kein DSP, keine Mehrspur. Vorhanden sind nur die ACE-Step-Musik-KI und das Herausziehen der Tonspur.
3. **Bild hat keine Ebenen, Masken oder Pinsel.** `ImageTools.cs` bearbeitet die Datei destruktiv (Crop/Rotate/Resize/Helligkeit). Es gibt kein natives Projektformat.
4. **Video ist kein NLE.** Es gibt Clipliste, Ausschnitt und Concat (`MainWindow.W4Media.cs`, `MediaStudio.cs`). Mehrspur, Übergänge, Keyframes, Renderqueue und ffprobe-Prüfung fehlen.
5. **Live hat keine Regie.** Szenengraph, Overlays, Audiomischpult und Preview-/Program-Bus fehlen. Der Stream-Schlüssel liegt nur im Speicher, die Spec verlangt den OS-Store.

**Konflikte**
- Überschneidung mit den bestätigten IMP-51 (Video), IMP-52 (Bild/Grafik), IMP-53 (Audio) und IMP-73 (Broadcast/Live). Kein zweiter Kern, keine Doppel-Studios.
- „Trendo Grafik“ soll als Programm in einem **bestehenden** Studio (CORE-08) entstehen. Ein neuer `StudioCatalog`-Eintrag wäre eine eigene Entscheidung.
- Die TV/IPTV-Trennung ist im Code eingehalten (Live-Studio und TV getrennt).

**Fehlende Quellen und Abweichungen**
- Die Spec verortet `LiveSession`/`FfmpegCaptureFactory` in `MainWindow.Live.cs`. Tatsächlich liegen sie in `src/Trendo.Core/W6/LiveStream.cs`. Die Spec nennt nur Entwurfsverträge (`IMediaProjectStore`, `IRenderJob` …), im Repo gibt es dazu nichts.

**Erste Arbeitspakete**
- **AP-101-1: SVG-Sanitizer als Core-Funktion.** Neu z. B. in `src/Trendo.Core/MediaStudio.cs` oder einer eigenen Datei unter `src/Trendo.Core/`. Er entfernt `script`, `on*`-Attribute, externe `href` und `foreignObject`. Fixture-Tests in `tests/Trendo.Core.Tests/W4MediaTests.cs` (101-G08, Grundlage für Grafik). Agent 03 baut, Agent 19 prüft.
- **AP-101-2: Loudness-Messung über vorhandenes FFmpeg** (`ebur128`) als `MediaStudio.Loudness(ffmpeg, file)`, Abgleich mit `Broadcast2.LoudTargets`. Test mit erzeugtem Sinuston in `W4MediaTests.cs` (101-A10). Agent 14 baut.

## Verbesserung 102 – CAD, Engineering & Manufacturing

| IMPLEMENTIERT_MIT_TEST | TEILWEISE | NUR_SPEZIFIZIERT | IMPLEMENTIERT_OHNE_TEST |
|---|---|---|---|
| 1 (FER-38 Bambu-Adapter) | 28 | 21 | 0 |

**Wichtigste Lücken**
1. **Elektronik als Zeichenprogramm fehlt ganz** (ELE-11…17, 19, 20). Vorhanden sind nur Rechner (`W6/Electronics*.cs`) und eine Stückliste.
2. **CAD hat keinen Constraint-Solver, keine Maus-Skizze, keinen Import und keinen Quality-Gate.** Der Kern ist BSP-CSG. STEP wird nur facettiert erzeugt und ist im Code korrekt so benannt.
3. **Architektur hat weder Wand-Editor noch Geschossmodell, IFC oder Clash-Prüfung.** `RoomPlanner` legt Räume nebeneinander als SVG und rechnet.
4. **Fertigung begrenzt die Drehzahl still** auf die Maschinengrenze (`Cnc.cs`, Schnittwerte). Die Spec fordert eine Blockierung (FER-32). Es fehlen außerdem Hash-Manifest und ApprovalGate-Bindung für Druck/CNC (FER-39/40).
5. **Simulation ist nur analytisch** (`Engineering*.cs`). FEM-, Thermal- und SPICE-Solver fehlen. Die analytischen Werte taugen aber als Referenz für spätere Solver.

**Konflikte**
- **ARC-21…30 überschneiden sich mit IMP-55 und IMP-95** (beide Architektur/BIM, APPROVED). Laut decisions.yaml fehlt die Funktionsliste von IMP-95 im Repo („Abgrenzung zu IMP-55 klären“). Vor der Umsetzung ist eine Abgrenzung nötig.
- ELE überschneidet sich mit IMP-57, FER mit IMP-61, SIM mit IMP-56. Kein zweiter Kern.
- **SIM-48/50 und FER (Validierung)** berühren **IMP-97 „Invention, Prototyping & Product Validation“ (OFFEN)**. Diesen Umfang nicht über 102 vorwegnehmen.
- Die Spec nennt als Dateinamen `TRENDO_VERBESSERUNG_102_CAD_ENGINEERING_MANUFACTURING_NATIVE_SUITE_GODMODE.md`, geliefert wurde `VERBESSERUNG_102_GODMODE.md`. Das ist nur eine Namensabweichung.

**Fehlende Quellen**
- Funktionsliste von IMP-95 (laut Register). Sonst sind alle genannten Pfade vorhanden (`Cad/*.cs`, `MainWindow.Spec01/02/03/14.cs`, `MainWindow.Print3D.cs`, `Print3D/BambuLan.cs`).

**Erste Arbeitspakete**
- **AP-102-1: Masseneigenschaften im CAD.** `CadBody` bekommt Material und Dichte (Quelle Pflicht), dazu Masse und Schwerpunkt aus `Solid` (`src/Trendo.Core/Cad/CadDoc.cs`, `CadCsg.cs`). Test: Quader 100×50×20 ergibt 100000 mm³, mit Dichte die richtige Masse, ohne Dichte „nicht berechenbar“, in `tests/Trendo.Core.Tests/CadTests.cs` (CAD-08). Agent 12 oder 05 baut, Agent 19 prüft.
- **AP-102-2: Werkzeuggrenzen blockieren statt still begrenzen.** In `src/Trendo.Core/W6/Cnc.cs` wird eine Drehzahl über der Maschinengrenze als Fehler gemeldet, eventuell mit ausdrücklichem Bestätigungsweg. Test in `tests/Trendo.Core.Tests/W6Tests.cs` (FER-32). Achtung: Das ändert bestehendes Verhalten, deshalb Entscheidung vorlegen.

## Hinweise zur Bewertung

- Statusvergabe streng: Wenn ein Teil getestet ist, die Spec aber mehr verlangt, steht **TEILWEISE**. Die vorhandenen Tests sind trotzdem in `test_belege` aufgeführt.
- IMPLEMENTIERT_OHNE_TEST habe ich nicht vergeben. Zu jedem gefundenen Code gab es mindestens einen Test, oder der Code deckte die Funktion nur teilweise ab.
- Unklare Spec-Passagen: V98-F47 „Cross-Studio Consistency“ (Umfang Farbmanagement/Einheiten unbestimmt), V98-F49 und mehrere REA-Punkte sind reine Prozessregeln, V101/V102 nennen Entwurfsschnittstellen ohne verbindliche Signatur.
