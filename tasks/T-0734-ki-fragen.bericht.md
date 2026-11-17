# Bericht T-0734: CORE-02 KI fragen – Datei anhängen und Antwort beim Entstehen

**Branch:** `claude/T-0734-ki-fragen` (eigene Kopie von `main` in `%TEMP%\trendo-auftrag-T-0734`, danach gelöscht)
**Status:** umgesetzt, gezielt getestet, Echtbeleg mit Ollama vorhanden. Kein Volltest (macht die Hauptsitzung).

## Was war (Beleg)
- audit-4, CORE-02 Kriterium 4 „Dateien importieren: FEHLT – kein Anhang im Chat“ (`MainWindow.ChatWindow.cs`, nur Textfeld).
- audit-4, Kriterium 1: „Antwort erscheint erst am Ende, während des Wartens nur ‚Trendo denkt nach …‘“. Der Stream lag schon vor (`IntelligenceCore.cs`, `IStreamingKiBackend`), ging aber nur in die Fortschrittsanzeige (`KiActivityHub`).

## Was ist jetzt
- **Datei anhängen:** Knopf „📎 Datei anhängen“ (Dateiauswahl, mehrere Dateien) und Ablegen auf Chatfläche oder Eingabe. Je Datei eine Karte (📄/🖼 Name, ✕ entfernt). Möglich: TXT, MD, CSV, JSON, LOG, XML, YAML, HTML, PDF (über vorhandenes `PdfText`/pdftotext), DOCX/ODT (vorhandenes `Ooxml`), PNG/JPG/WEBP/BMP/GIF; höchstens 5 Dateien, Text höchstens 40 000 Zeichen je Datei, Bild höchstens 15 MB. Andere Arten werden mit Grund abgelehnt. Ohne getippte Frage gilt „Fasse die angehängte Datei kurz zusammen“ bzw. „Was ist auf dem Bild zu sehen?“.
- **Hinweis, wohin die Datei geht** (`ask-attach-hint`, 🔒): Text bleibt auf diesem PC – oder geht an Claude (dein Abo), wenn Claude als Haupt-KI verbunden ist; Bilder bleiben immer auf diesem PC und gehen nur an ein Bildmodell; an andere KI-Dienste im Internet geht kein Anhang.
- **Durchsetzung im vorhandenen Kern** (`IntelligenceCore.AskAsync`, kein zweiter KI-Kern): neue optionale Felder `KiRequest.Attachments` und `KiRequest.OnText`, `KiMessage.Images`. Mit Anhang werden KI-Dienste mit Schlüssel (`IRemoteKiBackend`, nicht Haupt-KI) übersprungen; mit Bild auch Claude – nur Modelle auf dem PC, die nach Namen Bilder verstehen (`KiAttachments.IsVisionModel`: llava, qwen2.5vl, llama3.2-vision, gemma3 außer 1b …). Kein Bildmodell → klare Meldung `KiAttachments.NoVisionModel`, keine erfundene Antwort. Bei Bildern kein Ausweichen auf kleinere Textmodelle. Protokoll nur `anhang=N bild=M`, nie Inhalt.
- **Ollama/OpenAI-kompatibel lokal:** Bilder als `images` (Ollama) bzw. `image_url` (OpenAI-Format) – `KiBackendsShared.OllamaMessages/OpenAiMessages`; Kontextgröße berücksichtigt Bilder.
- **Antwort beim Entstehen:** eigene Sprechblase `ask-stream`, deren Text bei jedem Stück aktualisiert wird (ohne die Seite neu zu bauen). Gilt für Ollama, lokale OpenAI-Server und Claude (Abo, Stück für Stück je Absatz); KI-Dienste mit Schlüssel liefern weiter am Ende (kein Stream im vorhandenen `CloudKiBackend`).
- **Stopp:** vorhandener Knopf; neu bleibt das bis dahin Geschriebene mit „(abgebrochen)“ im Gespräch.
- Folgefragen kennen den Dateitext (Verlauf der Sitzung); im gespeicherten Gespräch steht die Frage mit „📎 Dateiname“ (nicht der ganze Dateitext).

## Tests (Zahlen aus der Ausgabe)
| Lauf | Ergebnis |
|---|---|
| Core `--filter KiAttachmentsT0734\|CORE02` | Bestanden: 8 erfolgreich, 0 Fehler, 0 übersprungen (inkl. echtes pdftotext) |
| Core breiter Filter (`W2\|Agent\|KiAttachmentsT0734\|CORE\|KiModel\|PromptAction` – „CORE“ traf den Namensraum, also fast alle Core-Tests) | 2806 erfolgreich, 40 übersprungen, 1 Fehler: `T-0729: Löschen wartet, bis eine gesperrte Datei frei wird` (TempDirTests, nicht berührt) – allein wiederholt: 9/9 bestanden (zeitabhängig unter Last) |
| App `ChatAttachT0734_Ui` | 4/4 bestanden (Echtlauf ohne Umgebungsvariable kehrt sofort zurück) |
| App `ChatAttachT0734_Ui` mit `TRENDO_ECHT_KIFRAGEN` (echtes Ollama) | 1/1 bestanden, Rohlog `reports/T-0734/testlauf-roh.txt` |
| App-Klassen, die die geänderten Dateien berühren (AgentEchtlauf_Ui, AssistChain_Ui, CORE02_Backends, CORE03/10/12_Ui, ChatAttachT0734_Ui, ChatWindow_Ui, Claude_Ui, CodeAiKi_Ui, KiMemory_Ui, KiModelT0703_Ui, KiModelT0712_Ui, OllamaContextTests, QA017_Keyboard, QA019_OfflineUi, SYS05_Ui, T0717_HuelleE5_Ui, T0727_Offen_Ui, W2_Ui, X05_Ui, DesignHonesty) | Total 69, 0 Fehler – davon 4 aus `ChatAttachT0734_Ui` (in der Liste mitgezählt); die 21 übrigen Klassen allein: 65 (Korrektur nach Gegenprüfung) |

Rot-Grün: die neuen Tests nutzen `KiRequest.Attachments/OnText`, `KiAttachments`, `AskAttach`, `ask-attach`, `ask-stream` – auf dem alten Code kompilieren sie nicht (rot), auf dem neuen sind sie grün.

## Echtbeleg (`reports/T-0734/`, SHA-256 in `t0734-SHA256.txt`)
Ollama 127.0.0.1:11434, Modelle qwen2.5:7b und qwen2.5vl:7b, Fenster `MainWindow` headless, Klicks über echte Zeigerereignisse:
- Text-Anhang `lieferschein.txt` → „12 Kartons Druckerpapier A4 werden am Donnerstag, 20. November geliefert.“; Mitschrift sichtbar in 8 Ständen (7 → 73 Zeichen).
- Folgefrage ohne neuen Anhang („Welche Lieferschein-Nummer …“) → „4711“.
- Stopp mitten in einer langen echten Antwort: 71 ms, 97 Zeichen behalten, endet mit „(abgebrochen)“.
- Bild (selbst erzeugtes PNG, rote Fläche, `t0734-testbild.png`) → qwen2.5vl:7b: „Rot“.
- Audit: `anhang=1 bild=0` / `anhang=1 bild=1`, kein Inhalt.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – neues Gespräch, Datei anhängen, Antwort aus echtem Ollama im Gespräch gespeichert (Echtlauf).
2. Fremddatei importieren/bearbeiten/exportieren, im Fremdprogramm öffnen: **TEILWEISE** – Fremddateien (TXT, DOCX, echtes PDF über pdftotext, PNG) werden gelesen; Export bleibt Markdown („Als Dokument“), nicht in einem Fremdprogramm geöffnet geprüft.
3. Abbruch verliert höchstens 30 s: **TEILWEISE** – unverändert: Gespräch atomar nach jeder Antwort, ungesendeter Text als Entwurf (T-0717). Angehängte, noch nicht gesendete Dateien werden nicht in der Notsicherung gemerkt; hartes Beenden nicht geprüft.
4. KI führt Fachaufgabe aus, Ergebnis im Dokument, rückgängig: **TEILWEISE** – echte KI liest die Datei und antwortet im Gespräch; Rückgängig gilt nur für die Eingabe (Hülle), nicht für Nachrichten.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – Klick „Senden“ mit echtem Ollama bis zur gespeicherten Gesprächsdatei (`ChatAttachT0734_Ui.Real_ollama_attachment_and_stream`).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Ablegen eingebaut (Chatfläche und Eingabe), Enter/Umschalt+Enter wie bisher; Ablegen selbst nicht automatisch getestet (kein Drop-Ereignis im Headless-Test, Test ruft dieselbe Methode `AskAttach`), kein Rechtsklick-Menü, Einfach/Profi nicht gesondert geprüft.

## Was NICHT geprüft wurde
- Claude über das Abo mit Anhang (kein angemeldetes Claude im Testfenster; nur mit nachgebildeter Haupt-KI im Core-Test geprüft) und Claude-Mitschrift.
- Ablegen per echtem Drag-and-drop aus dem Explorer.
- Lokale OpenAI-kompatible Server (LM Studio) mit Bild – nur Code, kein Lauf.
- PDF-Scans (Texterkennung) – bewusst nicht angebunden; Scan-PDF meldet, dass kein Text drin ist.
- Bildschirmbild mit angehängter Karte/Mitschrift (Render zeigt die leere Seite mit „📎 Datei anhängen“; Karte und Sprechblase nur im logischen Baum geprüft).

## Offene Punkte
- Data-URL im OpenAI-Format nennt immer `image/png`, auch bei JPG.
- Dateitext steht nach Neustart nicht mehr im Verlauf eines wieder geöffneten Gesprächs (nur der Dateiname).
- Antwort neu erzeugen, Frage bearbeiten, Modell je Gespräch, Seite „ki“ als zweiter Chat: weiter offen (audit-4 Punkte 3–4).

## Berührte Dateien
`src/Trendo.Core/W2/KiAttachments.cs` (neu), `src/Trendo.Core/W2/IntelligenceCore.cs`, `src/Trendo.App/KiBackends.cs`, `src/Trendo.App/MainWindow.ChatWindow.cs`, `tests/Trendo.Core.Tests/KiAttachmentsT0734Tests.cs` (neu), `tests/Trendo.App.Tests/ChatAttachT0734UiTests.cs` (neu), `reports/T-0734/*`, `tasks/T-0734-ki-fragen*.md`. Keine gemeinsamen Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer/neuerungen).

## Gegenprüfung behoben (17.11.2026)

Unabhängige Gegenprüfung: keine Freigabe wegen 2 mittleren und 4 niedrigen Befunden. Alle sechs sind behoben. Jeder neue Test lief zuerst auf dem alten Code und schlug dort fehl (Rot-Probe). Für die App-Tests hatte der alte Code dabei nur den Test-Haken `AskPdfToTextOverride`.

| # | Befund | Was jetzt gilt | Test (Rot auf altem Code → Grün) |
|---|---|---|---|
| 1 (mittel) | Frage ging verloren, wenn ein Anhang erst beim Senden nicht lesbar war (Feld geleert, Entwurf geschlossen, erst danach gelesen) | `AskChat(q, fromInput: true)` liest zuerst alle Anhänge. Erst wenn das klappt, wird der Entwurf geschlossen (`LinkClose`). Bei einem Fehler bleiben Frage und Anhänge stehen, und die Meldung nennt die Datei und den Grund. Kommt danach keine Antwort zustande (z. B. `NoVisionModel` oder keine KI erreichbar), steht die Frage wieder im Eingabefeld (`_askRestore`). | `ChatAttachT0734_Ui.Unreadable_attachment_keeps_question`: leere TXT, nach dem Anhängen gelöschte Datei, Bild ohne Bildmodell. Rot: `Expected "Was steht in der Notiz?" Actual null` |
| 2 (mittel) | `Load` lief auf dem UI-Faden, ohne Größengrenze, pdftotext bis 60 s ohne Stopp, nur UTF-8 | `KiAttachments.LoadAllAsync` läuft mit `Task.Run` und `CancellationToken` neben der Oberfläche, und „Stopp“ ist schon beim Lesen sichtbar und wirksam. Von Textdateien werden höchstens die ersten 160 KB per Stream gelesen. Die Kodierung wird erkannt (UTF-8 mit/ohne BOM, UTF-16 mit BOM, sonst Windows-1252, über das vorhandene `TextFileCodec.Decode`), und beim Kürzen wird kein UTF-8-Zeichen zerbrochen. pdftotext läuft über `Cli.RunStreamAsync`, Stopp beendet das Programm. PDF/DOCX/ODT über 200 MB werden abgelehnt. | `KiAttachmentsT0734.Big_text_reads_only_head`, rot: „für 40 MB Datei 240 MB Speicher belegt“. `KiAttachmentsT0734.Encoding_detected` (ANSI-CSV), rot: Teilstring nicht gefunden. `ChatAttachT0734_Ui.Reading_off_ui_and_stoppable` (langsames pdftotext), rot: „Oberfläche stand 11495 ms still“ |
| 3 (niedrig) | SHA-256 des Echtlauf-Logs nach dem Auschecken nicht prüfbar (gemischte Zeilenenden) | Der Test schreibt Log und SHA-Datei einheitlich mit `\n` (`.gitattributes` unverändert). Der Echtlauf ist neu erzeugt, `testlauf-roh.txt` ebenfalls mit LF. Hashes aus dem Commit-Stand geprüft (siehe unten). | Echtlauf `Real_ollama_attachment_and_stream` mit `TRENDO_ECHT_KIFRAGEN`, 1/1 bestanden |
| 4 (niedrig) | OpenAI-Format immer `data:image/png` | Der MIME-Typ kommt aus den ersten Bytes (`KiBackendsShared.ImageMime`, `KiAttachments.ImageMime`). PNG und JPG gehen unverändert mit. GIF, BMP und WEBP werden vorher mit SkiaSharp nach PNG umgewandelt (`AttachmentToPng`). Lässt sich ein Bild nicht umwandeln, wird es ehrlich abgelehnt („Speichere das Bild bitte als PNG oder JPG“). | `ChatAttachT0734_Ui.OpenAi_image_mime`, rot: Teilstring `image/jpeg` fehlt. `KiAttachmentsT0734.Image_formats_honest`, rot: keine Ausnahme bei GIF. `ChatAttachT0734_Ui.Bmp_converted_to_png` (echtes SkiaSharp) |
| 5 (niedrig) | Während der Antwort abgelegte Dateien gingen durch `_askFiles.Clear()` verloren; voller Anhangstext im Verlauf; `sofar.ToString()` je Stück | Nur die gesendeten Anhänge werden entfernt. `IntelligenceCore.FitHistory` passt den Verlauf für die KI auf dem PC in `LocalPromptChars` (36 000 Zeichen = (16384 − 4096) Token × 3) ein: Passt alles, bleibt alles unverändert. Sonst zählen die neuesten Nachrichten zuerst, und die erste zu lange wird gekürzt und markiert. Die aktuelle Frage wird bei Bedarf ebenfalls gekürzt, die Systemanweisung bleibt vollständig vorn. `OnText` liefert nur noch die neuen Stücke; die Anzeige holt den Text höchstens einmal je UI-Durchlauf. | `ChatAttachT0734_Ui.Drop_during_answer_is_kept`, rot: `Expected [zweite.txt] Actual []`. `KiAttachmentsT0734.History_capped_system_intact`, rot: „200125 Zeichen gehen an die KI“. `KiAttachmentsT0734.Text_attachment_and_stream` erwartet jetzt die Stücke statt des Gesamttexts, rot: Collections differ |
| 6 (niedrig) | Bericht: 69 statt 65 Tests | Die 69 enthielten die 4 Tests von `ChatAttachT0734_Ui`. Die 21 weiteren Klassen allein ergeben 65 (wie die Gegenprüfung gezählt hat). Neuer Stand siehe unten. | – |

### Tests nach der Behebung (Zahlen aus der Ausgabe)
| Lauf | Ergebnis |
|---|---|
| Build Release | 0 Fehler |
| Core `KiAttachmentsT0734\|CORE02` | 12 erfolgreich, 0 Fehler, 0 übersprungen (inkl. echtes pdftotext) |
| App `ChatAttachT0734_Ui` | 9/9 bestanden (Echtlauf ohne Umgebungsvariable kehrt sofort zurück) |
| App `ChatAttachT0734_Ui` mit `TRENDO_ECHT_KIFRAGEN` (echtes Ollama, qwen2.5:7b + qwen2.5vl:7b) | 1/1 bestanden, Rohlog `reports/T-0734/testlauf-roh.txt` |
| App: dieselben 21 weiteren Klassen wie oben + `ChatAttachT0734_Ui` | 65 + 9 = 74 Tests, 0 Fehler |

Echtlauf neu: „12 Kartons Druckerpapier A4 werden am Donnerstag, 20. November geliefert.“ (7 Mitschrift-Stände), Folgefrage „4711“, Stopp nach 66 ms mit 107 Zeichen behalten, Bild → qwen2.5vl:7b „Rot“. Im Audit nur `anhang=1 bild=0` / `anhang=1 bild=1`.

Hash-Prüfung: `git show HEAD:reports/T-0734/<datei> | sha256sum` stimmt für alle drei Dateien mit `t0734-SHA256.txt` überein, ebenso ein frischer Checkout mit `core.autocrlf=false`. Einschränkung: Ein Windows-Checkout mit `core.autocrlf=true` wandelt die (jetzt reinen LF-)Textdateien in CRLF um, dann weicht der Hash ab. Dauerhaft hilft nur `reports/T-0734/** -text` in `.gitattributes`, wie bei den anderen Belegordnern. Das war hier ausdrücklich nicht erlaubt, deshalb bleibt es ein Vorschlag für die Zusammenführung.

Weiter ersetzt (kein Ende-zu-Ende): die KI in den UI-Tests (nachgebildeter Stream) und pdftotext im Stopp-Test (absichtlich langsames Skript). Echt geprüft: Ollama (Text, Folgefrage, Stopp, Bild), pdftotext (Core), SkiaSharp-Umwandlung BMP → PNG.

Erledigt aus „Offene Punkte“: der MIME-Typ im OpenAI-Format. Weiter offen: Bild mit einem echten lokalen OpenAI-kompatiblen Server (LM Studio), nicht gelaufen. Ebenso echtes Ziehen und Ablegen aus dem Explorer.

Berührte Dateien (Gegenprüfung): `src/Trendo.Core/W2/KiAttachments.cs`, `src/Trendo.Core/W2/IntelligenceCore.cs`, `src/Trendo.App/KiBackends.cs`, `src/Trendo.App/MainWindow.ChatWindow.cs`, `tests/Trendo.Core.Tests/KiAttachmentsT0734Tests.cs`, `tests/Trendo.App.Tests/ChatAttachT0734UiTests.cs`, `reports/T-0734/*`, `tasks/T-0734-ki-fragen.bericht.md`.
