# Bericht T-0748: Schaltungen: Gerber-Beleg und Import; Statik: Gegenprobe

**Auftrag:** `tasks/T-0748-schaltung-statik.md` (Restliste D7, Rang 23 SPEC-02 und Rang 24 SPEC-03)
**Branch/Arbeitskopie:** `claude/T-0748-schaltung-statik`, eigene Kopie von `main` (48ad4a727, 0.9.105) in `%TEMP%\trendo-auftrag-T-0748`

## 1. SPEC-02 Schaltungen – Gerber-Prüfung

**Vorher:** Gerber-ZIP (Kupfer oben/unten, Bestückungsdruck, Umriss, Excellon-Bohrdatei) wurde geschrieben, aber nie gelesen (Audit: „Gerber ungeprüft“). Der UI-Test zählte nur die ZIP-Einträge.

**Fremdwerkzeug:** gesucht, nichts installiert (`reports/T-0748/T-0748-werkzeugsuche.log`): `gerbv`, `kicad-cli`/KiCad und die Python-Pakete `pygerber`, `gerber`, `gerbonara`, `pcb_tools` fehlen auf diesem PC. **Der Beleg mit einem Fremdwerkzeug ist BLOCKIERT** – Voraussetzung: gerbv oder KiCad 7+ (`kicad-cli gerber …`/GerbView) oder pygerber/gerbonara.

**Nachher (Ersatz, ehrlich benannt):** eigener, unabhängiger Leser `Trendo.Core.Elektro.GerberCheck` für Gerber RS-274X und Excellon. Er liest Befehl für Befehl wie ein Betrachter (Koordinatenformat, Einheit, Blendendefinitionen, D01/D02/D03, Dateiende) und prüft über alle Lagen: Umriss vorhanden, nichts ragt über den Umriss, jede Bohrung trifft eine Lötfläche oben und unten, Lötfläche größer als die Bohrung, Anzahl Lötflächen = Bohrungen. Das ist eine Plausibilitätsprüfung, **kein Fremdwerkzeug**; sie ersetzt die Sichtprüfung beim Hersteller nicht (so steht es auch in der Meldung im Programm).
- Export „Fertigungsdaten (Gerber, ZIP)“ liest jede erzeugte ZIP sofort wieder ein und bricht mit Meldung ab, wenn sie fehlerhaft ist.
- Neuer Befehl „Fertigungsdaten prüfen“ (Gruppe Platine) zeigt das Ergebnis.
- **Echter Fehler gefunden und behoben:** Ein Schaltungsname mit `*` oder `%` landete ungefiltert im Gerber-Kommentar `G04 …*` und zerbrach alle fünf Dateien (Leser meldete „Befehl „Test“ nicht lesbar“). `Pcb.Gerber` ersetzt diese Zeichen jetzt. Rot-Grün: Test `Name_with_special_chars` war auf altem Code rot, danach grün.

**Beleg:** `reports/T-0748/T-0748-beleg.log` (je Vorlage Dateien, Lötflächen, Bahnstücke, Ausmaße), ZIPs `T-0748-gerber-*.zip`; Tests `T0748_GerberKiCad.Fab_zip_reads_clean` (5 Vorlagen), `Reader_finds_errors`, `Name_with_special_chars`, UI `T0748_Ui.KiCad_import_bis_Gerber`. Echt: der eigene Leser; ersetzt: das Fremdwerkzeug (fehlt).

## 2. SPEC-02 Schaltungen – KiCad-Import

**Vorher:** kein Dateiimport (Audit Kriterium 4: FEHLT).

**Nachher:** `Trendo.Core.Elektro.KiCadNetlist` liest KiCad-Netzlisten (.net, S-Ausdrücke; KiCad 5 ohne Anführungszeichen und KiCad 7 mit `pinfunction`). Bauteile: R, C, L, D, LED, Q (NPN/PNP, Pin-Reihenfolge aus Pin-Funktion, Bibliotheksteil oder Name wie `Q_NPN_EBC`), Batterie/Spannungsquelle, Schalter. Werte wie `4k7`, `10K`, `100n`, `2.2uF`, `4R7`, `1meg`, `9V`, `dc 5`. Masse-Netz (GND, 0, …) bekommt das Massezeichen. Unbekannte Bauteile (z. B. NE555) werden **nicht stillschweigend weggelassen**, sondern in der Meldung genannt. Der Schaltplan wird neu angeordnet (Bauteile nebeneinander, je Netz eine Sammelleitung, jeder Anschluss mit eigenem senkrechtem Draht – Kreuzungen verbinden nicht). Befehl „KiCad-Netzliste öffnen …“ (Gruppe Austausch, ab Stufe Erweitert) öffnet die Datei als neues Dokument.
Warum Netzliste statt `.kicad_sch`: die Netzliste enthält die Verbindungen direkt; ein Schaltplan bräuchte Pin-Geometrie aus `lib_symbols` und Drahtverfolgung – deutlich mehr Code ohne Gewinn für die Simulation.

**Beleg:** `T0748_GerberKiCad.Import_led_kicad7` (LED-Strom des Imports = Strom der gezeichneten Vorlage auf 1e-9 A), `Import_transistor_kicad5` (Transistorschalter, gleicher LED-Strom wie die Vorlage auf 1e-7 A, NE555 gemeldet), `Values`, `Bad_files`; Beispiel-Netzliste `reports/T-0748/T-0748-kicad-transistor.net`, daraus `T-0748-kicad-import.svg` und `.cir`. Die Netzlisten sind **selbst geschrieben** im KiCad-Format – keine von KiCad erzeugte Datei (KiCad fehlt).

## 3. SPEC-03 Statik – Gegenprobe

**Vorher:** keine Gegenprobe (Audit Kriterium 12 TEILWEISE). Einzelne Formeltests gab es (T0271_FramePlus), in der Oberfläche nichts.

**Nachher:** `Trendo.Core.Statik.FrameCrossCheck` prüft jede Rechnung dreifach, erlaubte Abweichung 0,5 %:
1. **Gleichgewicht** – Summe der Lagerkräfte/-momente gegen die direkt aus den Eingaben summierten Lasten (jedes System).
2. **Formel**, wenn das System eine Lehrbuchform hat: Einfeldträger (qL²/8 + FL/4, 5qL⁴/384EI + FL³/48EI, qL/2 + F/2), Kragarm (qL²/2 + FL, qL⁴/8EI + FL³/3EI), Zweifeldträger mit gleichen Feldern (qL²/8, 3/8·qL, 10/8·qL, Biegelinie des einseitig eingespannten Trägers ≈ qL⁴/185EI).
3. **Zweite Rechnung mit anderer Methode** – jeder Stab in bis zu 40 Teilstäbe zerlegt, Streckenlasten als Knotenkräfte (statt Volleinspann-Lasten), Gelenke als eigener Drehwinkel (statt statischer Kondensation), Lösung mit Cholesky (statt Gauß). Vergleicht Lagerkräfte, größtes Moment je Stab und größte Verschiebung. Nicht für Temperatur, Lagersetzung und schräge Loslager – dann steht dort ehrlich „zweite Rechnung nicht für … – nur Gleichgewicht geprüft“.

Oberfläche: Inspektor zeigt „Gegenprobe ✓ Abweichung 0,05 %“ (grün) oder „⚠ Gegenprobe weicht ab: … – Ergebnis nicht verwenden“ (rot), Tooltip mit Verfahren; die Statuszeile zeigt sie in jeder Stufe (auch Einfach, wo der Inspektor zu ist). Neuer Export „Gegenprobe (Bericht)“ (.md, jede Zeile mit Hauptrechnung, Vergleich, Quelle, Abweichung, Schlusssatz „ersetzt keine Prüfstatik durch eine Fachperson“). Der Hinweis „ersetzt keine geprüfte Statik“ stand schon im Inspektor und in der Hilfe.

**Ergebnis der Vorlagen** (`reports/T-0748/T-0748-beleg.log`, `T-0748-gegenprobe-*.md`): Einfeld 0,05 %, Kragarm < 0,01 %, Durchlauf 0,06 %, Rahmen 0,14 %, Fachwerk < 0,01 %. Die Abweichungen kommen aus der zweiten Rechnung (Knotenkräfte statt Volleinspann-Lasten, Fehler fällt mit der Teilung); gegen die Formeln liegt die Hauptrechnung unter 0,01 %.

**Testfälle mit selbst nachgerechneten Lehrbuchwerten** (`T0748_CrossCheck`): Einfeld 5 m/10 kN/m → 31,25 kNm, 19,94 mm, 25 kN; Kragarm Holz 2 m/3 kN → 6 kNm, 10,91 mm; Zweifeld 2×4 m/8 kN/m → 16 kNm, 12 kN, 40 kN; **Zweigelenkrahmen** 6×4 m, 12 kN/m → H = ql²/(4h(2k+3)) ≈ 4,49 kN (Kraftgrößenverfahren, selbst hergeleitet) – Haupt- und zweite Rechnung je unter 0,5 %; Einfeld mit Mitteneinzellast (FL/4, FL³/48EI) allein gegen die zweite Rechnung; Trapez-, Einzel- und Eigenlast beidseitig eingespannt; Warnpfad über 0,5 %.

## Abnahmekriterien

| # | Kriterium | Urteil | Begründung |
|---|---|---|---|
| 1 | Leere Vorlage → echtes Ergebnis über die Oberfläche | VOLLWERTIG (unverändert) | Zeichnen gab es schon (SPEC02_Ui, SPEC03_Ui); neu: Ergebnis jetzt mit Gegenprobe |
| 2 | Fremddatei importieren, bearbeiten, exportieren, Export im Fremdprogramm | TEILWEISE / BLOCKIERT | KiCad-Netzliste → Schaltung → Platine → Gerber-ZIP geht durch (Test). Gerber im Fremdprogramm: BLOCKIERT (kein gerbv/KiCad); Netzliste selbst geschrieben, nicht aus KiCad exportiert |
| 3 | Prozessabbruch ≤ 30 s | VOLLWERTIG (Hülle, unverändert) | Import öffnet ein normales Dokument der Programm-Hülle (Autosave/Wiederherstellung); nicht eigens für T-0748 getestet |
| 4 | Claude/KI-Fachaufgabe im offenen Dokument | NICHT GETESTET | nicht Teil dieses Pakets |
| 5 | Test vom Klick bis zur echten Datei | VOLLWERTIG | `T0748_Ui.KiCad_import_bis_Gerber` (Knopf → Datei → Simulation → Bahnen → Prüfung → ZIP gelesen), `Statik_Gegenprobe` und `Statik_Einfach_Menue` (Klick → Bericht .md) |
| 6 | Einfach/Profi, Kürzel, Rechtsklick, Ziehen & Ablegen | TEILWEISE | Stufen berücksichtigt (Import ab Erweitert, Gegenprobe in Einfach über Statuszeile + Menü, UebersichtEinfach_Ui grün); kein neues Kürzel, kein Rechtsklick, kein Ablegen einer .net-Datei |

## Tests (gezielt, kein Volltest)

- Build `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (`reports/T-0748/T-0748-core-tests.log`): SPEC02_Electronics, SPEC02_Plus, SPEC03_Engineering, SPEC03_Plus, SpiceCrossCheckTests, T0258_Frame, T0259_Circuit, T0271_FramePlus, T0275_Eurocode, T0277_Pcb, T0748_CrossCheck, T0748_GerberKiCad – **85/85 bestanden**.
- App (`reports/T-0748/T-0748-app-tests.log`, `Trendo.App.Tests.exe -class …`): SPEC02_Ui 3/3, SPEC02_UiPcb 1/1, SPEC02_UiPlus 1/1, SPEC03_Ui 3/3, SPEC03_UiEc 1/1, SPEC03_UiPlus 1/1, ProgramFrameTests 4/4, ProgramLevel_Ui 2/2, T0728_Bedienung_Ui 7/7, **DeadButtonTests 83/83**, StudiosTechnikWissen_Ui 39/39, **UebersichtEinfach_Ui 2/2** (1. Lauf rot: `prog-export-gegenprobe` fehlte in Einfach → Ersatzweg Menüleiste eingetragen und in `Statik_Einfach_Menue` geprüft, 2. Lauf grün), T0748_Ui 3/3 (letzter Lauf nach Statuszeilen-Änderung; SPEC03-Klassen danach erneut grün).
- Oberfläche im Bild geprüft (Trendo.Render, Profi): Tragwerk zeigt „Gegenprobe ✓ Abweichung 0,05 %“, Schaltung zeigt „Fertigungsdaten prüfen“ und Gruppe „Austausch“. Bilder nicht eingecheckt.

## Was NICHT geprüft wurde

- Gerber/Excellon in einem Fremdwerkzeug (gerbv, KiCad GerbView, Hersteller-Viewer) – BLOCKIERT, siehe oben.
- Eine von KiCad selbst exportierte Netzliste (KiCad fehlt); `.kicad_sch` wird nicht gelesen.
- `.cir` in ngspice/LTspice (nicht Teil des Auftrags, ngspice nicht gesucht).
- Statik gegen eine freie FEM-Software (z. B. Frame3DD) – stattdessen Formeln und eigene zweite Rechnung.
- Das Bild eines importierten Schaltplans (SVG liegt in `reports/T-0748/`, nicht angesehen); UebersichtEinfach_Ui lief vor der letzten Statuszeilen-Änderung (die fügt kein Bedienelement hinzu).

## Offene Punkte

- Fremdwerkzeug-Beleg nachholen, sobald gerbv oder KiCad installiert ist (Nutzerentscheidung, nichts installiert).
- Ablegen einer .net-Datei auf der Arbeitsfläche, Rechtsklick-Menü (Audit SPEC-02 Punkt 5).
- KiCad-Schaltplan `.kicad_sch` und SPICE-Netzliste als weitere Importe; ICs/OPV aus KiCad (heute gemeldet, nicht übernommen).
- OPV-Vorlage: der Autorouter lässt eine Verbindung offen (schon vorher so, nicht geändert).
- `UebersichtEinfachUiTests.cs` (gemeinsame Datei) bekam eine Zeile in `Replaced` – bei der Zusammenführung auf Konflikte mit anderen Paketen achten.

## Berührte Dateien

- neu: `src/Trendo.Core/Statik/FrameCrossCheck.cs`, `src/Trendo.Core/Elektro/GerberCheck.cs`, `src/Trendo.Core/Elektro/KiCadNetlist.cs`, `tests/Trendo.Core.Tests/T0748CrossCheckTests.cs`, `tests/Trendo.Core.Tests/T0748GerberKiCadTests.cs`, `tests/Trendo.App.Tests/T0748UiTests.cs`, `reports/T-0748/*`, `tasks/T-0748-schaltung-statik.md`, `tasks/T-0748-schaltung-statik.bericht.md`
- geändert: `src/Trendo.Core/Elektro/Pcb.cs` (Gerber-Kommentar), `src/Trendo.App/MainWindow.Spec02.cs`, `src/Trendo.App/MainWindow.Spec03.cs`, `tests/Trendo.App.Tests/UebersichtEinfachUiTests.cs` (eine Zeile), `.gitattributes` (eine Zeile)

## Sprachwahl

C# im bestehenden Kern; Rust nicht eingesetzt (keine Engstelle: die 26 T-0748-Kerntests mit allen Gegenproben und Gerber-Lesungen laufen zusammen in 0,26 s; eine zweite Sprache brächte nur Bindungsaufwand).

**Status:** umgesetzt und gezielt getestet; Fremdwerkzeug-Beleg BLOCKIERT.
