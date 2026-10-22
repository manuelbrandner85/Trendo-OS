# I43-07 Stil-Vorlagen mit einem Klick

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 7. Wortlaut: „Stil-Vorlagen mit einem Klick (z. B. Apple-artig, verspielt, luxuriös, Tech), Schriften/Farben, alles änderbar.“

## Ziel und Nutzen

Ein Klick gibt der Seite einen stimmigen Stil. Schrift und Farben bleiben danach änderbar. Die Seite wirkt wie aus einem Guss, ohne dass die Person CSS schreiben muss.

## Nutzerablauf

1. Die Person sieht die Vorlagen als Knöpfe.
2. Ein Klick setzt Schrift, Farben und Rundung.
3. Sie ändert Schrift oder eine Farbe und übernimmt das.
4. Die bestehende Seite wird neu geschrieben. Die Vorschau zeigt den neuen Stil.
5. Gibt es noch keine Seite, gilt der Stil für den nächsten Bau.

## Anforderungen

- Vorlagen mit einem Klick. Die Quelle nennt als Beispiele: Apple-artig, verspielt, luxuriös, Tech.
- Schrift und Farben sind danach änderbar, nicht nur die Vorlage als Ganzes.
- Eine eigene Kombination bleibt erhalten und heißt nicht weiter wie eine Vorlage, wenn die Werte abweichen.
- Farben, die in die Seite geschrieben werden, sind konkrete Werte. Die Oberfläche verlangt `#` und sechs Hex-Zeichen (`Hex` in `MainWindow.W4Web.cs`).

Die vorhandenen Vorlagen in `WebsiteStyle` (`src/Trendo.Core/Website/WebsiteModels.cs`):

| Id | Beschriftung | Bezug zum Beispiel in der Quelle |
|---|---|---|
| `klar` | Klar | helle Fläche, im Bericht nicht „Apple-artig“ genannt |
| `verspielt` | Verspielt | Beispiel „verspielt“ |
| `luxus` | Luxuriös | Beispiel „luxuriös“ |
| `technik` | Technik | Beispiel „Tech“ |

## Betroffene Module

- `src/Trendo.Core/Website/WebsiteModels.cs` – `WebsiteStyle.Presets`
- `src/Trendo.Core/Website/WebsiteStudio.cs` – `ApplyStyle`
- `src/Trendo.Core/Website/WebsitePlan.cs` – `SanitizeFont`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – schreibt `stil.css` aus dem Stil
- `src/Trendo.App/MainWindow.W4Web.cs` – Knöpfe `web-style-*`, Felder Schrift und Farben, „Farben übernehmen“

## Sicherheit und Datenschutz

- Lokal zuerst. Der Stil steht in `inhalt.json` und `stil.css` im Seitenordner.
- Cloud nur nach Opt-in. Schriften kommen aus der lokalen Schriftliste (`SanitizeFont`), nicht aus einem Schrift-Dienst.
- Kein kostenpflichtiger Dienst und keine kostenpflichtige Schrift als Voraussetzung.
- Keine Geheimnisse in den Farbwerten.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` braucht der Stilwechsel nicht. Er veröffentlicht die Seite nicht.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 3 liefert die Seite, auf die der Stil wirkt.
- Punkt 8 prüft unter anderem den Kontrast nach dem Wechsel (`WebsiteColor.Contrast`).
- Keine Abhängigkeit zum KI-Laden (#252).

## Akzeptanzkriterien

- Jeder Knopf der vier Vorlagen setzt Id, Beschriftung, Schrift, Farben und Rundung in der Seite, wenn schon ein Ordner da ist.
- Ein geänderter Hex-Wert, der zu keiner Vorlage passt, ergibt Id `eigen` und Beschriftung `Eigen`.
- Eine Schrift außerhalb der erlaubten Liste wird von `SanitizeFont` auf die sichere Vorgabe gesetzt und nicht roh in das CSS geschrieben.
- Ungültige Farben werden nicht übernommen. Die Fläche sagt, dass die Farbe wie `#0b4f9c` aussehen soll.

## Offene Fragen

- Die Quelle sagt „Apple-artig“. Der Knopf heißt „Klar“. Soll die Beschriftung „Klar“ bleiben?
- Fehlt eine fünfte Vorlage, oder sind die vier Beispiele mit „Klar“ für „Apple-artig“ gemeint?

## Ist-Stand

Vier Vorlagen, Klick, eigene Farben und `ApplyStyle` sind in `main`. Die Beschriftung „Apple-artig“ kommt im Code nicht vor.
