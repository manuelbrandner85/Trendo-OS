# T-0779 ECHT: KI schreibt ins offene Dokument (Quiz erstellen, Kurse erstellen)

Datum: 2026-11-26 17:36 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Text für die KI, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Quiz erstellen (quiz-programm)

- Text für die KI: 361 Zeichen (Photosynthese)
- Auftrag: Mach aus dem Text 4 Quizfragen: zwei Auswahlfragen, einen Lückentext und eine Wahr/Falsch-Aussage.
- Plan (79 s, Phase vorschau): Neue Fragen mit Inhalt ins Quiz schreiben (fragen = alle Fragen im Fragen-Format) – Fragen im Fragen-Format: ? Wo findet die Photosynthese statt?
+ in den Chloroplasten der Pflanzenzellen
- in den Mitochondrien der Pflanzenzellen
- in den Zellkernen der Pflanzenzellen

? Welches Gas entsteht als Nebenprodukt der Photosynthese?
+ Sauerstoff
- Kohlenstoffdioxid
- Wasserstoff

L: Pflanzen nutzen Lichtenergie, um aus [Kohlenstoffdioxid] und [Wasser] [Traubenzucker] herzustellen.

W: Nachts findet keine Photosynthese statt, weil das Licht fehlt.
+ Wahr
- KI-Antwort (roh): `{"steps":[{"action":"quiz.fragen","args":{"fragen":"? Wo findet die Photosynthese statt?\n+ in den Chloroplasten der Pflanzenzellen\n- in den Mitochondrien der Pflanzenzellen\n- in den Zellkernen der Pflanzenzellen\n\n? Welches Gas entsteht als Nebenprodukt der Photosynthese?\n+ Sauerstoff\n- Kohlenstoffdioxid\n- Wasserstoff\n\nL: Pflanzen nutzen Lichtenergie, um aus [Kohlenstoffdioxid] und [Wasser] [Traubenzucker] herzustellen.\n\nW: Nachts findet keine Photosynthese statt, weil das Licht fehlt.\n+ Wahr"}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=98Z antwort=524Z wissen=0
  - Protokoll: assist.plan quiz-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: 4 Fragen im Quiz (Auswahl, Auswahl, Lückentext, Wahr oder falsch), Prüfung: 0 Fehler
  - Auswahl: Wo findet die Photosynthese statt? → Lösung: in den Chloroplasten der Pflanzenzellen
  - Auswahl: Welches Gas entsteht als Nebenprodukt der Photosynthese? → Lösung: Sauerstoff
  - Lückentext: Pflanzen nutzen Lichtenergie, um aus [Kohlenstoffdioxid] und [Wasser] [Traubenzucker] herzustellen. → Lösung: Kohlenstoffdioxid, Wasser, Traubenzucker
  - Wahr oder falsch: Nachts findet keine Photosynthese statt, weil das Licht fehlt. → Lösung: Wahr
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Mach aus dem Text 4 Quizfragen: zwei Auswahlfrag…“
- HTML-Quiz: quiz-aus-ki.html, SHA-256 736e55af1b5eb247f8244000e4b195fc7b0e90a283774877aab041c2403299d8
- Dokument: quiz-aus-ki.tdoc, SHA-256 d8bbda372301137888b60b67195f1ce81b400cb65179c1bd7d3eda66974e87a9
- Rückgängig: Quiz wieder leer = True

## Kurse erstellen (kurse-programm)

- Auftrag: Schreib eine neue Lektion mit dem Titel „Blickkontakt“: drei kurze Absätze, eine Liste mit drei Tipps und ein Merksatz.
- Plan (159 s, Phase vorschau): Lektion schreiben – Titel der Lektion: Blickkontakt, Lerntext: Ein guter Blickkontakt ist wichtig, um die Zuhörer zu begeistern und zu beflügeln.
Dabei ist es entscheidend, den Kontakt zu den Zuhörergruppen aufrechtzuerhalten.
Vermeide, dass deine Augen in der Masse verloren gehen.
- Verwende den Blickkontakt als Werkzeug, um mit den Zuhörern Kontakt aufzunehmen.
- Suche in der Gruppe nach Personen, mit denen du intensiver Kontakt aufbauen kannst.
- Nutze den Blickkontakt, um bestimmte Zuhörer zu befreunden und sie in den Vordergrund zu stellen.
! Verwende den Blickkontakt, um die Zuhörer zu beflügeln und zu begeistern.
- KI-Antwort (roh): `{"steps":[{"action":"kurs.lektion","args":{"titel":"Blickkontakt","inhalt":"Ein guter Blickkontakt ist wichtig, um die Zuhörer zu begeistern und zu beflügeln.\nDabei ist es entscheidend, den Kontakt zu den Zuhörergruppen aufrechtzuerhalten.\nVermeide, dass deine Augen in der Masse verloren gehen.\n- Verwende den Blickkontakt als Werkzeug, um mit den Zuhörern Kontakt aufzunehmen.\n- Suche in der Gruppe nach Personen, mit denen du intensiver Kontakt aufbauen kannst.\n- Nutze den Blickkontakt, um bestimmte Zuhörer zu befreunden und sie in den Vordergrund zu stellen.\n! Verwende den Blickkontakt, um die Zuhörer zu beflügeln und zu begeistern."}}]}`
  - Protokoll: assist.apply quiz-programm „Mach aus dem Text 4 Quizfragen: zwei Auswahlfrag…“
  - Protokoll: ki.model.ausweichen quelle=Ollama modell=qwen3:14b grund=InvalidOperationException
  - Protokoll: ki.model.fallback qwen3:14b: prediction aborted, token repeat limit reached
  - Protokoll: ki.ask quelle=Ollama modell=qwen2.5:7b frage=119Z antwort=651Z wissen=0
  - Protokoll: assist.plan kurse-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen2.5:7b) einstieg=Auftragszeile
- Nach „Ja, behalten“: neue Lektion „Blickkontakt“ mit 3 Bausteinen (Text, Liste, Hinweis)
  - Text: Ein guter Blickkontakt ist wichtig, um die Zuhörer zu begeistern und zu beflügeln. Dabei ist es entscheidend, den Kontakt zu den Zuhörergruppen aufrechtzuerhalten. Vermeide, dass deine Augen in der Masse verloren gehen.
  - Liste: Verwende den Blickkontakt als Werkzeug, um mit den Zuhörern Kontakt aufzunehmen. / Suche in der Gruppe nach Personen, mit denen du intensiver Kontakt aufbauen kannst. / Nutze den Blickkontakt, um bestimmte Zuhörer zu befreunden und sie in den Vordergrund zu stellen.
  - Hinweis: Verwende den Blickkontakt, um die Zuhörer zu beflügeln und zu begeistern.
- Kurs-Webseite: kurs-aus-ki.html, SHA-256 e856ab9631ffb62ae74f091589faccb8a67d3ab15c5fccdf1305de3a2dc83ee4
- Rückgängig: Kurs wieder wie vorher = True

Ergebnis: beide Programme bestanden.
