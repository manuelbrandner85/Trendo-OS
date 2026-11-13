# Bericht T-0621: Webseite bauen, Stufe 2 (Design und Mechanik)

**Auftrag:** `tasks/T-0621-web-stufe2.md`. Vorgabe: `docs/nachbau/web-ki/anforderungen.md` (A5, W1, W6, W5, W4, A2, A8) und `anforderungen-ergaenzung.md` (Z4, Z1, Z2, Z6, Z7). Freigabe des Nutzers für Stufe 2 kam über die Sitzung „Spiele-Studio Stufe 1“.
**Branch/Worktree:** `claude/T-0621-web-stufe2`, `C:\Users\<user>\TrendoEntwicklung\wt-T-0621-web-stufe2`, Basis `main` 7f0f9e3fa (Release 0.9.92).
**Status:** Alle elf Funktionen gebaut, jede Prüfung der Listen mit Test belegt oder unten begründet. QA-Prüfung je Teil, zuletzt ohne offenen Muss-Befund (siehe QA). Nicht in `main`, kein Release, nichts veröffentlicht.

## Was gebaut wurde

| ID | Ergebnis | Belege (Tests) |
|---|---|---|
| A5 | Baustein „Stapel“ (Richtung, Abstand als Stufe des Gestaltungswerts, „passt sich an“/„füllt aus“, Ausrichtung), bricht um statt überzulaufen; Ansichten in fester Reihenfolge. | Pixelwerte abgelehnt; Chrome: Abstand = Gestaltungswert × Stufe, Stapel bei 390 px im Rahmen (Seite und Box) |
| W1 | Ansichten mit Breite; Anpassungen (Sichtbarkeit, Ausrichtung, Schriftgröße, Spaltenzahl) gelten in ihrer und allen kleineren Ansichten, nie nach oben; sichtbar und zurücksetzbar im Feld „Anpassungen je Ansicht“; Export mit Medienregeln. | Kaskade, Medienregeln, Zurücksetzen, Bereinigung; Chrome in Editor und Export |
| Z4 | Eigene Ansichten mit Name und Breite; Spalten schalten über ihre eigene Containerbreite um. | Gilt nur dort und darunter; Chrome: innere Spalten in schmaler Spalte untereinander |
| W6 | Gestaltungswerte für das Projekt (`gestaltung.trendo.json`), hell und dunkel, benannt; Kontrast schon beim Wählen. | Projektweit, Kontrast hell/dunkel, Rückgängig setzt Werte zurück; Chrome: dunkler Satz |
| W5 | Komponenten projektweit (`komponenten.trendo.json`), Überschreibungen je Verwendung, eigene Vorlagen, keine Verschachtelung. | Original ändern ändert 2 Verwendungen außer überschriebenen Feldern; Chrome: Anzeige im Editor und Export |
| Z1 | Navigation, Hero, Preise, Häufige Fragen, Galerie, Kontaktformular, Fußzeile; eigene Umsetzung mit den Gestaltungswerten, Symbole von Lucide (ISC). | Chrome: Baustein folgt Gestaltungswert, Tastatur, axe-core je Baustein ohne schweren Befund; Lizenzhinweis im Export |
| W4 | Erscheinen und Darüberfahren je Baustein, Seitenübergang beim Öffnen; Web Animations API. | Chrome: Bewegung läuft, bei „weniger Bewegung“ keine Animation, ohne IntersectionObserver alles sichtbar, Ersatzweg im Stil |
| Z2 | Zeitleiste mit fünf Spuren und Schlüsselbildern, beim Laden oder an den Scroll-Fortschritt gebunden, Wiederholen höchstens etwa 5 s. | Wert zur Zeit t (C# und JS gleich); Chrome: Export spielt ohne Editor, folgt dem Scrollen |
| A2 | 8 Web-Techniken mit Stand, MDN-Quelle, Ersatzweg und Beispiel; die KI bekommt passende Einträge. | Vollständigkeit; MDN-Stichprobe am 13.11.2026 (alle 8 von mir, 6 davon unabhängig von der QA) |
| A8 | Zwei bis drei Richtungen mit kleiner Vorschau; gebaut wird erst nach der Wahl; Schrift und Farben werden fest eingesetzt. | Ohne Wahl nichts gebaut; gleiche oder kontrastarme Richtungen abgelehnt |
| Z6 | Prüflauf mit drei festen Web-Aufgaben, Empfehlung nach Hardware nur für Bestandene, klarer Hinweis ohne passendes Modell. | Bewertung gültig/ungültig, Empfehlung je Hardware; PC-Lauf mit drei Modellen |
| Z7 | Medien je Projekt (`medien/`, `medien.trendo.json`) mit Herkunft und Lizenz, Suche, Auswahl im Bild-Feld, Bilder auf 1920 px verkleinert, Schriften von Fontsource lokal, Namensnennung für CC-BY. | Fehlende Lizenz beim Export gemeldet, CC-BY in `namensnennung.html`, Schrift lokal ohne Netz |

## PC-Lauf Z6 (echte Modelle über Ollama)

`reports/web-modell-pruefung-2026-11-13.md` und `.json` (SHA-256 im Bericht): qwen2.5:3b 1 von 3, qwen2.5-coder:7b 2 von 3, mistral:7b 0 von 3. Kein Modell besteht alle Aufgaben, Trendo empfiehlt keins. Die QA bekam beim selben Modell andere Werte (qwen2.5-coder:7b 0 von 3): Die Ergebnisse streuen. Das bestätigt das Risiko aus Stufe 1: Lokale Modelle dieser Größe liefern selten gültige Pläne; mit Claude über das Abo oder einem größeren Modell geht es.

## QA-Prüfung

Unabhängig vom Implementierer, je Teil in eigener Kopie, Ergebnisse in diesem Bericht.

- **Teil 1, nicht bestanden:** Wiederherstellen setzte Gestaltungswerte nicht zurück; Varianten änderten über die Gestaltungswerte die Seite; Ausrichtung im Kopfbereich wirkungslos. Behoben, mit Tests.
- **Teil 2, nicht bestanden:** Komponente mit über 60 Bausteinen verschwand still; verschachtelte Verwendungen ungeprüft; Anpassungen in Komponenten wirkungslos; Vorlagen verloren Listen und Bewegung und überschrieben sich still; Link-, Bild- und Mailfelder der Profi-Bausteine auf dem Editor-Weg nicht entschärft. Behoben, mit Tests; von der QA in Teil 3 bestätigt.
- **Teil 3, nicht bestanden:** SVG-Symbol im Bild-Feld angeboten, beim Speichern still entfernt. Behoben, dazu Medienliste nicht mehr im Export, strenge Medienpfade gegen CSS-Einschleusung, Zeilenumbruch im Schriftnamen ungültig, beschädigtes Medienverzeichnis wird nicht überschrieben, Fontsource nur WOFF2 mit Größengrenze.
- **Nachprüfung (nur am Code, c98705bfb):** bestanden mit Empfehlungen; die zwei Empfehlungen stehen unter „Offene Punkte“.

## Entscheidungen für den Nutzer

1. **Varianten und Gestaltungswerte:** W6 verlangt projektweite Werte, K5 verlangt, dass eine Variante das Original nicht ändert. Umgesetzt: Seite, Export und neue Varianten nutzen die Projektwerte; eine bestehende Variante behält ihre eigene Gestaltung, bis sie übernommen wird. Komponenten ebenso.
2. **Zeitleiste ohne Theatre oder Motion:** Z2 nennt `@theatre/core` oder Motion als Laufzeit. Umgesetzt ist eine eigene kleine Laufzeit über die Web Animations API (keine Lizenzfragen, Theatre ruht seit 2024).
3. **shadcn/ui und Magic UI** sind nicht übernommen (Tailwind); Z1 ist eine eigene Umsetzung, nur Lucide wird genutzt.

## Was NICHT geprüft wurde

- Kein Lauf mit echtem Claude; die App nicht im echten Fenster und `NativeWebView` bedient (Editor-Bündel in echtem Chrome geprüft).
- Bedienung der Komponenten im Editor per Maus, besonders „Original ändern (alle Verwendungen)“.
- Fontsource gegen das echte Netz aus der App (Schnittstelle von Hand abgefragt, im Test nachgebildet).
- „Weniger Bewegung“ in der Editor-Vorschau (nur im Export geprüft).
- Linux-USB und Pi 4.

## Offene Punkte und Grenzen

- Eigene Schrift wird im Editor nicht geladen (kein `@font-face` im Editor), erst im Export.
- Bildverkleinerung beachtet die EXIF-Drehung nicht, prüft die Größe erst nach dem Dekodieren und entfernt keine Metadaten bei Bildern bis 1920 px.
- Bausteine in Bereichen einer Komponente behalten ihre ids: Anpassungen gelten für alle Verwendungen gemeinsam.
- Vorlagen übernehmen keine Inhalte von Bereichen.
- Prüflauf Z6: einmaliger Lauf ohne feste Temperatur; der Test im PC-Lauf nutzt keine Skills, die App schon. Die Empfehlung steht im Webdesign-Bereich, nicht in der KI-Bibliothek.
- Videos, 3D-Modelle und SVG-Symbole werden in den Medien abgelegt, haben aber noch keinen Baustein (Z3 ist Stufe 3; SVG ist keine erlaubte Bilddatei).
- Fontsource-Abruf: Die 5-MB-Grenze greift ohne Content-Length erst nach dem Laden; `MaxResponseContentBufferSize` fehlt, ebenso Tests für die Ablehnungen (falsche Endung, `..`, falscher Dateikopf).
- „Einrasten an Hilfslinien“ (A5) entfällt, weil der Editor keine freie Positionierung hat.
- Übergänge zwischen Seiten (W4) brauchen mehrere Seiten (Stufe 3).
- `https`-Bildadressen werden jetzt auf dem Editor-Weg geleert statt erst beim Export gemeldet.

## Tests und Kommandos

```
npm ci --ignore-scripts; npm install lucide-react@0.460.0 --save-exact --ignore-scripts; npx vite build; node lizenzen.mjs
npm install axe-core@4.14.0 --prefix %TEMP%\trendo-axe --ignore-scripts   (nur für die Chrome-Prüfung)
dotnet test Trendo.sln -c Release
python -m unittest discover -s tests/claude_handoff
TRENDO_PC_LAUF=1 dotnet test ... --filter Z6_pc_run   (PC-Lauf)
```

Volltest auf 1a7d3ee23 (vor den letzten Korrekturen): Api 16/0, Core 2309 bestanden + 5 übersprungen, App 1217 bestanden + 5 übersprungen, 0 Fehler.

## Abschluss

Abschließender Volltest auf c98705bfb, alle Exitcodes 0:

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---|---|---|---|
| Trendo.Api.Tests | 16 | 0 | 0 | 16 |
| Trendo.Core.Tests | 2311 | 5 | 0 | 2316 |
| Trendo.App.Tests | 1217 | 5 | 0 | 1222 |
| tests/claude_handoff | 11 | 0 | 0 | 11 |

Unter den übersprungenen App-Tests ist der PC-Lauf Z6 (nur mit `TRENDO_PC_LAUF=1`, separat gelaufen). Die Chrome-Prüfungen von Teil 1 und Teil 2 liefen mit (axe-core in `%TEMP%	rendo-axe`). `tools/webdesign-editor/node_modules` und `dist` vor den Paket-Tests entfernt.

## Rust

Nicht eingesetzt. Die Arbeit liegt in Datenprüfung, Oberfläche und Browser-Code ohne gemessenen Engpass; bestehender Code ist C# und JavaScript.
