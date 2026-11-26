# T-0774 ECHT: KI schreibt ins offene Dokument (PDF-Werkstatt, Dokumente scannen)

Datum: 2026-11-26 11:27 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, „Dateien/Fotos hinzufügen …“, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## PDF-Werkstatt (pdf-werkstatt-programm)

- Eingang: vertrag.pdf, 5 Seiten, SHA-256 c5cadc63e5d3480c61cbfc83de5f30feb6ff343cea6385f87c680f2b78a3bbe4
- Auftrag: Zieh aus der PDF die Seiten 1 bis 3 heraus und mach daraus eine neue PDF.
- Plan (verstanden über: KI (Ollama, qwen3:14b), 13 s): Seiten als neue PDF herausziehen – Seiten: 1-3
- KI-Antwort (roh): `{"steps":[{"action":"pdf.seiten_heraus","args":{"seiten":"1-3"}}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=73Z antwort=77Z wissen=0
  - Protokoll: assist.plan pdf-werkstatt-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: neue PDF vertrag-teil1.pdf, 3 Seiten (PDFsharp), Text Seite 1 „Seite eins“, Seite 3 „Seite drei“ (PDFium)
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Zieh aus der PDF die Seiten 1 bis 3 heraus und m…“; Original unverändert = True
- Datei pdf-werkstatt-seiten-1-3.pdf: 34658 Bytes, SHA-256 003e0390a3e9f615cfe050ac03834c765805255f57611c046b0092dbb75bc8c4
- Datei pdf-werkstatt-eingang.pdf: 35455 Bytes, SHA-256 c5cadc63e5d3480c61cbfc83de5f30feb6ff343cea6385f87c680f2b78a3bbe4
- Rückgängig: Dokument wieder wie vorher = True, neue PDF beiseitegelegt = True

## Dokumente scannen (scannen-programm)

- Eingang: 2 Fotos (schräges Blatt auf dunklem Tisch, 600×450), SHA-256 847d08689389011cf08226d1c785b618010ed35d652f72c54e61ccdd7f1d25a2 / 847d08689389011cf08226d1c785b618010ed35d652f72c54e61ccdd7f1d25a2
- Auftrag: Mach aus den beiden Fotos eine PDF mit dem Titel „Rechnung Stadtwerke“ im Modus Dokument.
- Plan (verstanden über: KI (Ollama, qwen3:14b), 13 s): Inhalt im Dokument ändern – Stelle: title, Neuer Inhalt: Rechnung Stadtwerke → Fotos als PDF ablegen – Titel: Rechnung Stadtwerke, Modus: dokument
- KI-Antwort (roh): `{"steps":[{"action":"dokument.setzen","args":{"pfad":"title","wert":"Rechnung Stadtwerke"}},{"action":"scan.pdf","args":{"titel":"Rechnung Stadtwerke","modus":"dokument"}}],"frage":""}`
  - Protokoll: assist.apply pdf-werkstatt-programm „Zieh aus der PDF die Seiten 1 bis 3 heraus und m…“
  - Protokoll: assist.datei.zurueck vertrag-teil1.pdf
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=89Z antwort=184Z wissen=0
  - Protokoll: assist.plan scannen-programm schritte=2 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: 2026-11-26 Rechnung Stadtwerke.pdf im Ordner Scans/Rechnungen, 2 Seiten; Titel im Dokument „Rechnung Stadtwerke“
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Mach aus den beiden Fotos eine PDF mit dem Titel…“
- Datei scannen-fotos-zu-pdf.pdf: 5155 Bytes, SHA-256 023277f379fe68f5b29927d59461ae00c5e84d254493e975e31370ae073de80d
- Rückgängig: Dokument wieder wie vorher = True, PDF beiseitegelegt = True

Ergebnis: beide Programme bestanden.
