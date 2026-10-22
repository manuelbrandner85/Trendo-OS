# T-0002b Bericht: Zeitlimit im Startskript

**Auftrag:** Grok-Lauf zeitlich begrenzen. Nach `-TimeoutMin` (Standard 60) nur diesen Prozess samt Kindern beenden, in der Zusammenfassung `Ende: ZEITLIMIT nach N min` schreiben und mit Exit-Code 2 enden. Wird ein Befehl abgelehnt, nicht warten und nicht wiederholen.
**Branch:** `grok/T-0002b-zeitlimit`

## Geänderte Dateien

- `scripts/grok-trendo.ps1` – Parameter `-TimeoutMin` (Standard 60, mindestens 1). Grok läuft als eigener Prozess, Ausgabe weiter in die `.jsonl`. Nach dem Limit `taskkill /T /F /PID` nur für diese PID samt Kindern. Zusammenfassung `Ende: ZEITLIMIT nach N min`, Exit-Code 2. Normales Ende übernimmt den Exit-Code von Grok. Kopfkommentar mit Beispiel. Ein Punkt nachgezogen: stderr von `taskkill` setzt die Fehleraktion kurz auf `Continue`, damit das Skript danach trotzdem Exit-Code 2 schreibt.
- `AGENTS.md` – Regel 6: abgelehnten oder freigabepflichtigen Befehl nicht wiederholen, erlaubte Form nehmen (einfach `git commit -m`, kein `git -c`), sonst aufhören und den Befehl unter „Offene Punkte“ nennen.
- `tasks/README.md` – `-TimeoutMin` und Exit-Code 2 erwähnt.
- `tasks/T-0002b-zeitlimit.md` – Auftrag, mit committed.
- `tasks/T-0002b-zeitlimit.bericht.md` – dieser Bericht.

`.grok/config.toml` ist unverändert. Keine C#-Datei angefasst.

## Testergebnis

Keine C#-Änderung. Die drei Testprojekte (`Trendo.Core.Tests`, `Trendo.App.Tests`, `Trendo.Api.Tests`) sind laut Auftrag nicht vorgesehen und wurden nicht gestartet. Es gibt deshalb keine Bestanden-/Fehler-Zahlen.

`[System.Management.Automation.Language.Parser]::ParseFile` ist nicht gelaufen. Beide Aufrufe wurden abgelehnt, bevor sie starteten (kein Freigabedialog in dieser Sitzung, Prüfung gegen die Befehlsregeln nicht möglich). Nicht wiederholt.

Abgelehnte Befehle:

- Inline-Aufruf von `[System.Management.Automation.Language.Parser]::ParseFile` auf `scripts/grok-trendo.ps1` (mit Token- und Fehlerliste).
- `& "C:\Users\<user>\AppData\Local\Temp\trendo-t0002b-parse.ps1"` (dieselbe Prüfung als Datei, ohne `-ExecutionPolicy`).

## Offene Punkte / Risiken

- Ohne ParseFile-Ausgabe ist nicht belegt, dass der Parser 0 Fehler meldet. Die Datei ist nur gelesen; die Klammern und der Ablauf stimmen beim Durchsehen.
- `taskkill` und Exit-Code 2 sind nicht ausgeführt. Ob der Prozessbaum wirklich endet, ist offen.
- Die Hilfsdatei `C:\Users\<user>\AppData\Local\Temp\trendo-t0002b-parse.ps1` liegt noch, falls das Aufräumen ebenfalls scheitert.

## Nicht geprüft

- Kein Live-Test, kein Prozessstart, kein `csc`, kein `powershell -ExecutionPolicy` (so vorgegeben).
- Kein `dotnet build` und kein `dotnet test`.
- ParseFile nicht ausgeführt (siehe oben).
- Kein PR.
