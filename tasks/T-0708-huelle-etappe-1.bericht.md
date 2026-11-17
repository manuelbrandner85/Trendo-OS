# Bericht T-0708: B6 „Programm-Hülle für alle“ – Etappe 1

**Branch:** `claude/T-0708-huelle-etappe-1` (vom Integrationszweig `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%\trendo-auftrag-T-0708`.
**Grundlage:** Audit `BERICHT.md` Befund 1 und Maßnahme 8, `audit-6-architektur.md` Punkt 1 und 6.

## Was war → was ist jetzt

| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| 1 Umzugsliste | keine Liste; Audit nennt „rund 85“ Programme ohne Hülle | `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`: alle 86 Programme mit `Shell: false` aus `docs/programme/katalog.json`, je Speicherart, Autosave, eigenes Rückgängig, Fach, Etappe; Ergebnis: 12 Dokumente ohne jedes Autosave (E1), 51 ohne Dokument | Liste (Erhebung per Quelltextsuche, Unsicheres als „ungeprüft“ markiert) |
| 1 Dateiformat/Migration | `ProgramDocument.FormatVersion = 1`, fehlende Angabe = 1, neuere Fassung abgelehnt (T-0680) – nicht für Programme außerhalb der Hülle festgelegt | festgelegt und beschrieben (Abschnitt 2 der Liste): Hüllen-`.tdoc` Fassung 1, Migrationsweg je Stufe im Laden; angeschlossene Programme behalten ihr eigenes Format, Notsicherung als `.tdoc` mit `data.format` | Core `T0708_ProgramLinkTests.Old_files_stay_readable` (alte `.tdoc` ohne `version`/`meta`, mit BOM; neuere Fassung abgelehnt), `.Old_letter_file_readable` (Brief-JSON im Format von Commit 48054cd71) |
| 2 Anschluss ohne zweites System | 15 eigene Rückgängig-Systeme, zwei Notsicherungswege (Audit 6) | neu `src/Trendo.Core/Programs/ProgramLink.cs`: legt den Stand eines Programms in ein `ProgramDocument` – Rückgängig (100), Notsicherung (`programme/sicherung`, atomar), Wiederherstellen der Hülle; kein neues Speicher- oder Rückgängig-System. Die Hülle ruft angeschlossene Programme in ihrem 20-s-Takt mit (eine Zeile in `ProgramShell.cs`) | Core `Undo_redo_many_steps`, `Autosave_pending_restore_saved` |
| 2 Briefe schreiben | kein Autosave, kein Rückgängig (Brief-Entwurf nur im Speicher bis „Speichern“; `W1Office.cs` LetterView, DocActions ohne Rückgängig) | an `ProgramLink` angeschlossen: jede Aktion und jede Tipp-Pause (1 s) ist ein Rückgängig-Schritt; Notsicherung im Takt der Hülle (Getipptes ohne Pause wird mitgenommen); beim nächsten Öffnen Wiederherstellen-Karte (Wiederherstellen / Verwerfen); nach dem Speichern ist die Sicherung weg; `.trendo-brief.json` jetzt atomar. Claude-Änderungen am Brief (`ClaudeSyncLetter`) sind damit ebenfalls rückgängig machbar | App `T0708_HuelleEtappe1_Ui.Letter_undo_redo` (echtes Tippen, Knöpfe), `.Letter_recover_after_restart` (Fenster ohne Speichern geschlossen, neues Fenster, Karte, Zurückholen, Speichern räumt weg, Verwerfen löscht); Core `Kill_while_autosaving_link` (16 harte Prozessabbrüche) |
| 2 Trendo CAD | Autosave war schon da (T-0691, Office-Weg, atomar, echter Abbruchtest); Rückgängig 80 | Rückgängig 100 wie die Hülle; Notsicherung unverändert (Umzug in E2, siehe Liste) | `Doc_actions_cad_3d_code` (nach 120 Änderungen 100 Schritte zurück; mit 80 bliebe der Wert stehen), bestehend `Studio3DCadCrashTests`, `Studio3DMeshEditUiTests` |
| 2 3D-Modelle erstellen | Autosave war schon da (T-0691) | unverändert (Rückgängig 100 / 150 MB, Notsicherung Office-Weg) | bestehend `Studio3DCadCrashTests.Kill_while_autosaving_scene`, `Studio3DMeshEditUiTests` |
| 2 Code-Editor | Entwürfe waren schon da (T-0688, `CodeDrafts`, atomar, Karte) | unverändert; Rückgängig bleibt das des Editors (zeichengenau je Datei) | bestehend `CodeEditor_Ui`, `T0688_SoftwareStudio_Ui` |
| 3 V-14-Gruppe | Briefe: nur Speichern + Exportieren; CAD: Speichern im Menü „Datei“, Rückgängig/Wiederholen am Ende der Leiste; 3D: in der linken Karte; Code-Editor: kein Rückgängig-Knopf | Speichern · Rückgängig · Wiederholen · Exportieren (`DocActions`, T-0700) links oben in allen vier: Briefe neben dem Titel; CAD am Anfang des Menübands nach „Datei“ (Exportieren-Menü STL/3MF/STEP/PDF/DXF); 3D in „Einfach“ neben der Eingabe, ab „Erweitert“ am Anfang der Dateizeile (Exportieren-Menü OBJ/STL/GLB/glTF/FBX/Blender); Code-Editor am Anfang der Werkzeugleiste (ohne Exportieren – kein Export-Begriff im Editor). Alte Knöpfe behalten Namen und Wirkung | `Doc_actions_cad_3d_code`, `Letter_undo_redo`, `DesignPaket3_Ui.Doc_actions_same_place` (für Briefe jetzt strenger: verlangt Rückgängig und Wiederholen); Bilder `reports/T-0708/*.png` |

Gefundener Fehler beim Testen: Die V-14-Gruppe als eigene Zeile über „3D-Modelle erstellen“ machte die 3D-Fläche im Testfenster (1440×900) so niedrig, dass ein Klick auf den Würfel nicht mehr traf (`Studio3DViewport_Ui` rot; Gegenprobe ohne die Zeile grün). Die Gruppe steht deshalb in einer vorhandenen Zeile.

## Angepasste bestehende Tests (nicht geschwächt)
- `DesignPaket3_Ui.Doc_actions_same_place`: erwartete für Briefe nur „Speichern, Exportieren“. Jetzt „Speichern, Rückgängig, Wiederholen, Exportieren“ – strenger.
- `tests/Trendo.CrashProbe`: neuer Modus `link` (bestehende Modi unverändert).

## Rot-Grün
Die neuen Tests nutzen `ProgramLink`, `MainWindow.LinkTypingDelay`, `letter-undo`, `cad-export`, `3d-export`, `ce-undo` – auf dem alten Stand nicht übersetzbar bzw. nicht vorhanden, damit rot. `Studio3DViewport_Ui` war mit einer Zwischenfassung rot und ist mit der endgültigen grün.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (`T0708_ProgramLinkTests`, `Studio3DCadCrashTests`, `ProgramCrashTests`, `ProgramDocument*`, `Letter*`, `NoFixedUser*`, `CodeDrafts*`): **63 bestanden, 0 Fehler, 1 übersprungen** (Rechtschreibung im Brief braucht das deutsche Wörterbuch, fehlt auf dem PC – unabhängig). Darin der echte Abbruchtest `Kill_while_autosaving_link` (16 harte Abbrüche). Rohlog `reports/T-0708/core-tests.log`.
- App, 44 Klassen (u. a. `T0708_HuelleEtappe1_Ui`, `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket3_Ui`, `ProgramShellUiTests`, `ProgramCrashUiTests`, `OfficeRecoveryUiTests`, alle Brief-, CAD-, 3D- und Code-Editor-Klassen, `SimpleMode_Ui`, `EinfachBedienen_Ui`, `EveryButton_Alltag`): **212 Tests, 0 Fehler, 0 übersprungen**. Rohlog `reports/T-0708/app-tests.log` (der Läufer schreibt nur Fehler und Summe), Nachlauf der eigenen Klasse `app-tests-t0708.log` (3/3).
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests OK**. Rohlog `reports/T-0708/baseline.log`.
- Bilder (Render, 1600×1000): `reports/T-0708/briefe.png`, `cad.png`, `3d.png` (Einfach), `3d-profi.png`, `code.png` – angesehen: Gruppe links oben; die neuen Knöpfe sind nicht golden (Speichern in Briefe war schon die goldene Hauptaktion). Prüfsummen `reports/T-0708/SHA256SUMS.txt`. Ein Bild der Wiederherstellen-Karte entstand nicht (Bildaufnahme im Testfenster lieferte kein Bild); die Karte ist im UI-Test geprüft.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien (für die Programme dieser Etappe)
1. Aus leerer Vorlage ein echtes Ergebnis: **NICHT GETESTET** in diesem Paket (nicht Gegenstand; Briefe speichern im UI-Test einen echten Brief als DOCX + JSON).
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: **NICHT GETESTET** (nicht Gegenstand dieser Etappe).
3. Prozessabbruch verliert höchstens 30 s: Briefe **VOLLWERTIG** für den Sicherungsweg (16 echte Abbrüche des Hilfsprogramms mit genau dem Code der App, nie halbe Sicherung, höchstens der laufende Schritt verloren; Takt 20 s + Tipp-Pause 1 s ≤ 30 s; UI-Test mit geschlossenem Fenster). Ein Abbruch der *App selbst* (Prozess der Oberfläche getötet) ist nicht gefahren – das Fenster wurde ohne Speichern geschlossen. CAD/3D/Code-Editor: wie vorher (T-0691/T-0688, echte Abbruchtests).
4. Claude/KI ändert, rückgängig machbar: **TEILWEISE** – Briefe: Claudes Änderung kommt über `ClaudeSyncLetter` ins Programm und ist durch den Anschluss ein Rückgängig-Schritt; nicht mit echtem Claude geprüft.
5. Test vom Klick bis zur echten Datei: **TEILWEISE** – Briefe: Klick „Speichern“ → echte `.docx` + `.trendo-brief.json`, Notsicherung als echte Datei; ohne Fremdprogramm.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – V-14-Gruppe in allen Bedienstufen sichtbar; Strg+Z im Brief wirkt weiter im jeweiligen Eingabefeld (Feld-Rückgängig), die Knöpfe nehmen ganze Schritte zurück; keine neuen Kürzel.

## Was NICHT geprüft wurde
- Kein Volltest (macht die Hauptsitzung).
- Kein harter Abbruch des App-Prozesses selbst (nur des Hilfsprogramms mit demselben Kern-Code und Schließen des Fensters ohne Speichern).
- Zwei Trendo-Fenster gleichzeitig mit Briefen: Die Sperre der Hülle (`ProgramLocks`) gilt für angeschlossene Programme noch nicht – zwei Fenster könnten dieselbe Brief-Sicherung anbieten (offener Punkt).
- Echter Claude-Lauf.

## Offene Punkte
- E2: `OfficeAutosave` (Text, Tabelle, Präsentation, CAD, 3D) auf `ProgramLink` umziehen → eine Notsicherung; alte Dateien in `wiederherstellen/` beim ersten Start noch anbieten.
- Sperre gegen zweites Fenster für `ProgramLink`-Sicherungen.
- Versionsfeld in `.tcad` und Szene-JSON (bei deren Umzug).
- Reihenfolge der nächsten Etappen: siehe `huelle-umzug.md` Abschnitt 4 (E1b restliche 11 Dokumente ohne Autosave → E2 Büro/eine Notsicherung → E3 → E4 → E5).

## Berührte Dateien
- neu: `src/Trendo.Core/Programs/ProgramLink.cs`, `src/Trendo.App/MainWindow.ProgramLinks.cs`, `tests/Trendo.Core.Tests/T0708_ProgramLinkTests.cs`, `tests/Trendo.App.Tests/T0708HuelleEtappe1UiTests.cs`, `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`, `tasks/T-0708-*`, `reports/T-0708/*`
- geändert: `src/Trendo.Core/Programs/ProgramDocument.cs` (+`MarkClean`), `src/Trendo.Core/Letters.cs` (Brief-JSON atomar), `src/Trendo.App/MainWindow.ProgramShell.cs` (**eine Zeile** `LinksAutosave()` in `ProgramAutosaveAll`), `MainWindow.W1Office.cs`, `MainWindow.LetterPro.cs` (Briefe), `MainWindow.Cad.cs`, `MainWindow.W4Studio3D.cs`, `MainWindow.SimpleMode.cs`, `MainWindow.CodeEditor.cs` (V-14), `MdiIcons.cs` (Symbole Rückgängig/Wiederholen), `tests/Trendo.CrashProbe/Program.cs`, `tests/Trendo.App.Tests/DesignPaket3UiTests.cs`
- nicht angefasst: ApprovalGate (B4), Claude-Brücke (B5), Büro-Studios aus T-0705.

**Status:** Etappe 1 umgesetzt und gezielt geprüft; nicht im Volltest.
