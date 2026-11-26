# Belege T-0781 – Mathe-Helfer und Naturwissenschaft

| Datei | Inhalt |
|---|---|
| `echtlauf-lokale-ki.md`, `rohlog-echtlauf.txt` | **Echtlauf (bestanden)** mit Ollama qwen3:14b: Textaufgabe → Rechnung im offenen Dokument, beide Programme, „Ja, behalten“, Rückgängig |
| `echtlauf-lokale-ki-versuch1.md` … `versuch3.md`, `rohlog-echtlauf-versuch*.txt` | frühere, **fehlgeschlagene** Echtläufe (ehrlich aufbewahrt): 1) qwen3 schrieb ungültiges JSON; 2) Abbruch ohne Protokoll (danach Protokoll im Test verbessert); 3) Grafikkarte durch andere Sitzungen belegt, Ausweichen auf mistral:7b – Mathe bestanden, Naturwissenschaft nahm den Menübefehl „Ins Laborheft“ statt der Fachaktion (Befund behoben: Befehle mit `run` im Namen sind für die KI gesperrt, Test `Eingebunden`) |
| `elemente-abgleich.txt`, `rohlog-elemente-abgleich.txt` | Abgleich der eigenen Elementdaten mit der PubChem-Periodentabelle (nur Prüfung): 118 Elemente, 3 bekannte Abweichungen (Li-Intervall, Lr) |
| `fremdprogramm/` | Fremddatei rein und raus: `aufgaben.html` → **LibreOffice** (Kopie im eigenen Temp-Ordner, eigenes Profil) → `aufgaben-libreoffice.txt` → über „Aufgabenblatt öffnen …“ eingelesen (6 Aufgaben, alle Proben bestanden), eine Ableitung ergänzt → Export PDF/Markdown/LaTeX; Laborheft als PDF/CSV, Periodensystem als CSV. Gegenprobe: LibreOffice öffnet die CSV (→ XLSX → CSV zurück), importiert beide PDFs in Draw (→ PNG), `pdftotext` 4.06 liest beide PDFs. Logs `lo-txt.log`, `lo-gegenprobe.log`, `ui-test.log` |
| `bilder/` | Trendo.Render: Mathe-Helfer (Einfach leer, Profi Analysis), Naturwissenschaft (Periodensystem Einfach, Chemie, Physik Profi) |
| `rohlog-app-tests.txt`, `rohlog-core-tests.txt`, `rot-alter-stand.txt` | gezielte Testläufe und Rot-Lauf |
| `SHA256.txt` | Prüfsummen aller Belege (`sha256sum -c SHA256.txt`) |

Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt.
| `nachbesserung/` | Nachbesserung nach der Gegenprüfung: Rot-Läufe gegen 5f38f9b4c (`rot-vorher.txt`, `rot-vorher-app.txt`), gezielte Testläufe danach (`rohlog-core-tests.txt`, `rohlog-app-tests.txt`); der Fremdprogramm-Beleg in `fremdprogramm/` ist mit dem neuen Stand neu erzeugt (Hochzahlen im PDF) |
