# Bericht T-0722: VR-Teile entfernt, „3D-Modelle ansehen“

**Auftrag:** Nutzerentscheidung 16.11.2026 („ja“ auf: VR-Teile aus dem Modell-Prüfer entfernen, Reiter neutral benennen; Bibliothek und Prüfer bleiben für 3D-Druck und Architektur).
**Branch:** `claude/T-0722-3d-modelle-ansehen` (vom Integrationszweig `claude/T-0685-b1-integration`).

## Ergebnis in einem Satz

Unter der Kennung SPEC-04 gibt es keine VR-Teile mehr; die Seitengruppe heißt „3D-Modelle ansehen“ (Reiter, Seitentitel, Kopfkarte, Fenstertitel, Suche, Katalog), der Modell-Prüfer zeigt Maße, Dreiecke, Teile, Materialien, Texturen und „Fehler im Modell“ ohne Zielgerät; 3D-Bibliothek, glTF-Werkzeuge und die Wege aus 3D-Druck und Architektur sind unverändert.

## Stücke und Entscheidung

| Stück | Entscheidung | Grund / wer nutzt es |
|---|---|---|
| `XrInspector.Budgets`, `XrBudget`, `XrCheck`, `XrInspector.Check` (Zielbrillen Quest 3/2, Handy-AR, PC-VR mit Richtwerten) | entfernt | nur Zielbrillen; Referenzsuche: nur Modell-Prüfer, Bericht und Tests |
| Knöpfe „Zielgerät“ (`xr-dev-*`), Balken gegen Gerätegrenzen, Liste „Alle Geräte“ (`xr-check-*`), Hinweis „Richtwerte … Gerät“ | entfernt | nur VR |
| `XrInspector.FrameBudget` + Rechner „Bildzeit“ (`xr-frame`) | entfernt | nur VR |
| KI-Auftrag `bildzeit.pruefen` (`XrBudgetAdapter`, `XrFields`) | entfernt, **ersetzt** durch `modell.pruefen` (`ModelCheckAdapter`) | Die Katalogmatrix verlangt je Kennung einen Auftrag (`Matrix_deckt_den_Katalog`, `PromptBarTests`). Neuer Auftrag liest ein 3D-Modell aus dem Projekt (GLB/glTF/OBJ/STL), schreibt `modell/vN/pruefbericht.md` (Maße, Dreiecke, Teile, Maßstab, Fehler im Modell), verändert das Modell nicht. Zahlen bleiben: 107 Aktionen, 78 teilweise fähig |
| `XrPlus.Comfort` + Rechner „Wird jemandem übel?“ (`xr-move`) | entfernt | nur VR |
| `XrPro.Headset`, `HeadsetOf`, `XrHeadsetView`, Knopf „Brillen-Ansicht“ (`xr-hmd`), `XrHeadset`-Schalter, Render-Schalter `TRENDO_XR_HMD` | entfernt | nur Brillen-Ansicht |
| `MeshView.EyeOffsetCm`, `ShowEyeInfo` | entfernt | nur Brillen-Ansicht (Augenversatz, Infozeile über zwei Augen); `FovDeg` ist jetzt Konstante 90° |
| `XrPro.MaxTextureSide`, `FixPlan`, `Plan`, `FixScript`, `XrFix`, `XrFixOverride`, Karte „Für die Brille reparieren“ | entfernt | nur „Für die Brille reparieren“ |
| `XrDevice` | entfernt | nur Zielgerät |
| Bericht `XrPlus.Report` | behalten, Abschnitt „Geräte“ → „Fehler im Modell“, Titel „3D-Modell-Prüfbericht“ | Modell-Prüfer |
| `XrInspector.Problems` | neu (aus `Check` herausgelöst, ohne Gerät) | „Fehler im Modell“: kein Material (glTF), keine Dreiecke |
| `XrInspector.Inspect`, `XrPlus.Detail`, `ScaleVerdict`, `ImageSize`, `ExtensionNotes`, `TexelDensity`, `LodPlan` | behalten | `ImageSize` nutzen Briefe, PDF-Werkstatt, Formulare, POD, Scan; die übrigen der Prüfer (Maße, Maßstab, Texturen); Detailstufen- und Texturschärfe-Rechner sind nicht VR-gebunden (Echtzeit-Ansicht, Webseite, Architektur). Texte „Quest/WebXR/Brille“ in den Erweiterungshinweisen neutral |
| Vorschau „Aus Augenhöhe“ (`XrEye`, `XrPro.EyeCm`) | behalten | stehend im Raum (Architektur/Einrichtung), nicht in der Liste des Nutzers |
| `XrSample` (Leseecke) | behalten | Prüfer, `Library3D_Ui`, `PlanPhoto_Ui`, `T0282_XrSample` |
| glTF-Werkzeuge (`GltfTools`, Khronos-Prüfer, glTF-Transform) | behalten | Knopf „Für Web & Brille packen“ → „Für Web packen“, Texte ohne Brille |
| 3D-Bibliothek (`MainWindow.Library3D*.cs`, Seite `bib3d`) | unverändert (nur Kommentar) | 3D-Druck „Freie 3D-Modelle suchen“, Architektur „3D-Bibliothek öffnen“, „Ansehen“ öffnet den Prüfer |
| `StudioCatalog["SPEC-04"]` | Name „3D-Modelle ansehen“, neuer Satz; bleibt in `StudioCatalog.Removed` | Name der Seitengruppe, Reiter, Kopfkarte, Fenstertitel |
| `StudioIcons["SPEC-04"]` | VR-Brille → Würfel-Umriss (Pfad wie `MdiIcons.CubeOutline`, Apache-2.0) | sichtbar in Kopfkarte und Fenstertitel; Fluent-Quelle liegt nicht im Repo, darum der vorhandene MDI-Pfad |
| `CapabilityCatalog`, `StudioKnowledge`, `StudioIdeas`, `StudioProfiles`, `StudioTeams` („VR-Team“ → „Modell-Team“), `HardwareCompat`, `DependencyCatalog` (glTF-Validator) | neutral umformuliert | erscheinen auf den bleibenden Seiten (Team, Freie KI, Ideen, Rolle) |
| `KiSuggestions` (3D aus Bild, Geräusche), `StudioMcp` (blender) | unverändert | nicht VR-gebunden |
| `Assets/studios/SPEC-04.jpg`, Bereich „3D, Spiele & VR“ | unverändert | Bild nicht sichtbar (Studio nicht in Listen); Bereichsname gehört 5 Studios – nicht Teil des Auftrags |

## Rot auf dem Integrationszweig: `SPEC04_Ui.Heavy_model_and_calcs`

Ursache: Seit T-0700 (V-07, `PathFieldLook`) sind Pfadfeld `xr-file-path` und Folgeknopf „Öffnen“ außerhalb von „Professionell“ ausgeblendet; man wählt mit „Datei wählen …“, der Folgeknopf liest sofort ein. Der Test (Stufe Erweitert) suchte das sichtbare Feld und fand nichts („Sequence contains no matching element“). Nachgewiesen mit Ausgabe aller `xr-*`-Steuerelemente: `xr-file-path|False|0`, `xr-open|False|0`. Kein Fehler im Prüfer. Behoben im Test: Datei über `xr-file-path-pick` (echter Klick, `PickOverride` statt Windows-Dialog) – prüft Einlesen, Dreiecke, Maße, fehlende Datei, Detailstufen-Rechner; VR-Teile (Geräte, Komfort, Bildzeit) entfallen und werden als „nicht mehr da“ geprüft.

## Entfernte / geänderte Tests

- gelöscht: `XrHeadsetUiTests.cs` (`XrHeadset_Ui`), `XrHeadsetTests.cs` – nur Brillen-Ansicht.
- `ScanXrProTests.Xr_fix_plan` gelöscht – nur Brillen-Reparatur.
- `W6Tests` „SPEC-04 F10: Bewegungs-Komfort“ gelöscht; „SPEC-04: GLB lesen, Dreiecke zählen, Geräte-Grenzen und Bildzeit prüfen“ → „…, Fehler im Modell finden“ (Gerätegrenzen/Bildzeit raus, `Problems` rein); F06 und F13 auf `Problems`/neuen Bericht.
- `PromptActionTests` „Bildzeit rechnet das Budget …“ → „3D-Modell prüfen liest Maße, Dreiecke und Fehler und lässt das Modell unverändert“ (echte GLB, Bytes vorher = nachher, fehlende Datei ohne Ergebnis).
- `ScanXrPro_Ui.Xr_eye_and_fix` → `Xr_eye` (Augenhöhe bleibt, Reparatur-Karte fehlt).
- `SPEC04_Ui`: siehe oben; dazu Suche (Titel, Suchwort, keine VR-Wörter) und Seitentitel.

## Nachgezogen

- PromptAction-Matrix (`TRENDO_UPDATE_MATRIX=1`): nur Zeile SPEC-04.
- Programmkatalog `katalog.json`/`KATALOG.md`: nur Eintrag `xr` (Name, F04/F05/F10 „offen“ mit Vermerk, Testnamen). Kein Neulauf fremder Messwerte.
- `docs/w6-katalog/SPEC-04.md`: F04, F05, F10 „offen“ mit Vermerk „entfernt mit T-0722“, Testverweise F01/F03/F06 auf den umbenannten Test; Anzahl (16) und Freigabe unverändert. `docs/spec/kataloge/SPEC-04_KATALOG.md`: XR-02/03/06/07 angepasst.
- `requirements.yaml`: nur Notiz zu SPEC-04 ergänzt, Status unverändert, kein PC_VERIFIED.
- Funktions-Baseline: **keine Zeile entfernt** – keine Baseline-Zeile verwies auf die entfernten VR-Knöpfe (Paket-Test grün ohne Änderung).
- `einfach-vorher-ausgeblendet.txt`: unverändert – Einträge `xr: page-freeki-*`, `xr: page-team` gelten weiter (Kopf der Seite unverändert).
- Historische Doku (`docs/programme/xr/*.md`, `docs/STUDIO_REBUILD_4_0/…`, Audit) bleibt als Verlauf.

## Tests

- `dotnet build Trendo.sln -c Release`: 0 Fehler (vor und nach dem Hereinholen des Integrationszweigs, Stand `1416ce7ea`).
- Core gezielt (SPEC04, W6*, ScanXrPro, PromptAction inkl. Matrix, GltfTools, PlanPhoto, T0282, Evidence, QA015, QA019, VorbildAnalysen, NoFixedUser, StudioTeams/Ideas, HardwareCompat, WebsiteStudio, LetterF3, Connected): vor dem Merge 180 gesamt, 179 bestanden, 1 übersprungen, 0 Fehler (nach Matrix-Neuschrieb). Nach dem Merge: 178 bestanden, 1 übersprungen, **1 Fehler `EvidenceTests.Register_evidence_unchanged`** – „Beleg verändert: reports/echtlauf/2026-11-16/acestep.log, sdcli.log“. Kommt aus dem Integrationszweig (T-0724, Commit `bca6c8ed2` änderte die Logs, Prüfsummen in requirements.yaml nicht nachgezogen; Datei byte-gleich mit `origin/claude/T-0685-b1-integration`). Nicht von T-0722, Belege bewusst nicht angefasst.
- App Lauf vor dem Merge: SPEC04_Ui, ScanXrPro_Ui, GltfUi, Library3D_Ui, Library3DPro_Ui, UebersichtEinfach_Ui, ProgramHeadDesign_Ui, T0710_MenuBar_Ui, StudioIconTests, FreigabenKernUi, PlanPhoto_Ui: 38, 0 Fehler (nach Testkorrektur Dreieckszahl); ClaudeEverywhere_Ui, DesignHonesty, EinfachBedienen_Ui, EveryButton_AllPages, EveryButton_Alltag, KiPy_Ui, ProgramCatalog_Ui, PromptBarTests, StudioBilder, StudioMcp_Ui, StudioTeams_Ui, StudiosAlle_Ui, StudiosTechnikWissen_Ui, W1_Search_Access: 178, 0 Fehler, 1 übersprungen.
- App Lauf nach dem Merge (SPEC04_Ui, ScanXrPro_Ui, GltfUi, Library3D_Ui, Library3DPro_Ui, UebersichtEinfach_Ui, ProgramHeadDesign_Ui, T0710_MenuBar_Ui, StudioIconTests, FreigabenKernUi, PlanPhoto_Ui, EveryButton_AllPages, EveryButton_Alltag, PromptBarTests, ClaudeEverywhere_Ui, StudioTeams_Ui, W1_Search_Access, ProgramCatalog_Ui, StudiosTechnikWissen_Ui): 92, 0 Fehler, 1 übersprungen.
- Paket-Tests `python -m unittest discover -s tests/claude_handoff`: 11 OK (Funktions-Baseline unverändert grün).
- Echt statt ersetzt: Modell-Prüfer und KI-Auftrag lesen echte GLB-Dateien (Leseecke aus eingebauten Blender-Möbeln, Kugel mit ~360 000 Dreiecken). Ersetzt bleibt nur der Windows-Dateidialog (`PickOverride`) – der Klick auf „Datei wählen …“ und das Einlesen laufen echt.
- Nachträglich angepasst: `VorbildAnalysenTests` Programmliste „VR & AR gestalten“ → „3D-Modelle ansehen“ (Umbenennung, nicht abgeschwächt), Vorbild-Analyse `modell-pruefer-gltf.md` mit Nachtrag T-0722.

## Bild geprüft

`TRENDO_SHOT=seite TRENDO_STUDIO=SPEC-04 TRENDO_XR=1`, Stufe Einfach und Profi (1600 × 1000): Kopf „3D-Modelle ansehen“ mit Würfel-Symbol und neuem Satz, Fenstertitel „3D-Modelle ansehen“, Einfach: „Datei wählen …“ + Beispielmodell, Profi zusätzlich Pfadfeld und goldenes „Öffnen“; rechts Maße/Dreiecke/Teile/Materialien/Texturen/Dateigröße, „Fehler im Modell: ✓ Keine Fehler gefunden“, glTF-Karte „Für Web packen“, Rechner nur Detailstufen und Texturschärfe; keine Zielgeräte, keine Brillen-Knöpfe. Nachgebessert: Werte rechts lagen unter der Bildlaufleiste (rechter Rand 14 px). Die Rechner-Ergebnisse sind blau (gemeinsames `LiveCalc`, schon vorher so, nicht geändert).

## Nicht geprüft

- Kein Volltest (macht die Hauptsitzung).
- `ModelCheckAdapter` nur mit echter GLB (Leseecke) und fehlender Datei geprüft, nicht mit OBJ/STL über den KI-Auftrag (der Seitenpfad liest OBJ/STL wie zuvor).
- Abnahmekriterien (Löschen/Umbenennen-Paket): 1 entfällt (Prüfer zeigt echte Datei, `SPEC04_Ui`) · 2 TEILWEISE (Import echter GLB, Prüfbericht als Datei; kein Export in Fremdprogramm in diesem Paket) · 3 entfällt (keine Bearbeitung) · 4 TEILWEISE (KI-Auftrag „3D-Modell prüfen“ schreibt Prüfbericht ins Projekt; liest nur, darum kein Rückgängig nötig) · 5 VOLLWERTIG für den Prüfer (`SPEC04_Ui`: Klick „Datei wählen“ bis Anzeige, „Prüfbericht speichern“ bis Datei) · 6 TEILWEISE (Einfach/Profi im Bild geprüft; Ziehen und Ablegen über die gemeinsame Pfadfeld-Hilfe, hier nicht eigens getestet).

## Offene Punkte

- Bereichsname „3D, Spiele & VR“ (5 Studios) – Umbenennen wäre ein eigener Auftrag.
- Studio-Bild `Assets/studios/SPEC-04.jpg` zeigt eventuell noch VR; derzeit nirgends sichtbar.
- Die Reiterleiste zeigt im Fenster nur einen Reiter; die 3D-Bibliothek erreicht man wie vorher aus 3D-Druck, Architektur und über die Suche (unverändert, `Library3D_Ui` grün).

**Status:** gebaut, gezielt getestet, gepusht auf den eigenen Zweig; keine Abnahme durch eine Person.
