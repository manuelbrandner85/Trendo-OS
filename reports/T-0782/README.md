# Belege T-0782 (Nachhilfe mit KI, Programmieren lernen)

| Datei | Inhalt |
|---|---|
| `echtlauf-lokale-ki.md`, `rohlog-echtlauf.txt` | **Echtlauf bestanden** (26.11.2026 17:28, qwen3:14b über Ollama, Claude nicht beteiligt): Nachhilfe `uebung.erzeugen` und Programmieren `hinweis.geben` über die Auftragszeile → Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt → „Rückgängig“; Code-Test echt im AppContainer. Testlauf 1/1 bestanden, exit 0 |
| `lauf2-fehlschlag/` … `lauf5-fehlschlag/` | Frühere Echtläufe, alle ehrlich aufbewahrt. Lauf 2 (15:55): qwen3:14b und qwen2.5:7b ohne Antwort binnen 90 s, Ausweichen bis mistral:7b, dessen Plan schrieb Unsinn in „themen“ – die Sperre (`KiLocked`) lehnte das ab („legst du selbst fest“). Läufe 3–5 (16:07–16:56): keine Antwort binnen 10 bzw. 20 min, weil gleichzeitig andere Programme (Blender, eine Bild-KI, andere Läufe) die Grafikkarte voll belegten (nvidia-smi: 96–100 %, 11,6 von 12 GB). Erst danach, mit freier Grafikkarte, bestand Lauf 6. |
| (Lauf 1, 15:21) | Nicht mehr als Datei vorhanden – von Lauf 2 überschrieben (Fehler meinerseits). Beobachtet: Nachhilfe-Teil mit Ausweichmodell qwen2.5:7b bestanden, Übung aber in LaTeX und mit ungekürzten Brüchen als Antworten → behoben (`Nachhilfe.OhneLatex`, Core-Test); Programmieren-Teil endete an der Zeitgrenze des Tests (Test verbessert). |
| `rot-kilokal.txt` | Rot-Lauf: Nachhilfe ohne `KiLocalOnly` → die Haupt-KI (Claude-Ersatz) wird gefragt, Test rot |
| `fremd/` | Exportdateien (Arbeitsblatt ODT/DOCX, Übungen CSV, Lösung `.py`) und was LibreOffice daraus las |
| `fremdprogramm-libreoffice.log` | LibreOffice (Kopie des Trendo-Werkzeugordners im eigenen Temp-Ordner, eigenes Profil) liest ODT, DOCX und CSV |
| `fremdprogramm-python.log` | System-Python führt die exportierte `begruessung.py` aus: 3 Tests OK |
| `SHA256.txt` | Prüfsummen aller Dateien hier |
