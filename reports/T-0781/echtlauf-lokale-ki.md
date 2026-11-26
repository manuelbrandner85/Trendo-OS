# T-0781 ECHT: Textaufgabe → Rechnung im offenen Dokument (Mathe-Helfer, Naturwissenschaft)

Datum: 2026-11-26 16:23 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Gerechnet hat jeweils der eigene Kern.

## Mathe-Helfer (mathe-helfer-programm)

- Auftrag: Ein Rechteck ist 3 cm länger als breit und hat eine Fläche von 40 cm². Wie breit ist das Rechteck?
- Phase: vorschau – So sieht es aus. Behalten? Gerechnet hat der Rechenkern, nicht die KI – „Ja, behalten“ übernimmt die Rechnung ins Rechenblatt (ein Rückgängig-Schritt).
- Plan (verstanden über: KI (Ollama, qwen3:14b), 13 s): Textaufgabe als Rechnung eintragen – Aufgabenart: gleichung, Rechnung (nur die Aufgabe, ohne Ergebnis): x*(x+3) = 40
- KI-Antwort (roh): `{"steps":[{"action":"mathe.rechnung","args":{"art":"gleichung","eingabe":"x*(x+3) = 40"}}]}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=98Z antwort=91Z wissen=0
  - Protokoll: assist.plan mathe-helfer-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: Rechnung „x*(x+3) = 40“ (gleichung) → Kern: x₁ = −8;  x₂ = 5; Probe: ✓ x = −8 einsetzen: links 40, rechts 40 – stimmt (exakt). | ✓ x = 5 einsetzen: links 40, rechts 40 – stimmt (exakt).
- Rückgängig-Liste: 0 → 1, oben „KI: Ein Rechteck ist 3 cm länger als breit und hat e…“
- Rückgängig: Dokument wie vorher = True

## Naturwissenschaft (naturwissenschaft-programm)

- Auftrag: Wie groß ist die molare Masse von Traubenzucker (Glucose, Summenformel C6H12O6)?
- Phase: vorschau – So sieht es aus. Behalten? Gerechnet hat das Programm, nicht die KI – „Ja, behalten“ übernimmt den Eintrag ins Laborheft (ein Rückgängig-Schritt).
- Plan (verstanden über: KI (Ollama, qwen3:14b), 12 s): Textaufgabe ins Laborheft rechnen – Art: molmasse, Aufgabe (Formel, Reaktion, Einheiten-Rechnung oder bekannte Werte): C6H12O6
- KI-Antwort (roh): `{"steps":[{"action":"natur.rechnung","args":{"art":"molmasse","eingabe":"C6H12O6"}}],"frage":" "}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=80Z antwort=97Z wissen=0
  - Protokoll: assist.plan naturwissenschaft-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: Eintrag {"art":"molmasse","eingabe":"C6H12O6","formel":"","gesucht":"","ziel":"","quelle":"KI: Textaufgabe"} → Programm: M = 180,156 g/mol; Probe: ✓ Gegenrechnung: Beiträge der Zeilen ergeben zusammen 180,156 g/mol. | ✓ Massenanteile ergeben zusammen 100,00 %.
- Rückgängig: Laborheft wie vorher = True

Ergebnis: beide Programme bestanden.
