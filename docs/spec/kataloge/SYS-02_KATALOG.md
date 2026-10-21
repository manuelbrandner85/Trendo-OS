# SYS-02 – Ultimate Desktop Utilities & Media Center: Funktionskatalog (APPROVED)

> **Status: APPROVED (APPROVED_SPEC).** Von Manuel am 21.10.2026 im Chat freigegeben (Vorschlag aus PR #143). Freigegeben heißt: Umfang festgelegt – nicht ACCEPTED. Die Spalte „Stand im Code“ wird bei Umsetzung ehrlich gepflegt (höchstens „umgesetzt, Box getestet“; PC-Prüfung offen).

| ID | Funktion | Beschreibung | Abnahmekriterium | Stand im Code |
|---|---|---|---|---|
| SYS-02-P01 | Systemmonitor | CPU/RAM/Datenträger live in der Seitenleiste | Werte stimmen ±5 % mit Task-Manager | vorhanden |
| SYS-02-P02 | Notizen | Schnellnotizen mit Speichern in notizen.json | Neustart behält Notizen | vorhanden |
| SYS-02-P03 | Rechner | Ausdrucksrechner mit Klammern/Prozent | Testfälle 2+3*4=14, 10 % | vorhanden |
| SYS-02-P04 | Dateiablage | Ablage für häufige Dateien (Drag & Drop) | Datei öffnet aus Ablage | vorhanden |
| SYS-02-P05 | Zwischenablage-Verlauf | Letzte 50 Texte, lokal, löschbar | Kein Speichern von Passwortfeldern | umgesetzt, Box getestet |
| SYS-02-P06 | Screenshot-Werkzeug | Bereich/Fenster aufnehmen, Schwärzen | PNG im Projektordner, Schwärzung irreversibel | umgesetzt, Box getestet |
| SYS-02-P07 | Bildschirmaufnahme | Video mit ffmpeg (Benutzerinstallation) | MP4 abspielbar, Ton optional | umgesetzt, Box getestet |
| SYS-02-P08 | Medienbibliothek | Musik/Video/Bilder aus Benutzerordnern | Indiziert ≤ 2000 Dateien in < 5 s | umgesetzt, Box getestet |
| SYS-02-P09 | Medienwiedergabe | Abspielen über installierten Player/VLC | Klick startet Datei im Player | umgesetzt, Box getestet |
| SYS-02-P10 | Bildbetrachter | Vorschau, Drehen, Größe ändern | Original bleibt unverändert, Kopie gespeichert | umgesetzt, Box getestet |
| SYS-02-P11 | Batch-Umbenennen | Muster, Vorschau, Rückgängig | Vorschau = Ergebnis, Undo-Datei | umgesetzt, Box getestet |
| SYS-02-P12 | Doppelte Dateien | Suche per SHA-256, nur Vorschlag | Nichts wird ohne Bestätigung gelöscht | umgesetzt, Box getestet |
| SYS-02-P13 | Speicherplatz-Analyse | Größte Ordner/Dateien | Summen stimmen mit Explorer | umgesetzt, Box getestet |
| SYS-02-P14 | Archive | ZIP packen/entpacken (7-Zip optional) | Zip-Slip wird blockiert | umgesetzt, Box getestet |
| SYS-02-P15 | Wecker & Timer | Timer, Stoppuhr, Erinnerung | Benachrichtigung zur Sekunde ±2 | umgesetzt, Box getestet |
| SYS-02-P16 | Kalender lokal | Termine, .ics Import/Export | ICS-Rundweg verlustfrei | umgesetzt, Box getestet |
| SYS-02-P17 | Einheitenumrechner | Länge, Gewicht, Währung (manueller Kurs) | Kein Online-Kurs ohne Zustimmung | umgesetzt, Box getestet |
| SYS-02-P18 | Farbwähler | Pipette, HEX/RGB | Wert in Zwischenablage | umgesetzt, Box getestet |
| SYS-02-P19 | Fokusmodus | Benachrichtigungen dämpfen, Timer | Wird nach Ablauf zurückgesetzt | umgesetzt, Box getestet |
| SYS-02-P20 | Schnellstart (Ctrl+K) | Befehle, Seiten, Dateien | Treffer < 200 ms | vorhanden |
| SYS-02-P21 | Papierkorb-Übersicht | Anzeigen, nur mit Zustimmung leeren | Bestätigungsdialog Pflicht | umgesetzt, Box getestet |
| SYS-02-P22 | Fotos ordnen | Nach Datum (EXIF) in Ordner sortieren | Simulation vor Ausführung | umgesetzt, Box getestet |
| SYS-02-P23 | Audio konvertieren | MP3/OGG/WAV via ffmpeg | Ausgabe abspielbar, Hash im Log | umgesetzt, Box getestet |
| SYS-02-P24 | Widgets Start | Uhr, Wetter (optional online), Notizen | Offline ohne Fehler | umgesetzt, Box getestet |
| SYS-02-P25 | Ressourcen-Warnung | Hinweis bei RAM > 90 % / Platte < 5 GB | Warnung erscheint, keine Auto-Aktion | umgesetzt, Box getestet |

Stand: 5 vorhanden (vorher), 20 umgesetzt und auf der Linux-Box getestet, 0 teilweise, 0 offen. PC-Prüfung steht für alle aus.
