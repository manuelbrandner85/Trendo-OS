# Podcast aufnehmen – Testplan

Studio: Podcast aufnehmen (STUDIO-PODCAST-01) · Bereich: Kreativ & Medien · Seite: `podcast-programm` · Stand: 0.9.113 (Stand 1978f1a)

11 Tests sprechen dieses Programm an.

- Podcast: Kapitel erscheinen wie im Player, Fehler werden genannt, Shownotes und Kapiteldatei, Feed nur mit Aufnahme und https
- T-0681 Podcast: fehlt FFmpeg, nennt die Meldung den Einrichten-Weg – und „Lautheit angleichen“ geht danach weiter (Merker nicht hängen geblieben)
- T-0697 Comic: Bildfeld aufziehen, im Feld zeichnen (Strich bleibt im Feld), Sprechblase setzen, Dorn richten, Feld verformen, Entf, Rückgängig – drei Seiten als PDF, PNG und CBZ
- T-0697 Comic: KI schreibt die Geschichte in die Bildfelder des offenen Hefts – ein Schritt, Rückgängig holt den alten Stand
- T-0697 Comic: altes Dokument (feste Aufteilung, Texte je Bild) wird gelesen und als freie Blasen gezeigt
- T-0697 Podcast echt mit FFmpeg: zwei Spuren aufnehmen (Mikrofon durch Test-WAV ersetzt), Stelle an der Welle löschen (Kapitel rückt), Pausen kürzen, fertig machen → ffprobe: Dauer, −16 LUFS ±1, Kapitel, Tags
- T-0697 Podcast: ohne Mikrofon-Ersatz im Testmodus wird nichts vorgetäuscht – ehrlicher Hinweis, keine Spur
- T-0697 Storyboard: Einstellung beschreiben, Bildfelder werden Einstellungen in „Filme machen“
- T-0704 Comic in „Einfach“: Pinsel, Farbe, Strichstärke und Ebenen über die Zeichenleiste über der Seite – ohne die Eigenschaften aufzuklappen; der Strich hat die gewählte Farbe
- T-0730 Podcast-Beleg: Pfade mit Testordner, Benutzerordner und Benutzername werden vor dem Serialisieren ersetzt – auch JSON-maskiert
- V-12: Podcast – Kapitel in „Einfach“ als Liste mit Feldern und „+ Kapitel hinzufügen“, ohne Textformat; Profi behält das Textfeld
