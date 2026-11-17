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

**Status:** umgesetzt und gezielt getestet; Volltest steht aus.
