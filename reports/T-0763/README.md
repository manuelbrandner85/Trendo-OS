# T-0763 – Belege

Alle Pfade ohne Benutzernamen (`C:\Users\<user>`). Prüfsummen: `SHA256SUMS.txt`.

| Datei | Inhalt |
|---|---|
| `lastprobe.txt` | X-09 Lastprobe (echt, dieser PC, 16 Kerne): Prozessorlast je Prozess aus Trendo (`TaskManager.Rows`) gegen die Windows-Leistungsindikatoren `\Process(*)\% Processor Time` (pdh.dll, ohne Administratorrechte). Last 1: PowerShell-Endlosschleife (1 Kern), Last 2: Testprozess mit 8 Rechenfäden. Abweichung 0,00 bzw. 0,18 Prozentpunkte. Hinweis: Vergleich mit den Leistungsindikatoren, nicht mit dem Bild des Windows-Task-Managers (der zeigt „Processor Utility“, das bei Turbo-Takt abweichen kann). |
| `installation-echt.txt` | X-02 echte Installation dieses PCs (Velopack, Trendo 0.9.108): nur lesend gegen das Paket `packages\TrendoOS-0.9.108-full.nupkg` geprüft (418 Dateien, 0 Abweichungen, 1,1 s). Auf einer Kopie (Programmordner + Paket) `Trendo.Core.dll` gelöscht und ein Byte in `TrendoOS.deps.json` geändert → beide erkannt und aus dem Paket ersetzt → 0 Abweichungen. Erster Lauf fand „Squirrel.exe“ und „Trendo OS_ExecutionStub.exe“ als fehlend – die legt Velopack bewusst nicht in den Programmordner; seitdem ausgenommen. |
| `core-tests-rohlog.txt` | Rohlog der 9 neuen Core-Tests (`T0763SystemAlltagTests`) mit `TRENDO_BELEG_DIR` |
| `app-tests-lauf1-rohlog.txt` | 62 App-Testklassen, die die geänderten Seiten berühren (323 Tests, 1 Fehler: T-0736 erwartete „Leistung messen“ als Startseite von SYS-13 – angepasst, siehe Bericht) |
| `app-tests-lauf2-rohlog.txt` | Wiederholung der betroffenen Klassen nach der Anpassung (59 Tests, 0 Fehler) |
| `bilder/sys13.png` | „Alles prüfen“ mit Ergebnis (Render, eigener Datenordner) |
| `bilder/heal.png` | „Einrichten & Reparieren“ mit den neuen Karten |
| `bilder/modus.png` | Betriebsmodus mit Angebotsaussage |
