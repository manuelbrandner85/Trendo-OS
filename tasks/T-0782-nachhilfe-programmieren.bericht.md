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
- **Nebenwirkung der Abschottung (bitte entscheiden):** `CreateAppContainerProfile` legt für den Windows-Benutzer einmalig das Profil „Trendo.Lernen.Code“ an (Ordner unter `%LOCALAPPDATA%\Packages	rendo.lernen.code` und Einträge, die Windows selbst unter HKCU verwaltet). Das geschieht ohne Admin über die offizielle API, so wie bei jeder Windows-App mit AppContainer – ist aber eine Änderung außerhalb von Trendos Ordnern. Durch meine Tests existiert das Profil auf diesem PC bereits. Entfernen ginge mit `DeleteAppContainerProfile("Trendo.Lernen.Code")` (z. B. beim Deinstallieren); das ist noch nicht eingebaut.
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
