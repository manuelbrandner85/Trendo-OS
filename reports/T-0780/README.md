# Belege T-0780 – Referate & Hausarbeiten, Zeitleisten

Alle Dateien byte-genau (`.gitattributes`: `reports/T-0780/** -text`), Prüfsummen in `SHA256SUMS.txt` (`sha256sum -c SHA256SUMS.txt` im Ordner).
Benutzerpfade sind durch `C:\Users\<user>` ersetzt, der Rechnername durch `<pc>`.

| Ordner/Datei | Inhalt |
|---|---|
| `fremd/libreoffice-referat/` | Zotero-RIS (fremd) eingelesen → Trendo-DOCX/ODT → LibreOffice (Kopie, eigenes Profil) als Text und PDF; pdftotext liest „Seite 2 von N“ |
| `fremd/libreoffice-folien/` | Trendo-PPTX/ODP → LibreOffice Impress als PDF (eine Seite je Folie, Folientitel per pdftotext) und als ODP/PPTX zurück; Trendo liest die Impress-Fassung wieder (Titel, Notizen) |
| `fremd/inkscape-zeitleiste/` | TimelineJS-Tabelle (fremd) eingelesen → Trendo-SVG/PDF → Inkscape 1.4 (`--query-all`, einfaches SVG, PNG), pdftotext; Trendo-CSV → LibreOffice Calc (ODS und zurück) |
| `ki/echtlauf-lokale-ki.md`, `ki/rohlog-testlauf.txt` | Echtlauf mit lokaler KI (Ollama, qwen3:14b) über die Oberfläche: Gliederung, Abschnitt, Ereignisse aus Text; Rückgängig |
| `rot-alter-stand.txt` | Rot-Lauf: neue Testdateien gegen main 184fcbbfc – übersetzen nicht (Typen fehlen) |
| `core-klassen.txt` | Ergebnis der betroffenen Core-Testklassen (Endstand) |
| `rot-gegenpruefung-4.txt` | 4. Gegenprüfung: Rot-Lauf gegen 3817e61b0 (Core 31 Fälle rot, App `Tdoc_sync_guarded` und `Help_is_honest` rot) |
| `core-klassen-g4.txt`, `app-klassen-g4.txt` | 4. Gegenprüfung: betroffene Core-Klassen und 23 App-Klassen (Endstand) |
