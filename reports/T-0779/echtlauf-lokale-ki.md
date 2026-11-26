# T-0779 ECHT: KI schreibt ins offene Dokument (Quiz erstellen, Kurse erstellen)

Datum: 2026-11-26 16:06 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Text für die KI, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Quiz erstellen (quiz-programm)

- Text für die KI: 361 Zeichen (Photosynthese)
- Auftrag: Mach aus dem Text 4 Quizfragen: zwei Auswahlfragen, einen Lückentext und eine Wahr/Falsch-Aussage.
- Plan (409 s, Phase vorschau): Neue Fragen mit Inhalt ins Quiz schreiben (fragen = alle Fragen im Fragen-Format) – Fragen im Fragen-Format: ? Wo findet die Photosynthese statt? / + in den Chloroplasten der Pflanzenzellen / - in den Mitochondrien / - in der Zellmembran / - im Zellkern /  / ? Welches …
- KI-Antwort (roh): `{"steps":[{"action":"quiz.fragen","args":{"fragen":"? Wo findet die Photosynthese statt?\n+ in den Chloroplasten der Pflanzenzellen\n- in den Mitochondrien\n- in der Zellmembran\n- im Zellkern\n\n? Welches Nebenprodukt entsteht bei der Photosynthese?\n+ Sauerstoff\n- Kohlenstoffdioxid\n- Wasser\n- Traubenzucker\n\nL: Der grüne Farbstoff [Chlorophyll] fängt das Licht ein.\n\nW: Die Photosynthese findet nachts statt.\n+ Falsch"}}]}]`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=98Z antwort=435Z wissen=0
  - Protokoll: assist.ki.nachfrage quiz-programm
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=156Z antwort=434Z wissen=0
  - Protokoll: assist.plan quiz-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: 4 Fragen im Quiz (Auswahl, Auswahl, Lückentext, Wahr oder falsch), Prüfung: 0 Fehler
  - Auswahl: Wo findet die Photosynthese statt? → Lösung: in den Chloroplasten der Pflanzenzellen
  - Auswahl: Welches Nebenprodukt entsteht bei der Photosynthese? → Lösung: Sauerstoff
  - Lückentext: Der grüne Farbstoff [Chlorophyll] fängt das Licht ein. → Lösung: Chlorophyll
  - Wahr oder falsch: Die Photosynthese findet nachts statt. → Lösung: Falsch
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Mach aus dem Text 4 Quizfragen: zwei Auswahlfrag…“
- HTML-Quiz: quiz-aus-ki.html, SHA-256 cb7508500f1ae75e4593bb0cdf22cbdda4375b2212406d4f3b209049a1703428
- Dokument: quiz-aus-ki.tdoc, SHA-256 5b4e310c8055e062ac4652504cd817cdd2b209bfc9d32bf58b6791d0d1d1623e
- Rückgängig: Quiz wieder leer = True

## Kurse erstellen (kurse-programm)

- Auftrag: Schreib eine neue Lektion mit dem Titel „Blickkontakt“: drei kurze Absätze, eine Liste mit drei Tipps und ein Merksatz.
- Plan (279 s, Phase vorschau): Lektion schreiben – Titel der Lektion: Blickkontakt, Lerntext: Ein guter Vortrag hält die Aufmerksamkeit durch Blickkontakt. Schau nicht starr in die Luft oder auf den Boden – das wirkt unsicher. /  / Verdeute dir, dass du …, Kapitel: Vortragen
- KI-Antwort (roh): `{"steps":[{"action":"kurs.lektion","args":{"titel":"Blickkontakt","inhalt":"Ein guter Vortrag hält die Aufmerksamkeit durch Blickkontakt. Schau nicht starr in die Luft oder auf den Boden – das wirkt unsicher.\n\nVerdeute dir, dass du alle Zuhörer ansprichst. Schau abwechselnd in verschiedene Gesichter, ohne einen einzigen zu lange zu fixieren.\n\nAuch wenn du nervös bist, bleib ruhig. Ein natürlicher Blickkontakt signalisiert Sicherheit und Authentizität.\n\n- Schau abwechselnd in verschiedene Gesichter\n- Vermeide starrer Blick in eine Richtung\n- Halte den Kopf leicht geneigt, nicht zu steif\n\n! Blickkontakt hält die Aufmerksamkeit wach","kapitel":"Vortragen"}}],"frage":""}`
  - Protokoll: assist.apply quiz-programm „Mach aus dem Text 4 Quizfragen: zwei Auswahlfrag…“
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=119Z antwort=685Z wissen=0
  - Protokoll: assist.plan kurse-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: neue Lektion „Blickkontakt“ mit 5 Bausteinen (Text, Text, Text, Liste, Hinweis)
  - Text: Ein guter Vortrag hält die Aufmerksamkeit durch Blickkontakt. Schau nicht starr in die Luft oder auf den Boden – das wirkt unsicher.
  - Text: Verdeute dir, dass du alle Zuhörer ansprichst. Schau abwechselnd in verschiedene Gesichter, ohne einen einzigen zu lange zu fixieren.
  - Text: Auch wenn du nervös bist, bleib ruhig. Ein natürlicher Blickkontakt signalisiert Sicherheit und Authentizität.
  - Liste: Schau abwechselnd in verschiedene Gesichter / Vermeide starrer Blick in eine Richtung / Halte den Kopf leicht geneigt, nicht zu steif
  - Hinweis: Blickkontakt hält die Aufmerksamkeit wach
- Kurs-Webseite: kurs-aus-ki.html, SHA-256 36b41d88d98585caef9d3fa4cbd973d61c4563d3ac90bd80345c01f605b2982b
- Rückgängig: Kurs wieder wie vorher = True

Ergebnis: beide Programme bestanden.
