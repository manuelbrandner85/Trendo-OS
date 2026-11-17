# Bericht T-0710: Klassische Menüleiste in allen Programmen

**Auftrag:** Freigabe des Nutzers 17.11.2026 („Das Menüband in den Programmen wie bei Windows, wo Datei usw. steht, so machen“, gewählt: „Klassische Menüleiste“).
**Branch:** `claude/T-0710-menueleiste` (Basis `claude/T-0685-b1-integration`, inkl. T-0711 hereingeholt). Kopie in `%TEMP%\trendo-auftrag-T-0710`, danach gelöscht.

## Was war
- Programme der Hülle: nur das Menüband aus Knöpfen (in „Einfach“ eine Zeile mit „Mehr“). Eine Menüzeile gab es nur im Bedienstil „Windows“ (aus den Knöpfen gebaut, ohne ausgegraute Einträge, ohne Untermenüs, ohne Alt/F10).
- Programme mit eigener Werkzeugleiste (Text, Tabelle, Präsentation, Geld, Briefe, Mail, CAD, 3D, Code-Editor) und die über ProgramLink angeschlossenen: keine Menüleiste; Tabelle, Präsentation und CAD hatten nur einen Knopf „Datei“ mit Klappmenü.

## Was ist jetzt
- **Ein Baustein** `MainWindow.MenuBar.cs` (`MenuBarOf`) + Modell `Trendo.Core/Programs/ProgramMenu.cs` (`MenuCmd`, `MenuTop`, Reihenfolge, unterstrichene Buchstaben, Kürzel, Konfliktprüfung).
  Reihenfolge Datei · Bearbeiten · Ansicht · (Werkzeuge) · Fachmenüs · KI · Hilfe; Kürzel rechts in deutscher Schreibweise (Strg+N/O/S, Strg+Umschalt+S, Strg+W, Strg+Z/Y, Strg+X/C/V/A, F1, F11, Strg+K, Strg+Umschalt+K), Trennlinien, ausgegraute Einträge (beim Aufklappen neu gefragt), Untermenüs (Neu aus Vorlage ▸, Zuletzt geöffnet ▸, Frühere Versionen ▸, Exportieren ▸, Bedienstufe ▸), Häkchen (Werkzeugleiste, Eigenschaften, Bereich unten, Werkzeug, Bedienstufe, Fokusmodus).
- **Keine zweite Befehlslogik:** In der Hülle teilen Knöpfe, Menü und Kürzel dieselben Handler (`doNew/doSave/doUndo …`, `CmdRun`, `ExportRun`, `VersionRun`, `ProgramNewAct/ProgramOpenAct`, `ProgramTogglePanel` – vorher Lambdas in den Knöpfen). Außerhalb der Hülle löst jeder Eintrag den vorhandenen Knopf/Menüeintrag des Programms aus (`ByControl`, `MirrorMenu` für die vorhandenen „Datei“-Klappmenüs). KI › „Auftrag an die KI …“ öffnet die Auftragszeile (einheitlicher Assistenzweg T-0707).
- **Tastatur:** Alt allein und F10 aktivieren die Leiste (erstes Menü markiert), Alt+Buchstabe öffnet das Menü (Alt+D Datei, Alt+B Bearbeiten, Alt+A Ansicht, Alt+K KI, Alt+H Hilfe, Fachmenüs erster freier Buchstabe), Buchstabe im offenen Menü springt/löst aus, Pfeile wandern (ausgegraute werden übersprungen), Enter löst aus, Esc schließt und gibt den Fokus an die Arbeit zurück. Neue Kürzel der Hülle aus dem Modell: Strg+O, Strg+Umschalt+S.
- **Platz:** Hülle – die Leiste steht in der vorhandenen Kopfzeile (keine neue Zeile, V-01 bleibt ≤ 294 px). Reicht die Breite nicht, wandern die hinteren Menüs unter „…“ (bleiben per Maus, Pfeilen und Alt+Buchstabe erreichbar). Außerhalb der Hülle – die Leiste steht in der Titelleiste des Programmfensters (keine neue Zeile; 3D-Fläche und Text-Arbeitsfläche werden nicht kleiner), ebenfalls mit „…“.
- **Programme:** alle 39 Hüllen-Programme (Startseite und Dokument), Text, Tabelle, Präsentation, Geld, Briefe, Mail (Posteingang), CAD, 3D, Code-Editor sowie die über ProgramLink angeschlossenen Seiten editor, post, pdf, musik-studio, spiele, roboter, recherche, ki-wissen, ki-vorlagen, ki-skills, nachbau („Bearbeiten“ = Rückgängig/Wiederholen der Hülle).
- **ProgramSpec-Erweiterung:** `MenuNames` (Gruppe → Menüname) und `Menus` (eigene Fachmenüs mit Kürzeln, die die Hülle auslöst).
- **Design:** Dunkel-Gold, eine Textzeile 26 px, nichts golden in der Leiste (geprüft), sichtbar in Einfach/Erweitert/Professionell. In „Einfach“ enthält die Leiste alles aus „Erweitert“ (Profi-Einträge – Verlauf, Gespeicherte Daten – nur in „Professionell“).
- **Bedienstile:** Windows-Stil nutzt dieselbe neue Leiste (ersetzt die alte aus Knöpfen); Mac (Menüleiste oben) und GNOME (☰) behalten ihre Art.
- **Render-Werkzeug:** `TRENDO_MENU_OPEN=<Buchstabe>` zeigt ein Menü aufgeklappt (Klappliste liegt als Überlagerung im Fenster und ist im Bild).

## Tastenkürzel-Konflikte (geprüft und gelöst)
- Global belegt (Strg+K/Q, Strg+Umschalt+K/A/F, F1, F11, Strg+1…5, Strg+Tab, Alt+←): Menüs zeigen sie nur für genau diese Funktion (Hilfe, Befehl suchen, Alle Tastenkürzel, Fokusmodus) – `ProgramMenu.Conflicts` prüft das in jedem Hüllen-Programm und jeder Stufe (Test grün, keine Konflikte).
- Alt+A (Ansicht) ↔ 3D-Netz „Alt+A alles abwählen“: Die Leiste reagiert erst nach den Bedienelementen (Bubble, nur unbehandelt) – im 3D-Fenster gewinnt das Netz, sonst öffnet „Ansicht“.
- Strg+C/X/V/A: Textfelder behandeln sie selbst; das Menü wirkt auf das zuletzt benutzte Textfeld, löst die Kürzel aber nicht doppelt aus.
- Tabelle Strg+D (Ausfüllen) ↔ Alt+D (Datei): verschiedene Tasten, kein Konflikt. Geld Strg+O (Kontoauszug öffnen) steht genau so im Menü.
- Außerhalb der Hülle zeigt das Menü nur Kürzel, die das Programm selbst auslöst (im Code geprüft); die Leiste löst sie dort nicht zusätzlich aus.

## Tests (Release, gezielt; kein Volltest)
- Neu `T0710_MenuBar_Ui` (8 Tests, echte Tastatur im Headless-Fenster): Alt+D → Pfeil runter → Enter auf „Speichern“ schreibt die echte .tdoc in **bild, schnitt, ton, formulare-programm, karten-programm**; Rückgängig über Alt+B/Enter; ausgegrauter Eintrag: Buchstabe und Pfeil erreichen ihn nicht, nichts passiert; Alt allein, F10, Pfeil rechts, Esc mit Fokus zurück; Strg+O und Strg+Umschalt+S (echte Datei); Trendo Text Alt+D → Speichern → echte DOCX; alle Hüllen-Programme × 3 Stufen (eine Zeile, nichts golden, keine Konflikte, Einfach ⊇ Erweitert); schmales Fenster mit „…“; ProgramLink-Seiten inkl. Rückgängig im Editor über das Menü.
- Neu `T0710_ProgramMenuTests` (Core, 5 Tests).
- Lauf aller Pflicht- und berührten Klassen: **App 169 Tests, 0 Fehler** (UebersichtEinfach_Ui, ProgramHeadDesign_Ui, DesignPaket3_Ui, DesignPaket4_Ui, W0b_Look, FreigabenKernUi, AssistChain_Ui, T0708_HuelleEtappe1_Ui, T0711_HuelleEtappe1b_Ui, BueroAusbau_Ui, T0710_MenuBar_Ui, OsStyles_Ui, ProgramShellUiTests, SimpleMode_Ui, ProgramLevel_Ui, EinfachBedienen_Ui, Einfachheit_Ui, Präsentation, Tabelle (2), Text (5), Geld, Code-Editor, CAD-Druck, Mail, Briefe (5), 3D (3)). **Core 18/18** (T0710, T0708, T0711, ProgramDocument). **Funktions-Baseline** `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK.
- Rot-Grün: Die neuen Tests nutzen `ActiveMenuBar`, `MenuOpenTop`, `ProgramMenu` – auf dem alten Stand nicht vorhanden (schlagen fehl). Zwischenstände, die rot waren und behoben sind: V-01 (Leiste in eigener Zeile → jetzt in der Kopfzeile mit „…“), T-0692 Tabelle im Text und 3D-Arbeitsfläche (eigene Zeile verschob die Fläche → jetzt Titelleiste).
- Bilder (Render-Werkzeug, 1600 × 1000) angesehen: Bildbearbeitung „Einfach“ mit offenem „Datei“, „Professionell“ mit offenem „Bearbeiten“ (Rückgängig/Wiederholen ausgegraut, Verlauf …/Gespeicherte Daten …), Videoschnitt Profi mit „Datei“, Tabelle mit Leiste in der Titelleiste. Bilder nicht im Repo (nur lokal angesehen).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: NICHT BETROFFEN (Paket ändert Bedienung, nicht Fachfunktionen); Speichern über das Menü schreibt die echte Datei – VOLLWERTIG für den Menüweg.
2. Fremddatei importieren/exportieren, Export im Fremdprogramm: NICHT GETESTET (kein Teil des Pakets; Exportieren ▸ ruft die vorhandenen Exporte).
3. Absturz ≤ 30 s: NICHT BETROFFEN (unverändert, Hülle/ProgramLink).
4. KI-Fachaufgabe ins Dokument: TEILWEISE – Menü „KI › Auftrag an die KI …“ öffnet die Auftragszeile des einheitlichen Wegs; der KI-Lauf selbst ist hier nicht neu geprüft.
5. Test vom Klick bis zur echten Datei: VOLLWERTIG (Tastatur → Menü → .tdoc/.docx auf der Platte, fünf Fächer + Text).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen/Ablegen: Einfach/Profi und Tastenkürzel VOLLWERTIG; Rechtsklick und Ziehen unverändert (nicht Teil des Pakets).

## Was NICHT geprüft wurde
- Echte Windows-Darstellung mit Maus (nur Headless + Render-Bild). Unterstreichung der Buchstaben erscheint wie unter Windows erst bei gedrückter Alt-Taste – im Render-Bild daher nicht sichtbar.
- Im Test (Ersatzschrift ohne echte Maße) stehen bei 1600 px viele Menüs unter „…“; im echten Bild (Render mit Schrift) passt die ganze Leiste in die Zeile. Die Tasten-Tests laufen deshalb in einem breiten Testfenster; der „…“-Weg hat einen eigenen Test.
- Mac- und GNOME-Stil bekommen keine neue Leiste (eigene Konvention); nicht neu geprüft außer OsStyles_Ui.
- Mail-Seite „posteingang“: Einträge nur, wenn ein Schreibfenster/eine Nachricht offen ist (sonst ausgegraut).

## Offene Punkte
- Fachprogramme könnten eigene Kürzel über `ProgramSpec.Menus` ins Menü bringen (z. B. Videoschnitt S = Teilen) – noch nicht befüllt.
- Programme ganz außerhalb (ohne Werkzeugleiste/ProgramLink) bekommen die Leiste beim Umzug in die Hülle (Umzugsliste T-0708): Eintrag in `ExternalMenuPages` oder Umzug genügt.
- Tabelle/Präsentation-Startseite: „Datei“ zeigt dort nur „Öffnen …“ (Vorlagen bleiben die Kacheln).

## Berührte Dateien
- neu: `src/Trendo.App/MainWindow.MenuBar.cs`, `src/Trendo.Core/Programs/ProgramMenu.cs`, `tests/Trendo.App.Tests/T0710MenuBarUiTests.cs`, `tests/Trendo.Core.Tests/T0710_ProgramMenuTests.cs`, `tasks/T-0710-menueleiste.md`, `tasks/T-0710-menueleiste.bericht.md`
- geändert: `src/Trendo.App/MainWindow.ProgramShell.cs` (Kopfzeile, gemeinsame Handler, ProgramSpec.MenuNames/Menus, ProgramTogglePanel), `src/Trendo.App/MainWindow.Windows.cs` (Platz in der Titelleiste, Ziehen ignoriert die Leiste), `src/Trendo.App/MainWindow.Sys13.cs` (Seitenaufbau), `src/Trendo.App/MainWindow.OsPrograms.cs` (Windows-Stil), `tools/Trendo.Render/Program.cs` (`TRENDO_MENU_OPEN`)

**Status:** umgesetzt, gezielt getestet (grün), gepusht auf `claude/T-0710-menueleiste`; Merge in den Integrationszweig durch die Hauptsitzung.
