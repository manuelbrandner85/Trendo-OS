# PC-Prüfung Windows-Teile (Stand a48ca61, Branch deps/qpdf)

Rohlogs: `win.log` (Ergebniszeilen), `tests-win.txt` (10/10 WinRealTests bestanden), `full-*.txt` (Api 14, Core 420, App 112 – alle grün). Prüfsummen: `SHA256SUMS`. Benutzer, Rechner und SID sind geschwärzt.

| Bereich | Ergebnis |
|---|---|
| Autostart | Autostart-Ordner (1) und HKCU\Run (3) nur gelesen; Ein-/Ausschalten nur in einem Temp-Ordner, mit Bestätigungspflicht |
| Dateizuordnungen | 12 Endungen gelesen, 8 mit eigener Benutzerwahl (.pdf → Edge, .html → Chrome) |
| winget | `winget export` → 47 Pakete → ImportIds, Rundreise identisch |
| Papierkorb | Testdatei in den Papierkorb, $I-Datei gelesen (9 B, Originalpfad stimmt), danach nur diese Testdatei entfernt |
| Defender | MpCmdRun-Scan einer sauberen Datei: exit 0, „Keine Bedrohung gefunden“, ohne Admin |
| Drucken | 1 Drucker gelistet (Microsoft Print to PDF), nichts gedruckt; Seite per Chrome als PDF (15 588 B) |
| Vorlesen | SAPI-Stimmen de-DE, en-US; stumm in WAV (118 636 B) |
| Narrator | Narrator.exe vorhanden; die Bedienung damit lässt sich nur von Hand prüfen → **nicht geprüft** |
| Mehrere Bildschirme | Nur 1 Bildschirm (1365×720); die Position wird auf dem echten Schirm übernommen, für einen fehlenden Schirm gilt der Standardplatz. **Ein echter zweiter Schirm ist nicht geprüft** |
| qpdf | verwaltet installiert (offizielles ZIP 12.4.2, SHA-256 db87077e…5d, ohne Admin, %LOCALAPPDATA%\TrendoOS\werkzeuge) in 2,9 s; echt zusammengefügt (2 Seiten) und Seite 2 herausgelöst (1 Seite) |
| Rückfall vorbereiten | Sicherung von Einstellungen und Protokoll (2 Dateien) |

Hinweis: Der erste Core-Lauf scheiterte an SYS-13-P22, weil die App-Pakete im frischen Klon noch nicht wiederhergestellt waren (Reihenfolge). Nach dem App-Lauf war der Core-Lauf grün (420/420). Das gespeicherte Log ist der zweite Lauf.
