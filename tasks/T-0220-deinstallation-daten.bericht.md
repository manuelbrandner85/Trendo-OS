# T-0220 Bericht: Beim Deinstallieren nach den eigenen Daten fragen

**Branch:** `claude/T-02xx` (Claude Code am Windows-PC) · **Commits:** `8e9c66e`, `9870c78`

## Umgesetzt
- **Velopack-Hook** `OnBeforeUninstallFastCallback` (nur Windows) ruft `UninstallPrompt.Run` auf.
  - **Abfrage:** Ja/Nein-Frage, „Nein“ ist vorausgewählt. Sie nennt den Datenordner und sagt, dass Projekte und Dokumente bleiben, auch `projects`/`projekte` im Trendo-Ordner.
  - **Ohne Antwort:** Spätestens 22 s nach Hook-Beginn bleiben die Daten. Velopack bricht nach 30 s ab.
  - **Stille Deinstallation:** Mit `--silent`/`-s` am Aufruf von Update.exe kommt keine Frage, die Daten bleiben.
  - **Protokoll:** `%TEMP%\trendo-deinstallation.log`.
- **`DataLocation.RemoveUserData`** löscht nur im Trendo-Datenordner, mit folgenden Schutzregeln:
  - Abgelehnt werden relative und laufwerksrelative Pfade, Laufwerkswurzeln und zu flache Pfade.
  - Der Ordner muss „Trendo…“ heißen und darf selbst keine Verknüpfung sein.
  - Benutzer-, Dokumente-, Desktop-, Windows- und Programmordner sind gesperrt.
  - Liegt der Projektordner im Datenordner, wird nichts gelöscht.
  - Die Unterordner `projects` und `projekte` bleiben immer.
  - Verknüpfungen (Junction/Symlink) werden nur entfernt, nie verfolgt, und es wird nur „schreibgeschützt“ entfernt.

## Review (Security-Agent)
- **B1 (mittel):** Attribute wurden über Junctions hinweg geändert. Behoben, mit Negativtest.
- **B2 (mittel/hoch):** Projektdateien im Datenordner wären gelöscht worden. Behoben: Projekt-Unterordner bleiben, die Frage nennt sie, bei unlesbaren Einstellungen wird nichts gelöscht. Test vorhanden.
- **B3 (niedrig):** Das Zeitbudget konnte 30 s überschreiten. Behoben: Die Frage-Zeit zählt ab Hook-Beginn, die Prüfung auf stille Deinstallation dauert höchstens 2 s.
- **B4 (niedrig):** PowerShell wurde ohne vollen Pfad aufgerufen. Behoben: voller Systempfad.
- **Selbst gefundener Fehler vor dem Review:** Die erste Fassung normalisierte „C:\“ zu „C:“ (laufwerksrelativ). Der eigene Test hat das aufgedeckt. Schaden: keiner, denn die Funktion brach beim ersten Dateizugriff ab, und keine Datei gleichen Namens lag unter `C:\`. Jetzt doppelt abgesichert und getestet.

## PC-Prüfung (2026-10-25, echter Datenordner)
- Hook ohne Antwort: Die Frage erschien, nach 25,2 s endete der Hook mit „Keine Antwort: Daten bleiben“. Die Daten waren laut SHA-256-Vergleich unverändert.
- Hook mit `--silent` im Aufruf: keine Frage („Stille Deinstallation: Daten bleiben“), Dauer 1,2 s.
- Den Fall „Ja, löschen“ prüfen hier nur Tests mit Temp-Ordnern; der echte Lauf mit Setup 0.9.8 folgt unten.

## Tests (Release, Windows-PC)
Build 0 Warnungen, 0 Fehler · Api 15/15 · Core 670 bestanden, 1 übersprungen (von 671) · App 328 bestanden, 2 übersprungen (von 330), 17 min 15 s · Paket-Tests OK.

## Echte Deinstallation mit Setup 0.9.8 (2026-10-25, Manuels PC)
Skript `tools/messung/deinstallation-datenfrage.ps1`. Die Daten waren vorher gesichert und wurden am Ende byte-gleich zurückgeschrieben. Rohlog mit SHA-256: `reports/c4-setup/deinstallation-datenfrage-2026-10-25.txt`.
- **Deinstallieren ohne Antwort** (`Update.exe uninstall`, nicht still): Die Frage erschien nach 2,9 s. Nach Ablauf blieben die Daten unverändert; die Deinstallation endete nach 28,8 s.
- **Deinstallieren mit „Ja“:** Die Frage erschien nach 2,5 s, „Ja“ wurde per Fensternachricht geklickt.
  - Gelöscht: 20 Einträge. Einstellungen, Protokolle und Sicherungen sind weg.
  - Übrig blieb nur `projects` mit dem Testprojekt.
- **Endzustand:** 0.9.8 installiert, Daten byte-gleich (37 Dateien).
- **Velopack:** Das Programm zeigt eigene Aufgabendialoge mit dem Titel „Trendo OS Deinstallation“, unter anderem „Deinstallation abgeschlossen – Die Anwendung wurde erfolgreich von Ihrem Computer entfernt.“. Das Testskript bestätigt sie mit OK. Den Inhalt des ersten Dialogs (nach 0,5 s, vor der Trendo-Frage) hat das Skript nicht ausgelesen.
- **Fehler im Testskript (behoben):**
  - PowerShell macht aus `$null` einen leeren Klassennamen, dadurch fand `FindWindow` die Frage zunächst nicht.
  - Typografische Anführungszeichen beenden in PowerShell 5.1 Zeichenketten.
  - Beides betraf nur das Skript, nicht Trendo.

## Nicht geprüft / offen
- Deinstallation über „Einstellungen → Apps“ von Windows: Dort läuft derselbe Befehl `Update.exe uninstall`, geprüft ist er direkt.
- Von Trendo geladene Zusatzprogramme (`%LOCALAPPDATA%\TrendoWerkzeuge`) werden nicht abgefragt und bleiben liegen.
