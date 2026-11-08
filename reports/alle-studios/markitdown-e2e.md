# Word, PDF, PowerPoint, Excel ins Wissen (markitdown) – 08.11.2026

Alle Studios, Runde 8, CORE-10 „Wissen merken“.

**markitdown 0.1.9** (Microsoft, MIT, Fassung vom 21.10.2026) mit den Teilen für PDF, Word, PowerPoint und Excel (`markitdown[pdf,docx,pptx,xlsx]==0.1.9`).
- Es läuft in Trendos KI-Laufzeit **„Dokumente“**: eigene Python-Umgebung über uv, eigener Prozess, auf dem Prozessor.
- Eingerichtet wird nach Zustimmung, einmalig etwa 150 MB.

## Was die Nutzerin oder der Nutzer sieht
In „Wissen merken“ gibt es unter „Dateien einlesen“ neben dem bisherigen Knopf (Text, Markdown, CSV) **„Word, PDF, PowerPoint, Excel einlesen“**:
- **Was es tut:** Ordner oder Datei angeben. Jede passende Datei wird zu einer Wissensseite: Titel aus dem Dokument, sonst Dateiname; Quelle „Datei: name“; Kennzeichen „dokument“.
- **Wiederholtes Einlesen:** Schon vorhandene, gleiche Inhalte werden übersprungen.
- **Fehler:** Kaputte oder verschlüsselte Dateien werden gezählt und genannt; die übrigen gehen trotzdem durch.

## Umsetzung
- **Startskript** `trendo_markdown.py` (eigener Code):
  - Es nimmt **nur lokale Dateien** mit passender Endung (pdf, docx, pptx, xlsx, html, htm), keine Adressen, höchstens 50 MB.
  - Es nutzt `MarkItDown(enable_plugins=False).convert_local(…)`. Die Schnittstelle (`enable_plugins`, `convert_local`, `title`, `text_content`) ist **im Quellcode von 0.1.9 nachgeprüft**.
  - Ohne Text (z. B. nur gescannte Bilder) kommt eine verständliche Meldung.
- **C#** `DocMarkdown`:
  - Dateiauswahl, auch in Unterordnern; höchstens 500 Dateien, leere und zu große werden ausgelassen.
  - Antwort lesen; Titelregel.
- Die vorhandene Wissensablage (`KnowledgeStore.Add`) bleibt unverändert und wird mitbenutzt.

## Tests
- **`DocMarkdownTests`** (3 Tests, grün):
  - Dateiauswahl
  - Antwort und Titel
  - KI-Laufzeit mit fester Fassung; Startskript ohne Plugins, nur lokal, mit Größengrenze
- **`WissenDocs_Ui`** (1 Oberflächentest, grün):
  - Einrichten-Knopf
  - 2 Dokumente ergeben 2 Seiten mit Titel und Quelle
  - Ein zweites Einlesen überspringt beide.
  - Eine kaputte Excel-Datei wird gemeldet, ohne die anderen zu stören.
- Gefunden beim Testen: Meine eigene Nachbildung im Test war fehlerhaft, nicht das Programm.

## Nicht geprüft (NOT RUN)
- ~~**Echter Lauf mit markitdown** auf echten Word-, PDF- und Excel-Dateien.~~ → erledigt am 08.11.2026 nach Freigabe, siehe „Echter Lauf“ unten. Dafür müssten heruntergeladene Pakete ausgeführt werden; das braucht die Freigabe des Nutzers.
- Gescannte PDFs. Dafür bräuchte es Texterkennung; Trendo hat dafür bereits Tesseract an anderer Stelle.

## Echter Lauf (08.11.2026, Freigabe des Nutzers „Ja, alle testen“)
- **Einrichtung:** `markitdown[pdf,docx,pptx,xlsx]==0.1.9` (mit pdfminer.six, mammoth, openpyxl, python-pptx, pandas).
- **Echte Testdokumente:** Word (selbst gebautes DOCX), Excel (openpyxl), PowerPoint (python-pptx), HTML, PDF (mit typst gesetzt).
  - Alle werden mit Umlauten richtig umgewandelt.
  - Excel wird zur Markdown-Tabelle.
  - Der HTML-Titel wird übernommen.
- **Gefunden und behoben:**
  1. **Eine kaputte .xlsx wurde als Müll übernommen** („PK kaputt“), weil markitdown auf „reinen Text“ zurückfiel. Jetzt prüft das Skript vorher, ob Office-Dateien gültige ZIP-Archive sind und PDFs mit `%PDF-` beginnen. Kaputte Dateien werden abgelehnt.
  2. **Titel:** Word, Excel und PDF liefern keinen Titel. Trendo nimmt jetzt die erste Überschrift des Dokuments („Mietvertrag Musterweg 1“) statt des Dateinamens.
  3. **Umlaute in Fehlermeldungen aller Python-Werkzeuge waren verstümmelt** („besch�digt“). Die KI-Laufzeit setzt `PYTHONIOENCODING=utf-8`, aber `Cli.Run` las mit der Konsolen-Codepage. `Cli.Run` hat jetzt eine optionale Kodierung, die KI-Laufzeit liest UTF-8. Das betrifft auch Noten, Open3D, IFC, YOLOX und andere.
- **Tests:** `DocMarkdownRealTests` und `CliEncodingTests` sind grün.
