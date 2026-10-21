# Startzeit 0.9.4 auf dem PC (SYS-13-P15: Start < 3 s, Seite < 300 ms)

Rechner: Ryzen 7 5700X, 32 GB. Gemessen wird mit `--startup-log` vom Prozessstart bis zum ersten gezeichneten Bild.

**Ursache der 4,1 s aus 0.9.3:** Das war der Bildschirmfoto-Modus. Er wartete zuerst auf die Ollama-Prüfung und dann fest 2,5 s, bevor das Foto entstand. Der echte Start lag ohne ReadyToRun bei ~1,42 s (`profil.txt`, „base“).

| Messung | Ergebnis |
|---|---|
| 0.9.4 installiert, Läufe 2–8 | **0,81–0,84 s** bis zum ersten Bild |
| 0.9.4 installiert, Lauf 1 nach dem Update | 1,87 s (neue Dateien, Windows prüft sie beim ersten Start) |
| Frisch kopierte Dateien | 2,09–2,19 s (3 Kopien); einmal 3,5 s direkt nach dem Publish, während die Dateien noch geprüft wurden |
| Seitenaufbau | start 24–30 ms, studios 6, system 23, office 15, einstellungen 70 ms |

**Was den Start schneller macht:**
1. ReadyToRun im Publish: Der Abschnitt vom Avalonia-Start bis zum Fenster sank von ~640 auf ~420 ms, der Fensteraufbau von ~430 auf ~270 ms.
2. Die erste CPU/GPU-Messung (~65 ms) läuft erst nach dem ersten Bild.
3. Die Ollama-Prüfung blockiert weder den Start noch das Bildschirmfoto.

Seiten wurden schon vorher erst beim Öffnen gebaut. Die Programm-Prüfung der Studios kostet ~40 ms und bleibt synchron, weil die Kachel-Status davon abhängen.

**Gefunden und behoben:** Portable-Werkzeuge (qpdf) lagen in `%LOCALAPPDATA%\TrendoOS`, und das Setup ersetzt diesen Ordner beim Rückfall. Sie liegen jetzt in `%LOCALAPPDATA%\TrendoWerkzeuge`. qpdf wurde neu installiert und ist nach dem Update auf 0.9.4 noch da.

Tests auf dem PC (Stand 49b6f82): Core 420/420 (inklusive Windows-Echttests mit qpdf), App 114/114, Api 14/14.
