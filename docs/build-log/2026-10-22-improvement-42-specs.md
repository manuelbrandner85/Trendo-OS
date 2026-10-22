# Verbesserung 42 – Einzelspezifikationen (nur Doku)

- 22.10.2026, Branch `cursor/improvement-42-specs-5c20`, Basis `main` `af4f3a7`.
- Quelle `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, hat 20 Funktionen. Ausformuliert unter `docs/specs/improvement-42/`. Texte auf Deutsch, Fachwörter jeweils kurz erklärt. Kein Produktionscode. `requirements.yaml` und `docs/spec/decisions.yaml` unverändert.
- Punkte 23, 25, 34 und Language 2.0 bleiben gesperrt und sind keine der zwanzig Funktionen. `SPEC-07` und `SPEC-12` im Katalog bleiben dabei. Punkt 86 ist im Repo nicht vorhanden. Punkt 35: nie aktivieren (`X-02`). 
- KI-Laden: Entwurf #252. Betroffen sind der KI-Vorschlag von Funktion 3, die Modellwahl von Funktion 7 und die Online-Dienste von Funktion 8, jeweils erst nach Merge von #252.
- Test `dotnet test Trendo.sln -c Release` auf diesem Cloud-Image: Api 15 bestanden. Core 529 bestanden, 10 übersprungen. App 221 bestanden, 1 fehlgeschlagen. Der Fehlschlag ist der UI-Test „SPEC-10 UI: diesen PC live messen“ in `tests/Trendo.App.Tests/W6UiTests.cs`. Der gemessene Arbeitsspeicher steht auf „Warnung“, der Test erwartet „Ok“. Diese Ausarbeitung ändert keinen Programmcode. Der Fehlschlag hängt an der Messung auf diesem Rechner.
