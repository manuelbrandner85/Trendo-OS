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
| App-Klassen, die die geänderten Dateien berühren (AgentEchtlauf_Ui, AssistChain_Ui, CORE02_Backends, CORE03/10/12_Ui, ChatAttachT0734_Ui, ChatWindow_Ui, Claude_Ui, CodeAiKi_Ui, KiMemory_Ui, KiModelT0703_Ui, KiModelT0712_Ui, OllamaContextTests, QA017_Keyboard, QA019_OfflineUi, SYS05_Ui, T0717_HuelleE5_Ui, T0727_Offen_Ui, W2_Ui, X05_Ui, DesignHonesty) | Total 69, 0 Fehler |

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
