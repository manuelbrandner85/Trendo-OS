# Code mit KI – Testplan

Studio: Code mit KI (STUDIO-CODEAI-01) · Bereich: KI & Automatik · Seite: `code-ki-programm` · Stand: 0.9.112 (Stand d6f9fcd)

11 Tests sprechen dieses Programm an.

- Code mit KI: Ordner öffnen, Datei bearbeiten und speichern, Schnappschuss und Zeilen-Diff, Tests erst nach zweitem Klick, Exporte
- T-0688 Code mit KI: ohne Claude-Anmeldung klare Meldung, keine Datei geändert, keine Sicherung offen
- T-0688 Code mit KI: „Mit Claude ändern“ – Claude (ersetzt) läuft im Projektordner und ändert Dateien, Diff je Datei, eine Datei zurücknehmen, Rest übernehmen
- T-0723 ECHT: lokale KI (Ollama, bestes Modell nach T-0703) ergänzt im Python-Beispielprojekt eine Funktion mit Test → Diff → Ja → echtes unittest → Rückgängig (nur mit TRENDO_CODEKI_ECHT)
- T-0723: Auftrag → Plan → Diff je Datei → Ja → ein Rückgängig-Schritt → echtes Python rot → „zurücknehmen?“ → alter Stand, Wiederholen (KI ersetzt)
- T-0723: Nein zu einer Datei – nur Angenommenes wird geschrieben, Tests grün; Rückgängig der Hülle stellt alles her (KI ersetzt, Python echt)
- T-0723: Projektgrenzen, Not-Stopp und keine Freigabe – nichts außerhalb des Projekts, KI nicht gefragt, kein Testlauf ohne Ja (KI ersetzt)
- T-0728 C2: „Los“ in der Auftragszeile von „Code mit KI“ geht über „Mit KI ändern“ – Plan mit Dateiblöcken, Vorschau als Unterschied, nichts geändert vor dem Ja (KI ersetzt)
- T-0764 M1: Kind – „Code mit KI“ mit eingetipptem Autostart-Ordner legt keine start.bat an; Erwachsene wie bisher
- T-0764 M2: Kind – „Viele Dateien umbenennen“: spiel → spiel.cmd nicht (auch nicht in der Vorschau), nichts außerhalb der Freigabe; Muster ohne neue Endung geht; Erwachsene wie bisher
- T-0775 ECHT Code mit KI: „Mit Claude ändern“ ändert im Projektordner eine Python-Datei, Diff, „Zurücknehmen“ stellt sie her (nur mit TRENDO_ECHT_CLAUDE)
