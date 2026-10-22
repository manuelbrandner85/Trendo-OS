# T-0042 Briefe

**Ziel:** Briefstudio schreibt eine echte DOCX, liest sie wieder und zeigt den Text in der Vorschau.
**Branch:** cursor/v42-briefe-fa7f
**Dateien:** `LetterPromptAdapter.cs`, `PromptActions.CreateDefault`, Matrix, Leistentest, `tools/Trendo.Render/Program.cs`.

## Testergebnis

`dotnet test Trendo.sln -c Release`: Api 15 bestanden, Core 523 bestanden und 10 übersprungen, App 212 bestanden, Fehler 0.

## Offene Punkte

- PDF nur mit LibreOffice. Hier fehlt `soffice`, deshalb Warnung und kein PDF.
- Versand eines Briefs gibt es nicht.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
