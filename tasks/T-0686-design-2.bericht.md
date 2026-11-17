# Bericht T-0686: Design-Paket 2 – Programm-Hülle verständlich

**Auftrag:** `tasks/T-0686-design-2.md` (Bild-Audit „Audit 7: Anordnung und Design“, Vorschläge V-01, V-02, V-03, V-04, V-08; vom Nutzer am 16.11.2026 freigegeben)
**Branch:** `claude/T-0686-design-2` (vom Integrationszweig `claude/T-0685-b1-integration`, Stand bf19f45cf) – nur committet, kein Push, kein Merge, kein Release
**Status:** umgesetzt, im Bild nachgeprüft und gezielt getestet; Volltest steht aus (Hauptsitzung)

## Wichtig vorab
- Auf diesem Zweig gibt es **32 Hüllen-Programme**, nicht 39. Bewerbung, Formulare, Karten & Einladungen, PDF-Werkstatt, Dokumente scannen, Verträge-Ordner und die „Präsentationen gestalten“-Hülle aus dem Audit liegen nur in der Arbeitskopie `studios-aufraeumen`. Alles hier wirkt über die Hülle; diese Programme bekommen Kopf, Gold-Regel, Werkzeugleiste und Speicherstand beim Zusammenführen von selbst. Einen „ersten Schritt“ konnte ich für sie nicht eintragen.
- Vorher hatten 17 Programme den kompakten Kopf (volle Fensterhöhe, `FillHeight`), 15 den hohen.

## Geänderte Dateien
| Datei | Was |
|---|---|
| `src/Trendo.App/MainWindow.ProgramShell.cs` | neue Kopfzeile (`ProgramHead`), Speicherstand (`SaveStateText`, `PaintSaveState`), Gold-Regel (`ProgramOneGold`, `IsGold`, `QuietGold`), Werkzeugleiste mit Wort, erster Schritt (`ProgramFirstStep`, `ProgramFirstStepCard`, `ProgramPickThen`, `ProgramShowProperties`), Statusleiste |
| `src/Trendo.App/MainWindow.axaml.cs` | `Page(…, pageHead: false)`: Seite ohne Kopfkarte (4 Zeilen) |
| `src/Trendo.App/App.axaml` | Akzentfarbe der Bedienelemente ist Gold statt des blauen System-Akzents |
| `src/Trendo.App/MainWindow.OsPrograms.cs` | Bedienstile Windows/Mac heben die eine Hauptaktion hervor (1 Zeile) |
| `MainWindow.VideoSchnitt.cs`, `MainWindow.AudioEditor.cs`, `WaveView.cs`, `MainWindow.Bildbearbeitung.cs`, `MainWindow.Spec09.cs`, `MainWindow.KreativStudios.cs` | Angabe „erster Schritt“, je ein Befehl mit Dateiauswahl, Hinweise „Rechts …“ berichtigt |
| `tools/Trendo.Render/Program.cs` | `TRENDO_AUTOSAVE=1`: Speicherstand so rendern, wie ihn Nutzer sehen (1 Zeile) |
| `tests/Trendo.App.Tests/ProgramHeadDesignUiTests.cs` (neu) | 6 Tests für die Regeln |
| `tests/Trendo.App.Tests/EinfachBedienenUiTests.cs`, `ProfiRahmenUiTests.cs`, `OsStylesUiTests.cs` | an die neue Vorgabe angepasst (siehe „Angepasste Tests“) |

## Je Punkt

### V-01 Schmaler Kopf
- **Ursache der sechs Streifen:** Nicht `ScrollWorkspace`, sondern `FillHeight`. Nur Programme in voller Fensterhöhe liefen über `FitPage(…, pageHead: false)` und zeigten ihre Meldung im Band. Alle anderen liefen über `Page()` und bekamen zusätzlich die Kopfkarte der Seite (`page-hero`), die Hinweis-Karte mit „Kopieren“ (`ResultCard`) und ein leeres Band mit Titel und Speicherstand; dazu in beiden Fällen eine eigene Reiterzeile.
- **Jetzt:** Für jedes offene Dokument eine Zeile: Programmname · Titel (Feld zum Ändern) · „×“ „＋“ bzw. Reiter · kurzer Hinweis · Speicherstand · „Mehr Werkzeuge“ bzw. Stufenwahl · „?“. Darunter sofort das Menüband.
  - Reiter stehen in derselben Zeile. Bei einem Dokument ist der Reiter ausgeblendet (der Titel steht im Feld), ab zwei Dokumenten erscheinen sie, ab vier in einer eigenen Zeile. In „Einfach“ mit einem Dokument bleibt auch „＋“ weg („Neu“ unter „Mehr“, Strg+N).
  - „Vorlage … geöffnet.“ / „Neues Dokument.“ steht klein in der Kopfzeile und verschwindet nach 4 Sekunden. Lange oder mehrzeilige Ergebnisse und Fehler bleiben eine Karte.
  - Die Startseite (kein Dokument offen) behält Kopfkarte und Erklärsatz.
  - „Team“ und „Freie KI“ aus der alten Kopfkarte stehen bei den 15 betroffenen Programmen jetzt in der Gruppe „Hilfe“ (in „Einfach“ unter „Mehr“).
- **Im Bild nachgemessen** (1600 × 1000, „Einfach“, obere Kante der Arbeitsfläche in Pixeln = Promille der Fensterhöhe; Messung aus den Bildern, Skript `edges.py`/`measure.py` im Scratch-Ordner):

| Programm | vorher | nachher |
|---|---|---|
| agenten-bauen, bild-ki, bilder-erkennen, buch, code-ki, fotos-verwalten, ki-trainieren, logo, modelle-vergleichen, podcast, quellen, tagesplaner, video-ki, zusammenfassen (14) | 481 | 276 |
| spiele-programm | 449 | 244 |
| bild, schnitt, ton (mit Zeile „Gehört hierher:“) | 327 | 281 |
| animation, architektur, comic, elektronik, film, karten, konsole, kuenstler, pod | 303 | 257 |
| wissenschaft | 295 | 249 |
| cnc, mode, punktwolke, technik | 271 | 225 |

  Alle 32 liegen jetzt zwischen 22,5 % und 28,1 % (Ziel höchstens rund 30 %).
- **Test:** `ProgramHeadDesign_Ui.Workspace_starts_in_upper_third` – alle Hüllen-Programme, Grenze 300 px; Name, Titel und Speicherstand in einer Zeile; keine Kopfkarte, keine Hinweis-Karte. `Head_line_details` – Hinweis in der Kopfzeile, Reiter ab zwei Dokumenten, Titel ändern, Fehler als Karte.

### V-03 Genau ein goldener Knopf
- **War:** „Speichern“ in jedem Programm golden, dazu jeder `Primary`-Befehl (bis zu vier), die Hauptaktion in der Arbeitsfläche noch einmal in Blau (Klasse `accent` = Akzent des Systems), aktive Reiter in Blau.
- **Jetzt (Regel in der Hülle, `ProgramOneGold`):** Golden gefüllt bleibt genau ein Knopf, in dieser Reihenfolge: erster Schritt im leeren Dokument → Rückfrage-Karte (Sicherung, Einführung, Fehler) → der erste sichtbare `Primary`-Befehl der Stufe. „Speichern“ ist neutral. Alle anderen Hervorhebungen – auch in Arbeitsfläche, Eigenschaften und unterem Bereich – werden ruhig: goldener Rand, zarter Goldton. Dasselbe gilt für „gewählt“ (aktives Werkzeug, gewählte Stufe).
- **Blau:** `App.axaml` setzt die Akzentfarbe von Fluent auf Gold. Damit sind auch Schieberegler und Häkchen golden, und Knöpfe, die ein Programm später ohne Neuzeichnen einsetzt, sind wenigstens nicht blau.
- **Test:** `One_gold_button_per_view` – 128 Ansichten (32 Programme × Startseite und Dokument × „Einfach“ und „Erweitert“): höchstens ein goldener Knopf, es ist der erwartete, „Speichern“ nie, keine Akzentklasse mehr.
- **Ehrlich:** „Genau einer“ gilt, wenn das Programm einen Hauptbefehl hat. **Fünf Programme haben keinen `Primary`** (karten, konsole-programm, pod-programm, technik, wissenschaft), Bildbearbeitung nach dem ersten Foto ebenfalls – dort ist kein Knopf golden. Einen Hauptbefehl zu bestimmen ist eine Entscheidung je Programm, das habe ich nicht geraten.

### V-04 Werkzeugleisten
- **War:** Die Leiste war 40 bzw. 88 Pixel breit; die Wort-Automatik hängte in „Einfach“ das Wort an – abgeschnitten („Verschiel“, „Zuschnei“).
- **Jetzt:** In „Einfach“ baut die Hülle die Leiste selbst: links, 164 Pixel breit, Symbol und Wort, bei vielen Werkzeugen scrollbar. In „Erweitert“ und „Professionell“ nur das Symbol, das Wort steht im Hinweis (unverändert, im DAW-Stil weiter rechts). Im Menüband 6 statt 2 Pixel Abstand zwischen den Knöpfen; es bricht weiter um.
- **Test:** `Tool_rail_with_words` – beide Programme mit Werkzeugleiste (Bildbearbeitung, Künstlerstudio): Wort passt vollständig, Leiste links, genau ein Werkzeug markiert; Menüband bei 1600 und 1000 Pixel Breite: kein Knopf ragt hinaus, keine zwei kleben aneinander.
- **Nicht in diesem Paket:** die Abspiel-Leiste im Videoschnitt und die Leisten in „Dateien“ (Befund S-03, Punkt 2) gehören zu den Programmen selbst.

### V-02 Erster Schritt im Leerzustand
- **Neu:** `ProgramSpec.FirstStep` = `ProgramFirstStep(Label, Hint, Run, Empty)`. Die Hülle zeigt die Karte mit dem großen Knopf (`prog-first-step`) waagerecht mittig und knapp über der Mitte der Arbeitsfläche (unten liegen oft Zeitleiste und Abspielknöpfe). Nur die Karte nimmt Klicks an.
- **Eingetragen:**

| Programm | Knopf | Was er auslöst |
|---|---|---|
| Videoschnitt | Video hinzufügen | Dateiauswahl (mehrere) → `EditAdd`, Ton auf die Musikspur |
| Bildbearbeitung | Foto öffnen | Dateiauswahl → `ImgAddImage` |
| Audio bearbeiten | Tondatei öffnen | Dateiauswahl → `AuOpen` |
| Punktwolke | Scan-Datei öffnen | Dateiauswahl → `ScanLoadFile` (aus dem Knopf „Laden“ herausgelöst) |
| Logos & Marken | Namen eingeben | holt die in „Einfach“ eingeklappten Eigenschaften (Name, Untertitel, Farben) |
| Comics & Storyboards | Texte und Bilder eintragen | holt die eingeklappten Eigenschaften (Sprechblase, Unterschrift, Bild je Feld) |

- Damit der Weg nach dem ersten Mal nicht wieder verschwindet, haben Videoschnitt, Bildbearbeitung und Audio je einen Befehl im Menüband bekommen („Video hinzufügen“, „Foto öffnen“, „Tondatei öffnen“).
- Berichtigt: „Rechts Videos, Fotos oder Musik hinzufügen.“, „Noch kein Ton – rechts eine Datei öffnen“, „Scan-Datei rechts wählen …“, „WAV oder MP3 rechts wählen“ und die Hilfetexte dieser Programme.
- **Übrige Programme:** kein Eintrag, weil die Arbeitsfläche den ersten Schritt schon selbst zeigt (Fotos verwalten „Ordner wählen …“, Bilder erkennen „Bild öffnen …“, Podcast, Zusammenfassen, Tabellen und Zeichenflächen mit Werkzeugpalette).
- **Test (echte Funktion, Dateiauswahl über `PickOverride` ersetzt):** `First_step_runs_the_real_function` – in „Einfach“ bei eingeklappter rechter Spalte: Video-Stück liegt auf der Zeitleiste, Foto ist Ebene und bestimmt die Leinwand, WAV ist Quelle, Scan ist geladen, Logo/Comic zeigen die Felder; Abbrechen ändert nichts; gefüllte Vorlage zeigt den Knopf nicht.

### V-08 Speicherstand
- **War:** oben „nicht gespeichert“ (auch bei eingeschaltetem automatischem Speichern, weil noch keine Datei da war) und unten „Noch nicht gespeichert · 00:51“.
- **Jetzt nur in der Kopfzeile:** „Wird automatisch gespeichert“ · „Gespeichert um HH:MM“ · „Nicht gespeichert –“ mit Knopf „Speichern“ (nur ohne automatisches Speichern oder wenn es gerade nicht ging) · „Nur lesen“ · „Nicht gespeichert – bitte oben entscheiden“ (von außen geändert). Die Statusleiste nennt nur noch den Dateinamen. Ihr Text steht in normaler Schrift statt blau in Schreibmaschinenschrift (Befund S-11).
- **T-0680:** Die Karten „Sicherung gefunden“, „anderes Fenster“, „von außen geändert“, „beschädigt“ sind unverändert und stehen zwischen Kopfzeile und Menüband. Ihr goldener Knopf hat Vorrang vor der Hauptaktion.
- **Test:** `Save_state_once_and_clear` – beide Fälle, Klick auf „Speichern“ speichert, Nachziehen ohne Neuzeichnen, kein zweiter Speicherstand auf der Seite. `ProgramCrashUiTests` („Nur lesen“) läuft unverändert.

## Angepasste Tests (neue Vorgabe, nichts abgeschwächt)
- `EinfachBedienenUiTests.Autosave`: Wortlaut „Wird automatisch gespeichert“ / „Gespeichert um HH:MM“ statt „Wird gespeichert …“ / „✓ Gespeichert“.
- `ProfiRahmenUiTests.Tools_rail_and_toggles`: prüfte „aktives Werkzeug hat dieselbe Fläche wie Speichern“. Jetzt: aktives Werkzeug ist markiert (anderer Rand und Grund), weder es noch „Speichern“ ist golden gefüllt.
- `OsStylesUiTests.Programs_like_os` (GNOME): Im leeren Bild-Dokument ist jetzt „Foto öffnen“ die hervorgehobene Aktion; „Speichern“ als empfohlene Aktion wird wie bisher geprüft, sobald eine Ebene da ist.

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler, 13 Warnungen.
- App-Tests gezielt: alle 111 Testklassen, deren Datei `prog-` enthält, in 10 Gruppen mit `Trendo.App.Tests.exe -class …`: **425 Tests, 425 bestanden, 0 fehlgeschlagen** (26 + 30 + 37 + 37 + 66 + 34 + 19 + 145 + 27 + 4). Darin die 6 neuen und `UebersichtEinfach_Ui` (beide Tests, also auch `Simple_keeps_every_function`).
  - Ein Lauf davor hatte 2 Fehlschläge: `OsStyles_Ui.Programs_like_os` (siehe oben) und `UebersichtEinfach_Ui.Every_view_stays_clear` (Kleidung & Produkte: 41 statt höchstens 40 Knöpfe, weil „?“ und „Speichern“ in der Kopfzeile dazukamen). Behoben, indem „＋“ in „Einfach“ bei einem Dokument wegbleibt – nicht durch eine Ausnahme im Test.
- Datenschutz-Grep über alle geänderten Dateien: leer.
- `einfach-vorher-ausgeblendet.txt` unverändert.

## Bilder
`<Scratch>\ux-t0686\` – je Programm `<schlüssel>_einfach_vorher.png` und `_nachher.png` (alle 32), dazu `_profi_vorher/_nachher` für bild, schnitt, logo-programm, tagesplaner-programm, architektur, buch-programm, sowie `tagesplaner-programm_*_ohne-autosave.png` („Nicht gespeichert – Speichern“). „nachher“ ist mit `TRENDO_AUTOSAVE=1` gerendert. Angesehen (verkleinert im Kontaktbogen, einzelne in voller Größe): 24 der 32 „Einfach“-Bilder nachher und alle sechs „Professionell“-Bilder nachher, dazu Windows- und GNOME-Stil je einmal. 16 der „Einfach“- und die sechs „Professionell“-Bilder habe ich in einem Lauf vor den letzten beiden kleinen Änderungen angesehen („＋“ in „Einfach“ ausgeblendet, Statuszeile in normaler Schrift); vom letzten Lauf 8 weitere ganz und 3 Kopfbereiche. Nicht einzeln angesehen: agenten-bauen, bild-ki, buch (Einfach), code-ki, ki-trainieren, modelle-vergleichen, technik, video-ki – gleicher Aufbau, im Bild nur vermessen.

## Was NICHT geprüft wurde
- Kein Volltest, keine Core-, Api- oder Paket-Tests (Core wurde nicht geändert).
- Heller Stil, Kontrast-Stil, große Schrift, andere Fenstergrößen als 1600 × 1000 im Bild; Mac-Stil im Bild. Die neue Akzentfarbe im hellen Stil (`#B8901C`) habe ich nicht angesehen.
- Echte Dateiauswahl des Systems (Tests ersetzen sie), echte Bedienung mit Maus im laufenden Programm.
- Der 4-Sekunden-Hinweis läuft über einen Zeitgeber; geprüft ist nur, dass er anfangs dasteht.
- Stufe „Erweitert“ nur in Tests, nicht im Bild.

## Offene Punkte
1. **„Los“ in der KI-Leiste** über jedem Programm ist weiter golden – mit der Hauptaktion sind es im Fenster zwei goldene Knöpfe. Die KI-Leiste liegt außerhalb der Hülle (Design-Paket 1).
2. **Programme ohne Hauptbefehl** (siehe V-03): je Programm festlegen, welcher Befehl `Primary` ist.
3. **Bedienstile Windows/GNOME** zeigen ihre Akzentfarbe weiter in Blau; GNOME hebt nach seiner Gewohnheit „Speichern“ hervor (der vorhandene Test verlangt das). Wenn „Gold statt Blau“ auch dort gelten soll, ist das eine eigene Entscheidung.
4. **Knöpfe, die ein Programm ohne Neuzeichnen einsetzt,** laufen nicht durch die Gold-Regel; sie wären golden gefüllt, bis die Seite das nächste Mal gezeichnet wird.
5. **`App.axaml`** kann sich mit Design-Paket 1 überschneiden (eine Stelle, `<FluentTheme>`).
6. **Sicherungszweig:** Während der Arbeit hat ein anderer Vorgang in diesem Worktree den Zweig `sicherung/T-0686-design-2-zwischenstand` angelegt, ausgecheckt und einen Zwischenstand committet (fc92f8b38). Ich habe nur `HEAD` wieder auf `claude/T-0686-design-2` gestellt; der Sicherungszweig ist unberührt und kann weg.
