# T-0662 Bericht: A1-Aktion „seite_anlegen“ ausgebaut

**Auftrag:** Die in Z6 empfohlene A1-Aktion „seite_anlegen“ bauen, damit lokale Modelle mehr Web-Aufgaben bestehen. Freigabe des Nutzers: „Yes, build it“.
**Branch:** `claude/T-0662-seite-anlegen` von `origin/main` b9d73b16e.

## Befund vorab

„seite_anlegen“ gab es schon (ersetzt die aktuelle Seite). Die Z6-Aussage, die Aktion fehle, war falsch. Die echten Ursachen aus den Rohantworten:

- Ollama lief mit 4096 Token Kontext. Die Web-Anweisungen allein brauchen etwa 2500 Token, darum brachen ganze Seiten mitten im JSON ab.
- Die Modelle bauten nur einen Baustein oder hängten Bausteine an eine erfundene Seiten-id („in“: „seite1“).
- Einzelne Formfehler (Felder neben `props`, Wahrheitswerte statt „ja“/„nein“, erfundene Bildadressen) verwarfen den ganzen Plan.

## Umsetzung

- `seite_anlegen` mit `bausteine`: ganze Seite in einer Aktion, intern wie `baustein_einfuegen` auf oberster Ebene; zählt gegen `MaxActions`.
- `seite_anlegen` mit `adresse`: weitere Seite der Website. Die bisherige Seite bleibt unberührt. Vorprüfung vor dem Schreiben (Variante, Adresse frei, höchstens 200 Seiten, kein Sammlungsname). Rückgängig nur, solange die Seite offen und unverändert ist; dann Papierkorb, Gestaltungswerte und Komponenten wie vorher, vorige Seite wieder offen. Der Menschen-Aktionsweg lehnt `adresse` ab (dafür gibt es die Seitenliste); der Prüfkreislauf verwirft Nachbesserungen, die eine Seite anlegen wollen.
- Feste Regeln für häufige Fehler: Felder neben `typ` gelten wie in `props`; eine Aktion ohne `props`, deren Angaben in `props` stehen, gilt wie direkt angegeben; eine Liste aus Texten gilt als Zeilen; `true`/`false` gilt bei Ja/Nein-Wahl; ein ungültiges Bild in einem neuen Baustein bleibt leer und steht in der Änderungsliste. Doppelte Angaben bleiben Fehler.
- Eine Nachfrage: Ein abgelehnter Plan geht mit der genauen Meldung einmal an die KI zurück (Studio und Prüflauf).
- Ollama bekommt `num_ctx` 8192, bei sehr langen Anfragen 16384 (zwei feste Stufen, damit das Modell nicht dauernd neu lädt). Das gilt für alle lokalen Anfragen über `OllamaBackend`.
- KI-Bibliothek: `qwen2.5-coder:14b` (Apache-2.0, etwa 9 GB). Empfohlen wird es weiterhin erst nach bestandenem Prüflauf auf dem eigenen PC. Das Modell wird nicht mitgeliefert.

## QA

Unabhängige Prüfung durch einen Unteragenten (nur lesend). Befunde und Behebung:

1. Ein veralteter Rückgängig-Zustand konnte eine falsche Seite löschen. Behoben: Rückgängig nur bei offener, unveränderter Seite; der Zustand wird beim Seitenwechsel, beim Anlegen und beim Löschen geleert.
2. Gestaltungswerte der neuen Seite galten nach Rückgängig weiter fürs ganze Projekt. Behoben: Werte und Komponenten werden gesichert und zurückgesetzt.
3. Rückgängig konnte eigene Änderungen löschen. Behoben: Rückgängig wird dann abgelehnt.
4. Nach Rückgängig war die Startseite offen. Behoben: Die vorige Seite ist wieder offen.
5. Ein Fehler zeigte sich erst beim Übernehmen. Behoben: Vorprüfung vor dem Sichern des Entwurfs.
6. Der Prüfkreislauf konnte eine Seite anlegen. Behoben: Diese Nachbesserung wird verworfen.
7. Ein Titel nur aus Leerzeichen. Behoben: wird abgelehnt.

Die genannten Testlücken (Adressen, Grenze 199 + 1, Rückgängig-Fälle, Prüfkreislauf) sind mit Tests geschlossen.

## Prüflauf (Z6)

Siehe `reports/web-modell-pruefung-2026-11-15-seite-anlegen.md`:

| Modell | Vorher | Jetzt |
|---|---|---|
| `qwen2.5-coder:14b` | 2/3 | 6/6 |
| `qwen3:14b` | 2/3 | 5/6 |
| `qwen2.5-coder:7b` | 1/3 | 4/6 |

Grenze: Eine ganze Seite dauert mit 14B etwa 2,5 bis 3,5 Minuten auf der RTX 5070.

## Tests

- Neu: `tests/Trendo.Core.Tests/WebDesignSeiteAnlegenTests.cs` (11 Tests).
- Neu: `tests/Trendo.App.Tests/OllamaContextTests.cs`.
- Erweitert: `WebDesignKiUiTests.cs` (neue Seite mit Rückgängig, eine Nachfrage).
- Gezielt grün: Core-Webdesign und KI-Bibliothek 254/254, App-Webdesign 55/57 (2 übersprungen wie vorgesehen).
- Volltest (`dotnet test Trendo.sln -c Release`, `python -m unittest discover -s tests/claude_handoff`): Api 16/16, Core 2567/2572 (5 übersprungen), App 1312/1318 (6 übersprungen), Python 11/11 – alles grün.

## Geänderte Dateien

- `src/Trendo.Core/Website/WebDesignActions.cs`, `WebDesignKi.cs`, `WebDesignCheck.cs`, `WebDesignModelBench.cs`
- `src/Trendo.Core/W2/KiLibrary.cs`
- `src/Trendo.App/KiBackends.cs`, `MainWindow.WebDesignKi.cs`, `MainWindow.WebDesignSite.cs`
- Tests wie oben
- `docs/nachbau/web-ki/anforderungen-ergaenzung.md`, `tasks/T-0622-web-stufe3.bericht.md`, Berichte unter `reports/`
