# T-0782 ECHT: lokale KI schreibt ins offene Dokument (Nachhilfe mit KI, Programmieren lernen)

Datum: 2026-11-26 17:28 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Nachhilfe mit KI (nachhilfe-programm)

- Auftrag: Erzeuge mir eine neue Übung zum Brüche kürzen mit dem Bruch 12/18, mit Lösungsweg und einem Hinweis, der die Lösung nicht verrät.
- Phase nach 319 s: vorschau · Quelle:    verstanden über: KI (Ollama, qwen3:14b)
- KI-Antwort (roh, gekürzt): {"steps":[{"action":"uebung.erzeugen","args":{"frage":"Kürze den Bruch 12/18 so weit wie möglich.","antwort":"2/3","erklaerung":"Um den Bruch zu kürzen, suche den größten gemeinsamen Teiler (ggT) von Zähler und Nenner. Der ggT von 12 und 18 ist 6. Teile Zähler und Nenner durch 6: 12 ÷ 6 = 2 und 18 ÷ 6 = 3. Der gekürzte Bruch lautet 2/3.","hinweis":"Überlege, welche Zahlen beide Zähler und Nenner teilen können. Probier mit kleinen Zahlen wie 2, 3 oder 6 an."}}],"frage":""}
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=129Z antwort=476Z wissen=0
  - Protokoll: assist.plan nachhilfe-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Übernommen: Frage „Kürze den Bruch 12/18 so weit wie möglich.“ · Antwort(en) 2/3 · Hinweise 1 · Art bruch · Herkunft ki
- Rückgängig-Schritte vorher/nachher: 1 → 2 („KI: Erzeuge mir eine neue Übung zum Brüche kürzen mi…“)
- Antwort „2/3“ geprüft: richtig (Richtig!)
- Nach „Rückgängig“ (Assistent): eigene Übungen im Thema = 0

## Programmieren lernen (programmieren-lernen-programm)

- Code der lernenden Person: print statt return · echter Testlauf im AppContainer: fail, 0/3 bestanden
- Auftrag: Meine Tests für die Begrüßung schlagen fehl. Gib mir einen Hinweis, was ich ändern muss, ohne mir die Lösung zu verraten.
- Phase nach 80 s: vorschau · Quelle:    verstanden über: KI (Ollama, qwen3:14b)
- KI-Antwort (roh, gekürzt): {"steps":[{"action":"hinweis.geben","args":{"hinweis":"Die Funktion sollte den Gruß als Wert zurückgeben, nicht mit print() ausgeben. Überlege, welcher Befehl den Wert anstelle von print() verwendet."}}],"frage":""}
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=121Z antwort=215Z wissen=0
  - Protokoll: assist.plan programmieren-lernen-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Übernommen: Hinweis „Die Funktion sollte den Gruß als Wert zurückgeben, nicht mit print() ausgeben. Überlege, welcher Befehl den Wert anstelle von print() verwendet.“ · Rückgängig-Schritte 2 → 3
- Bewertung unverändert (die KI bewertet nicht): gelöst = False, Versuche = 1
- Nach „Rückgängig“: KI-Hinweise = 0

Ergebnis: bestanden
