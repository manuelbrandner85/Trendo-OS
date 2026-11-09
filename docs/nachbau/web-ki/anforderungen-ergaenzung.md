# Nachbau Webdesign mit KI: Ergänzung zur Anforderungsliste

Stand: 09.11.2026. Status: Entwurf, noch nichts davon gebaut.
Ergänzt `anforderungen.md` (W1–W6, K1–K8, A1–A8). Für den Vorschlag W1–W12 aus der Internetsuche gilt hier die Bezeichnung **V1–V12**, weil dieselben Nummern in `anforderungen.md` etwas anderes bedeuten.
Iststand geprüft gegen den Branch-Stand 9cff19506 per Stichwortsuche und `docs/web-design-godmode/19_IMPLEMENTATION_STATUS.md`, nicht durch Bedienen der App. Es gelten die Clean-Room-Regeln aus `anforderungen.md`.

## Zuordnung der freigegebenen Vorschläge

| Vorschlag | Inhalt | Wo erfasst |
|---|---|---|
| V1 | KI baut im Editor | K4, A1 |
| V2 | Klicken und ändern | K1, K2, K6 |
| V3 | KI prüft selbst | A6 |
| V4 | Profi-Bausteine | **Z1**, baut auf W5 auf |
| V5 | Design-Skills | A3, A8; dazu Quelle unten |
| V6 | Animations-Zeitleiste | W4 für Effekte, **Z2** für die Zeitleiste |
| V7 | 3D-Ausbau | **Z3** |
| V8 | Freie Breakpoints, Container Queries | W1 und A2, **Z4** für frei wählbare Breiten |
| V9 | CMS und Mehrsprachigkeit | W2, **Z5** für Mehrsprachigkeit |
| V10 | Lokales Web-Modell | A7, **Z6** für Auswahl und Test |
| V11 | Asset-Verwaltung | **Z7** |
| V12 | Import und Veröffentlichen | K8, W3, A6; **Z8** für den Import |

Für A3 darf der Skill `frontend-design` von Anthropic als Quelle dienen (Apache-2.0, Lizenzdatei im Repository `anthropics/skills` geprüft). Wird Text übernommen, gehört der Lizenzhinweis in das Produkt.

## Zusätzliche Anforderungen

Iststand: **fehlt**, **teilweise** oder **vorhanden**.

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| Z1 | Bausteine in Profi-Qualität | Eine Bibliothek fertiger Bausteine (Navigation, Hero, Preise, Häufige Fragen, Galerie, Formular, Fußzeile) im Editor, jeder folgt den zentralen Gestaltungswerten und ist per Tastatur bedienbar. Grundlage sind freie Teile von shadcn/ui, Magic UI (MIT) und Lucide (ISC), jeweils mit Lizenzhinweis im Export. | Test: Baustein einfügen, Gestaltungswert ändern, Baustein folgt. Test: Export enthält alle Lizenzhinweise. Axe-Prüfung je Baustein ohne schweren Befund. | teilweise; 8 einfache Bausteine | shadcn/ui, Magic UI, Lucide (Repositories, 09.11.2026) | 2 |
| Z2 | Animations-Zeitleiste | Eigene Zeitleiste mit Spuren und Schlüsselbildern für Position, Größe, Drehung und Deckkraft, abspielen und an den Scroll-Fortschritt binden. Laufzeit über `@theatre/core` oder Motion (Apache-2.0, MIT). Die Oberfläche ist eigen, weil Theatre Studio unter AGPL steht. GSAP kommt nicht zum Einsatz. Beachtet „weniger Bewegung“. | Test: Schlüsselbild setzen, Wert zur Zeit t stimmt. Test: Export spielt ohne Editor. Test: bei „weniger Bewegung“ keine Bewegung. | fehlt; nur Einblenden und Schweben | Framer und Webflow als Vorbild (Sekundärquellen), Theatre-Analyse im Repository | 2 |
| Z3 | 3D-Ausbau | Der 3D-Baustein lädt eigene GLB-Modelle, auch direkt aus Blender. WebGPU, wenn der Browser es kann, sonst WebGL. Gaussian Splats über Spark (MIT). Kamera, Licht und Material einstellbar. Ohne 3D-Fähigkeit des Geräts erscheint ein Standbild. Für das 3D-Ergebnis gelten Reality Check und Quality Gate. | Test: GLB laden, Modell erscheint im Export. Test: ohne WebGPU läuft WebGL. Test: ohne 3D erscheint das Standbild. PC-Lauf mit Bildern und gemessener Bildrate. | teilweise; 3D-Baustein mit Grundformen, GLB nur im älteren Seitenbau | Three.js, drei, Spark (Repositories, 09.11.2026) | 3 |
| Z4 | Frei wählbare Breiten | Eigene Breakpoints zusätzlich zu Handy, Tablet und Desktop, benannt und in der Breite einstellbar. Bausteine können auf ihre Containerbreite reagieren. Anpassungen folgen den Regeln aus W1. | Test: Breite anlegen, Anpassung gilt nur dort und darunter. Test: Baustein im schmalen Bereich schaltet über die Containerbreite um. | fehlt; 3 feste Vorschaubreiten | Webstudio-Konzept (nur Clean-Room), Webflow-Hilfe | 2 |
| Z5 | Mehrsprachige Websites | Sprachen je Projekt anlegen, Texte und CMS-Einträge je Sprache pflegen, fehlende Übersetzungen werden angezeigt. Der Export erzeugt je Sprache eigene Adressen, `lang`-Attribute und `hreflang`-Verweise. Übersetzen durch die KI nur als Vorschlag. Betrifft die erstellten Websites, die Oberfläche von Trendo bleibt deutsch. | Test: Zwei Sprachen ergeben zwei Seiten mit korrekten Verweisen. Test: fehlende Übersetzung wird gemeldet. Test: Vorschlag ändert erst nach Übernahme. | fehlt nach Stichwortsuche; setzt mehrere Seiten voraus | Webflow, Framer (Sekundärquellen) | 3 |
| Z6 | Lokales Modell für Webdesign | In der KI-Bibliothek steht eine Empfehlung für Web-Aufgaben, passend zu Speicher und Grafikkarte. Ein Prüflauf mit festen Web-Aufgaben vergleicht die Kandidaten und speichert das Ergebnis. Empfohlen wird nur, was den Prüflauf besteht. Fehlt ein geeignetes Modell, sagt Trendo das klar. | Test: Empfehlung passt zu vorgegebener Hardware. Test: Prüflauf bewertet gültige und ungültige Editor-Daten. PC-Lauf mit mindestens zwei Modellen und Protokoll. | teilweise; Qwen 2.5 Coder in der Bibliothek, kein Web-Prüflauf | Modell-Listen (Sekundärquellen, kein Webdesign-Vergleich gefunden) | 2 |
| Z7 | Asset-Verwaltung | Bilder, Videos, Schriften, Icons und 3D-Modelle je Projekt an einem Ort, durchsuchbar und direkt in Bausteinen nutzbar. Freie Quellen nur über offizielle Schnittstellen, Schriften über Fontsource (MIT) und lokal im Export. Jede Datei hat Herkunft und Lizenz, CC-BY erzeugt eine Namensnennung im Export. Bilder werden für das Web verkleinert. | Test: Asset ohne Lizenzeintrag wird beim Export gemeldet. Test: CC-BY erscheint in der Namensnennung. Test: Export lädt keine Schrift aus dem Netz. | teilweise; `WebsiteMedia` im älteren Seitenbau, nicht am Bild-Baustein | Fontsource (Repository, 09.11.2026), vorhandene freie Quellen in Trendo | 2 |
| Z8 | Import | Eigene HTML-Seiten werden über den GrapesJS-Kern (BSD-3-Clause) in Editor-Daten übersetzt. Was nicht abbildbar ist, wird einzeln gemeldet und bleibt als HTML-Block erhalten. Figma-Dateien nur mit eigenem Konto und Schlüssel des Nutzers, der Schlüssel wird nicht gespeichert. Fremde Websites werden nicht importiert. | Test: Beispielseite ergibt Bausteine plus Meldeliste. Test: nichts geht verloren. Test: eine Internet-Adresse als Quelle wird abgelehnt. | fehlt; als geplant dokumentiert | GrapesJS-Analyse im Repository, Figma-Context-MCP (MIT) | 3 |

## Einordnung in die Reihenfolge

- Stufe 1 bleibt wie in `anforderungen.md`. Diese Ergänzung ändert sie nicht.
- **Stufe 2:** Z4 mit W1, Z2 nach W4, Z1 nach W5, dazu Z6 und Z7.
- **Stufe 3:** Z8, Z3, und Z5 nach W2.

## Noch offen

- Die Iststände beruhen auf Stichwortsuche und dem Status-Dokument. Vor Baubeginn je Funktion im Code und in der App nachprüfen.
- `shadcn/ui` und Magic UI setzen auf React und Tailwind. Ob sie sich ohne Laufzeit-Download in das vorhandene Editor-Bündel einfügen, ist noch zu prüfen.
- `@theatre/core` ruht seit April 2024. Motion ist die Ausweichmöglichkeit und vor dem Bau zu vergleichen.
- Z3 braucht einen Lauf am echten PC, Z8 mit Figma eine Prüfung durch den Security-Agenten.
- `requirements.yaml` und `decisions.yaml` sind unverändert.
