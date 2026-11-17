# Bericht T-0700 Design-Paket 3

**Auftrag:** `tasks/T-0700-design-3.md` – Bild-Audit „Anordnung und Design“ (16.11.2026), Vorschläge V-07, V-09, V-11, V-12, V-14, V-15, V-17 und die offenen Punkte aus T-0686. Freigabe: alle 22 Vorschläge am 16.11.2026 mit Ja.
**Branch:** `claude/T-0700-design-3` vom Integrationszweig `claude/T-0685-b1-integration` (Stand 23346fae1; neuere Pakete auf dem Integrationszweig sind nicht hineingemergt – das macht die Hauptsitzung). Eigene Kopie in `%TEMP%`, nur dieser Zweig gepusht, kein Merge, kein Release.
**Status:** umgesetzt, gezielt getestet, im Bild nachgeprüft; Volltest steht aus (Hauptsitzung).

## Je Punkt: was war, was ist jetzt, Beleg

Tests: `tests/Trendo.App.Tests/DesignPaket3UiTests.cs` (Klasse `DesignPaket3_Ui`, 14 Tests, echtes Avalonia-Headless-Fenster; ersetzt ist nur die System-Dateiauswahl über `PickOverride`), Core: `WriteMarksTests`, `PathFieldsTests.Prompt_is_never_a_path`.

### V-07 Dateiauswahl mit einem Knopf
- **War:** Feld „Klicken, um eine Datei zu wählen – …“ (abgeschnitten) + „Datei wählen …“ + „Einlesen/Laden/Übernehmen“ (Adressbuch, Medien, Karten, Messdaten, Pflegen …); die KI-Eingabe konnte selbst zum Ordnerfeld werden.
- **Jetzt (gemeinsamer Baustein `MainWindow.Files.cs`):** außerhalb von „Professionell“ ist das Pfadfeld ausgeblendet (bleibt im Baum: Tests, Ziehen und Ablegen, Inhalt für den Folgeknopf). Sichtbar: „Datei wählen …“, kurzer Text, welche Datei gemeint ist (aus dem Platzhalter, in Alltagswörtern), danach das Kärtchen mit der Datei. Folgt direkt ein Knopf, der nur einliest (`IsReadStep`: Einlesen, Öffnen, Laden, Übernehmen, Aufnehmen, Importieren, „… und prüfen“), ist er ausgeblendet und läuft nach Auswahl, Ablegen oder „Zuletzt“ von selbst. Prüfen-und-Entpacken, Löschen, Senden, Installieren, Drucken laufen nie von selbst. „Professionell“: Feld und Folgeknopf sichtbar wie bisher, nichts läuft von selbst. `PathFields`: `auftrag-prompt`/`*-prompt` ist nie ein Pfadfeld.
- **Beleg:** `File_pick_is_one_click` (Adressbuch: ein Klick → Kontakt aus echter .vcf-Datei eingelesen; Profi: erst „Einlesen“ liest), `Read_step_rule`; angepasst (gleiche Prüfung, neuer Weg per Klick statt Eintippen ins sichtbare Feld): `MapUiTests` (GPX, Kachel-Ordner), `MeasureUiTests` (CSV), `FloorPlanUiTests` (Vorlage-Bild), `AddressBookUiTests` (CSV), `FilePickUiTests` (Feld über den logischen Baum statt sichtbar; „Ablegen füllt das Feld“ in „Professionell“, weil Ablegen sonst sofort einliest und die Seite neu baut), `PathPickAuditTests` (KI-Eingabe ausgenommen, wie im Audit S-07 verlangt).

### V-09 „Jetzt einrichten“ an der Hauptaktion
- **War:** „Bild-KI nicht eingerichtet“ nur in der Statuszeile, „Bilder malen“ trotzdem golden; 3D: „Die lokale KI (Ollama) ist nicht verbunden“ als grauer Satz.
- **Jetzt:** Baustein `MainWindow.Blocker.cs` (`ProgramBlocker`, `BlockerChip`) und `ProgramSpec.Blocker` in der Hülle: Hinweis „Bild-KI fehlt“ mit goldenem „Jetzt einrichten“ direkt neben „Bilder malen“ (gleiche Gruppe im Menüband, auch in „Einfach“); die Hauptaktion bleibt bedienbar, aber ruhig. „Bild-KI einrichten“ steht dann in „Einfach“ unter „Mehr“ statt doppelt. 3D: „Lokale KI fehlt – Jetzt einrichten“ neben „Teile vorschlagen“ (öffnet Zusatzprogramme; installiert wird dort erst nach Zustimmung).
- **Beleg:** `Blocker_sits_at_the_main_action` (einziger goldener Knopf, Klick öffnet KI-Bibliothek; mit eingerichteter Bild-KI kein Hinweis), `Blocker_in_3d`; angepasst: `ProgramHeadDesign_Ui.One_gold_button_per_view` erwartet bei fehlendem Werkzeug „Jetzt einrichten“, ohne Hauptbefehl die Hauptausgabe (Prüfung „genau dieser eine“ bleibt).

### V-11 Alltagswörter in „Einfach“
- **Jetzt:** `PlainWords.SimpleLabels` +19 Knopftexte (ICS, vCard, .vcf, GPX/GeoJSON/KML, CSV, SVG, Eurocode, On-Air-Uhr, Etiketten, Störpunkte …), neue `PlainWords.SimpleTexts` (15 feste Texte/Platzhalter: winget, Get-PnpDevice, GPIO, GPX/CSV laden, vCard-Platzhalter, SHA-256, Slicen, COLMAP, Texturqualität, EXIF), angewandt zentral nach jedem Seitenaufbau (`SimpleWords.PlainTexts`), Fachwort im Hinweis. Tastenkürzel im Knopftext („Teilen (S)“, „Löschen (Entf)“, „Abspielen (Leertaste)“) stehen in „Einfach“ nur im Hinweis („Taste: S“). TV-Begrüßung ohne M3U/Xtream. `GlyphIcons.NameFor` unverändert (Wörter nur aus dem Zusammenhang).
- **Beleg:** `Plain_words_rules`, `No_jargon_in_simple` (Betriebsmodus, Adressbuch; Erweitert behält Fachwörter), `Tabs_only_with_content` (kein „Xtream“).

### V-12 Knöpfe statt Schreibzeichen
- **Jetzt:** Baustein `MainWindow.WriteMarksBar.cs` + `Trendo.Core/Text/WriteMarks.cs` (Markdown bleibt Dateiformat): Bücher „Kapitelüberschrift · Fett · Kursiv · Aufzählung“, Wissen „Verweis auf Seite · Überschrift · Fett · Aufzählung“, KI-Vorlagen „Lücke einfügen“. Zweites Drücken nimmt die Zeichen weg; ohne Markierung wird ein markiertes Beispielwort eingefügt. Platzhalter und Tipp in „Einfach“ ohne „[[…]]“/„{{…}}“.
- **Beleg:** `Marks_by_button` (UI), `WriteMarksTests` (Core, 3 Tests).
- **Nicht:** Podcast-Kapitel und Comics-Sprechblasen (gesperrt), Präsentationen-Gliederung (Hülle gibt es auf diesem Zweig nicht).

### V-14 Speichern, Rückgängig, Export, KI, Hilfe am selben Platz
- **Jetzt:** Baustein `MainWindow.DocActions.cs` (Gruppe `doc-actions`: Speichern · Rückgängig · Wiederholen · Exportieren, links oben, wie das Menüband der Hülle). Eingesetzt: Trendo Text (vorher Speichern, dann Neu/Öffnen, Rückgängig weiter rechts), Geld & Rechnungen (Rückgängig/Wiederholen vorher rechts neben den Reitern), Briefe (Speichern/Exportieren vorher rechts oben, jetzt neben dem Titel; Als PDF/Senden rechts). KI („KI ▾“) steht überall rechts in der KI-Leiste, „?“ im Seitenkopf. Namen, Klicks und Hinweise unverändert.
- **Beleg:** `Doc_actions_same_place` (Reihenfolge, Lage im linken oberen Bereich, sichtbar, „KI ▾“ vorhanden).
- **Nicht umgestellt:** Tabelle, Präsentation („Datei“-Menü), CAD, 3D (Speichern/Rückgängig in der linken Spalte), Mail – siehe Offene Punkte.

### V-15 Reiter nur mit Inhalt
- **Jetzt:** TV & Live ohne Sender zeigt nur „Mediatheken & Streaming“, „Video-Plattformen“, „Quellen“ (`TvTabHasContent`); Verkaufen zeigt die Ideen-Zeile erst ab zwei Ideen (eine Reiterzeile weniger).
- **Beleg:** `Tabs_only_with_content`; Bilder `tv.png`, `verkaufen.png`.
- **Nicht:** Verkaufen hat weiter Studio-Reiter („Verkaufen/Geschäftsidee“) und Kasse-Reiter – zwei Zeilen.

### V-17 Eine Flächenfarbe, ruhige Kennzahlen
- **Jetzt:** Kennzahl-Kacheln (`W5Sell.Kpi`, wirkt in Geld, Haushaltsbuch, Kasse, Kassenauswertung, Spiele) dunkel wie alle Karten, Zahl in Gold, nur Warnungen (Minus) rot. Eine Fläche `PageSurface` für Text, Tabelle, Präsentation, PDF, CAD, 3D-Druck (Präsentation und CAD waren dunkelblau). Blaue Auswahl in Präsentation-Menüband, CAD-Körperliste, Karten-Hinweis → Gold. Vorlagen-Kacheln (Tabelle, Präsentation) mit einfarbigen Gold-Symbolen statt bunter Bildzeichen. Briefe: gewählte Kachel dunkle Schrift auf Gold (gemessen: Schrift RGB 27,27,27). Reiter-Symbole nur, wenn sie den Reiter unterscheiden (vorher zehnmal dasselbe Studio-Symbol).
- **Beleg:** `One_surface_calm_numbers`; Bilder `finanzen.png`, `praesentation.png`, `tabelle.png`, `brief.png`, `prog-pflege.png`.

### Offene Punkte aus T-0686
- **„Los“:** neutral, sobald die Seite eine sichtbare goldene Aktion hat (`PromptGoLook`), sonst golden. Beleg `Prompt_go_yields_to_page_action` (10 Seiten + Tragwerk).
- **Hauptaktion je Programm** (`ProgramSpec.PrimaryExport`, steht auch in „Einfach“ direkt im Menüband): Karten → „Für Navi oder Uhr“ (GPX, Ziel einer Tour), Tragwerk → „PDF-Bericht“ (Ergebnis der Statik), Messdaten → „Auswertung“, Print on Demand → „Druckdatei speichern“ (das, was der Anbieter braucht), Bildbearbeitung nach dem ersten Foto → PNG („Als Bild speichern“; leer bleibt „Foto öffnen“). Konsole: Hauptaktion der Spiele-Bibliothek (Klasse „haupt“: „Spiel daraus erfinden“, „Spiel holen“, „Spielen“). Beleg `Every_program_has_one_main_action` (Einfach und Erweitert: genau ein goldener Knopf, der erwartete).
- **Windows/Mac/GNOME-Stil:** Akzent Gold (#C9A227, hell #B8901C) statt Blau, Windows-Desktopauswahl golden. Beleg `Os_styles_use_gold`.
- **Dreiecke:** Ausrichtung in Trendo Text und Präsentation mit Ausrichtungs-Symbolen (`GlyphIcons.AlignIcon`), keine ⯇/⯈ mehr im Quelltext. Weitere Suche im Projekt: nur „▶ Ausführen“ (Code) und „▶ Spielen“ (Spiel) – dort ist das Dreieck richtig. Beleg `Align_buttons_have_align_icons`, Bild `text.png`.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- Core gezielt (`WriteMarks`, `PathFields`, `PlainWords`, `NoFixedUser`): **42 bestanden, 0 Fehler**.
- App, alle Testklassen, die geänderte Seiten oder Bausteine berühren (214 Klassen, ermittelt per grep, in zwei Prozessen): erster Lauf auf dem Zwischenstand **963 Tests, 16 rot** (446 + 517; 4 übersprungen). Daraus behoben bzw. an die neue Regel angepasst (nichts abgeschwächt): `DesignPaket3_Ui` (3 Testfehler im neuen Test), `ProgramHeadDesign_Ui` (2: Erwartung „Jetzt einrichten“/Hauptausgabe; Kopfhöhe Bilder mit KI 313 → 283 px, weil „Bild-KI einrichten“ in „Einfach“ jetzt unter „Mehr“ steht), `SimpleMode_Ui` (img-setup wieder erreichbar), `T0201_Dateiauswahl_Ui` (3), `SPEC13_Ui`, `SPEC13_UiPack`, `SPEC05_Ui`, `SPEC01_Ui`, `AddressBook_Ui`, `PathPickAuditTests`.
- Nachlauf der betroffenen 16 Klassen (74 Tests) und danach `DesignPaket3_Ui` + `FilmSchnitt_Ui` + `T0201_Dateiauswahl_Ui` + `SPEC13_UiPack` (24 Tests): grün bis auf die vorbestehenden Fehler (siehe Offene Punkte) und `FilmSchnitt_Ui.Render_click_to_file_real` (siehe unten).
- `UebersichtEinfach_Ui.Simple_keeps_every_function`: Liste der fehlenden Elemente **identisch** mit dem Integrationszweig (Vergleich der Berichtsdatei in einer zweiten Kopie auf demselben Stand) – dieses Paket verliert keine Funktion; `einfach-vorher-ausgeblendet.txt` unverändert.
- **Nicht stabil:** `FilmSchnitt_Ui.Render_click_to_file_real` (echtes FFmpeg) war in zwei großen Sammelläufen rot („nicht sichtbar: prog-cmd-film-import-music“), allein grün (5/5), zusammen mit jedem einzelnen `DesignPaket3_Ui`-Test grün und mit beiden Hälften der Klasse grün. Eine Ursache in diesem Paket habe ich nicht gefunden; Verdacht Zeitverhalten unter Last. Bitte im Volltest beobachten.
- Datenschutz-Grep (ohne Groß-/Kleinschreibung) über alle geänderten und neuen Dateien: leer.
- Kein Volltest (macht die Hauptsitzung).

## Bilder (vorher/nachher)
Scratch-Ordner der Sitzung, `ux\vorher\` (32 Seiten, Stand Integrationszweig) und `ux\nachher\` (25 Seiten + bild-ki), 1600 × 1000, je eigener `TRENDO_RENDER_ROOT`. Angesehen nachher: text, kontakte, finanzen, tv, brief, praesentation, buch, medien, technik, modus, prog-pflege, konsole, bild-ki (13 einzeln); vorher 20 einzeln.

## Was NICHT geprüft wurde
- Kein Volltest, keine Api- und Paket-Tests.
- Heller Stil, Kontrast, große Schrift, andere Fenstergrößen; Mac-Stil nur im Test (Akzentfarbe), nicht im Bild.
- Echte System-Dateiauswahl (Tests ersetzen sie); echte Maus im laufenden Programm.
- „Erweitert“ nur in Tests.

## Offene Punkte
1. V-14 noch nicht umgestellt: Tabelle und Präsentation („Datei“-Menü + Bearbeiten-Gruppe), CAD, 3D (Speichern/Rückgängig in der linken Spalte, KI-Leiste unter dem Titel), Mail. Der Baustein `DocActions` ist dafür da.
2. V-15: Verkaufen behält zwei Reiterzeilen (Studio + Kasse); KI-Zentrale/Abläufe nicht angefasst.
3. Bereits auf dem Integrationszweig rot (vorher in einer zweiten Kopie geprüft, nicht von diesem Paket): `W0b_Look.No_jargon` (Geld „Laufzeit“, Code-Editor), `UebersichtEinfach_Ui.Simple_keeps_every_function` (finanzen: geld-datev …), `UebersichtEinfach_Ui.Every_view_stays_clear` (3D: vorher 43, jetzt 44 Knöpfe – +1 durch „Jetzt einrichten“), `GameStage3_Ui.Services_with_license`.
4. Für gesperrte Programme (Podcast, Comics, Videoschnitt-Vorschau …) gelten die Bausteine automatisch, soweit sie die Hülle nutzen; eigene Umstellungen dort (Podcast-Kapitel als Knöpfe, Comic-Sprechblasen) bleiben offen.
5. Zeilenenden: Git meldet „LF will be replaced by CRLF“ – keine inhaltliche Änderung.

## Berührte Dateien
src/Trendo.App: GlyphIcons.cs, SimpleWords.cs, MainWindow.{Files, Look, PromptAction, ProgramShell, SimpleMode, Text, Praesentation, Tabelle, Cad, Pdf, Print3D, Spec03, Spec05, Spec13, PodStudio, Bildbearbeitung, GameLibrary, OsStyles, W5Sell, Haushaltsbuch, GeldProgramm, LetterPro, W1Office, W1Letter, Tv, W5Market, W45, KreativStudios2, Wissen, Prompts, W4Studio3D}.cs; neu MainWindow.Blocker.cs, MainWindow.DocActions.cs, MainWindow.WriteMarksBar.cs. src/Trendo.Core: Programs/PlainWords.cs, Shell/PathFields.cs, neu Text/WriteMarks.cs. tests: neu DesignPaket3UiTests.cs, Core WriteMarksTests.cs; angepasst PathFieldsTests.cs, ProgramHeadDesignUiTests.cs, FilePickUiTests.cs, MapUiTests.cs, MeasureUiTests.cs, FloorPlanUiTests.cs, AddressBookUiTests.cs, PathPickAuditTests.cs. tools/Trendo.Render/Program.cs (Stufe auch ohne Vorlage).

## Abnahmekriterien (Paket ist reine Gestaltung)
1. Ergebnis aus leerer Vorlage: NICHT GETESTET (kein Fachumbau). 2. Fremddatei-Import/Export: TEILWEISE – echte .vcf/.gpx/.csv werden per Klick eingelesen (Tests), kein Export-Beleg im Fremdprogramm. 3. Absturz ≤ 30 s: NICHT GETESTET (unverändert). 4. KI-Fachaufgabe: NICHT GETESTET. 5. Test vom Klick bis zur echten Datei: TEILWEISE (Klick → eingelesene Datei; Dialog ersetzt). 6. Einfach/Profi: VOLLWERTIG für die geänderten Regeln (Stufen getestet); Tastenkürzel/Rechtsklick/Ziehen: unverändert, Ablegen löst jetzt auch das Einlesen aus.

```
ENTWICKLUNGSBERICHT
- Projekt: Trendo OS, T-0700 Design-Paket 3
- Verwendete Sprachen: C# (.NET 10, Avalonia 12)
- Rust eingesetzt: NEIN
- Rust-Komponenten: keine
- Grund der Sprachwahl: Oberflächen-Bausteine in der bestehenden C#-App; kein Leistungsengpass
- Durchgeführte Tests: siehe „Kommandos und Ergebnisse“
- Gemessene Verbesserungen: keine Leistungsmessung (nicht Ziel)
- Offene Probleme: siehe „Offene Punkte“
```
