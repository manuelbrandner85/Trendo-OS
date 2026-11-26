# T-0774 ECHT: KI schreibt ins offene Dokument (PDF-Werkstatt, Dokumente scannen)

Datum: 2026-11-26 10:59 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche (Vorlage, „Dateien/Fotos hinzufügen …“, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt.

## PDF-Werkstatt (pdf-werkstatt-programm)

- Eingang: vertrag.pdf, 5 Seiten, SHA-256 1fca4d1e3ed3d95d0e8c3c8f786a913fe3d1858bb45a1e4179d74ef6690d5c6c
- Auftrag: Zieh aus der PDF die Seiten 1 bis 3 heraus und mach daraus eine neue PDF.
- Plan (verstanden über: KI (Ollama, qwen3:14b), 16 s): Seiten als neue PDF herausziehen – Seiten: 1-3
- KI-Antwort (roh): `{"steps":[{"action":"pdf.seiten_heraus","args":{"seiten":"1-3"}],"frage":""}`
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=73Z antwort=76Z wissen=0
  - Protokoll: assist.plan pdf-werkstatt-programm schritte=1 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: neue PDF vertrag-teil1.pdf, 3 Seiten (PDFsharp), Text Seite 1 „Seite eins“, Seite 3 „Seite drei“ (PDFium)
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Zieh aus der PDF die Seiten 1 bis 3 heraus und m…“; Original unverändert = True
- Datei pdf-werkstatt-seiten-1-3.pdf: 34658 Bytes, SHA-256 f19b25cb25c9ebedfc87e5fbad17437fd9d7317dc3732cf8f93159b30cec0dfd
- Datei pdf-werkstatt-eingang.pdf: 35455 Bytes, SHA-256 1fca4d1e3ed3d95d0e8c3c8f786a913fe3d1858bb45a1e4179d74ef6690d5c6c
- Rückgängig: Dokument wieder wie vorher = True, neue PDF beiseitegelegt = True

## Dokumente scannen (scannen-programm)

- Eingang: 2 Fotos (schräges Blatt auf dunklem Tisch, 600×450), SHA-256 847d08689389011cf08226d1c785b618010ed35d652f72c54e61ccdd7f1d25a2 / 847d08689389011cf08226d1c785b618010ed35d652f72c54e61ccdd7f1d25a2
- Auftrag: Mach aus den beiden Fotos eine PDF mit dem Titel „Rechnung Stadtwerke“ im Modus Dokument.
- Plan (verstanden über: KI (Ollama, qwen3:14b), 8 s): Inhalt im Dokument ändern – Stelle: title, Neuer Inhalt: Rechnung Stadtwerke → Fotos als PDF ablegen – Titel: Rechnung Stadtwerke, Modus: dokument
- KI-Antwort (roh): `{"steps":[{"action":"dokument.setzen","args":{"pfad":"title","wert":"Rechnung Stadtwerke"}},{"action":"scan.pdf","args":{"titel":"Rechnung Stadtwerke","modus":"dokument","ordner":""}}],"frage":""}`
  - Protokoll: assist.apply pdf-werkstatt-programm „Zieh aus der PDF die Seiten 1 bis 3 heraus und m…“
  - Protokoll: assist.datei.zurueck vertrag-teil1.pdf
  - Protokoll: ki.ask quelle=Ollama modell=qwen3:14b frage=89Z antwort=196Z wissen=0
  - Protokoll: assist.plan scannen-programm schritte=2 fragen=0 quelle=KI (Ollama, qwen3:14b) einstieg=Auftragszeile
- Nach „Ja, behalten“: 2026-11-26 Rechnung Stadtwerke.pdf im Ordner Scans/Rechnungen, 2 Seiten; Titel im Dokument „Rechnung Stadtwerke“
- Rückgängig-Liste: 1 → 2 Schritte, oben „KI: Mach aus den beiden Fotos eine PDF mit dem Titel…“
- Datei scannen-fotos-zu-pdf.pdf: 5155 Bytes, SHA-256 ffce58f0e62962d0266e01dc00ff34c9dccd22d19cd1f9cb156937ecf72a27b6
- Rückgängig: Dokument wieder wie vorher = True, PDF beiseitegelegt = True

Ergebnis: beide Programme bestanden.
