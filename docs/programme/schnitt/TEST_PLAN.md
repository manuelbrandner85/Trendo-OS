# Videoschnitt – Testplan

Studio: Videos & Fotos bearbeiten (CORE-08) · Bereich: Kreativ & Medien · Seite: `schnitt` · Stand: 0.9.36 (Stand dfd134a)

13 Tests sprechen dieses Programm an.

- CORE-08 (UI): Datei wählen, Ton herausholen – echte Ausgabedatei
- CORE-08: Alle Aufgaben laufen mit echtem FFmpeg, Original bleibt, nichts wird überschrieben
- CORE-08: Fehlende Datei gibt verständliche Meldung, Argumente ohne Shell
- Videoschnitt Export X1/X11/X16: Vorlage Hochformat per Klick, Bereich und Lautheit gehen an den Export, Dateiname mit Vorlage, EDL und FCPXML werden geschrieben
- Videoschnitt UI: Medien auf die Spuren, Stück ziehen ordnet um, Kante ziehen kürzt, Lineal setzt Abspielkopf, S teilt, Strg+Z, Entf, Export, Einfach-Stufe
- Videoschnitt UI: falsche Dateien und leere Bildspur geben klare Meldungen
- Videoschnitt UI: Überblenden und Titel setzen (Länge kürzer, Titel in der Vorschau), Abspielen zeigt Bilder und Titel, Hintergrund-Export mit Fortschritt, Abbrechen
- Videoschnitt X10: Untertitel erzeugen (Whisper ersetzt), Anzahl und Beispiel sichtbar, Einbrennen geht an den Export, SRT liegt neben dem Video
- Videoschnitt X15: verschobene Datei wird gemeldet und per Ordnerwahl neu verknüpft (Rückgängig möglich)
- Videoschnitt X5: L spielt (zweimal = 2×), J rückwärts, K hält an, I/O setzen den Export-Bereich, M setzt eine Marke, Pos1/Ende springen
- Videoschnitt übersichtlich: Spurnamen, Abspielleiste unter der Vorschau, rechts Medien · Auswahl · Export – Auswahl öffnet sich beim Anklicken eines Stücks
- Videoschnitt „Meine Exporte“: fertiges Video bleibt in der Liste, Abspielen startet es, Ordner öffnen markiert die Datei im Explorer, Löschen in den Papierkorb
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
