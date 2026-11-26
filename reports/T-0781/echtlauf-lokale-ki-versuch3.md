# T-0781 ECHT: Textaufgabe → Rechnung im offenen Dokument (Mathe-Helfer, Naturwissenschaft)

Datum: 2026-11-26 16:07 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Gerechnet hat jeweils der eigene Kern.

## Mathe-Helfer (mathe-helfer-programm)

- Auftrag: Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm². Wie breit ist das Rechteck?
- Phase: vorschau – So sieht es aus. Behalten? Gerechnet hat der Rechenkern, nicht die KI – „Ja, behalten“ übernimmt die Rechnung ins Rechenblatt (ein Rückgängig-Schritt).
- Plan (verstanden über: KI (Ollama, mistral:7b), 726 s): Inhalt im Dokument ändern – Stelle: title, Neuer Inhalt: Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm² → Textaufgabe als Rechnung eintragen – Aufgabenart: gleichung, Rechnung (nur die Aufgabe, ohne Ergebnis): x*(x+3) = 40
- KI-Antwort (roh): `{"steps":[{"action":"dokument.setzen","args":{"pfad":"title","wert":"Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm²"}},{"action":"mathe.rechnung","args":{"art":"gleichung","eingabe":"x*(x+3) = 40"}}]}`
  - Protokoll: ki.model.ausweichen quelle=Ollama modell=qwen3:14b grund=TimeoutException
  - Protokoll: ki.model.fallback qwen3:14b: keine Antwort nach 90 s
  - Protokoll: ki.model.ausweichen quelle=Ollama modell=qwen2.5:7b grund=TimeoutException
  - Protokoll: ki.model.fallback qwen2.5:7b: keine Antwort nach 90 s
  - Protokoll: ki.ask quelle=Ollama modell=qwen2.5:0.5b frage=98Z antwort=180Z wissen=0
  - Protokoll: assist.ki.nachfrage mathe-helfer-programm
  - Protokoll: ki.ask quelle=Ollama modell=mistral:7b frage=156Z antwort=223Z wissen=0
  - Protokoll: assist.plan mathe-helfer-programm schritte=2 fragen=0 quelle=KI (Ollama, mistral:7b) einstieg=Auftragszeile
- Nach „Ja, behalten“: Rechnung „x*(x+3) = 40“ (gleichung) → Kern: x₁ = −8;  x₂ = 5; Probe: ✓ x = −8 einsetzen: links 40, rechts 40 – stimmt (exakt). | ✓ x = 5 einsetzen: links 40, rechts 40 – stimmt (exakt).
- Rückgängig-Liste: 0 → 1, oben „KI: Ein Rechteck ist 3 cm länger als breit und hat e…“
- Rückgängig: Dokument wie vorher = True

## Naturwissenschaft (naturwissenschaft-programm)

- Auftrag: Wie groß ist die molare Masse von Traubenzucker (Glucose, Summenformel C6H12O6)?
- Phase: vorschau – So sieht es aus. Behalten?
- Plan (verstanden über: KI (Ollama, mistral:7b), 79 s): Ins Laborheft
- KI-Antwort (roh): `{"steps":[{"action":"nw-rechnen","args":{"art":"molmasse","eingabe":"C6H12O6"}}]}`
  - Protokoll: ki.ask quelle=Ollama modell=mistral:7b frage=80Z antwort=81Z wissen=0
  - Protokoll: assist.plan naturwissenschaft-programm schritte=1 fragen=0 quelle=KI (Ollama, mistral:7b) einstieg=Auftragszeile
- Nach „Ja, behalten“: Eintrag {"art":"element","eingabe":"Fe","formel":"","gesucht":"","ziel":"","quelle":""} → Programm: Eisen: 55,845 g/mol; Probe: ✓ Molmasse des Elements aus der Formel „Fe“ = Atommasse der Tabelle.

FEHLER: Assert.Equal() Failure: Strings differ
           ↓ (pos 0)
Expected: "M = 180,156 g/mol"
Actual:   "Eisen: 55,845 g/mol"
           ↑ (pos 0)
