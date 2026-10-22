# T-0002b: Zeitlimit im Startskript, Regel „abbrechen statt warten“

**Ziel:** Ein Grok-Lauf hängt nie mehr unbegrenzt (T-0002 wartete über 20 Minuten auf eine Freigabe für `git -c user.name=… commit`, die nie kam).
**Branch:** grok/T-0002b-zeitlimit
**Dateien/Bereich:** nur `scripts/grok-trendo.ps1`, `AGENTS.md`, `tasks/README.md`.

**Vorgehen:**
1. `scripts/grok-trendo.ps1`: neuer Parameter `-TimeoutMin` (Standard 60). Grok als eigenen Prozess starten (Ausgabe weiter in die .jsonl), höchstens so lange warten; danach den Prozess samt Kindprozessen beenden (`taskkill /T /F /PID …`, nur diese PID), in der .txt-Zusammenfassung `Ende: ZEITLIMIT nach N min` vermerken und mit Exit-Code 2 enden. Bei normalem Ende Exit-Code von grok übernehmen. Kopfkommentar um ein Beispiel ergänzen.
2. `AGENTS.md`: Regel ergänzen: Wird ein Befehl abgelehnt oder bräuchte er eine Freigabe, nicht warten und nicht wiederholen. Stattdessen die erlaubte Form nehmen (z. B. einfach `git commit -m "…"`; Name/E-Mail sind im Repo gesetzt, kein `git -c`), sonst sofort aufhören und im Bericht unter „Offene Punkte“ den abgelehnten Befehl nennen.
3. `tasks/README.md`: `-TimeoutMin` kurz erwähnen.

**Fertig, wenn:**
- [ ] Skript parst fehlerfrei (`[System.Management.Automation.Language.Parser]::ParseFile(...)` ohne Fehler)
- [ ] Regel in AGENTS.md steht
- [ ] keine C#-Änderung, daher keine dotnet-Tests nötig

**Nicht tun:** `.grok/config.toml` nicht ändern, nichts außerhalb des Bereichs, keine anderen Prozesse beenden.
**Am Ende:** normaler `git commit -m` auf dem Branch, `git push -u origin grok/T-0002b-zeitlimit`, Bericht `tasks/T-0002b-zeitlimit.bericht.md` mitcommitten. Keinen PR öffnen.