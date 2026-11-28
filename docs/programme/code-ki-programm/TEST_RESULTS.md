# Code mit KI – Testergebnisse

Studio: Code mit KI (STUDIO-CODEAI-01) · Bereich: KI & Automatik · Seite: `code-ki-programm` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 9 · Fehlgeschlagen 0 · Übersprungen 2 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Code mit KI: Ordner öffnen, Datei bearbeiten und speichern, Schnappschuss und Zeilen-Diff, Tests erst nach zweitem Klick, Exporte | ✓ bestanden |
| T-0688 Code mit KI: ohne Claude-Anmeldung klare Meldung, keine Datei geändert, keine Sicherung offen | ✓ bestanden |
| T-0688 Code mit KI: „Mit Claude ändern“ – Claude (ersetzt) läuft im Projektordner und ändert Dateien, Diff je Datei, eine Datei zurücknehmen, Rest übernehmen | ✓ bestanden |
| T-0723 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ergänzt im Python-Beispielprojekt eine Funktion mit Test → Diff → Ja → echtes unittest → Rückgängig (nur mit TRENDO_CODEKI_ECHT) | übersprungen |
| T-0723: Auftrag → Plan → Diff je Datei → Ja → ein Rückgängig-Schritt → echtes Python rot → „zurücknehmen?“ → alter Stand, Wiederholen (KI ersetzt) | ✓ bestanden |
| T-0723: Nein zu einer Datei – nur Angenommenes wird geschrieben, Tests grün; Rückgängig der Hülle stellt alles her (KI ersetzt, Python echt) | ✓ bestanden |
| T-0723: Projektgrenzen, Not-Stopp und keine Freigabe – nichts außerhalb des Projekts, KI nicht gefragt, kein Testlauf ohne Ja (KI ersetzt) | ✓ bestanden |
| T-0728 C2: „Los“ in der Auftragszeile von „Code mit KI“ geht über „Mit KI ändern“ – Plan mit Dateiblöcken, Vorschau als Unterschied, nichts geändert vor dem Ja (KI ersetzt) | ✓ bestanden |
| T-0764 M1: Kind – „Code mit KI“ mit eingetipptem Autostart-Ordner legt keine start.bat an; Erwachsene wie bisher | ✓ bestanden |
| T-0764 M2: Kind – „Viele Dateien umbenennen“: spiel → spiel.cmd nicht (auch nicht in der Vorschau), nichts außerhalb der Freigabe; Muster ohne neue Endung geht; Erwachsene wie bisher | ✓ bestanden |
| T-0775 ECHT Code mit KI: „Mit Claude ändern“ ändert im Projektordner eine Python-Datei, Diff, „Zurücknehmen“ stellt sie her (nur mit TRENDO_ECHT_CLAUDE) | übersprungen |
