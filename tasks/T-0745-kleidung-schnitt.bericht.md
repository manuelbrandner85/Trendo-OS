# Bericht T-0745: Kleidung – Schnittteile frei bearbeiten

**Auftrag:** `tasks/T-0745-kleidung-schnitt.md` (Restliste Rang 19, SPEC-08 TEILWEISE: „Schnitt nur aus Vorlagen“)
**Branch:** `claude/T-0745-kleidung-schnitt` (eigene Kopie von main 0.9.105 in `%TEMP%\trendo-auftrag-T-0745`)

## Was war
- Schnittteile (`GarmentBlocks`, `src/Trendo.Core/W6/GarmentPattern.cs`) entstanden nur aus Körpermaßen; die Fläche `garment-pattern` war ein reines Bild (`MainWindow.ProgModeGarment.cs`, alte Funktion `GarmentPatternCanvas`).
- Export PDF/DXF/Datenblatt und 3D-Anprobe rechneten immer mit dem Grundschnitt. Kein SVG des Schnitts (nur die Schnittlage aus Rechtecken).

## Was ist jetzt
- **Kern** `src/Trendo.Core/W6/GarmentEdit.cs`: Teil → Punkte mit Bézier-Griffen (Typ `VNode` und Abtastung `VectorKit.Sample` aus dem vorhandenen Vektor-Kern, kein zweiter Geometrie-Kern). Kurvenanpassung (≤ 0,05 cm Abweichung) zerlegt jede Kante in wenige Bézier-Stücke; benannte Kanten bleiben erhalten (Nähte, Saumzugabe, Anprobe hängen daran). Punkte verschieben, Griff ziehen (glatte Punkte bleiben glatt), Punkt einfügen ohne Formänderung (de Casteljau), Punkt löschen (Nahtecken nur, wenn die Kante einen weiteren Punkt hat – sonst Erklärung), Gerade ↔ Kurve, Selbstüberschneidung, Nahtlängenvergleich, Speichern im Dokumentfeld `gEdits` je Schnittart und Teil; kaputte/alte Daten fallen auf die Vorlage zurück.
- **Arbeitsfläche** `src/Trendo.App/GarmentPatternEditor.cs` (ersetzt das reine Bild, Name `garment-pattern` bleibt): Teil anklicken, Punkte und Griffe ziehen (Umschalt rastet auf 5 mm), Doppelklick auf eine Kante setzt einen Punkt, Kantenlängen am gewählten Teil, Rechtsklick-Menü (`garment-edit-menu-*`), Kürzel Entf/Rücktaste, Einfg, K, Pfeile (0,1 cm, mit Umschalt 1 cm), Tab/Umschalt+Tab, Esc. Strg+Z/Strg+Y über die Programm-Hülle – jeder fertige Zug ist genau ein Rückgängig-Schritt.
- **Leiste und Maße** (`MainWindow.ProgModeGarment.cs`): ab „Erweitert“ Leiste Punkt einfügen/löschen, Gerade ↔ Kurve, Teil zurücksetzen; Maße (Fläche, Umfang, jede Kante); Profi/Erweitert zusätzlich Nahtvergleich (✓/⚠ mit Unterschied in cm) und Punkt per Zahl setzen. „Einfach“: keine Leiste (V10-Grenze 40 Knöpfe), Bearbeitung mit Maus, Rechtsklick und Kürzeln; Maße sichtbar.
- **Nahtzugabe** folgt automatisch (`GarmentProduction.WithAllowance` rechnet auf dem bearbeiteten Umriss), ebenso Zuschnitt/Stoffbedarf.
- **Export**: PDF, DXF und Datenblatt nutzen die bearbeiteten Teile; neu **SVG 1:1** (`GarmentProduction.Svg`, Knopf `garment-svg`). Datenblatt nennt bearbeitete Teile und sagt ehrlich, dass der Größensatz mit den Grundschnitten rechnet.
- **3D-Anprobe** nutzt die bearbeiteten Teile (Auftrag aus `GarmentOf`); Hinweis `garment-tryon-edited` im Panel. Echter Blender-Lauf belegt (siehe unten).

## Geänderte Dateien
- neu: `src/Trendo.Core/W6/GarmentEdit.cs`, `src/Trendo.App/GarmentPatternEditor.cs`, `tests/Trendo.Core.Tests/GarmentEditTests.cs`, `tests/Trendo.App.Tests/GarmentEditUiTests.cs`, `reports/T-0745/*`, `tasks/T-0745-kleidung-schnitt*.md`
- geändert: `src/Trendo.App/MainWindow.ProgModeGarment.cs`, `src/Trendo.Core/W6/GarmentProduction.cs` (SVG, Datenblatt-Hinweis)
- gemeinsame Dateien (unvermeidbar): `.gitattributes` (eine Zeile `reports/T-0745/** -text`), `tests/Trendo.App.Tests/UebersichtEinfachUiTests.cs` (ein `Replaced`-Eintrag `garment-edit-` mit Ersatzweg Rechtsklick-Menü/Kürzel)

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…GarmentEditTests|…GarmentPatternTests|…GarmentProductionTests|…VectorKit|…NoFixedUser"` → **Bestanden! 50/50** (`reports/T-0745/core-tests-T-0745.log`)
- `Trendo.App.Tests.exe -class` GarmentEdit_Ui, Garment_Ui, SPEC08_Ui, SchnittEditor_Ui, Marker_Ui, Sewing_Ui, EinfachBedienen_Ui, ProgramLevel_Ui, DeadButtonTests, EveryButton_Alltag, UebersichtEinfach_Ui, T0710_MenuBar_Ui, ProgramCrashUiTests, StudiosAlle_Ui → **Total 232, Failed 0** (`reports/T-0745/app-tests-T-0745.log`). Zwischenstand: V10 schlug zuerst an (44 Knöpfe in Einfach, dann fehlender Ersatzweg) – behoben wie oben beschrieben.
- Rot-Grün: die neuen Tests kompilieren auf dem alten Stand nicht (`GarmentEdit`, `GarmentEditor`, `GarmentProduction.Svg` fehlen).
- Kein Volltest (macht die Hauptsitzung).

## Belege (`reports/T-0745/`, SHA-256 in `SHA256SUMS.txt`)
- Klick bis Datei: `GarmentEdit_Ui.Click_to_file` mit `TRENDO_T0745_OUT` → `schnitt-bearbeitet.dxf/.svg/.pdf`, `anprobe-auftrag.json`, `erwartet.json`.
- DXF → eigener Python-Leser (Standardbibliothek): alle Teile OK, Vorderteil +5,00 cm Schnitthöhe (`dxf-python-T-0745.log`). **Echt, aber selbst geschriebener Leser** (kein CAD-Programm vorhanden).
- SVG → Blender 5.2.3 SVG-Import: alle Schnittkanten maßgleich (≤ 0,01 cm) (`svg-blender-T-0745.log`). **Echtes Fremdwerkzeug.**
- PDF → xpdf pdftotext 4.06: 56 Seiten, alle Teilbeschriftungen (`pdf-text-T-0745.txt`). **Echtes Fremdwerkzeug** (prüft Text/Seiten, nicht die Geometrie).
- 3D-Anprobe → Blender 5.2.3 mit `trendo_anprobe.py` und dem von Trendo geschriebenen Auftrag (samples 48 statt 200, mit `-b`): TRENDO_FERTIG, 4 Bilder (`blender-anprobe-T-0745.log`, `anprobe-bearbeitet-vorn.png`).

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **TEILWEISE** – Teile werden frei umgeformt bis zur Produktionsdatei; ein ganz neues Teil von Null zeichnen geht nicht (Start ist immer ein Grundschnitt).
2. Fremddatei importieren/bearbeiten/exportieren: **TEILWEISE** – Export öffnet sich in Fremdwerkzeugen (Blender SVG, pdftotext, Python-DXF); Import fremder Schnitte (DXF-AAMA, Seamly) gibt es weiterhin nicht.
3. Abbruch verliert ≤ 30 s: **VOLLWERTIG (geerbt)** – Bearbeitungen liegen im Dokument, Notsicherung der Programm-Hülle; `ProgramCrashUiTests` grün; nicht eigens mit Schnittdaten getötet.
4. Claude/KI-Fachaufgabe: **FEHLT** für die Schnittbearbeitung (kein eigener KI-Befehl; nur der allgemeine Weg über die Projektdaten).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `Click_to_file` (Maus, Tastatur → DXF/SVG/PDF/Datenblatt/Auftrag), Dateien mit Fremdwerkzeugen gelesen.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **VOLLWERTIG** auf der Arbeitsfläche (Ziehen von Punkten/Griffen); Ablegen von Dateien auf der Schnittfläche gibt es nicht.

## Was NICHT geprüft wurde
- Echte Maus/Tastatur im sichtbaren Fenster (nur Avalonia headless); kein Bild mit gewähltem Teil und sichtbaren Punkten (Render-Werkzeug kann keine Auswahl setzen).
- DXF in einem echten CAD/Plotter-Programm (nicht vorhanden, nichts installiert).
- Anprobe über den Knopf in Trendo mit echtem Blender (Test ersetzt `GarmentRunOverride`); der echte Lauf wurde mit demselben Skript und Auftrag von Hand gestartet, mit weniger Samples; kein Vorher/Nachher-Bildvergleich.
- Andere Schnittarten als T-Shirt in der Oberfläche (Kern-Rundreise für alle 6 Arten in `GarmentEditTests.RoundTrip`).

## Offene Punkte
- Bearbeitung wird nicht gradiert (Größensatz rechnet mit Grundschnitten; im Datenblatt so benannt).
- Ändern der Körpermaße lässt bearbeitete Teile unverändert („Teil zurücksetzen“ holt die Vorlage).
- Kein Neuzeichnen ganzer Teile, kein Import fremder Schnittformate, keine Nahtzugabe je Kante einstellbar, kein KI-Befehl.

## Gegenprüfung behoben (18.11.2026, zweiter Commit auf diesem Zweig)
Rot-Grün: die neuen Tests wurden zuerst gegen den alten Stand gefahren (`reports/T-0745/rot-core-T-0745-fix.log`: 3 Kern-Tests rot; `reports/T-0745/rot-ui-T-0745-fix.log`: 3 Oberflächen-Tests rot, alter Editor und alte `MainWindow.ProgModeGarment.cs` eingesetzt, nur die Zeile mit dem ersetzbaren Zeitgeber entfernt), danach grün.

| Befund | Was jetzt | Test (Rot-Probe) |
|---|---|---|
| 1 MITTEL doppelter Punkt → Zugabe 0 | `GarmentEdit.Merge` führt Punkte zusammen, deren Stück kürzer als 0,5 mm ist (Griffe und Kantenbeginn bleiben); fielen dabei zwei Kantenanfänge aufeinander, wird mit Erklärung abgelehnt („Die Kante „Saum“ hätte so keine Länge mehr …“). `FromNodes` und das Übernehmen in der Oberfläche führen zusammen; `GarmentProduction.WithAllowance` überspringt zusätzlich Stücke ohne Länge (alte/fremde Daten). | `GarmentEditTests.DuplicatePoint_keeps_allowance` (kleinster Abstand Schnittkante–Naht = 1,2 cm; rot: 0), `Merge_and_edited`, `GarmentEdit_Ui.Reject_and_export_hint` (Ecke per Zahl auf die Nachbarecke → abgelehnt, Dokument unverändert) |
| 2 MITTEL Tastatur-Falle | `OnKey` verbraucht nur wirksame Tasten; Esc ohne Auswahl tut nichts. `WantFocus` wird nach dem Fokussieren zurückgesetzt; Zahlenfelder setzen es beim Fokus zurück, Enter im Feld behält den Fokus im neu aufgebauten Feld (`FocusField`). | `GarmentEdit_Ui.Keyboard_no_trap` (rot: Tab blieb in der Fläche) |
| 3 NIEDRIG Umlaufrichtung | `WithAllowance` bestimmt die Richtung über die vorzeichenbehaftete Fläche, die Zugabe liegt immer außen. | `GarmentEditTests.Mirrored_part_allowance_outside` (rot: gespiegelte Manschette 16,0 statt 20,8 cm breit) |
| 4 NIEDRIG Fläche 0 / Selbstüberschneidung | `FromNodes` lehnt Teile unter 1 cm² ab (Meldung). Selbstüberschneidung bleibt Warnung auf der Fläche und steht jetzt auch in der Export-Meldung („Achtung: …“). | `GarmentEditTests.Zero_area_rejected` (rot: keine Ablehnung), `Problems_reported`, `GarmentEdit_Ui.Reject_and_export_hint` (rot: Export ohne Hinweis) |
| 5 NIEDRIG schmaler Schlitz | `GarmentEdit.AllowanceGap`/`Problems` melden, wenn die Schnittkante näher als 1,05 cm an die Naht kommt (Fläche: `garment-measure-gap`, Export-Meldung). Nicht beschnitten – Abschneiden der überlappenden Zugabe wäre ein Polygon-Verschnitt, nicht „einfach“. Alle Grundschnitte aller 6 Arten ohne Hinweis. | `GarmentEditTests.Problems_reported` (0,6-cm-Schlitz → Hinweis; neue Funktion, Rot = fehlte) |
| 6 NIEDRIG Präfix in `Replaced` | statt `garment-edit-` die vier Namen `garment-edit-insert`, `garment-edit-delete`, `garment-edit-toggle`, `garment-edit-reset`. `GarmentEdit_Ui.Levels` öffnet in „Einfach“ per Rechtsklick das Menü und löst „Punkt hier einfügen“ aus (Punkt eingefügt, Rückgängig-Schritt). | `UebersichtEinfach_Ui` grün; `Levels` war schon auf altem Stand grün (Test verschärft, kein Fehler im Code) |
| 7 NIEDRIG Pfeil-Schritte | Pfeil-Schritte am selben Punkt innerhalb 1 s ohne andere Änderung dazwischen werden mit `ProgramDocument.Squash` EIN Rückgängig-Schritt; Pause, Rückgängig oder andere Änderung trennt. Zeitgeber im Test ersetzbar (`GarmentEditState.Clock`). | `GarmentEdit_Ui.Arrow_steps_grouped` (rot: 3 statt 1 Schritt) |
| 8 INFO „bearbeitet“ | `GarmentEdit.Edited(edits, Garment)` zählt nur Bearbeitungen, die wirklich gelten (Fläche, Datenblatt, Anprobe-Hinweis, „Teil zurücksetzen“). | `GarmentEditTests.Merge_and_edited` |

Berührte Dateien: `src/Trendo.Core/W6/GarmentEdit.cs`, `src/Trendo.Core/W6/GarmentProduction.cs`, `src/Trendo.App/GarmentPatternEditor.cs`, `src/Trendo.App/MainWindow.ProgModeGarment.cs`, `tests/Trendo.Core.Tests/GarmentEditTests.cs`, `tests/Trendo.App.Tests/GarmentEditUiTests.cs`, `tests/Trendo.App.Tests/UebersichtEinfachUiTests.cs`, neue Protokolle `reports/T-0745/*-T-0745-fix.log` (Prüfsummen in `SHA256SUMS.txt` ergänzt). Bestehende Belege unverändert (Grundschnitte laufen gegen den Uhrzeigersinn ohne doppelte Punkte, Nahtzugabe dort gleich – `dxf-vergleich-T-0745-fix.log`).

Tests: Kern GarmentEditTests + GarmentPatternTests + GarmentProductionTests **41/41** (`core-tests-T-0745-fix.log`); App GarmentEdit_Ui, Garment_Ui, SPEC08_Ui, SchnittEditor_Ui, Marker_Ui, Sewing_Ui, UebersichtEinfach_Ui, DeadButtonTests, T0710_MenuBar_Ui, ProgramCrashUiTests, EveryButton_Alltag **119 Tests, 0 Fehler** (7 + 6 + 2 + 1 + 2 + 1 + 2 + 83 + 8 + 6 + 1) (`app-tests-T-0745-fix.log`). Kein Volltest, kein Blender-Lauf.

Nicht geprüft: kein neues Bild der Oberfläche (der neue Hinweis erscheint nur mit gewähltem, problematischem Teil – das Render-Werkzeug kann keine Auswahl setzen); Tastatur nur headless.

**Status:** umgesetzt und gezielt getestet; Volltest steht aus.
