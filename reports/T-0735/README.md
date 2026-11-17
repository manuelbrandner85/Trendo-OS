# T-0735 – Beleg echter Abrufe (17.11.2026)

Alle Abrufe liefen über den echten Lese-Weg der App (`ReadOnlyWeb.GetAsync`: nur öffentliche Adressen, keine Formulare, höchstens 2 MB),
ausgelöst von den App-Tests `RechercheQuellen_Ui.Cite_Echt`, `Sys08_Echt`, `X07_Echt` mit `TRENDO_BELEG_DIR` auf diesen Ordner.
Rohausgabe des Testlaufs: `testlauf-echt.log`. Prüfsummen: `SHA256SUMS`.

| Bereich | Was wirklich geholt wurde | Dateien |
|---|---|---|
| STUDIO-CITE-01 | DOI 10.1038/nature14539 bei `api.crossref.org` (40 565 Bytes JSON), daraus die Quelle mit Herkunft; RIS und CSL-JSON daraus ausgegeben | `crossref-10.1038_nature14539.json`, `cite-echt.log`, `cite-echt.ris`, `cite-echt.csl.json` |
| SYS-08 | Suche „Phishing“ bei der Wikipedia-Schnittstelle (20 Treffer), ersten Treffer im Lesemodus gelesen; zusätzlich DuckDuckGo-HTML (10 Treffer) | `wikipedia-suche-phishing.json`, `sys08-echt.log` |
| X-07 | KI-Auftrag „Quelle holen“: Plan ohne Netz, nach Freigabe (ApprovalGate) HDRI `abandoned_hall_01` (1k, 1 636 687 Bytes, MD5 laut Katalog geprüft) von Poly Haven ins Projekt; Beleg je Datei | `x07-echt.log`, `x07-HERKUNFT.txt`, `x07-herkunft.json`, `x07-…hdr.kopf.txt` (erste 64 Bytes, Radiance-Kopf) |

Nicht im Repo: die HDR-Datei selbst (1,6 MB, CC0) – ihre SHA-256 steht in `x07-echt.log` und `x07-HERKUNFT.txt`.

Fremdprogramm-Prüfung RIS/CSL-JSON: **BLOCKIERT** – auf diesem PC ist weder Zotero, Citavi, EndNote noch Pandoc installiert (geprüft 17.11.2026, nichts installiert).
Ersatzweise nur Lesen mit Python `json` bzw. Zeilenprüfung: `fremdpruefung-python.log` – das ist kein Nachweis, dass ein Literaturprogramm die Dateien annimmt.

Hinweis Prüfsummen: `SHA256SUMS` gilt für die Bytes, wie sie im Repo liegen. Der Ordner steht in `.gitattributes` mit `-text`, git ändert die Zeilenenden also nicht. RIS (`cite-echt.ris`), CSL-JSON (`cite-echt.csl.json`), `x07-herkunft.json` und `fremdpruefung-python.log` haben CRLF-Zeilenenden, wie sie erzeugt wurden (RIS verlangt CRLF); die übrigen Dateien LF. Prüfen nach einem frischen Klon: `sha256sum -c SHA256SUMS` in diesem Ordner.
