# Symbole aus Iconify – 08.11.2026

Alle Studios, Runde 8, X-07 „Freie Bilder & Klänge finden“.

**Quelle:** Iconify-Symbolsätze (https://github.com/iconify/icon-sets), fest gepinnt auf den Stand `3eb33380c7dbc256d098ada3f5a5863ec4b145dc` (07.11.2026).
- Es sind **reine Daten**: JSON mit SVG-Zeichenanweisungen. Trendo liest sie mit eigenem Code (`IconSets`) und führt nichts aus.
- Geladen wird erst auf Klick, danach kommen die Daten aus dem Zwischenspeicher im Datenordner.

## Lizenzregel
Jeder Satz bringt seine Lizenz in `collections.json` mit. Trendo bietet nur an, was frei nutzbar ist, auch für Geld:
- **Frei:** MIT, Apache-2.0, ISC, BSD, Unlicense, CC0, OFL-1.1.
- **Mit Namensnennung:** CC BY 3.0 / 4.0. Beim Speichern schreibt Trendo die Nennung („Name“ von Autor – Lizenz, Adresse) einmal in `Lizenzen.txt`.
- **Ausgeblendet:** nicht-kommerziell (NC), „share-alike“ (SA), GPL, MPL und alles mit unbekannter Lizenz.

Am gepinnten Stand ergibt das: **258 freie Sätze, 18 ausgeblendet** (von 276).

## Sicherheit
Ausgegeben werden nur Zeichenanweisungen, ohne:
- `script`, `foreignObject`, `iframe`, `image`
- Ereignis-Attribute (`on…=`)
- `javascript:`
- externe `href`/`src`/`url(…)`

Interne Verweise (`#id`, z. B. `<use href="#…">`, Verläufe `url(#…)`) bleiben erlaubt.
Das zeigte der echte Test: Tabler nutzt `<use>` (Symbol „brand-livewire“), und ein erster, zu strenger Filter hätte es fälschlich verworfen.

## Echte Daten (gepinnter Stand, nur gelesen)
| Satz | Lizenz | Symbole (sichtbar) | Aliasse |
|---|---|---|---|
| mdi (Material Design Icons) | Apache-2.0 | 7 447 | 6 363 |
| tabler | MIT | 6 257 | 194 |
| game-icons | CC BY 3.0 | 4 133 | 3 |
| fluent-emoji-flat | MIT | 3 139 | 7 |

- **Ausgabe:** Alle über 21 000 Symbole samt Aliassen werden sicher und als **gültiges XML** ausgegeben. Aliasse mit Spiegeln und Drehen werden aufgelöst.
- **Gefunden und behoben:** mdi.json enthält 7 638 Einträge, Iconify zählt 7 447. Die Differenz sind veraltete Symbole mit `hidden: true`; Trendo blendet sie jetzt in Liste und Suche aus.
- **Sichtprüfung:** gerenderte Seite mit echten Daten über `tools/Trendo.Render` (`TRENDO_ICONS_DIR`):
  - `symbole-mdi.png` (Suche „arrow“, SHA-256 1a8e05aaa781eef3…)
  - `symbole-tabler.png` (Suche „heart“, Linien-Symbole, SHA-256 b0328cd734edc686…)
  - Gefüllte und offene Formen werden sauber dargestellt.

## Tests
- **`IconSetsTests`** (7 Tests, grün):
  - Lizenzregel
  - Satzliste
  - SVG mit viewBox und Seitenverhältnis
  - Aliasse (Spiegeln, Drehen, Ketten)
  - Suche
  - unsichere Inhalte
  - Speichern mit Namensnennung bzw. Verweigerung
- **`IconSetsRealTests`** (1 Echtdaten-Test, grün): Er findet die Daten im Prüfordner, sonst wird er übersprungen.
- **`MediaIcons_Ui`** (1 Oberflächentest, grün):
  - Laden, Suchen, Speichern per Klick, Namensnennung.
  - Ausgeschlossene Sätze sind nicht wählbar.
  - Jeder Abruf verwendet den gepinnten Stand; ein Satz wird nur einmal geladen.

## Grenzen / nicht geprüft
- Die Vorschau zeichnet Pfade, keine Kreise oder Rechtecke als eigene Elemente. Die gespeicherte SVG ist immer vollständig.
- Ein Stand-Wechsel (neuer Commit) ist bewusst Handarbeit, damit sich Lizenzen nicht unbemerkt ändern.
