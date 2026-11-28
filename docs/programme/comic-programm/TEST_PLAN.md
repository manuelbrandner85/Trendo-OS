# Comics & Storyboards – Testplan

Studio: Comics & Storyboards (STUDIO-COMIC-01) · Bereich: Kreativ & Medien · Seite: `comic-programm` · Stand: 0.9.114 (Stand 99cbb5c)

10 Tests sprechen dieses Programm an.

- Comics: Seite mit Sprechblasen, Raster-Vorschlag ergänzt Bildfelder und behält die Texte, Text bearbeiten, SVG-Export mit eingebettetem Bild
- T-0697 Comic: Bildfeld aufziehen, im Feld zeichnen (Strich bleibt im Feld), Sprechblase setzen, Dorn richten, Feld verformen, Entf, Rückgängig – drei Seiten als PDF, PNG und CBZ
- T-0697 Comic: KI schreibt die Geschichte in die Bildfelder des offenen Hefts – ein Schritt, Rückgängig holt den alten Stand
- T-0697 Comic: altes Dokument (feste Aufteilung, Texte je Bild) wird gelesen und als freie Blasen gezeigt
- T-0697 Podcast echt mit FFmpeg: zwei Spuren aufnehmen (Mikrofon durch Test-WAV ersetzt), Stelle an der Welle löschen (Kapitel rückt), Pausen kürzen, fertig machen → ffprobe: Dauer, −16 LUFS ±1, Kapitel, Tags
- T-0697 Podcast: ohne Mikrofon-Ersatz im Testmodus wird nichts vorgetäuscht – ehrlicher Hinweis, keine Spur
- T-0697 Storyboard: Einstellung beschreiben, Bildfelder werden Einstellungen in „Filme machen“
- T-0704 Comic in „Einfach“: Pinsel, Farbe, Strichstärke und Ebenen über die Zeichenleiste über der Seite – ohne die Eigenschaften aufzuklappen; der Strich hat die gewählte Farbe
- T-0730 Podcast-Beleg: Pfade mit Testordner, Benutzerordner und Benutzername werden vor dem Serialisieren ersetzt – auch JSON-maskiert
- V-02: erster Schritt im leeren Dokument – der große Knopf löst in „Einfach“ die echte Funktion aus, obwohl die rechte Spalte eingeklappt ist (Videoschnitt, Bildbearbeitung, Audio, Punktwolke, Logos, Comics); danach ist er weg
