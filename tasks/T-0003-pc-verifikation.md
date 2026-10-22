# T-0003: 22 TESTED_BOX-Module am PC verifizieren

**Ziel:** Für die 22 Module mit Status TESTED_BOX (außer BUS-*) echte Testläufe auf diesem Windows-PC mit Rohprotokoll und SHA-256; nur bei grünem, belegtem Lauf Status auf PC_VERIFIED.
Module: CORE-02, CORE-03, CORE-09, CORE-10, CORE-11, CORE-12, SYS-04, SYS-05, SYS-06, SYS-07, SYS-08, SYS-09, SPEC-08, SPEC-09, SPEC-10, SPEC-12, SPEC-13, SPEC-14, SPEC-15, SPEC-16, X-02, X-05.
**Branch:** grok/T-0003-pc-verifikation
**Dateien/Bereich:** `reports/pc-t0003/` (neu), `requirements.yaml` (nur Status/Nachweis dieser 22), `.grok/config.toml` (nur eine Zeile: allow bash `Get-FileHash*`), `docs/build-log/2026-10-22-pc-verifikation.md`.

**Vorgehen:**
1. Branch grok/T-0003-pc-verifikation ist schon aktiv.
2. ERLEDIGT durch Grok Bot (Datei ist für dich gesperrt, nicht bearbeiten, nur mitcommitten): Regel `{ action = "allow", tool = "bash", pattern = "Get-FileHash*" }` ergänzen (sonst nichts an der Datei). Sie gilt erst im nächsten Lauf; die Prüfsummen darfst du bis dahin weglassen, Grok Bot ergänzt sie dann.
3. Je Modul die zugehörigen Tests finden (grep nach der Modul-ID und dem Thema in tests/) und mit `dotnet test <projekt> -c Release --filter "<Filter>" --logger "trx;LogFileName=<ID>.trx" --results-directory reports/pc-t0003` laufen lassen.
4. `reports/pc-t0003/UEBERSICHT.md`: Tabelle Modul | Testfilter | Testanzahl | Ergebnis | Datei.
5. Status in requirements.yaml nur auf PC_VERIFIED, wenn: Tests für das Modul vorhanden, alle grün, .trx vorhanden. Sonst TESTED_BOX lassen und Grund nennen (z. B. „keine eigenen Tests“, „braucht Hardware“).
6. In Berichten Benutzername und Pfade unter C:\Users schwärzen (`<user>`). Prüfe die .trx-Dateien auf den Benutzernamen und nenne Treffer im Bericht (nicht selbst löschen).

**Fertig, wenn:**
- [ ] 22 Zeilen in der Übersicht, jede mit Ergebnis
- [ ] alle drei Testprojekte grün (Zahlen in den Bericht)

**Nicht tun:** keinen Code in src/ ändern, keine Tests ändern, QA-017/QA-019/Codesignatur nicht als bestanden werten, BUS-* nicht anfassen, keine Admin-Befehle, nichts löschen. Befehle, die nicht in .grok/config.toml erlaubt sind, gar nicht erst versuchen (sie beenden den Lauf).
**Am Ende:** einfach `git commit -m "…"`, `git push -u origin grok/T-0003-pc-verifikation`, Bericht `tasks/T-0003-pc-verifikation.bericht.md` mitcommitten. Keinen PR.