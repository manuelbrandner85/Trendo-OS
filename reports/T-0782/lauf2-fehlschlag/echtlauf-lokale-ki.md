# T-0782 ECHT: lokale KI schreibt ins offene Dokument (Nachhilfe mit KI, Programmieren lernen)

Datum: 2026-11-26 15:55 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## Nachhilfe mit KI (nachhilfe-programm)

- Auftrag: Erzeuge mir eine neue Übung zum Brüche kürzen mit dem Bruch 12/18, mit Lösungsweg und einem Hinweis, der die Lösung nicht verrät.
- Phase nach 620 s: fehler · Quelle:    verstanden über: KI (Ollama, mistral:7b)
- KI-Antwort (roh, gekürzt): {"steps":[{"action":"dokument.setzen","args":{"pfad":"themen","wert":{"m-kuerzen":{"einfach":{"12/18":{"frage":"Was ist 12/18?","antwort":"12/18 ist eine Kugel von 12 cm in die Höhe von 18 cm, also 12/18 ist 1/1, 1, 2, 1, 3, 1, 4, 1, 5, 1, 6, 1, 7, 1, 8, 1, 9, 1, 10, 1, 11, 1, 12."},"kuerzen":{"frage":"Mit welchem Bruch kann man 12/18 kürzen?","antwort":"Mit 12/18 kann man 12/18 nicht kürzen, da es eine ganze Zahl ist."}}}}]}
  - Protokoll: ki.model.ausweichen quelle=Ollama modell=qwen3:14b grund=TimeoutException
  - Protokoll: ki.model.fallback qwen3:14b: keine Antwort nach 90 s
  - Protokoll: ki.model.ausweichen quelle=Ollama modell=qwen2.5:7b grund=TimeoutException
  - Protokoll: ki.model.fallback qwen2.5:7b: keine Antwort nach 90 s
  - Protokoll: ki.ask quelle=Ollama modell=qwen2.5:0.5b frage=129Z antwort=510Z wissen=0
  - Protokoll: assist.ki.nachfrage nachhilfe-programm
  - Protokoll: ki.ask quelle=Ollama modell=mistral:7b frage=156Z antwort=429Z wissen=0
  - Protokoll: assist.plan nachhilfe-programm schritte=1 fragen=0 quelle=KI (Ollama, mistral:7b) einstieg=Auftragszeile
