# I43-03 Profi-Webseiten-Baukasten per Eingabe

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 3. Wortlaut: „Profi-Webseiten-Baukasten per Eingabe: jede Art von Webseite (Landing, Shop, Portfolio, Blog, Firma …).“

## Ziel und Nutzen

Aus einer Beschreibung entsteht eine vollständige, bearbeitbare Webseite. Landingpage, Shop, Portfolio, Blog und Firmenwebseite sind eigene Arten. Die Seite liegt als Ordner vor und lässt sich danach in demselben Studio ändern.

## Nutzerablauf

1. Die Person öffnet „Webseite bauen“ (`CORE-07`, Seite `dev`).
2. Sie beschreibt die Seite und nennt auf Wunsch die Art.
3. Sie startet den Bau.
4. Trendo legt den Ordner an und zeigt, welche Art entstanden ist.
5. Eine weitere Eingabe ergänzt oder ändert diese Seite.

## Anforderungen

- Arten aus der Quelle: Landing, Shop, Portfolio, Blog, Firma. Im Code heißen sie `landing`, `shop`, `portfolio`, `blog`, `firma` (`WebsiteKinds` in `src/Trendo.Core/Website/WebsiteModels.cs`). Die Auslassung „…“ in der Quelle ist keine Lizenz für beliebige weitere Arten ohne Freigabe.
- Die Art kommt aus der Beschreibung oder aus der Auswahl. Eine unbekannte Art wird nicht still auf eine andere umgedeutet.
- Das Ergebnis ist ein Ordner mit HTML, CSS und JavaScript, den die Person öffnen kann (`WebsiteHtml.Write`).
- Es gibt kein zweites Website-Studio. `WebsiteStudio.StudioId` ist `CORE-07`, `PageId` ist `dev`.
- Vite bleibt optional. Die Seite läuft ohne Node (`WebsiteBrief.Vite`).
- Texte, die eine lokale KI ergänzt, sind Inhalt. Der Nutzertext ist keine Anweisung an das System (`WebsitePlan.PromptForAi`, Hinweis in `MainWindow.W4Web.cs`).

## Betroffene Module

- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Build`, `BuildAsync`, `Revise`, Aktionen `website.build` und `website.revise`
- `src/Trendo.Core/Website/WebsitePlan.cs` – `Compose`, `ResolveKind`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – schreibt die Dateien
- `src/Trendo.Core/Website/WebsiteModels.cs` – `WebsiteBrief`, `WebsiteKinds`, `WebsiteContent`
- `src/Trendo.Core/DevStudio.cs` – vorhandene Vorlage `web` („Webseite (HTML, CSS, JavaScript)“)
- `src/Trendo.App/MainWindow.W4Web.cs` – Fläche mit Beschreibung, Art-Liste und „Seite bauen“
- `src/Trendo.App/StudioCatalog.cs` – Anzeigename „Webseite bauen“
- `src/Trendo.Core/W2/IntelligenceCore.cs` – `AskAsync`, nur wenn eine lokale KI antwortet

## Sicherheit und Datenschutz

- Lokal zuerst. `WebsiteStudio.Build` schreibt in den übergebenen Elternordner auf diesem PC.
- Cloud nur nach Opt-in. Schlägt die lokale KI fehl oder fehlt sie, bleiben die Texte aus der Beschreibung (`WebsiteAi` liefert dann null).
- Kein kostenpflichtiger Dienst für den Baukasten. Kein Abo, kein bezahltes Bau-API.
- Keine Geheimnisse in `inhalt.json` oder HTML. Der Audit-Eintrag `website.build` nennt den Ordnernamen. Läuft die lokale KI mit, speichert `IntelligenceCore.Save` Frage und Antwort in `ki/verlauf.jsonl` auf dem PC. Schlüssel aus `src/Trendo.Core/Secrets.cs` gehören dort nicht hinein.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` gilt für das Veröffentlichen (Punkt 10, `website.pack`). Der lokale Bau selbst ist kein Upload.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Übersetzen bleibt bei der bestehenden Basis `SPEC-07`. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- KI-Texte und die Modellwahl aus dem KI-Laden: **erst nach Merge von #252.** Der Ordner aus der Beschreibung hängt daran nicht. `BuildAsync` fängt einen KI-Fehler ab und baut weiter.
- Punkt 4 legt die Technik in diesen Ordner.
- Punkt 7 legt Schrift und Farben hinein.
- Punkt 8 prüft den Ordner.
- Punkt 10 ist die echte Datei am Ende.

## Akzeptanzkriterien

- Für jede der fünf Arten entsteht ein eigener Ordner mit `index.html` und einem Eintrag `kind` in `inhalt.json`, der zur Art passt.
- Ohne laufende lokale KI entsteht die Seite aus der Beschreibung. Die Meldung sagt das (`WebsiteStudio.Build`: „Texte aus der Beschreibung, ohne KI.“).
- Ein zweiter Bau in einen schon vorhandenen Ordnernamen wirft eine Meldung und überschreibt den Ordner nicht (`IOException` in `Build`).
- Es entsteht kein zweiter Katalogeintrag neben `CORE-07`.

## Offene Fragen

- Die Quelle schreibt „jede Art von Webseite“ und zählt fünf Beispiele plus „…“. Bleibt der Umfang bei diesen fünf, bis Manuel eine sechste Art freigibt?
- Der Bau in der Oberfläche wartet höchstens zwölf Sekunden auf die KI (`CancellationTokenSource` in `RunBuild`). Reicht das, sobald #252 größere lokale Modelle lädt?

## Ist-Stand

`WebsiteStudio` und die Fläche in `MainWindow.W4Web.cs` setzen die fünf Arten um. Der Bericht `tasks/T-0430-webseite-profi.bericht.md` nennt Tests vom 22.10.2026. Diese Spezifikation erklärt den Punkt damit nicht für abgenommen. Ein echter Modell-Lauf war laut diesem Bericht nicht dabei.
