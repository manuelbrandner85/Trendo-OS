# T-0042 Business

**Ziel:** Rechnung und Angebot mit Decimal rechnen, ohne zu buchen. Geschäftsidee als Bauplan. Zahlung bleibt aus.
**Branch:** cursor/v42-business-fa7f
**Dateien:** `BusinessPromptAdapter.cs`, `Finance.InvoiceHtml` (Überschrift Rechnung oder Angebot), Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 532 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- Kein Zahlungsdienst und keine Buchung. Eine Freigabe im Text kann nichts überweisen.
- Die Geschäftsidee rechnet mit der vorhandenen Schätzung (`double` in `BusinessStudio`), nicht mit einer zweiten Buchhaltung.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
