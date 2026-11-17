# T-0707 Bericht: Ein Assistenzweg (Paket B5)

**Zweig:** `claude/T-0707-ein-assistenzweg` (vom Integrationszweig `claude/T-0685-b1-integration`, B4-Stand 20f932624 hineingeholt, ohne Konflikt)
**Arbeitskopie:** eigene Kopie unter `%TEMP%\trendo-auftrag-T-0707`, nach dem Push gelöscht.

## 1. Die vier Wege – was war, was ist jetzt

| Weg vorher (Audit audit-6 Punkt 2.1, BERICHT „vier getrennte Assistenzwege“) | Fundstelle | Jetzt |
|---|---|---|
| A. Auftragszeile „Los“ → Studio-Ablauf (`StudioWorkflow`) → nur Datei im Projektordner | `MainWindow.PromptAction.cs` (`check.Click`) | Geht zuerst durch die Weiche `AssistRouteFor`. Programme der Hülle → Kette ins Dokument. Studio-Ablauf bleibt der Rückfall für Programme außerhalb der Hülle, jetzt mit der Zeile „Noch nicht im Dokument möglich: …“ (`auftrag-nicht-im-dokument`). |
| B. Programm-Assistent (`AssistPlanner`) → nur Menüband-Befehle ohne Inhalt, KI ohne Dokumentdaten | `Programs/Assist.cs`, `MainWindow.Assist.cs` | Das ist jetzt die Kette: Auftrag prüfen → Rechte prüfen → Plan → Vorschau Ja/Nein → **ein** Rückgängig-Schritt („KI: …“) → echte Datei. Die KI sieht die Daten des offenen Dokuments und hat die Aktion `dokument.setzen`. Gilt für alle 39 Hüllen-Programme, auch für die ohne passende Menüband-Befehle. |
| C. Claude-Brücke (MCP, `dokument_setzen`) mit eigener Prüfung | `MainWindow.ClaudeBridge.cs` | Bleibt Claudes Weg (Claude arbeitet selbst im Programm), prüft jetzt aber mit **derselben** Regel wie die Kette (`AssistChain.CheckEdit`: keine Freigabe-/Einwilligungsfelder, Stelle muss es geben, Text bleibt Text, Liste bleibt Liste). |
| D. KI-Befehle im Menüband („Mit Claude ausführlicher“, „Weiterschreiben/Lektorat mit Claude“, „Ideen mit Claude“, „Bewegung mit Claude planen“) → ohne Claude still nichts | `MainWindow.KreativStudios.cs`, `MainWindow.KreativStudios2.cs` | Gehen über `AssistFromProgram` durch dieselbe Weiche: Claude verbunden → Claude-Bereich wie bisher; sonst die Kette im offenen Dokument mit der KI auf diesem PC; keine KI → klare Meldung im Programm („Keine KI hat geantwortet …“). |

Die Weiche (`AssistRouteFor`, eine Stelle für alle Einstiege): Claude-Bereich nur, wenn Claude über das Abo als Haupt-KI verbunden ist **und** entweder ohne Klicks selbst im Programm arbeitet (Brücke) oder das Programm kein Dokument der Hülle hat. Sonst Kette ins Dokument. Die KI der Kette ist immer `IntelligenceCore` (`Intel.AskAsync`): Claude über das eigene Abo, wenn als Haupt-KI eingeschaltet, sonst das beste lokale Modell nach T-0703 (`KiModelName`). Kein zweiter KI-Kern, keine zweite Agenten-Laufzeit; Trendo speichert keine Claude-Zugangsdaten.

Nicht zusammengeführt (bewusst, keine Funktion entfällt): KI-Menü „KI ▾“ (öffnet den Claude-Bereich, Markierung an Claude, kie.ai – Gespräch, keine Dokumentänderung), Agenten-Lauf/KI-Team (`AgentRun`, arbeitet mit Projektdateien), Plan-Zeilen `[[trendo:…]]` im Claude-Chat, sowie `KrClaude`/`ClaudeOrHint` (Code-KI, Bilder erkennen …: geben schon eine klare Meldung ohne Claude, Tests verlangen diesen Text).

Zusätzlich aus der Übergabe von B4 (T-0706): `BridgeConsent` gibt nicht mehr selbst frei. Kinderprofil (`!CanApprove`) → sofort Nein; sonst erst die Zustimmungskarte im Claude-Bereich, nach „Ja“ `FreigabeAusRueckfrage` (Anfrage und Freigabe durch `CurrentPrincipal`), dann `Execute`. `MainWindow.ClaudeBridge.cs` aus `approveOk` gestrichen und als erlaubter Aufrufer von `FreigabeAusRueckfrage` eingetragen (Ja-Knopf einer sichtbaren Karte) – `FreigabenKernUi` grün. Die Rechteprüfung der Kette nutzt ebenfalls `CurrentPrincipal`.

## 2. Programme außerhalb der Hülle

Einheitliche Rückmeldung `AssistChain.NotInDocumentText` („Noch nicht im Dokument möglich: „…“ arbeitet noch nicht mit der Programm-Hülle …“). Liste wird zur Laufzeit aus `ProgramCatalog` minus angemeldete Hüllen-Programme berechnet (`AssistOutsideShell`), fällt also bei B6 von selbst kürzer aus. Stand dieses Zweigs: **86 außerhalb, 39 in der Hülle** – vollständige Liste `reports/T-0707/ausserhalb-der-huelle.md` (u. a. text, tabelle, praesentation, finanzen, brief, post, kalender, cad, 3d, musik-studio).

## 3. Nachweis

| Test | Art | Ergebnis |
|---|---|---|
| `AssistChainTests` (Core, 7) | Einheit: Auftrag prüfen, Rechte (Gast, Not-Stopp), KI-Plan mit `dokument.setzen`, Prüfung jeder Änderung, KI-Anweisung, Fähigkeit, zwei echte fehlerhafte qwen3-Antworten | grün |
| `AssistChain_Ui.Three_programs_into_document` | Oberfläche, KI **ersetzt**: Bücher schreiben, Tagesplaner, Messdaten – Auftrag → Vorschau (Original unverändert) → „Ja, behalten“ → ein Schritt → Datei auf der Platte gleich Dokument → Rückgängig | grün |
| `AssistChain_Ui.Ribbon_command_uses_chain` | Menüband-KI-Befehl ohne Claude → Kette; ohne KI klare Meldung | grün |
| `AssistChain_Ui.Broken_json_is_asked_again` | nicht reparierbare KI-Antwort → eine Nachfrage | grün |
| `AssistChain_Ui.Outside_shell_says_so` | Trendo Text: Rückmeldung sichtbar, Liste enthält text/tabelle/finanzen | grün |
| `AssistChain_Ui.Rights_and_bridge_same_rule` | Not-Stopp: KI wird nicht gefragt; Brücke lehnt Liste statt Text ab | grün |
| **`AssistChain_Ui.Real_local_ki` (ECHT)** | echtes Ollama 127.0.0.1:11434, Modell **qwen3:14b** (Regel T-0703 mit echtem Grafikspeicher 12 GB), drei Programme aus drei Fächern über die Oberfläche bis zur Datei, danach Rückgängig | **grün**, Beleg `reports/T-0707/echtlauf-lokale-ki.md`, Dateien `*-nach-ki.tdoc`, `SHA256.txt`, Rohlog `rohlog-testlauf.txt` |

Echtläufe (alle 16.11.2026, qwen3:14b außer Lauf 1), ehrlich mit allen Fehlschlägen – jeder Fehler hat zu einer Korrektur der Kette geführt:

| Lauf | Ergebnis | Ursache → Korrektur |
|---|---|---|
| 14:38 | Buch falsch („Neues Kapitel 3×“), Modell qwen2.5:0.5b | Testumgebung fand die Ollama-Modelle nicht (siehe offene Punkte) → Test liest die Modelle aus `/api/tags`; Regel-Planer griff das Wort „Kapitel“ → Fachaufgaben zuerst an die KI |
| 14:40 | Buch ✓, Tagesplaner ✗ | Test wartete auf den falschen Lauf → `AssistLastKey` |
| 15:00, 15:05 | Buch ✓, Tagesplaner ✗ | qwen3 ließ eine schließende Klammer weg (auch nach Nachfrage) → fehlende Klammern ergänzen |
| 15:13 | alle drei ✓ | – |
| 15:17 | Buch ✓, Tagesplaner ✗ | Plan mit angehängtem zweitem Objekt; dann Pfad „tasks[0]“ als neuer Feldname → erstes vollständiges Objekt; Pfade vereinheitlichen, seltsame Feldnamen ablehnen |
| 15:21 | **alle drei ✓, Klasse 6/6 grün** (Beleg) | – |

Die Reparaturen greifen nur, wenn danach gültiges JSON entsteht; überzählige/falsche Klammern werden nicht erraten (dann einmal nachfragen). Die lokale KI bleibt unsicher: ein einzelner Lauf kann weiter scheitern – dann zeigt die Kette nichts an und der Auftrag fällt auf den Studio-Ablauf zurück (kein falsches Ergebnis im Dokument). Ebenso: Fachaufgaben ab 8 Wörtern gehen zuerst an die KI, weil der Regel-Planer sonst „Kapitel“ als Befehl „Neues Kapitel (3×)“ las.

**Claude über das Abo: NICHT GETESTET.** Kein Lauf mit echtem Claude Code (keine Anmeldung in der Testumgebung; die Kette ruft Claude über `IntelligenceCore`/`ClaudeKiBackend` wie den Rest der App). Die Brücke ist nur mit ersetztem Claude-Prozess geprüft (`ClaudeBridge_Ui`).

Weitere berührte Testklassen (alle grün, Zahlen aus der Ausgabe): Assist_Ui 3/3, ClaudeBridge_Ui 7/7, ClaudeProgramDoc_Ui 2/2, UebersichtEinfach_Ui 2/2, PromptBarTests 6/6, KreativStudios_Ui 5/5, KreativStudios2_Ui 5/5, Modern_Ui 13/13, ClaudePrograms_Ui 6/6, ClaudeStudio_Ui 4/4, FreigabenKernUi 5/5; Core AssistTests + AssistChainTests + FreigabenKern grün; `python -m unittest discover -s tests/claude_handoff`: 11 Tests OK (Funktions-Baseline). Kein Volltest (Vorgabe).

Rot-Grün: Die neuen Tests rufen `AssistChain`, `AssistRouteFor`, `AssistFromProgram`, `AssistOutsideShell` auf, die es vorher nicht gab (auf altem Code nicht übersetzbar). Inhaltlich: vorher kam der Menüband-Befehl ohne Claude mit `false` zurück und änderte nichts; der alte Planer verwarf `dokument.setzen` (`AssistPlanner.FromKi` ohne Kette → nicht verstanden, im Core-Test belegt).

## Abnahmekriterien (für dieses Paket)

1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **TEILWEISE** – leeres Buch/Tagesplan/Messreihe erhalten über die Auftragszeile echten Inhalt (Echtlauf); kein eigenes Ziel dieses Pakets.
2. Fremddatei importieren/exportieren: **nicht Teil dieses Pakets** (nicht geprüft).
3. Prozessabbruch ≤ 30 s: **nicht geändert** – nach „Ja“ wird gespeichert (Programm-Hülle), ohne Dateinamen und ohne automatisches Speichern wird die Notsicherung geschrieben.
4. KI führt Fachaufgabe aus, Ergebnis im offenen Dokument, rückgängig: **VOLLWERTIG für die 39 Hüllen-Programme mit lokaler KI** (Echtlauf in drei Fächern), **Claude-Abo NICHT GETESTET**, **FEHLT für 86 Programme außerhalb der Hülle** (dort ehrliche Meldung, B6).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (Echtlauf und ersetzte Läufe lesen die Datei von der Platte zurück).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **unverändert** (nicht Teil des Pakets); Oberfläche im Bild geprüft (Bücher schreiben, Einfach) – unverändert, eine goldene Hauptaktion.

## Was NICHT geprüft wurde / offene Punkte

- Claude Code über das Abo (nicht angemeldet / nicht ausgeführt) – BLOCKIERT bis zu einem Lauf mit Anmeldung.
- Beobachtung zu T-0703: `KiModel.ScanManifests` fand auf diesem PC unter `~/.ollama/models` keine Manifeste (Ordner fehlt, `OLLAMA_MODELS` leer), obwohl Ollama neun Modelle meldet – die Regel wählte dadurch qwen2.5:0.5b („kein allgemeines Sprachmodell“). Der Echtlauf umgeht das, indem er die Modelle aus `/api/tags` liest; in der App ist das nicht geändert (gehört zu T-0703).
- Wenn die KI geantwortet hat, aber kein Plan lesbar ist, fällt die Auftragszeile wie bisher still auf den Studio-Ablauf zurück (nur Protokolleintrag `assist.ki.ohneplan`) – eine sichtbare Meldung dafür fehlt noch.
- Inhaltliche Schema-Prüfung je Programm fehlt: `CheckEdit` prüft nur die Art an der geänderten Stelle, nicht die Felder darin. Im Echtlauf schrieb qwen3 im Tagesplaner Zahlen/Ja-Nein statt Texte (`"min":60`, `"done":false`); ob der Tagesplaner das überall richtig anzeigt, ist nicht geprüft (Audit-Maßnahme „JSON-Schema je Programm“).
- Die Vorschlags-Chips („Schreib den Anfang von Kapitel 1“, 5 Wörter) laufen weiter über den Regel-Planer und können „Neues Kapitel“ auslösen; Schwelle 8 Wörter bewusst, damit kurze Befehle ohne KI bleiben.
- Bei Claude als KI der Kette bekommt die Nachfrage (kaputtes JSON) keinen Verlauf (Regel des KI-Kerns für Internet-KIs).
- Die Claude-Brücke fasst ihre Änderungen weiterhin nicht zu einem Rückgängig-Schritt zusammen (eigene Behalten/Zurücknehmen-Leiste).
- `MainWindow.ProgramShell.cs` wurde **nicht** geändert (Speichern über das vorhandene `ProgramSave`, Notsicherung über `DocBackup`).

## Berührte Dateien

`src/Trendo.Core/Programs/AssistChain.cs` (neu), `src/Trendo.Core/Programs/Assist.cs`, `src/Trendo.App/MainWindow.AssistChain.cs` (neu), `src/Trendo.App/MainWindow.Assist.cs`, `src/Trendo.App/MainWindow.PromptAction.cs`, `src/Trendo.App/MainWindow.ClaudeBridge.cs`, `src/Trendo.App/MainWindow.KreativStudios.cs`, `src/Trendo.App/MainWindow.KreativStudios2.cs`, `tests/Trendo.Core.Tests/AssistChainTests.cs` (neu), `tests/Trendo.App.Tests/AssistChainUiTests.cs` (neu), `tests/Trendo.App.Tests/FreigabenKernUiTests.cs` (Ausnahmeliste), `reports/T-0707/*`, `tasks/T-0707-ein-assistenzweg*.md`.

Status: umgesetzt und gezielt getestet, gepusht auf den eigenen Zweig; Merge in den Integrationszweig macht die Hauptsitzung.
