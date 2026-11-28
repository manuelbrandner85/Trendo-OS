# Konsolenspiele portieren – Testplan

Studio: Konsolenspiele portieren (STUDIO-CONSOLE-01) · Bereich: 3D & Spiele · Seite: `konsole-programm` · Stand: 0.9.113 (Stand 1978f1a)

12 Tests sprechen dieses Programm an.

- Bibliothek: Freies Spiel wird geholt, danach Spielen, Ändern und Export; Godot wird mit dem Projekt gestartet
- Bibliothek: Godot wählen prüft die Datei, merkt sich den Ort und startet danach damit
- Bibliothek: Homebrew mit falscher Prüfsumme startet nicht und meldet den Grund
- Bibliothek: Jeder Eintrag hat ein Cover in den Assets
- Bibliothek: Klick auf ein eigenes Spiel zeigt Beschreibung und schickt den Bau-Auftrag mit Nachbau-Verbot an Claude
- Bibliothek: Ohne Godot erklärt Spielen, was fehlt, und startet nichts
- Bibliothek: Startet mit allen Karten, der Filter zeigt nur die gewählte Art, die Disc-Schritte bleiben erreichbar
- Bibliothek: Wunsch wird geprüft und als eigenes Spiel beauftragt; leerer Wunsch bleibt in der Bibliothek
- Konsole T-0752: Freies Spiel – Klick auf Lizenz, Programm, Ordner und „Portieren starten“ bis zur Ergebnisdatei, dann Probelauf
- Konsole T-0752: Freies Spiel – ohne PS2Recomp und CMake BLOCKIERT vor dem ersten Schritt, Start gesperrt, nichts wird aufgerufen
- Konsole: Einrichten holt die feste Fassung, legt die Skills ins Projekt und meldet Fehler verständlich
- Konsole: Ohne Besitz-Bestätigung bleiben die Schritte gesperrt; danach geht der richtige Auftrag an Claude
