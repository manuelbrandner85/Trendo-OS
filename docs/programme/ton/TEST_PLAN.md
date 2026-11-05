# Audio bearbeiten – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `ton` · Stand: 0.9.36 (Stand 6bd5d2b)

10 Tests sprechen dieses Programm an.

- Audio-Editor Reparieren: ohne Profil freundlicher Hinweis, Rauschprofil merken, Rauschen entfernen, Brummen, Stille kürzen, Lautheit −16 LUFS, Spektrogramm, Aufnahme an der Marke
- Audio-Editor UI: WAV öffnen, Bereich mit der Maus ziehen, Löschen, Normalisieren, Einblenden, Kopieren/Einfügen, Abspielen, Rückgängig, Export WAV
- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- Einfach zuerst: neue Nutzer öffnen Programme in der Stufe „Einfach“ (weniger Werkzeuge, alle Funktionen über „Erweitert“/„Professionell“ erreichbar)
- Einfachheit: jede Seite höchstens drei Reiterleisten auf einmal, Aufklapper und Scrollbereiche höchstens zwei Ebenen tief; Bericht der verschachteltsten Seiten
- Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente
- Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt
- X17 Versionen: nach dem zweiten Speichern zeigt die Datei-Gruppe „Versionen (1)“, Wiederherstellen holt den alten Stand (rückgängig machbar)
