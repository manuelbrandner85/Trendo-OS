# Runde 12: Kostenlose KI weicht aus – 08.11.2026

Alle Studios, Runde 12: „Neues Projekt starten“ (X-11), „KI fragen“ (CORE-02), „Kostenlose KI finden“ (X-05), „Großprojekte umsetzen“ (X-01).

## Was sich ändert (X-05)
Kostenlose Modelle bei OpenRouter sind oft kurz ausgelastet. Bisher gab Trendo dann die ganze Quelle auf, obwohl andere kostenlose Modelle frei gewesen wären. Jetzt versucht Trendo bei so einem Fehler das nächste kostenlose Modell.

Eigener Code nach der Idee von LiteLLM, kein neues Paket.

## Regeln
- Höchstens drei Modelle je Frage, damit eine Frage das Tageskontingent nicht aufbraucht.
- Gilt nur für OpenRouter, wo Trendo ausschließlich `:free`-Modelle listet. Bezahlte Dienste wechseln nie von selbst das Modell.
- Ausgewichen wird nur bei 429, 502, 503 und 404. Andere Fehler, zum Beispiel ein ungültiger Schlüssel, beenden die Versuche.
- Ist die Grenze des ganzen Kontos erreicht, stoppt Trendo sofort.

## Geprüft
- `Runde12Tests` (Kern): Ausweichen, höchstens drei Versuche, Kontogrenze, kein Wechsel bei anderen Fehlern und bezahlten Diensten.
- `Runde12_Ui`: echte `CloudKiBackend` bis zur HTTP-Antwort mit nachgestellten Antworten von OpenRouter. Ein bezahltes Modell in der Liste wird nie gefragt.

## Kein Einbau bei X-11, CORE-02 und X-01
Die Funktionen sind vorhanden, oder die Kandidaten wären ein verbotener zweiter KI-Kern oder ein zweites Agenten-System. Begründung je Projekt: `docs/ALL_STUDIOS_GITHUB_GODMODE`.

Ein automatisches Gedächtnis über Chats (Idee von mem0) wurde nicht gebaut. Es würde persönliche Daten dauerhaft speichern und braucht eine ausdrückliche Entscheidung.

## Nicht geprüft (NOT RUN)
- Echter Lauf gegen OpenRouter: Dafür braucht es einen Schlüssel der Person, und ein Engpass lässt sich nicht gezielt auslösen. Die Fehlertexte stammen aus dem bekannten Verhalten von OpenRouter und sind nicht am echten Dienst nachgemessen.
