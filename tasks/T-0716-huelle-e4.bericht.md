# Bericht T-0716: B6 „Programm-Hülle für alle“ – Etappe E4 (3D/Spiele/Technik/Entwickeln)

**Branch:** `claude/T-0716-huelle-e4` (vom Integrationszweig `claude/T-0685-b1-integration`, Stand 492505cb6), eigene Kopie in `%TEMP%\trendo-auftrag-T-0716`.
**Grundlage:** T-0708, T-0711, T-0714, Umzugsliste `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E4). Vom Nutzer freigegeben. Parallel: T-0715 (E3), T-0717 (E5).

## Was war → was ist jetzt

| Programm / Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Allgemeiner Anschluss für sofort speichernde Programme | Baukasten, Fahrzeugbuch, Anlagen-Monitor speichern jede Änderung sofort; was in einem Feld getippt und noch nicht mit Enter/Verlassen übernommen war, war nach einem Absturz weg; kein bzw. eigenes Rückgängig | `MainWindow.LinkLive.cs` (`LiveSlot`, `LiveSync`, `LiveBind`) auf `LinkSlot`/`ProgramLink` – kein zweites System. Stand = `daten` (was das Programm speichert) + `eingaben` (offen Getipptes) + `ort` (Ansicht). Rückgängig/Wiederholen der Hülle (100): jede gespeicherte Änderung und jede Tipp-Pause ein Schritt, Rückgängig schreibt in die Datei des Programms zurück. Notsicherung nur bei offenen Eingaben (20 s, atomar, gesperrt), Karte holt die Eingaben in ihre Felder zurück, die Daten bleiben die gespeicherten. `keep`: Änderungen ohne Zurücknehmen beginnen den Verlauf neu | UI `T0716_HuelleE4_Ui.*_cycle` (unten) |
| Baukasten | `_builderUndo` Stapel **ohne Grenze**, kein Wiederholen; `PageBuilder.Save` = `File.WriteAllText`, im Projekt über `ProjectStore.WriteFile` (nicht atomar); `seite.json` ohne Version | Rückgängig/Wiederholen der Hülle (100), Strg+Z/Strg+Y (im Feld bleibt Strg+Z das des Felds), Notsicherung/Karte/Sperre; `seite.json` atomar (Projekt + Baukasten-Ordner), `index.html` atomar; `"Version": 1` vorn, fehlend = 1, neuere Fassung → verständliche Meldung, nichts wird überschrieben | UI `Builder_cycle`, `Builder_undo_100` (120 Änderungen → 100 zurück, Datei folgt; auf altem Code 120), Core `Builder_page_version` (alte Datei ohne Version lesbar, Gegenprobe im Quelltext), `Kill_while_saving_builder` |
| Fahrzeugbuch | **kein** Rückgängig; CSV-Exporte `File.WriteAllText` | Rückgängig/Wiederholen der Hülle, Notsicherung/Karte/Sperre; festgeschriebene/stornierte Fahrten nie zurücknehmbar (Verlauf beginnt neu, zusätzlich Prüfung beim Zurückschreiben); CSV atomar | UI `Vehicle_cycle`, `Locked_trips_not_undone` |
| Anlagen-Monitor | **kein** Rückgängig; CSV/SVG `File.WriteAllText` | Rückgängig/Wiederholen für Einheit/Grenzen (Anlegen/Löschen/Umbenennen → Verlauf neu), Notsicherung/Karte/Sperre; Rückgängig · Wiederholen vorn in der Werkzeugleiste; Exporte atomar. **Messwerte abgegrenzt** (bis 200 000, laufend) | UI `Twin_cycle` (Messwert bleibt unberührt), `Close_commits_field` |
| Web Design Studio | ungespeicherter Editor-Entwurf (`_wdDraft`) nur im Speicher | `MainWindow.WebDesignLink.cs`: Entwurf im Takt der Hülle nach `LINK-WEBDESIGN-*.tdoc` (Track ohne Rückgängig-Schritt – Rückgängig bleibt das des Editors), Karte „wd-recovery“ über dem visuellen Editor; „Speichern“ im Editor räumt weg | UI `WebDesign_draft` (Editor-Nachrichten wie aus dem WebView – der Editor selbst läuft im Test nicht, ersetzt) |
| 3D – Delta | je Schritt ganze Szene als Text (`List<string>`), 150-Mio.-Grenze | `Scene3D.History`: Kopf + Text je Teil, unveränderte Teile geteilt; gleiche Grenzen (100 / 150 Mio.) | **Messung** Core `Scene_undo_delta`: Netz 40 000 Dreiecke (8,4 Mio. Zeichen), 100 Schritte: bisher **17** behalten / 143 Mio. Zeichen, Delta **100** / 6,5 Mio. Zeichen (−95 %); Zeit 4,0 → 3,9 s bzw. 4,3 → 3,2 s; Rückgängig zeichengenau |
| CAD – Delta | `CadDoc.Clone()` je Schritt kopiert auch die Netze der Referenzkörper | `CadDoc.Snapshot()`/`CadReference.Share()`: Netze geteilt (werden nie verändert – im Code geprüft: nur Hinzufügen/Entfernen) | **Messung** Core `Cad_snapshot_delta`: Referenzkörper 20 000 Dreiecke, 100 Schritte: 93 871 KB → 109 KB, 23 ms → 0 ms |
| 3D-Druck | `drucker.json`, Zertifikat `File.WriteAllText` | atomar | Quelltext; Klassen `BambuPro_Ui`, `PrintPreview_Ui`, `N23N24_CadPrintUi` grün |
| Menüleiste (T-0710) | Baukasten, Fahrzeugbuch, Monitor ohne „Bearbeiten“ | „Bearbeiten › Rückgängig/Wiederholen“ (aus den Knöpfen, wie T-0711) | UI `Menu_bar_on_new_pages` (Eintrag wirkt) |
| Absturz | – | CrashProbe-Modus `baukasten` (seite.json + Notsicherung mit Sperre) | Core `Kill_while_saving_builder`: 10 harte Abbrüche, 10 mitten im Schreiben, 0 Schritte verloren. Gegenprobe mit altem `File.WriteAllText`: ebenfalls grün – ein Prozessabbruch zerreißt einen einzelnen Schreibaufruf praktisch nie; `AtomicFile` schützt zusätzlich bei Stromausfall (Flush) und gegen zwei Fenster. Rot-Grün liegt hier in der Quelltext-Gegenprobe. |

**Übrige Programme der Gruppe (geprüft, eingeordnet):** 3D-Druck – Druckplatte (Ausrichtung, Skalierung, Kopien, Slicer-Werte) im Speicher, Werkzeug, Quelle unverändert → abgegrenzt. Aus Fotos, VR/AR-Prüfer, Open Source, Erweiterungen – kein Dokument. Werkstatt, Roboter, Nachbau – seit T-0711 angeschlossen. Code-Editor – `CodeDrafts` (T-0688) bleibt eigener Entwurfsweg (Vorschlag Vereinheitlichung, Sperre dort ungeprüft).

## Gemeinsame Dateien (minimal, additiv)
- `src/Trendo.App/MainWindow.LinkSlots.cs`: `LinkSlot` + `Recovered`, `RecoveredMsg` (optional); `LinkRecover` nutzt sie (eine Zeile, Standard unverändert).
- `src/Trendo.App/MainWindow.ProgramLinks.cs`: eine Zeile `WebDesignAutosave();` in `LinksAutosave`.
- `MenuBar.cs`, `ProgramLink.cs`, `ProgramShell.cs`: **nicht** geändert (Menü entsteht aus den Slots wie bei T-0711).
- `src/Trendo.Core/Scene3D.cs`: `FromJson` in `Normalize` aufgeteilt (Verhalten gleich). `tools/Trendo.Render`: Schalter `TRENDO_RECOVERY_DEMO=e4`.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (24 Klassen + QA-019, Liste `reports/T-0716/core-filter.txt`): **100/100 grün** (`core-tests.log`); QA-019 einzeln 3/3 (`qa019.log`); T-0716 mit Messausgabe `messung.log`.
- App, ein Lauf mit 55 Klassen (`app-classes.txt`, alle Pflichtklassen + berührte: Baukasten, Fahrzeug, Monitor, Web Design inkl. *_Browser, 3D, CAD, 3D-Druck, Menüleiste, EveryButton_AllPages …): **276 Tests, 1 rot, 3 übersprungen** (`app-tests.log`). Rot: `SPEC10_Ui.Manage_and_live` – **vorbestehend**: auf dem unveränderten Integrationszweig (eigener Arbeitsbaum) ebenso rot an derselben Stelle (`twin-csv-path` ist in „Erweitert“ ausgeblendet, `spec10-basis.log`). `Library3D_Ui` (in T-0714 rot) ist hier grün. Nachlauf nach der letzten Änderung (T0716, SPEC10, TwinMqtt, MenuBar, SPEC16, AppDesigner): 22, 1 rot (dasselbe) (`app-tests-nachlauf.log`).
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: 11 OK (`baseline.log`; zwischendurch rot, weil „builder-undo“ nur zusammengesetzt im Quelltext stand – Name jetzt ausdrücklich).
- Bilder (1600×1000, angesehen): `baukasten.png` (Karte, Menüleiste mit „Bearbeiten“), `fahrzeug.png` (Karte, Rückgängig · Wiederholen über den Reitern), `zwilling.png`. Prüfsummen `reports/T-0716/SHA256SUMS.txt`.
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien (Programme dieser Etappe)
1. Aus leerer Vorlage ein echtes Ergebnis: **TEILWEISE** – Baukasten-Seite (seite.json, index.html), Fahrzeug mit Daten, Web-Seite über die Programmwege; nicht eigenes Ziel.
2. Fremddatei importieren/exportieren im Fremdprogramm: **NICHT GETESTET** (nicht Gegenstand).
3. Prozessabbruch verliert höchstens 30 s: **VOLLWERTIG** für den Baukasten (echter harter Abbruch, Code der App-Wege, 20-s-Takt); Fahrzeug/Monitor/Web Design über denselben Weg mit UI-Test „Absturz (Datenträgerstand) → Karte → Zurückholen“, ohne eigenen Abbruchtest. Harter Abbruch der App selbst nicht gefahren.
4. KI-Änderung rückgängig: **NICHT GETESTET** (nicht Gegenstand).
5. Klick bis zur echten Datei: **TEILWEISE** – Enter/Knopf → seite.json/garage.json/modell.json, Rückgängig-Knopf → Datei zurück.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Menüleiste „Bearbeiten“ neu, Strg+Z/Strg+Y im Baukasten; keine neuen Rechtsklick-Menüs.

## Was NICHT geprüft wurde
- Kein Volltest; kein harter Abbruch des App-Prozesses; der echte Web-Editor (Puck im WebView) – im Test werden seine Nachrichten nachgestellt.
- „Fenster schließen, während getippt ist“: Die Programme übernehmen das Feld beim Fokusverlust (wie vorher) – geprüft für den Monitor (`Close_commits_field`), für Baukasten/Fahrzeug nur über denselben Code.
- Sehr große Baukasten-Seiten/Fahrzeugbücher: jeder Rückgängig-Schritt ist die ganze Datei als JSON (klein im Alltag, nicht gemessen).

## Offene Punkte
- Vorbestehend rot: `SPEC10_Ui.Manage_and_live` (Test erwartet sichtbares Pfadfeld in „Erweitert“).
- Code-Editor `CodeDrafts` auf `ProgramLink` vereinheitlichen (Vorschlag); 3D-Druck-Druckplatte bewusst nicht angeschlossen; Monitor-Messwerte bewusst ohne Rückgängig.
- Fahrzeug/Monitor speichern über `Fach.Save` (Zwischendatei mit festem Namen, ohne Flush) – gemeinsame Fach-Ablage vieler Programme, nicht E4.

## Berührte Dateien
- neu: `src/Trendo.App/MainWindow.LinkLive.cs`, `src/Trendo.App/MainWindow.WebDesignLink.cs`, `src/Trendo.Core/Scene3DHistory.cs`, `tests/Trendo.App.Tests/T0716HuelleE4UiTests.cs`, `tests/Trendo.Core.Tests/T0716_HuelleE4Tests.cs`, `tasks/T-0716-*`, `reports/T-0716/*`
- App: `MainWindow.W4Builder.cs`, `Spec16.cs`, `Spec10.cs`, `WebDesign.cs` (1 Zeile), `W4Studio3D.cs`, `Studio3DMeshEdit.cs` (1 Zeile), `Cad.cs` (1 Zeile), `Print3D.cs`, `Print3DPro.cs`; gemeinsam: `LinkSlots.cs`, `ProgramLinks.cs` (siehe oben)
- Core: `PageBuilder.cs`, `Scene3D.cs`, `Cad/CadDoc.cs`, `Cad/CadDrawing.cs`
- Tests/Werkzeuge: `tests/Trendo.CrashProbe/Program.cs` (+Modus `baukasten`), `tools/Trendo.Render/Program.cs` (+Schalter), `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`

**Status:** Etappe E4 umgesetzt und gezielt geprüft; nicht im Volltest.
