# Bericht T-0779: Quiz erstellen und Kurse erstellen (Etappe 0.9.113 „Wissen & Lernen“)

**Auftrag:** `tasks/T-0779-quiz-kurse.md` (Freigabe 26.11.2026) · **Branch:** `claude/T-0779-quiz-kurse` (Basis main 184fcbbfc = 0.9.112) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0779`.

## Nachbesserung nach der Gegenprüfung („nicht freigabefähig“)
Rot-Lauf: die neuen Tests gegen a136c971b (`reports/T-0779/rot-gegenpruefung.txt`): Core 4 von 14 rot (H1, M2, M3, Niedrig), App 5 rot (H2, M2-KI, M4, dazu die zwei Tests mit umbenannten bzw. neuen Elementen). Tests mit neuen Hilfen (`QuizHtml.RuleExpectation`, `KursKit.MitKursGrenze`) stehen in `T0779GegenpruefungTests` und übersetzen auf altem Stand nicht.

| Nr. | Befund | Behebung | Test (rot → grün) |
|---|---|---|---|
| H1 | Lücken-Toleranz ließ „1988, 4, Ast, B“ für „1989, 3, Ost, A“ durch | `LearnBox.CheckTyped`: Lösungen bis 4 Zeichen und mit Ziffern nur genau (`ExactUpTo = 4`); gleiche Regel in `tqTypo` des HTML-Spielers | Core `H1_gaps_strict` (0 % statt 100 %), `H1_rule_cases`; **echt**: Chrome `#regeln` liefert `data-tq-rules="00000111"` = Trendo (`fremdprogramme.log`) |
| H1 Karteikarten | Die Änderung wirkt auch in „Eintippen“ der Karteikarten. **Bewusst so gelassen:** dort ist „1988“ für „1989“ ebenso falsch (Vokabeln, Jahreszahlen); der bestehende Test SPEC-06 F10 („elephant/elephat“ als Tippfehler, „dog/cat“ falsch) bleibt grün, kein Karteikarten-Test geändert | Core `H1_gaps_strict` (Karteikarten-Fall), `W6Tests` grün |
| H2 | „In Karteikarten übernehmen“ war ein Assistenten-Schritt und schrieb im Probelauf/Live-Vorschau in den Karteikarten-Speicher | Befehl heißt `quiz-export-cards` und fällt damit unter den vorhandenen Sperrfilter (keine Assistenten-Aktion) | App `H2_cards_not_from_assistant` (Aktionsliste, Regel-Plan + Probelauf wie die Live-Vorschau, KI-Plan – Kartenzahl unverändert) |
| M1 | StudioFoldsUiTests erwartete 61 | 63 (Hinweis: zählt `StudioCatalog`, war schon auf a136c971b 63 – der Test war veraltet, nicht rot-grün) | App `StudioFolds_Ui` grün |
| M2 | Kurs-Export bettete jede Datei ein; KI konnte `…/bild` und Fortschritt setzen (Sperre nur 1. Segment) | Einbetten nur PNG/JPG/GIF/WEBP/SVG bis 15 MB (`KursKit.BildOk`), sonst Fehler in `KursKit.Pruefen`; `KiLocked = bild, lernende, erledigt`; Sperre prüft jetzt **jedes Pfadsegment und die Schlüssel im geschriebenen JSON-Wert** (`ProgramAssistTarget.Session.LockedField`) – so auch kein ganzes `kurs`-Objekt mit Lernenden | Core `M2_only_images_embedded`; App `M2_ki_locked_fields` (drei Fälle abgelehnt, Titel weiter erlaubt); T0774 (Ordner-Sperre im Scannen) grün |
| M3 | `<!--` wurde als `<\!--` (ungültiges JSON) eingebettet | `<!--` | Core `M3_comment_in_json` (JSON-Parse); **echt**: Chrome spielt ein Quiz mit „<!--“ im Fragetext zu 100 % durch |
| M4 | Kürzung der Plan-Zeile galt für alle Aktionen | `AssistStep.Text` wieder vollständig; neue `ShortText`/`IsLong`; gekürzt (mit aufklappbarer Vollansicht „Ganz anzeigen“) nur bei Fachaktionen mit Dokument-Vorschau – in Plan, Protokoll und Fehlermeldung. Befund dabei: das Protokoll zeigte den langen Wert ebenfalls und schob „Ja, behalten“ aus dem Bild | App `Ki_questions_into_open_quiz` (Kurzform + Aufklapper), `M4_and_low` (dokument.setzen zeigt vollen Wert, 22 Fragen: Knopf erreichbar) |
| Niedrig | Manifest-Steuerzeichen, suspend_data, Bestehen, Größen, Meldung, CSV, Marken, Bild | Steuerzeichen raus; `kSuspend` liefert gültiges JSON (sonst ohne Quiz-Stand bzw. gar nicht, mit Kennzeichen); Quiz im Kurs nutzt `Kurs.Bestehen` (Studio, Webseite, SCORM); 2 MB für Quiz-Dateien im Kurs und „Text für die KI“; `quiz.fragen` meldet weggelassene/unlesbare Fragen; CSV-Zellen mit = + - @ bekommen ' davor; Markennamen aus Suchwörtern und Fachwissen-Sätzen entfernt; Quiz-Bild neu ohne Farbkacheln (FLUX.2 klein lokal) | Core `Low_fixes`, `Course_threshold`; App `M4_and_low`; Bild `StudioBilder` grün. suspend_data nur im Code und im HTML geprüft (keine Lernplattform) |

Neu berührt: `src/Trendo.Core/W6/Learning.cs`, `src/Trendo.Core/Programs/Assist.cs`, `src/Trendo.App/MainWindow.Assist.cs`, `src/Trendo.Core/Wissen/{Kurse,QuizExport}.cs`, `src/Trendo.App/MainWindow.QuizKurse.cs`, `src/Trendo.Core/PromptAction/StudioKnowledge.cs`, `tools/studio_bilder.py`, `src/Trendo.App/Assets/studios/STUDIO-QUIZ-01.jpg`, Tests `T0779QuizKurseTests.cs`, `T0779GegenpruefungTests.cs` (neu), `T0779QuizKurseUiTests.cs`, `T0779FremdTests.cs`, `StudioFoldsUiTests.cs`.

Tests nach der Nachbesserung (gezielt): Build 0 Fehler; Core 189/189 bestanden (`rohlog-core-tests.txt`); App 21 Klassen, 319 gesamt, 0 Fehler, 4 übersprungen (Echt-/Fremdläufe ohne Variable, `rohlog-app-tests.txt`); Echtlauf qwen3:14b erneut bestanden (Quiz: 4 Fragen, Kurs: Lektion, Rückgängig; `echtlauf-lokale-ki.md`), Fremdlauf Chrome/7-Zip/pdftotext/qpdf erneut bestanden (`fremdprogramme.log`).

## Was war → was ist
| Punkt | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Quiz erstellen | nur „Quiz mit Auswahl“ in den Karteikarten (`MainWindow.Spec06.cs:172`, `LearnBox.Quiz`), kein Editor, keine Ausgabe | Programm `quiz-programm` (STUDIO-QUIZ-01) in der Programm-Hülle: Fragen als Karten (Auswahl, Mehrfach, Wahr/Falsch, Lückentext, Zuordnen), Punkte, Zeit je Quiz/Frage, Mischen, Tempo-Bonus, Prüfung, Spielmodus mit Auswertung; Karteikarten ↔ Quiz über `LearnBox` | App `T0779QuizKurse_Ui` (Klicks, Tasten, Rechtsklick, Ziehen), Core `T0779QuizKurseTests` |
| Quiz-Ausgabe | – | eigenständiges HTML-Quiz (eine Datei, gleicher Spielablauf), Druck-PDF mit Lösungsblatt, GIFT für Moodle (Export + Import), Fragen als Text | Core `Html_and_pdf`, `Gift_roundtrip`; App `Quiz_editor_to_files` (PDFium); **echt**: Chrome headless spielt das HTML-Quiz durch (100 %), pdftotext + qpdf lesen das PDF (`reports/T-0779/fremdprogramme.log`) |
| Kurse erstellen | nur fertige Malkurse im Künstlerstudio (`MainWindow.Kuenstlerstudio.cs:743`, `Kreativ.Courses`) | Programm `kurse-programm` (STUDIO-COURSE-01): Kapitel → Lektionen → Bausteine (Text, Überschrift, Liste, Hinweis, Bild, Quiz aus „Quiz erstellen“), Kursansicht mit Person, Quiz im Kurs, „Lektion abschließen“, der Reihe nach, Fortschritt je Person; Vorlage „Malkurs aus dem Künstlerstudio“ nutzt `Kreativ.Courses` | App `Course_editor_view_files`, Core `Course_progress` |
| Kurs-Ausgabe | – | Webseite (eine HTML-Datei, Fortschritt im Browser), SCORM-1.2-Paket (imsmanifest.xml + index.html mit LMS-API), Fortschritt als CSV | Core `Scorm_package`; **echt**: 7-Zip prüft/entpackt das ZIP, Chrome läuft Kurs und entpackte index.html im Selbsttest durch |
| KI-Fachaktion | – | `quiz.fragen` (Fragen aus Text ins offene Quiz) und `kurs.lektion` (Lektion in den offenen Kurs) über `ProgramSpec.KiActions`; Vorschau → „Ja, behalten“ → **ein** Rückgängig-Schritt; keine Datei, darum neuer Schalter `ProgramKiAction.MakesFile = false` (Vorschau kündigt keine Datei an) | App `Ki_questions_into_open_quiz`, `Ki_lesson_into_open_course` (KI ersetzt); **Echtlauf qwen3:14b** `Real_local_ki` |
| Nebenbefund (gemeinsamer Code) | Plan-Zeile zeigte lange KI-Werte vollständig; im Echtlauf schob das „Ja, behalten“ aus dem sichtbaren Bereich (Klick traf ein anderes Element) | `AssistStep.Text` kürzt Werte auf 160 Zeichen, Zeilenumbrüche als „ / “ (nur Anzeige, der Wert bleibt vollständig) | `Ki_questions_into_open_quiz` (Plan-Zeile < 300 Zeichen), Echtlauf danach grün; Core „Assist“ 17/17, App `AssistChain_Ui`, `T0774KiDokument_Ui` grün |

Einbindung (am Ende der Listen, damit die Zusammenführung mit T-0780…T-0782 leicht bleibt): `StudioCatalog` (Wissen & Lernen), `StudioIcons` (eigene Pfade), `CapabilityCatalog`, `StudioProfiles`, `StudioTeams`, `StudioKnowledge`, Startideen (`PromptChips`), Adapter `QuizAnlegenAdapter`/`KursGliederungAdapter`, Bildbeschreibung in `tools/studio_bilder.py` und Bilder `Assets/studios/STUDIO-QUIZ-01.jpg`, `STUDIO-COURSE-01.jpg` (lokal mit ComfyUI + FLUX.2 klein erzeugt, danach ComfyUI beendet), Fähigkeitsmatrix neu geschrieben (`TRENDO_UPDATE_MATRIX=1`), zwei Einträge in `docs/programme/katalog.json` angehängt (aus dem Katalog-Erzeuger übernommen, übriger Katalog unverändert) plus Ordner `docs/programme/quiz-programm|kurse-programm`.
Zählungen in Tests angepasst (nicht geschwächt): Adapter 107 → 109, Teilweise 78 → 80 (`PromptActionTests`), Studios 83 → 85 (`PromptBarTests`).

## Vorbilder (Details und Quellen in `docs/nachbau/quiz|kurse/anforderungen.md`)
| Studio | Vorbilder | Offene Alternativen (Lizenz) |
|---|---|---|
| Quiz erstellen | Kahoot! (Spielmodus, Tempo), Quizlet (Karten → Test), Moodle-Test + GIFT (docs.moodle.org/401/en/GIFT_format), H5P, ActivePresenter/Quizmaker (Vergleichsartikel 2026) | Moodle GPL-3.0, H5P GPL-3.0/MIT – nur Clean-Room |
| Kurse erstellen | Articulate Rise 360 (Bausteine, Quiz-Sperre), Moodle (Abschluss, SCORM), Teachable (Fortschritt), SCORM 1.2 (edutechwiki, pipwerks) | Adapt Learning GPL-3.0, Moodle GPL-3.0 – nur Clean-Room |
Recherche-Ergebnis Austauschformat: GIFT statt QTI (Klartext, direkt in Moodle, vollständig dokumentiert); SCORM 1.2 statt H5P (H5P bräuchte die H5P-Laufzeit).

## Anforderungen erfüllt / teilweise / fehlt
- **Quiz (QZ-01…14):** erfüllt 12; teilweise QZ-04 (Timer im Fenster tickt headless nicht – Tick-Handler direkt geprüft, im Browser läuft der Zeitbalken), QZ-10 (GIFT-Import in echtem Moodle nicht geprüft), QZ-14 (Tastenkürzel nicht einzeln getestet; Tasten 1–9/Eingabe im Spiel getestet). Fehlt: Live-Mehrspieler, QTI, Bilder in Fragen.
- **Kurse (KU-01…12):** erfüllt 11; teilweise KU-08 (SCORM in echter Lernplattform nicht geprüft). Fehlt: H5P, xAPI/cmi5, Video, Zertifikat.

## Echtlauf mit lokaler KI (nichts installiert)
Ollama 127.0.0.1:11434, automatische Wahl qwen3:14b. Bedienung über die Oberfläche (Vorlage, Feld „Text für die KI“, Auftragszeile, „Los“, „Ja, behalten“, „Rückgängig“). Beleg `reports/T-0779/echtlauf-lokale-ki.md`, Rohlog `rohlog-echtlauf.txt` (1/1 bestanden, Exit 0).
- Quiz: Text über Photosynthese → Plan `quiz.fragen` → 4 Fragen (2× Auswahl, Lückentext, Wahr/Falsch), Prüfung 0 Fehler, ein Rückgängig-Schritt, Rückgängig → Quiz leer. Erste Antwort war kaputtes JSON, die vorhandene Nachfrage der Kette (`assist.ki.nachfrage`) reparierte es.
- Kurs: „Lektion Blickkontakt“ → Plan `kurs.lektion` → Lektion mit 3 Absätzen, Liste, Merksatz; Rückgängig → Kurs wie vorher.
- **Ehrlich:** Es brauchte drei Anläufe. Lauf 1: qwen3:14b nahm den Menübefehl „＋ Frage“ statt `quiz.fragen` (nur leere Musterfrage) und „Text für die KI laden …“ (Dateiauswahl) – behoben: Dateiauswahl-Befehle heißen `quiz-open-source`, `cs-open-image`, `cs-open-quiz` und sind damit wie alle „open“-Befehle für die KI gesperrt, Hinweis an die KI geschärft. Lauf 2 (parallel zu App-Tests): qwen3:14b und qwen2.5:7b antworteten nicht in 90 s, die Ausweichkette landete bei mistral:7b, das `dokument.setzen` statt Fragen lieferte. Lauf 3 (allein): bestanden, scheiterte aber zuerst am verdeckten Knopf (Nebenbefund oben); Lauf 4 nach der Kürzung: bestanden. Dauer je Plan 4–7 Minuten (qwen3:14b denkt lange).

## Fremdprogramme (Beleg `reports/T-0779/fremdprogramme.log`, Rohlog `rohlog-fremdprogramme.txt`, 1/1 bestanden)
Google Chrome (headless, eigenes Profil im Temp-Ordner): HTML-Quiz `#selbsttest` → `data-tq-result="100"`, Auswertung „8 von 8 Punkten + 4 Tempo-Bonus · Bestanden“; Kurs-Webseite → beide Lektionen erledigt; 7-Zip `t`/`x` des SCORM-ZIP „Everything is Ok“, entpackte index.html läuft in Chrome; pdftotext liest Fragen und Lösungsblatt; qpdf `--check`: lesbar, keine Fehler, aber Warnungen „input stream is complete but output may still be valid“ zu den Flate-Strömen von PDFsharp (Exit 3 = Erfolg mit Warnungen).
**Befund:** Microsoft Edge 159 dieses PCs beendet sich headless sofort ohne Ausgabe (auch direkt aus der Shell) – darum Chrome. LibreOffice wurde nicht gebraucht und nicht gestartet.

## Rot-Grün
`T0779_Rot` (nur Namen/Kennungen, übersetzt auf altem Code) im eigenen Worktree auf 184fcbbfc: **rot** („quiz-programm fehlt“, `reports/T-0779/rot-alter-stand.txt`); auf dem neuen Stand grün. Die übrigen neuen Tests nutzen neue Typen und übersetzen auf dem alten Stand nicht. Der neue Plan-Zeilen-Check in `Ki_questions_into_open_quiz` war auf dem Stand vor der Kürzung rot (Lauf 3 des Echtlaufs, Knopf verdeckt) – nicht als eigener Rot-Lauf protokolliert.

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (Filter T0779, PromptActionTests, BueroTests, Assist, StudioKnowledge, Learn, Kuenstler, ClaudeWeb): **178 bestanden, 0 Fehler** (`reports/T-0779/rohlog-core-tests.txt`).
- App (T0779QuizKurse_Ui, T0779_Rot, T0779_Fremd, StudioBilder, ClaudeEverywhere_Ui, StudioTeams_Ui, PromptBarTests, ProgramCatalog_Ui, T0774KiDokument_Ui, AssistChain_Ui, StudioIconTests, DeadButtonTests, StudiosAlle_Ui, StudiosTechnikWissen_Ui, StudioOrder_Ui, StudioClickTests, T0728_Bedienung_Ui, BueroAusbau_Ui): **306 gesamt, 0 Fehler, 4 übersprungen** (die Echt-/Fremdläufe ohne Umgebungsvariable, darunter T-0707/T-0774) (`reports/T-0779/rohlog-app-tests.txt`). Echt- und Fremdlauf einzeln mit Variable grün (siehe oben).
- Bilder mit Trendo.Render geprüft (Profi, Einfach, leerer Kurs): Dunkel-Gold, eine goldene Hauptaktion („＋ Frage“ bzw. „＋ Lektion“; Spiel- und Abschluss-Knöpfe ruhig gold), erster Schritt im Leerzustand; Befund behoben: „✕“ hieß in „Einfach“ „Schließen“ – jetzt „Antwort löschen“, „Paar löschen“, „Frage löschen“, „Baustein löschen“. Bilder in `reports/T-0779/bilder/`.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis: **VOLLWERTIG** – leeres Quiz → erster Schritt → Fragen → HTML/PDF/GIFT; leerer Kurs → Text, Kapitel, Bild, Quiz → HTML/SCORM/CSV (`Quiz_editor_to_files`, `Course_editor_view_files`).
2. Fremddatei rein/raus, Export im Fremdprogramm: **TEILWEISE** – Import: fremde GIFT-Datei (Moodle-Doku-Beispiele) und CSV; Export öffnet in Chrome, 7-Zip, pdftotext, qpdf (echt). Nicht geprüft: Import in Moodle bzw. SCORM-Lauf in einer Lernplattform (nicht vorhanden).
3. Abbruch ≤ 30 s: **TEILWEISE / NICHT GETESTET** – beide Programme nutzen die Programm-Hülle (Autosicherung alle 20 s, Wiederherstellung) und AtomicFile für alle Ausgaben; kein eigener Absturztest in diesem Paket. Spielstand im Spielmodus wird bewusst nicht gesichert (kein Dokumentinhalt).
4. KI im offenen Dokument, rückgängig: **VOLLWERTIG** – Echtlauf qwen3:14b in beiden Studios, je ein Rückgängig-Schritt.
5. Test vom Klick bis zur Datei: **VOLLWERTIG**.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Einfach/Profi (Bild + Test), Rechtsklick (Fragen, Lektionen, Bausteine, Kapitel) und Ziehen und Ablegen getestet; Menüleisten-Kürzel (Strg+Umschalt+N, Strg+D, F5, Strg+Umschalt+T) vorhanden, aber nicht einzeln getestet.

## Nicht geprüft / offene Punkte
- Kein Claude-Echtlauf (Vorgabe). Moodle-GIFT-Import und SCORM in einer echten Lernplattform nicht geprüft.
- DispatcherTimer des Spielmodus tickt im Headless-Fenster nicht; geprüft ist der Tick-Handler (`QuizPlayTick`), im Browser-Export läuft die Zeit (Chrome-Beleg).
- `docs/programme/KATALOG.md` und die Testzuordnung im Katalog nicht neu erzeugt – die Hauptsitzung erzeugt den Katalog nach der Zusammenführung neu (`TRENDO_KATALOG`).
- qwen3:14b braucht 4–7 Minuten je Plan; kleinere Modelle wählten falsche Aktionen.
- Microsoft Edge headless liefert auf diesem PC keine Ausgabe (nicht untersucht, kein Trendo-Fehler).
- Der Rest-Ordner des Rot-Worktrees `%TEMP%\trendo-T0779-rot` war beim Aufräumen noch durch einen Build-Prozess gesperrt (siehe Abgabe).

## requirements.yaml-Vorschlag
```yaml
- id: STUDIO-QUIZ-01
  name: Quiz erstellen
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), 26.11.2026 Etappe 0.9.113 Wissen & Lernen (T-0779)
- id: STUDIO-COURSE-01
  name: Kurse erstellen
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 09.11.2026 (79 neue Studios), 26.11.2026 Etappe 0.9.113 Wissen & Lernen (T-0779)
```
(Ohne diese Einträge stehen die Studios noch nicht in der Studio-Übersicht; Seite, Suche und Programmkatalog funktionieren schon.)

## Geänderte Dateien
- neu: `src/Trendo.Core/Wissen/Quiz.cs`, `src/Trendo.Core/Wissen/QuizExport.cs`, `src/Trendo.Core/Wissen/Kurse.cs`, `src/Trendo.Core/PromptAction/WissenPromptAdapters.cs`, `src/Trendo.App/MainWindow.QuizKurse.cs`, `src/Trendo.App/Assets/studios/STUDIO-QUIZ-01.jpg`, `STUDIO-COURSE-01.jpg`
- gemeinsam (klein, am Listenende): `src/Trendo.App/StudioCatalog.cs`, `StudioIcons.cs`, `MainWindow.PromptChips.cs`, `MainWindow.W6.cs` (Init), `MainWindow.ProgramShell.cs` (`MakesFile`), `MainWindow.Assist.cs` (OnApply), `src/Trendo.Core/Programs/Assist.cs` (Plan-Zeile kürzen), `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `IntelligencePromptModel.cs`, `StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `StudioTeams.cs`, `tools/studio_bilder.py`, `docs/improvement-42/studio-capability-matrix.md`, `docs/programme/katalog.json`, `docs/programme/quiz-programm/*`, `docs/programme/kurse-programm/*`, `.gitattributes`
- Tests: neu `tests/Trendo.Core.Tests/T0779QuizKurseTests.cs`, `tests/Trendo.App.Tests/T0779QuizKurseUiTests.cs`, `T0779FremdTests.cs`, `T0779RotTests.cs`; Zahlen in `PromptActionTests.cs`, `PromptBarTests.cs`
- Doku/Belege: `docs/nachbau/quiz/anforderungen.md`, `docs/nachbau/kurse/anforderungen.md`, `reports/T-0779/*` (mit `SHA256.txt`), `tasks/T-0779-quiz-kurse.md`, dieser Bericht

**Status:** umgesetzt und gezielt getestet; Merge erst nach OK.
