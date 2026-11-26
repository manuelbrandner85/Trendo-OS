# T-0740 – Beleg: Modell im Stapel anwenden, KI-Anweisungen als Datei

Lauf am 17.11.2026 auf dem Entwicklungs-PC (Windows 11, .NET 10, Release-Build), Arbeitskopie auf Basis `main`.

## Was lief echt

1. **Training aus Beispieldaten im Repo:** Vorlage „Beispiel: E-Mails sortieren“ (30 Beispiele, im Code `TrainSample`), über den Knopf „Trainieren“ – 24 zum Lernen, 6 zur Prüfung (Startwert 42). Es wird der vorhandene Klassifikator (`TextClassifier`, Naive Bayes) benutzt, kein zweites Lernsystem.
2. **Anwendung auf eine CSV-Datei:** `daten/neue-texte.csv` – 30 Texte, die beim Training nicht vorkamen, mit Spalte `erwartet` (von Hand gesetzt). Über den Knopf „CSV einordnen …“ im Menüband; Ergebnis über „Vorhersagen speichern …“ als `lauf/ausgabe-neue-texte-vorhersagen.csv` (UTF-8 mit BOM, Semikolon, LF). Alle Spalten der Eingabe bleiben, dazu `Vorhersage`, `Sicherheit`, `Bekannte Wörter`.
   Ergebnis laut unabhängiger Prüfung: **29 von 30 richtig**; daneben lag Zeile 6 („Zahlungsfrist endet am Freitag, bitte überweisen“ → Termin, 49 % sicher).
3. **Anwendung auf einen Ordner:** `daten/ordner/` (5 × .txt, 1 × .md) über „Ordner einordnen …“ → `lauf/ausgabe-ordner-vorhersagen.csv`, je Datei eine Zeile; alle 6 Dateien in der erwarteten Kategorie.
4. **Abbrechen:** 3 000 Zeilen, Fortschrittsbalken sichtbar, „Abbrechen“ mitten im Lauf → Meldung „Abgebrochen nach … von 3.000“, nichts übernommen (Test `Train_cancel`; damit der Abbruch sicher mitten im Lauf greift, wartet der Test je Fortschrittsmeldung 30 ms – das ist die einzige Testhilfe im Ablauf).
5. **KI-Anweisungen als Datei:** „Brief formulieren“ über „Als Datei …“ als `lauf/vorlage-export.md` (Markdown mit Kopfzeilen), Text geändert (`lauf/vorlage-geaendert.md`), über „Aus Datei …“ wieder eingelesen → Rückfrage → „Ersetzen“ ergibt Version 2 (Version 1 bleibt), „Als Kopie“ ergibt „Brief formulieren (Kopie)“; „Alle speichern …“ → `lauf/alle-vorlagen.json`, in eine frische Sammlung eingelesen. Eine fremd aufgebaute Datei (`daten/fremde-vorlage.md` mit `title:`/`category:`/`tags:` wie in Notiz-Programmen) wird mit Name, Bereich und Lücken übernommen.

## Ersetzt / nicht echt

- Der **Dateidialog** ist im Test ersetzt (`PickOverride`/`FolderPickOverride` liefern den Pfad); alles danach – Lesen, Trainieren, Einordnen, Schreiben – ist der echte Programmcode.
- **Fremdprogramm:** Excel und LibreOffice sind auf diesem PC nicht nutzbar (`C:\Program Files\LibreOffice` enthält kein `soffice`). Ersatzweise prüft `pruefe.py` die Ausgaben mit Pythons eigenem `csv`- und `json`-Leser (unabhängig vom Trendo-Code). Öffnen in Excel/LibreOffice: **BLOCKIERT** (Voraussetzung: eine Tabellenkalkulation installieren). **Nachtrag T-0776:** Für LibreOffice falsch – LibreOffice 26.2.4 liegt im Trendo-Werkzeugordner (`%LOCALAPPDATA%\TrendoWerkzeuge\libreoffice`); gesucht wurde nur unter `C:\Program Files\LibreOffice`, dort liegt nur ein Restordner `logs` (`reports/T-0776/werkzeuge.txt`). Eine Gegenprobe der T-0740-Dateien in LibreOffice ist damit möglich, aber noch nicht gemacht.

## Dateien

| Datei | Inhalt |
|---|---|
| `rohlog-ui-test.txt` | Rohlog des Testlaufs `T0740_Ui` (5 Tests, Exitcode 0) |
| `rohlog-python-pruefung.txt` | Rohlog der unabhängigen Prüfung (Exitcode 0) |
| `pruefe.py` | Prüfskript |
| `daten/` | Eingaben (von Hand geschrieben, neutrale Inhalte) |
| `lauf/` | von Trendo erzeugte Dateien (Kopie aus dem Testlauf) |
| `SHA256SUMS.txt` | SHA-256 aller Dateien außer dieser und der Liste selbst |

Alle Dateien haben LF als Zeilenende; `.gitattributes` setzt `reports/T-0740/** -text`, damit die Bytes im Commit gleich bleiben.

Nachprüfen: `sha256sum -c reports/T-0740/SHA256SUMS.txt` im Ordner `reports/T-0740`, `python -I -X utf8 reports/T-0740/pruefe.py reports/T-0740`.
