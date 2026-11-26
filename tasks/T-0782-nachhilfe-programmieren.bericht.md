# Bericht T-0782: „Nachhilfe mit KI“ und „Programmieren lernen“ (Wissen & Lernen, Etappe 0.9.113)

**Auftrag:** `tasks/T-0782-nachhilfe-programmieren.md` (Freigabe des Nutzers 26.11.2026) · **Branch:** `claude/T-0782-nachhilfe-programmieren` (Basis main 184fcbbfc = 0.9.112) · eigene Kopie in `%TEMP%\trendo-auftrag-T-0782`.

## Was war → was ist

| Punkt | Vorher (Beleg) | Jetzt | Nachweis (echt/ersetzt) |
|---|---|---|---|
| Nachhilfe mit KI | fehlte (`docs/audit/2026-11-26-neubewertung/fehlende-studios.md`, Wissen & Lernen Nr. 4: „nur allgemein KI fragen“) | Studio `STUDIO-TUTOR-01`, Seite `nachhilfe-programm`: Lernpfad je Fach (Mathe, Deutsch, Englisch) und Klasse, 12 Themen (7 Mathe-Generatoren, 5 feste Themen mit eigenen Aufgaben), Prüfung (Zahl, Bruch, Rest, Text), gestufte Hinweise, sokratischer Modus, Beherrschung je Thema, Wiederholungsplan (FSRS aus SPEC-06), eigene Übungen, CSV ein/aus, Arbeitsblatt ODT/DOCX, Karteikarten | Core `T0782_Nachhilfe` (6), UI `T0782Lernen_Ui` (5 Nachhilfe-Tests, KI ersetzt), Echtlauf `Real_local_ki` (Ollama) |
| Nachhilfe nur lokale KI | „KI fragen“ und die Auftragszeile nehmen Claude, wenn es als Haupt-KI verbunden ist (`MainWindow.Assist.cs`: `Intel.AskAsync`) | Neu `ProgramSpec.KiLocalOnly`: die Auftragszeile dieses Programms fragt denselben `IntelligenceCore`, aber nur Quellen auf diesem PC (`AgentKi("lokal")`); „KI fragen“ im Studio ebenso | UI `Tutor_ki_exercise_local_only` und `Tutor_ask_filtered` (ein Claude-Ersatz wird nie gefragt). Rot-Lauf ohne die Zeile: `reports/T-0782/rot-kilokal.txt` (1/1 rot) |
| Kinderschutz | – | keine Links (aus KI-Antworten, eigenen Übungen und CSV entfernt bzw. abgelehnt), keine Sendewege, sokratisch im Kinderprofil nicht abschaltbar, Exporte über die Hülle (`UserFile`/`ChildWrite`) | Core `Ki_filter`, `Own_items_csv_sheet`; UI `Tutor_drop_context_child` (Kinderprofil über `UserSettings.Child`) |
| Programmieren lernen | fehlte (Nr. 8: „`code-ki-programm` erklärt Dateien“) | Studio `STUDIO-CODELEARN-01`, Seite `programmieren-lernen-programm`: Python-Lernpfad (9 Aufgaben, 7 Konzepte, Freischaltung), Editor mit Python-Farben, echte unittest-Tests, Ergebnis je Test, Hinweise, Fortschritt, Export .py mit Tests, Import/Ablegen .py | Core `T0782_CodeLernen` (4, davon 2 ECHT), UI 3 Tests ECHT im AppContainer |
| Abgeschotteter Lauf | `Sys/Sandbox.cs` (niedrige Integrität + Job) sperrt das Netz nicht (eigener Kommentar dort); T-0766 Wasmtime läuft nur WASI-Module, kein Python | Neu `Lernen/LernSandbox.cs`: **AppContainer ohne Fähigkeiten** (OS sperrt Netz inkl. Loopback und jeden Dateizugriff außerhalb der freigegebenen Ordner) + Job (1 Prozess, 512 MB, kein UI-Zugriff) + Zeitgrenze 10 s; Laufzeit = Kopie des mitgelieferten Python in Trendos Datenordner (Werkzeugordner unverändert), ohne Admin | Core `Real_sandbox_hostile` ECHT: Loopback und Internet gesperrt (Gegenprobe ohne Container verbindet), Lesen/Schreiben außerhalb gesperrt (Gegenprobe ohne Container liest), Benutzerordner gesperrt, kein Unterprozess, Endlosschleife → timeout, eigener Ordner beschreibbar |
| KI-Fachaktion | – | Nachhilfe `uebung.erzeugen`, Programmieren `hinweis.geben` über `ProgramSpec.KiActions` (T-0774): Vorschau ändert nichts → „Ja, behalten“ → ins offene Dokument, genau ein Rückgängig-Schritt; gesperrte Felder (`KiLocked`) für Fortschritt, Code, Stand; neue Eigenschaft `ProgramKiAction.ApplyNote` (Hinweis „steht erst nach Ja im Dokument“ statt „neue Datei“) | UI `Tutor_ki_exercise_local_only`, `Tutor_ki_rejects`, `Code_ki_hint` (KI ersetzt); Echtlauf mit Ollama |
| Bewertung von Code | – | nur durch die echten Tests; die KI-Aktion ändert weder Code noch Stand; fertiger Code als „Hinweis“ wird abgelehnt | UI `Code_ki_hint` (Versuche/gelöst unverändert), Core `Ki_hint_rules` |
| Einbindung | – | StudioCatalog (Wissen & Lernen, am Listenende), Symbole, CapabilityCatalog + 2 Adapter (`arbeitsblatt.erstellen`, `aufgabe.datei`), StudioProfiles, StudioTeams, StudioKnowledge, Vorschlags-Chips, Bilder in der Übersicht (lokal mit stable-diffusion.cpp + SDXL-Turbo aus dem Trendo-Werkzeugordner, ComfyUI ist auf dem PC nicht vorhanden), Bildbeschreibung in `tools/studio_bilder.py`, Matrix-Dokument neu erzeugt | `PromptActionTests` (87/87), `StudioBilder` 1/1, `StudioIconTests` 4/4, `StudioTeams_Ui` 2/2 |

## Echtlauf mit lokaler KI (nichts installiert, Claude nicht beteiligt)
Ollama 127.0.0.1:11434, gewählt qwen3:14b (Auswahl nach T-0703). Bedienung über die Oberfläche. Belege: `reports/T-0782/echtlauf-lokale-ki.md`, `rohlog-echtlauf.txt`, `README.md`, `SHA256.txt`. Sechs Läufe, der letzte bestanden:
- **Lauf 1 (Befund, behoben):** qwen3:14b antwortete nicht binnen 90 s (gleichzeitig liefen andere Testklassen), Trendo wich auf qwen2.5:7b aus. Die Übung kam an, aber in LaTeX („\(\frac{2}{3}\)“) und mit ungekürzten Brüchen als „richtige“ Antworten. Behoben: `Nachhilfe.OhneLatex` macht daraus „2/3“; ein Bruch-Thema prüft gegen die erste (gekürzte) Antwort, „6/9“ gilt dort als „noch weiter kürzen“ (Core-Test ergänzt). Der Programmieren-Teil lief in die Zeitgrenze des Tests, weil der Test einen „kein Plan“-Ausgang nicht erkannte – Test verbessert (wie T-0774).
- **Läufe 2–5 (Befund, nicht behoben – Umgebung):** In Lauf 2 wich Trendo bis auf mistral:7b aus; dessen Plan wollte Unsinn in den Fortschritt („themen“) schreiben – die Sperre `KiLocked` lehnte das ab, nichts wurde geändert. Läufe 3–5 bekamen 10–20 min keine Antwort, weil andere Programme (Blender, eine Bild-KI anderer Sitzungen) die Grafikkarte voll belegten (nvidia-smi 96–100 %, 11,6/12 GB). Alle Logs liegen unter `reports/T-0782/lauf2…5-fehlschlag/`.
- **Endstand Echtlauf (Lauf 6, 17:28, bestanden, exit 0):** qwen3:14b ohne Ausweichen. Nachhilfe: „Erzeuge mir eine neue Übung zum Brüche kürzen mit dem Bruch 12/18 …“ → Plan `uebung.erzeugen` nach 319 s → Vorschau → „Ja“ → Übung „Kürze den Bruch 12/18 so weit wie möglich.“, Antwort 2/3 (Art Bruch), Hinweis ohne Lösung, Rückgängig-Schritte 1 → 2, „Rückgängig“ → 0 Übungen. Programmieren: Code mit `print` statt `return`, echter Testlauf 0/3 → „Gib mir einen Hinweis …“ → Plan `hinweis.geben` nach 80 s → Hinweis „… als Wert zurückgeben, nicht mit print() ausgeben …“ übernommen, gelöst bleibt „nein“, „Rückgängig“ → 0 Hinweise. Belege `reports/T-0782/echtlauf-lokale-ki.md`, `rohlog-echtlauf.txt`, Übersicht `reports/T-0782/README.md`.
- **Ehrlich:** Lauf 1 ist nicht mehr als Datei vorhanden (von Lauf 2 überschrieben); seine Beobachtungen stehen oben und im README.

## Fremdprogramme (Abnahme 2)
- LibreOffice (Kopie des Trendo-Werkzeugordners im eigenen Temp-Ordner, eigenes Profil, Original nicht gestartet): Arbeitsblatt ODT und DOCX → Text, CSV (Semikolon, UTF-8) → XLSX → CSV; Inhalte stimmen. `reports/T-0782/fremdprogramm-libreoffice.log` mit SHA-256.
- System-Python 3.13 (nicht Trendos Laufzeit): exportierte `begruessung.py` → `Ran 3 tests … OK`, exit 0. `reports/T-0782/fremdprogramm-python.log`. Zusätzlich im UI-Test `Code_click_to_real_tests`.

## Rot-Grün
- Neue Testklassen: Typen fehlen auf main (kompiliert nicht) – kein sinnvoller Rot-Lauf außer an der geänderten Hülle.
- Hülle/KI-Kette: `Tutor_ki_exercise_local_only` mit auskommentiertem `KiLocalOnly` → rot (Claude-Ersatz gefragt, kein Plan), `reports/T-0782/rot-kilokal.txt`; mit der Zeile grün.
- LaTeX-Befund: Core-Fall in `Own_items_csv_sheet` ergänzt (war vorher nicht abgedeckt; nicht separat rot belegt).

## Tests (gezielt, kein Volltest)
`dotnet build Trendo.sln -c Release`: 0 Fehler. Nach der letzten Änderung (Fach/Klasse in „Einfach“ über dem Lernpfad) erneut: T0782Lernen_Ui 9/0/1 (20,7 s), Core T0782 10/0/1.
- Core: `T0782_Nachhilfe` 6/6, `T0782_CodeLernen` 4/4 (2 ECHT im AppContainer), `T0782_Belege` (nur mit Variable); Filter PromptAction|StudioTeams|NoFixedUser|KreativKits4|GameTools|Tests|Capability|StudioKnowledge|RechteKern|Learn|Fsrs: 441 Tests, nach Anpassung der Zählwerte (107→109 Adapter, 78→80 „partial“) und neu erzeugtem Matrix-Dokument alle grün (PromptAction 87/87).
- App (Total/Failed/Skipped): T0782Lernen_Ui 9/0/1 (Echtlauf nur mit Variable), PromptBarTests 6/0, StudioFolds_Ui 3/0*, StudioBilder 1/0, StudioIconTests 4/0, StudioTeams_Ui 2/0, StudioHeadingTests 2/0, StudioOrder_Ui 3/0, ClaudeEverywhere_Ui 2/0, StartMenu_StudioFenster 5/1*, DeadButtonTests 85/0, T0728_Bedienung_Ui 7/0, Hardware_Ui 1/0, T0754DateienSchutzUi 4/0, T0763SystemAlltagUi 9/0, TvStudioUiTests 5/0, Modern_Ui 13/0, AssistChain_Ui 6/0/1, T0774KiDokument_Ui 20/0/1, Assist_Ui 3/0, KreativStudios4_Ui 5/0, T0710_MenuBar_Ui 8/0.
- \* **Hängt an `requirements.yaml`:** Die Übersicht und das Startmenü listen nur Studios aus `requirements.yaml` (`StudioRegistry`). Ohne die vorgeschlagenen Einträge fehlen die beiden Studios dort, `StartMenu_StudioFenster.Jedes_Studio` ist rot und die Zahl „63 Studios“ in `StudioFolds_Ui` stimmt erst mit den Einträgen. Mit den unten vorgeschlagenen Einträgen (nur lokal eingefügt, wieder entfernt, **nicht committet**): StartMenu_StudioFenster 5/0, StudioFolds_Ui 3/0. Ebenso ändert sich dann die Modulzahl in Core `Tests` „QA-015 … echte Liste hat N Module“ (+2) – bitte beim Eintragen mitziehen.

## requirements.yaml-Vorschlag
In `modules:` direkt vor `- id: STUDIO-MUSIK-01` (dort wurde es lokal geprüft):
```yaml
- id: STUDIO-TUTOR-01
  name: Nachhilfe mit KI
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 26.11.2026 (Etappe 0.9.113 Wissen & Lernen, T-0782)
- id: STUDIO-CODELEARN-01
  name: Programmieren lernen
  approved_expansion_functions: '0'
  approval_status: APPROVED_SPEC
  implementation_status: IN_PROGRESS
  test_status: NOT_RUN
  evidence: Freigabe im Chat 26.11.2026 (Etappe 0.9.113 Wissen & Lernen, T-0782)
```

## Vorbilder und Anforderungen
Je Studio mit Quellen, Datum, offenen Alternativen samt Lizenz und Prüfung je Zeile: `docs/nachbau/nachhilfe/anforderungen.md` (NH-01…NH-17), `docs/nachbau/programmieren-lernen/anforderungen.md` (PL-01…PL-15).
- Nachhilfe: erfüllt NH-01…NH-14; teilweise NH-15 (Karteikarten ohne eigenen Test); fehlt NH-16 (Elternbereich), NH-17 (Prüfungssimulation).
- Programmieren: erfüllt PL-01…PL-11; teilweise PL-12 (Kinderprofil nur im Code, kein UI-Test); fehlt PL-13 (Blöcke wie Scratch), PL-14 (weitere Sprachen), PL-15 (Programme mit Eingabe).

## Abnahmekriterien
| Nr. | Nachhilfe mit KI | Programmieren lernen |
|---|---|---|
| 1 Leere Vorlage → echtes Ergebnis | VOLLWERTIG (Übungen, Fortschritt, Arbeitsblatt-Datei; UI `Tutor_click_to_file`) | VOLLWERTIG (gelöste Aufgabe mit echten Tests; UI `Code_click_to_real_tests`) |
| 2 Fremddatei ein/aus, öffnet im Fremdprogramm | VOLLWERTIG (CSV ein, ODT/DOCX/CSV aus, LibreOffice liest alle drei) | VOLLWERTIG (.py ein/ablegen, .py aus läuft im System-Python) |
| 3 Abbruch ≤ 30 s Verlust | TEILWEISE – Hülle mit Notsicherung alle 20 s und AtomicFile wie jedes Programm; kein eigener Abbruch-Test für diese Studios | TEILWEISE – wie links; getippter Code wird nach 1 s Pause ins Dokument übernommen |
| 4 KI-Fachaufgabe im offenen Dokument, rückgängig | VOLLWERTIG (UI-Tests mit ersetzter KI; echt mit qwen3:14b bestanden, Lauf 6) | VOLLWERTIG (dto.) |
| 5 Test vom Klick bis zur Datei | VOLLWERTIG | VOLLWERTIG (echt, nichts ersetzt außer der KI im Fachaktions-Test) |
| 6 Einfach/Profi, Kürzel, Rechtsklick, Ablegen | VOLLWERTIG (Eingabetaste, Strg+J/L/Umschalt+N/P, Rechtsklick Thema, CSV ablegen; Einfach/Profi) | TEILWEISE (F5, Strg+J, .py ablegen, Einfach/Profi; kein Rechtsklick-Menü im Editor) |

## Bild geprüft
`Trendo.Render` (eigener `TRENDO_RENDER_ROOT`): Startseite und offenes Dokument beider Studios (Profi) und Nachhilfe in „Einfach“ – Dunkel-Gold, eine goldene Hauptaktion, erster Schritt im Leerzustand sichtbar. Befund im Bild behoben: In „Einfach“ fehlt die Eigenschaften-Spalte, Fach und Klasse stehen dort jetzt über dem Lernpfad; Texte sagen nicht mehr „rechts/links“. Im abgeschotteten Render-Ordner gibt es kein mitgeliefertes Python: dort steht ehrlich der Hinweis „Das mitgelieferte Python fehlt – ohne es laufen keine Tests“ mit „Zusatzprogramme öffnen“.

## Was NICHT geprüft wurde
- Volltest (macht die Hauptsitzung).
- Abbruch-/Wiederherstellungs-Test speziell für diese Studios (nur die Hülle allgemein).
- „In Karteikarten übernehmen“ ohne eigenen Test.
- Kinderprofil in „Programmieren lernen“ (nur Code).
- Andere Betriebssysteme: `LernSandbox` gibt es nur unter Windows; sonst blockiert der Testlauf mit Meldung.
- Ein laufendes Trendo mit echter Installation (nur Testfenster headless und Render).
- Der Katalog `docs/programme/katalog.json`/`KATALOG.md` und `docs/programme/<seite>/` wurden nicht neu erzeugt (macht die Hauptsitzung mit TRX).

## Offene Punkte
- **Nebenwirkung der Abschottung (bitte entscheiden):** `CreateAppContainerProfile` legt für den Windows-Benutzer einmalig das Profil „Trendo.Lernen.Code“ an (Ordner unter `%LOCALAPPDATA%\Packages\trendo.lernen.code` und Einträge, die Windows selbst unter HKCU verwaltet). Das geschieht ohne Admin über die offizielle API, so wie bei jeder Windows-App mit AppContainer – ist aber eine Änderung außerhalb von Trendos Ordnern. Durch meine Tests existiert das Profil auf diesem PC bereits. Entfernen ginge mit `DeleteAppContainerProfile("Trendo.Lernen.Code")` (z. B. beim Deinstallieren); das ist noch nicht eingebaut.
- `requirements.yaml`: Einträge oben eintragen (dann Startmenü/Übersicht + QA-015-Zahl).
- Merge-Hinweis: Zählwerte in `PromptBarTests` (83→85), `StudioFolds_Ui` (61→63), `PromptActionTests` (107→109, 78→80) und `docs/improvement-42/studio-capability-matrix.md` überschneiden sich mit T-0779…T-0781 – beim Zusammenführen addieren bzw. Matrix mit `TRENDO_UPDATE_MATRIX=1` neu schreiben.
- Lokale KI: qwen3:14b brauchte im ersten Lauf unter Last mehr als 90 s bis zum ersten Wort; dann antwortet das Ausweichmodell. Kleine Modelle liefern schwächere Übungen (LaTeX, ungekürzte Antworten – jetzt abgefangen).
- Wunschliste aus den Vorbildern: Elternbereich, Prüfungssimulation, Block-Programmierung für Kinder, weitere Sprachen.

## Berührte Dateien
Neu: `src/Trendo.Core/Lernen/Nachhilfe.cs`, `src/Trendo.Core/Lernen/CodeLernen.cs`, `src/Trendo.Core/Lernen/LernSandbox.cs`, `src/Trendo.Core/PromptAction/LernenPromptAdapters.cs`, `src/Trendo.App/MainWindow.Nachhilfe.cs`, `src/Trendo.App/MainWindow.CodeLernen.cs`, `src/Trendo.App/Assets/studios/STUDIO-TUTOR-01.jpg`, `src/Trendo.App/Assets/studios/STUDIO-CODELEARN-01.jpg`, `tests/Trendo.Core.Tests/T0782LernenTests.cs`, `tests/Trendo.App.Tests/T0782LernenUiTests.cs`, `docs/nachbau/nachhilfe/anforderungen.md`, `docs/nachbau/programmieren-lernen/anforderungen.md`, `reports/T-0782/*`, `tasks/T-0782-*`.
Geändert (klein, am Listenende): `src/Trendo.App/StudioCatalog.cs`, `src/Trendo.App/StudioIcons.cs`, `src/Trendo.App/MainWindow.PromptChips.cs`, `src/Trendo.App/MainWindow.W6.cs` (eine Zeile Init), `src/Trendo.Core/PromptAction/CapabilityCatalog.cs`, `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs`, `src/Trendo.Core/PromptAction/StudioKnowledge.cs`, `src/Trendo.Core/W2/StudioProfiles.cs`, `src/Trendo.Core/W2/StudioTeams.cs`, `tools/studio_bilder.py`, `docs/improvement-42/studio-capability-matrix.md`, `.gitattributes`.
Hülle/KI-Kette (gemeinsam, klein): `src/Trendo.App/MainWindow.ProgramShell.cs` (`ProgramKiAction.ApplyNote`, `ProgramSpec.KiLocalOnly`), `src/Trendo.App/MainWindow.Assist.cs` (`LocalOnly`, lokale KI für solche Programme, Hinweistext aus `ApplyNote`).
Tests angepasst (Zählwerte): `tests/Trendo.App.Tests/PromptBarTests.cs`, `tests/Trendo.App.Tests/StudioFoldsUiTests.cs`, `tests/Trendo.Core.Tests/PromptActionTests.cs`.

## Sprachwahl
Rust: NEIN. Die Abschottung braucht Windows-APIs (AppContainer, Job-Objekt), die Trendo schon in C# nutzt (`Sys/Sandbox.cs`); ein Rust-Baustein brächte keinen messbaren Vorteil, nur eine zweite Sprache. Lernende schreiben Python, weil es auf dem PC vorhanden und mitgeliefert ist.

## Gegenprüfung (Korrekturen, Commit 5ea9f1b39)

Jeder Punkt hat einen Regressionstest. Rot belegt auf zwei Wegen:
- **Alter Stand:** Für Kern-Punkte, die schon mit der alten Schnittstelle prüfbar sind, lief die Datei `T0782_RotAlt` gegen f16ba134b. Ergebnis 5 von 5 rot, auf dem neuen Stand 5 von 5 grün (`reports/T-0782/gegenpruefung/rot-alter-stand-kern.txt`, `gruen-neuer-stand-dieselbe-datei.txt`).
- **Rückbau:** Für die übrigen Punkte wurde die jeweilige Korrektur im neuen Stand zurückgebaut. Ergebnis App 6 von 6 rot, Kern 4 von 4 rot (`rot-rueckbau-app.txt`, `rot-rueckbau-kern.txt`). Danach wurde der Rückbau mit `git checkout` verworfen und alles lief wieder grün (`gruen-kern.txt`: 110 bestanden, 1 übersprungen; `gruen-app.txt`: 6/6 und 9/9 mit 1 übersprungen).

| Punkt | Behebung | Test | Rot belegt |
|---|---|---|---|
| H1 | `AssistRouteFor` gibt bei `KiLocalOnly` immer „Dokument“ oder „Projekt“ zurück, vor der Claude-Weiche. `ClaudeFromStudioBar` lehnt für solche Programme und Studios ab. Damit gilt das auch für den Menüband-Weg `AssistFromProgram`. | App `H1_tutor_never_claude`: ClaudeNoClicks an, Claude über Override angemeldet; Auftragszeile, `AssistFromProgram` und Studio-Leiste ergeben 0 Claude-Läufe und 0 Aufrufe der Haupt-KI. Gegenprobe: das Programm „text“ geht an Claude. `H1_child_codelearn_never_claude`: Kinderprofil, „Programmieren lernen“. | Rückbau |
| M1 | Eigener Desktop je Lauf (`CreateDesktopW`, DACL nur für Container, Person und SYSTEM; `lpDesktop`) | Kern `M1_own_desktop` ECHT: `GetPixel` liefert im Container überall CLR_INVALID, 0 sichtbare Fenster. Gegenprobe ohne Container: echte Pixel und mehr Fenster. | Rückbau |
| M2 | Hilfetext und Hinweis in der App sagen ehrlich, dass Rechner- und Benutzername sowie Teile der Registrierung lesbar bleiben. `ProgramSpec.KiView` entfernt Programmausgabe und Testmeldungen aus der Sicht der KI-Kette. „Hinweis von der KI“ schickt sie nur an die KI auf diesem PC (`KiFrage(…, mitAusgabe)`). | App `M2_no_output_to_claude`, Kern `M2_no_output_to_ki` | Rückbau |
| M3 | Ausgabe läuft über Pipes; Trendo behält nur die letzten 256 KB. Über 2 MB bricht der Lauf ab (Status „limit“). Der Lerncode kann nichts mehr ungebremst auf die Platte schreiben. `Tail` liest per Seek. | Kern `M3_output_flood` ECHT | Rückbau |
| M4 | Der Temp-Ordner des Containers (`…\Packages\trendo.lernen.code\AC\Temp`) wird vor und nach jedem Lauf geleert. Arbeitsordner und AC-Temp werden während des Laufs und am Ende gemessen (Grenze 50 MB). Läufe laufen nacheinander. | Kern `M4_disk_and_actemp` ECHT | Rückbau |
| M5 | Tests sind jetzt Daten (Aufruf plus erwarteter Wert). Im Container laufen nur die Aufrufe; die erwarteten Werte verlassen Trendo nie. Verglichen wird in C#. Es gibt keine Ergebnisdatei mehr; kaputte oder fremde Zeilen werden übergangen, Meldungen gekürzt. | Kern `M5_forgery_real` ECHT, `M5_parse_robust` | alter Stand |
| M6 | Versuche und „Lösung gesehen“ stehen zusätzlich in `ProgramDocument.Meta` (nicht in der Rückgängig-Liste, mit gespeichert). Wer die Lösung gesehen hat, bekommt für „richtig“ keinen Punkt. Leere, unlesbare und Format-Eingaben zählen nicht. Die KI-Kette sieht nach `Nachhilfe.KiSicht` keine Antworten, Lösungswege, Hinweise, Eingaben und keinen Verlauf. | Kern `M6_undo_cannot_reset`, `M6_ki_view`; App `M6_ki_chain_sees_no_answer` | alter Stand und Rückbau |
| M7 | `VerraetLoesung` erkennt: Rest, Bruch mit Leerzeichen, Zahlwörter bis 99, Unicode-Minus, Punkt/Komma, gesperrte Buchstaben, ae statt ä und Rechnungen (Punkt vor Strich) | Kern `M7_reveal_variants`, mit Fällen, die nicht anschlagen dürfen | alter Stand |
| M8 | Prüfung akzeptiert: Einheit nur wenn sie passt (`Item.Einheit`, Pythagoras in cm), „c = 5“, Unicode-Minus, „4 R 0“, „3, Rest 2“, −3/−4. Abgelehnt ohne Versuch: e-Schreibweise und 0,75 bei Bruchfragen. Kein Absturz bei Überlauf. Soll-Antworten werden beim Anlegen und Einlesen vereinheitlicht. | Kern `M8_answer_checks` | alter Stand |
| N2 | `MemoryError` gilt als Speichergrenze (im Lauf und im Ergebnis) | Kern `N2_memory` ECHT | Rückbau |
| N4 | „Auf diesem PC“ heißt Loopback-Adresse (`IsLoopbackKi`) | App `N4_remote_url_not_local` | Rückbau |
| N5 | Richtige Antwort mit anderer Groß-/Kleinschreibung bekommt einen Hinweis | Kern `N5_case_hint` | alter Stand |
| App-Profil | Nutzerentscheidung 26.11.2026. `UninstallPrompt.Run` ruft `RemoveLearnProfile` auf (`DeleteAppContainerProfile`), nur bei der echten Installation; eine Prüf-Instanz lässt das Profil stehen. Hinweis in der App: Karte „Testergebnis“ (in allen Stufen) und Hilfe. Das vorhandene Profil auf diesem PC habe ich nicht angefasst. | App `Uninstall_removes_profile` und Kern `Profile_remove`, beide mit eigenem, kurzlebigem Test-Profil | Rückbau |

**Ehrlich zu M5:** Der Lerncode läuft im selben Python-Prozess wie die Lauf-Hilfe. Er kann deren Zeilen fälschen, aber nur mit Werten, die er selbst ausrechnet. Die erwarteten Werte kennt er nicht.
- Bestehen kann er also nur, wenn er richtig rechnet. Das gleicht dem Fest-Eintragen von Antworten, das auch echte Tests nicht ausschließen.
- Ausnahme: Ein reiner Typ-Test (z. B. „gibt Text zurück“) lässt sich fälschen. Darum besteht im Test `M5_forgery_real` höchstens dieser eine Fall.
- Vollständig ausschließen ließe sich das nur, wenn die Aufrufe in einem zweiten Prozess gezählt würden. Das ist nicht gebaut.

**Ehrlich zu M2:** Rechner- und Benutzername sowie Teile von HKCU sind aus dem AppContainer lesbar. Ein Lerncode kann sie z. B. als Rückgabewert anzeigen. Gesperrt ist nur, dass sie über Trendo an Claude gehen. Die KI auf diesem PC bekommt sie im Kinderprofil zu sehen.

**Nicht erneut geprüft:** Der Echtlauf mit Ollama lief nicht neu. Die KI-Kette sieht jetzt weniger (KiView); die Fachaktionen sind unverändert. Die SDXL-Turbo-Bilder habe ich nicht angefasst, sie kommen aus T-0783.

**Weitere Testklassen (grün):** T0225_KiUi 3/0, AgentRun_Ui 5/0, AgentEchtlauf_Ui (2 übersprungen), ClaudeBridge_Ui 7/0, ClaudeLearn_Ui 2/0, UninstallPromptTests 8/0, AssistChain_Ui 6/0/1, T0774KiDokument_Ui 20/0/1, Assist_Ui 3/0, ClaudeEverywhere_Ui 2/0, PromptBarTests 6/0, DeadButtonTests 85/0, T0728_Bedienung_Ui 7/0, T0710_MenuBar_Ui 8/0, KreativStudios4_Ui 5/0. Kern: PromptAction, Sandbox, NoFixedUser, StudioTeams (123/0/2).
- StudioFolds_Ui ist 1 von 3 rot („63 Studios“). Das liegt wie bisher am fehlenden `requirements.yaml`-Eintrag.

**Berührte Dateien (Gegenprüfung):**
- `src/Trendo.Core/Lernen/LernSandbox.cs`, `CodeLernen.cs`, `Nachhilfe.cs`, `NachhilfePruefung.cs` (neu), `src/Trendo.Core/PromptAction/LernenPromptAdapters.cs`
- `src/Trendo.App/MainWindow.AssistChain.cs`, `MainWindow.ClaudeLos.cs`, `MainWindow.Assist.cs`, `MainWindow.ProgramShell.cs` (`KiView`), `MainWindow.AgentRun.cs`, `MainWindow.Nachhilfe.cs`, `MainWindow.CodeLernen.cs`, `UninstallPrompt.cs`
- Tests: `tests/Trendo.Core.Tests/T0782GegenpruefungTests.cs` (neu), `T0782LernenTests.cs`, `tests/Trendo.App.Tests/T0782GegenpruefungUiTests.cs` (neu)
- Belege: `reports/T-0782/gegenpruefung/*`

## 2. Gegenprüfung (Commits 7e04aa66e, bea63e589)

**Profile:** Alle echten Proben laufen jetzt in einem eigenen, kurzlebigen Test-Profil `Trendo.Lernen.Test…` (`LernSandbox.Current`, gesetzt von den Testklassen `T0782ProfilTest` und `T0782ProfilUiTest`). Nach jedem Test wird es wieder entfernt. Das vorhandene Profil `trendo.lernen.code` wurde nicht angefasst; am Ende ist unter `Packages` nur noch dieses eine Profil vorhanden.

**Rot-Grün:** Die Korrekturen wurden gleichzeitig zurückgebaut und danach mit `git checkout` verworfen (Belege in `reports/T-0782/gegenpruefung-2/`).
- App: 3 von 3 Tests rot (`rot-rueckbau-app.txt`)
- Kern: 6 von 7 Tests rot (`rot-rueckbau-kern.txt`). Grün blieb nur `Profile_exists_registry`, dessen Registry-Teil sich ohne Eingriff ins echte Profil nicht zurückbauen lässt; der Test prüft die Zuordnung direkt.
- Grün auf dem neuen Stand (`gruen.txt`): Kern T0782 und PromptAction 117 bestanden (1 übersprungen); App T0782Gegenpruefung2_Ui 3/3, T0782Gegenpruefung_Ui 6/6, T0782Lernen_Ui 8 bestanden (1 übersprungen).

| Punkt | Behebung | Test |
|---|---|---|
| H1b Plan/Chat | Eine einzige Prüfmethode `LocalOnlyRefusal(page)` (MainWindow.AssistChain.cs) für alle Claude-Wege. Aufgerufen an drei Stellen: Brücke (`BridgeLocalOnlyRefusal`), `ClaudeRunPlan` (Plan-Zeilen, auch automatisch bei ClaudeNoClicks; Meldung „✕ Nicht ausgeführt“ im Chat) und `ClaudeChatSend` (nichts von dieser Seite an Claude). | App `H1b_plan_and_chat_refused`: Claude-Bereich in einem anderen Programm, Plan offen, Wechsel in die Nachhilfe. Der Plan wird nicht ausgeführt (Übung unverändert), der Chat sendet nichts, Protokoll `claude.plan.lokal`. Gegenprobe: im anderen Programm arbeitet Claude. |
| H1b | `ClaudeBelongs` ist für `KiLocalOnly`-Programme falsch. Damit fehlen „KI › Claude öffnen“ und die Studio-Leiste. `ClaudeDockToggle` öffnet dort keinen Claude-Bereich (Meldung im Programm). „Claude fragen“ an Fehlern entfällt (`ClaudeBlockedFor`). Die Brücke lehnt bei einem Ziel oder einer offenen Seite mit `KiLocalOnly` jedes Werkzeug außer `programme` ab und meldet das an Claude. Das ist eine einzige Prüfung in `T(…)` vor der Verteilung (`BridgeLocalOnlyRefusal`). | App `H1b_no_claude_in_tutor`: Gegenprobe „text“ hat Claude. Die Nachhilfe hat kein Claude, keinen Bereich und keinen Knopf „Claude fragen“. Die Brücke lehnt `dokument_lesen`, `dokument_setzen`, `feld_setzen`, `befehl`, `klicken` und `seite_lesen` ab, ohne Lösung in der Antwort; „sokratisch“ bleibt an. |
| M4-Rest | Der ganze AC-Ordner wird gemessen und vor und nach jedem Lauf geleert. Die Standardordner Temp, INetCache, INetCookies, INetHistory und Microsoft werden nur geleert, neue Ordner entfernt. Der Registry-Speicher des eigenen Containers (`…\AppContainer\Storage\<name>`, nur dessen Unterschlüssel und Werte) wird während des Laufs gemessen (Grenze 1 MB) und vor und nach dem Lauf geleert. Der Verbrauch wird über die Job-Abrechnung gemessen: Schreibt der Lerncode mehr als 50 MB (alle Schreibvorgänge, auch in offene, wachsende Dateien), bricht der Lauf ab. Der sinkende freie Platz des Laufwerks zählt zusätzlich, aber nur, wenn der Lerncode selbst mindestens 10 MB geschrieben hat – ein erster Lauf ohne diese Bedingung wurde durch fremde Schreibvorgänge anderer Programme gestört. | Kern `M4_whole_ac_folder`, `M4_registry_storage` (Schreiben im Container über `GetAppContainerRegistryLocation`), `M4_drive_drop` |
| M6-Rest | `ProgramSpec.KiAllowed`: `dokument.setzen` gilt in der Nachhilfe nur für fach, klasse und thema (Positivliste). `ProgramSpec.KiShow` und `Nachhilfe.FuerAnzeige` gelten für Plan-Schritte, KI-Notiz, Rückfragen, Meldung, Protokollzeilen des Assistenten, die Live-Zeile und den Protokolleintrag `assist.ki.fehler`. Dort sind Antwort, Lösungsweg und Hinweis verdeckt; Texte mit einer Lösung werden zurückgehalten. Eine Text-Übung, deren Frage die Antwort schon enthält, wird abgelehnt. | App `M6_allowlist_and_hidden_answers`, Kern `M6_display_filter` |
| M7 | „drei Viertel“, „zweieinhalb“ und „anderthalb“ werden erkannt; wertgleiche Brüche (6/8) und Dezimalzahlen (0,75) bei Bruch-Antworten auch. Brüche und Zahlen, die schon in der Aufgabe stehen, zählen nicht. | Kern `M7_more` |
| Niedrig | Umlaut-Ersatz nur noch in der Eingabe (ae → ä), „Baür“ ≠ „Bauer“. „1.234“ zählt nicht als Versuch. `ProfileExists` prüft auch die Registry-Zuordnung. Der Hinweistext nennt jetzt „Teile der Registrierung (z. B. zuletzt geöffnete Dateinamen)“. | Kern `Umlaut_and_thousands`, `Profile_exists_registry` |

**Ehrlich:**
- Der freie Platz eines Laufwerks sinkt auch durch andere Programme. Darum entscheidet die Job-Abrechnung (nur Schreibvorgänge des Lerncodes); der freie Platz zählt nur zusammen mit eigenem Schreiben.
- Das Leeren des Registry-Speichers ändert Registry-Einträge unter HKCU, aber nur im Speicher von Trendos eigenem Container.
- Der Echtlauf mit Ollama lief nicht neu.

**Berührte Dateien (2. Gegenprüfung):**
- Kern: `src/Trendo.Core/Lernen/LernSandbox.cs`, `NachhilfePruefung.cs`, `Nachhilfe.cs`
- App: `src/Trendo.App/MainWindow.ClaudeBridge.cs` (nur `BridgeLocalOnlyRefusal` und der Aufruf in `T(…)`), `MainWindow.ClaudeOperate.cs` (`ClaudeRunPlan`), `MainWindow.ClaudeChat.cs` (`ClaudeChatSend`), `MainWindow.PromptAction.cs` (`ClaudeBelongs`), `MainWindow.ClaudeDock.cs`, `MainWindow.ProgramFrame.cs`, `MainWindow.AssistChain.cs` (`ClaudeBlockedFor`), `MainWindow.ClaudeLos.cs`, `MainWindow.Assist.cs`, `MainWindow.ProgramShell.cs` (`KiShow`, `KiAllowed`), `MainWindow.Nachhilfe.cs`, `MainWindow.CodeLernen.cs`
- Tests: `tests/Trendo.Core.Tests/T0782Gegenpruefung2Tests.cs` (neu), `tests/Trendo.App.Tests/T0782Gegenpruefung2UiTests.cs` (neu); `tests/Trendo.App.Tests/ClaudeBridgeUiTests.cs` (Programme mit KiLocalOnly müssen dort abgelehnt werden); Testprofile in `T0782LernenTests.cs`, `T0782GegenpruefungTests.cs`, `T0782LernenUiTests.cs`, `T0782GegenpruefungUiTests.cs`

## 3. Gegenprüfung (Commit 19cbac82f)

| Punkt | Behebung | Test | Rot belegt |
|---|---|---|---|
| M6-Rest: Zahl aus der Frage | Die Ausnahme „steht in der Frage“ gilt in `VerraetLoesung` nur noch für Brüche und Dezimalzahlen bei Bruch-Antworten (z. B. „Kürze 12/18“). Bei Zahl-Antworten gibt es sie nicht mehr: Bei „4x + 7 = 35“ ist „x = 7“ verraten. Damit die Aufgabe selbst nicht als Lösung gilt, werden wörtliche Zitate der Frage (ganz oder ein Teil nach „:“) vor der Prüfung herausgenommen. | Kern `Equations_hidden`: alle 200 Gleichungen aus dem Erzeuger – „x = <Lösung>“ und „Die Lösung ist <Lösung>“ sind verdeckt, die Aufgabe selbst bleibt sichtbar, und die Stichprobe enthält Gleichungen mit der Lösung in der Frage.<br>Kern `Fractions_counterproof` (200 Kürz-Aufgaben): Bruch aus der Aufgabe bleibt sichtbar, Lösung verdeckt. | Alter Stand 660a6a444: `Equations_hidden` rot („Löse nach x auf: 4x + 7 = 35 → x = 7“ nicht verdeckt). Die Bruch-Gegenprobe ist dort grün, wie es sein soll. |
| M6-Rest: Werte der Positivliste | Neu `ProgramSpec.KiAllowedValue` mit `Nachhilfe.KiWertProblem`: „thema“ nur gültige Themen-Kennungen, „fach“ nur bekannte Fächer, „klasse“ 1 bis 13. | Kern `Allowed_values`; App `T0782Gegenpruefung3_Ui.Thema_only_valid_ids` über die echte KI-Kette (KI ersetzt): freier Text abgelehnt, Dokument unverändert; gültige Kennung führt zur Vorschau. | Rückbau (`KiWertProblem` liefert immer null): beide Tests rot |

**Grün:**
- Kern T0782 und PromptAction: 120 bestanden, 1 übersprungen.
- App: `T0782Gegenpruefung3_Ui` 1/1, `T0782Gegenpruefung2_Ui` 3/3, `T0782Gegenpruefung_Ui` 6/6, `T0782Lernen_Ui` 8 bestanden, 1 übersprungen.

**Belege:** `reports/T-0782/gegenpruefung-3/`

**Berührte Dateien:**
- Kern: `src/Trendo.Core/Lernen/NachhilfePruefung.cs`
- App: `src/Trendo.App/MainWindow.ProgramShell.cs` (`KiAllowedValue`), `src/Trendo.App/MainWindow.Assist.cs` (Prüfung neben der Positivliste), `src/Trendo.App/MainWindow.Nachhilfe.cs`
- Tests (neu): `tests/Trendo.Core.Tests/T0782Gegenpruefung3Tests.cs`, `tests/Trendo.App.Tests/T0782Gegenpruefung3UiTests.cs`
