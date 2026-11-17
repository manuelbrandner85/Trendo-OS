# T-0723 Bericht: Code mit KI echt machen (Audit D6, Rest)

**Zweig:** `claude/T-0723-code-mit-ki` (vom Integrationszweig `claude/T-0685-b1-integration`, vor dem Push erneut hereingeholt: schon aktuell)
**Arbeitskopie:** eigene Kopie `%TEMP%\trendo-auftrag-T-0723`, nach dem Push gelöscht.

## Was war (Audit `audit-4-ki-wissen.md`, STUDIO-CODEAI-01, Kriterium 13)
„Mit Claude ändern“ endete als Text im Chat; seit T-0688 ändert Claude Code (nur mit Abo-Anmeldung) direkt im Ordner, danach Übernehmen/Zurücknehmen je Datei. Ohne Claude ging gar nichts: der lokale Adapter schrieb nur einen Bericht. Keine Vorschau **vor** der Änderung, kein Ja/Nein je Änderung, kein Rückgängig über die Hülle, kein Testlauf nach der KI-Änderung.

## Was ist jetzt
Neue Karte **„Mit KI ändern“** über dem Editor (und Menüband „KI → Mit KI ändern“). Kette (alles vorhandene Bausteine, keine zweite KI-Engine):

1. **Auftrag prüfen** – `AssistChain.Validate` (T-0707).
2. **Rechte** – `AssistChain.RightsBlock` (Rbac, Not-Stopp): bei Not-Stopp wird die KI gar nicht gefragt.
3. **Plan** – `Intel.AskAsync` mit `KiModelName`: Claude nur, wenn über das eigene Abo als Haupt-KI verbunden (IntelligenceCore), sonst das beste Modell auf diesem PC (T-0703/T-0712). Die KI sieht Dateiliste und kleine Textdateien (offene Datei zuerst, ≤ 24 000 Zeichen) und antwortet mit `PLAN:` + `DATEI:`-Blöcken mit vollständigem Inhalt (statt JSON – lokale Modelle verlieren in JSON-Texten Zeilenumbrüche). Fehlen die Blöcke, wird genau einmal nachgefragt.
4. **Vorschau** – `CodeKiChange.Build`: Zeilen-Diff je Datei (vorhandenes `CodeAiKit.Diff`), aufgeteilt in einzelne Änderungen. Nichts ist geändert.
5. **Ja/Nein je Änderung** – Häkchen je Änderung und je Datei; Knopf „Ja – n Änderung(en) übernehmen und testen“ (einzige goldene Aktion der Vorschau), „Nein – alles verwerfen“.
6. **Anwenden als EIN Rückgängig-Schritt** – `CodeKiChange.Apply`: prüft vorher, dass keine Datei seit dem Vorschlag geändert wurde (sonst wird gar nichts geschrieben), schreibt jede Datei atomar (`AtomicFile`), CRLF bleibt CRLF. Der Schritt („KI: …“) steht im Dokument (`kiSchritte`: Kennung, Wunsch, Dateinamen), die Inhalte vorher/nachher im Verlauf unter den Trendo-Daten (`code-ki/<projekt>/ki-schritte/<id>.json`). Neues Ereignis `ProgramDocument.Restored` nach Rückgängig/Wiederholen/Zurücksetzen: Die Dateien folgen dem Dokument (Strg+Z, „Rückgängig“ der Hülle, Verlauf). Eine danach von Hand geänderte Datei wird nicht überschrieben, sondern gemeldet.
7. **Bauen/Testen** – vorhandenes `DevBuild.Test` (T-0688) nach **Freigabe**: Rückfrage über `FreigabeAsync` („Tests starten“, Abbrechen vorbelegt), Ausführung nur über `ApprovalGate.Execute`. Ergebnis je Test (`DevBuild.ReadResults`). Ohne Ja: kein Start.
8. **Rot → Rückfrage** – „Die Tests sind nach der KI-Änderung rot … KI-Änderung zurücknehmen?“ mit „Ja, KI-Änderung zurücknehmen“ (nimmt genau den KI-Schritt über das Rückgängig der Hülle zurück) und „Nein, behalten“.

**Projektgrenzen** (`CodeKiChange.Blocked`): nur relative Pfade im Projektordner, kein `..`, keine absoluten Pfade, keine verknüpften Ordner (`CodeAiKit.InsideNoLinks`), nichts in `.git`/`node_modules`/`bin`/`obj` usw. (`CodeProject.Hidden`), keine Zugangsdaten-Dateien (`.env*`, `.key`, `.pem` …), keine Binärdateien, höchstens 12 Dateien je Vorschlag. Abgelehnte Pfade stehen sichtbar in der Vorschau.

Der bisherige Weg „Mit Claude ändern“ (Claude Code arbeitet selbst im Ordner, T-0688) bleibt unverändert erhalten.

## Nachweis

| Test | Art | Ergebnis |
|---|---|---|
| Core `CodeKiChangeTests` (4) | Antwort lesen (Denkteil, Zäune, abgeschnittener Block), Projektgrenzen, Diff/Blöcke/Teil-Annahme, Anwenden, Rückgängig/Wiederholen, Fremdänderung, CRLF | 4/4 grün |
| `CodeAiKi_Ui.Red_tests_ask_and_undo` | Oberfläche; **KI ersetzt**, Tests **echtes Python**: Vorschau ändert nichts → Ja → ein Schritt → 1 rot/2 grün → Rückfrage → zurück → Wiederholen → „Rückgängig“-Knopf | grün |
| `CodeAiKi_Ui.Partial_accept_green` | Nein zu README-Änderung, Rest übernommen, echtes Python 3/3 grün, Rückgängig der Hülle | grün |
| `CodeAiKi_Ui.Boundaries_and_approval` | `../draussen.py` und `.git/config` abgelehnt, nichts geschrieben; Not-Stopp → KI nicht gefragt; Freigabe „Nein“ → kein Testlauf | grün |
| **`CodeAiKi_Ui.Real_local_ki` (ECHT)** | echtes Ollama 127.0.0.1:11434, Modell **qwen3:14b** (Regel T-0703, Grafikspeicher 12 GB), Python-Beispielvorlage des Studios, Auftrag „multipliziere(a, b) ergänzen und Test schreiben“ | **grün**: KI 22 s, 2 Dateien / 4 Änderungen, nach Ja echtes `python -m unittest -v` **3 bestanden, 0 fehlgeschlagen**, Rückgängig → alle SHA-256 wie vorher. Beleg `reports/T-0723/` (`echtlauf-code-mit-ki.md`, `ki-antwort-roh.txt`, `vorschau.diff`, `nach-ki-*.py`, `testlauf-roh.txt`, `rohlog-testlauf.txt`, `SHA256SUMS`) |

Beobachtung im Echtlauf: qwen3 entfernte nebenbei eine Leerzeile in `rechner.py` – genau dafür gibt es das Nein je Änderung (im Echtlauf wurde alles angenommen).

Gezielte Testklassen (Zahlen aus der Ausgabe, alle 0 Fehler): CodeAiKi_Ui 4/4, AssistChain_Ui 6/6, FreigabenKernUi 5/5, T0710_MenuBar_Ui 8/8, UebersichtEinfach_Ui 2/2, ProgramHeadDesign_Ui 6/6, KreativStudios4_Ui 5/5, T0688_SoftwareStudio_Ui 6/6, CodeEditor_Ui 1/1, W4_Core07_Ui 2/2, ProgramShellUiTests 3/3. Core (Filter CodeKiChangeTests, AssistChainTests, KreativKits4Tests, ProgramDocumentTests, QA019_Offline/QA019_SimulatedNoNetwork, T0688_DevBuild, CodeProjectTests, QA004): 56/56; W4_Core07_DevStudio 3/3. Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK. Kein Volltest (Vorgabe). Oberfläche im Bild geprüft (Render „Code mit KI“, Vorlage Beispiel): eine goldene Hauptaktion („Ordner öffnen …“), erster Schritt sichtbar.

Rot-Grün: Die neuen Tests rufen `CodeKiChange`, `CodeAiKiPropose/Apply/Undo`, `ProgramDocument.Restored` und die Namen `ca-ki-*` auf, die es vorher nicht gab (auf altem Code nicht übersetzbar); inhaltlich gab es vorher ohne Claude keine Dateiänderung.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** für das Python-Beispielprojekt (Echtlauf: neue Funktion + Test, grün). Leeres Projekt neu anlegen bleibt im Code-Editor (T-0688).
2. Fremddatei importieren/exportieren: **nicht Teil des Pakets** (Ordner öffnen gab es schon; .patch-Export unverändert, `git apply` NICHT GETESTET).
3. Prozessabbruch ≤ 30 s: **TEILWEISE** – Dateien atomar, Schritt-Verlauf auf der Platte, .tdoc gespeichert bzw. Notsicherung; das Rückgängig der Hülle selbst überlebt keinen Neustart (wie überall).
4. KI führt Fachaufgabe aus, Ergebnis im offenen Projekt, rückgängig: **VOLLWERTIG mit lokaler KI** (Echtlauf), **Claude über das Abo NICHT GETESTET** (keine Anmeldung in der Testumgebung; der Weg ist derselbe `IntelligenceCore`).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (Echtlauf und ersetzte Läufe lesen die Dateien von der Platte, echtes Python).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: Strg+Z/Strg+Y nehmen die KI-Änderung jetzt mit; sonst **unverändert** (Rechtsklick/Ziehen im Dateibaum FEHLT weiter).

## Was NICHT geprüft wurde / offene Punkte
- Claude über das Abo als KI dieser Kette: NICHT GETESTET.
- Nur Python echt geprüft; .NET/Node laufen über dasselbe `DevBuild.Test`, hier nicht mit der KI-Kette durchgespielt.
- Rendern/Tests wählen ohne Ollama-Liste weiter `qwen2.5:0.5b` (bekannter Punkt aus T-0707/T-0712, `KiModel.ScanManifests`); der Echtlauf liest die Modelle aus `/api/tags`.
- Die Auftragszeile oben („Los“) geht für dieses Programm weiter durch die allgemeine Kette (Dokumentfelder), nicht durch „Mit KI ändern“ – zusammenführen wäre ein Folgeschritt.
- Löschen von Dateien durch die KI ist bewusst nicht vorgesehen (nur ändern/neu anlegen).
- Große Projekte: die KI sieht höchstens 24 000 Zeichen; keine Auswahl relevanter Dateien.

## Berührte Dateien
`src/Trendo.Core/Kreativ/CodeKiChange.cs` (neu), `src/Trendo.Core/Programs/ProgramDocument.cs` (Ereignis `Restored`), `src/Trendo.App/MainWindow.CodeAiKi.cs` (neu), `src/Trendo.App/MainWindow.KreativStudios4.cs` (Karte, Menüband-Gruppe „KI“, Texte, `CkHook` in `CaState`), `tests/Trendo.Core.Tests/CodeKiChangeTests.cs` (neu), `tests/Trendo.App.Tests/CodeAiKiUiTests.cs` (neu), `reports/T-0723/*`, `tasks/T-0723-code-mit-ki*.md`.

Status: umgesetzt, gezielt getestet, Echtlauf grün, auf den eigenen Zweig gepusht; Merge macht die Hauptsitzung.
