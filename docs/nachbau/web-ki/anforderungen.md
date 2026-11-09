# Nachbau Webdesign und Webentwicklung mit KI: Anforderungen

Stand: 09.11.2026. Status: Stufe 1 gebaut (T-0620, Zweig `claude/T-0620-web-ki`, noch nicht in `main`); Stufe 2 und 3 offen.
Iststand geprüft gegen `origin/main` (023a6d742) per Stichwortsuche, Lesen einzelner Dateien und `docs/web-design-godmode/19_IMPLEMENTATION_STATUS.md`, nicht durch Bedienen der App.

## Clean-Room-Regeln

Diese Liste ist die einzige Brücke zwischen Beobachten und Bauen. Sie beschreibt Verhalten in eigenen Worten.

- Vorbilder liefern Funktionen und Abläufe, keine Texte, Grafiken, Namen oder Prompts.
- Offene Projekte nur mit geprüfter Lizenz. Wird Code übernommen, gehört der Lizenzhinweis in das Produkt und in den Nachweis.
- Fremde Websites werden nicht geklont und ihre Gestaltung nicht ausgelesen, um sie zu kopieren. Werkzeuge wie Open Lovable oder design-extract nur für eigene Seiten.
- GSAP bleibt draußen: Die Lizenz verlangt für visuelle No-Code-Animationswerkzeuge eine Erlaubnis (siehe `docs/recherche/webdesign-werkzeuge.md`).
- Bezahl-Dienste dürfen nie Voraussetzung sein. Claude nur über das Abo im installierten Claude Code, nie über die API; sonst lokale KIs.
- Bestehende Module erweitern (`WebDesign`, `WebsiteStudio`, `WebsiteQuality`, `AxeCheck`, `WebsitePreview`, `SkillLibrary`, `Intel.AskAsync`), kein zweiter KI-Kern.
- Veröffentlichen bleibt gesperrt, bis der Nutzer im Einzelfall zustimmt.

## Vorbilder

| Bereich | Vorbild | Grundlage | Tiefe der Beobachtung |
|---|---|---|---|
| Design, CMS, SEO | Webflow | Hilfe zu Breakpoints, Vergleichsartikel, 09.11.2026 | App nicht bedient, CMS-Hilfe nicht abrufbar (403) |
| Animation | Framer | Vergleichsartikel, 09.11.2026 | Nur Sekundärquellen |
| KI-Bedienung | Lovable | Dokumentation der Preview-Toolbar, 09.11.2026 | App nicht bedient |
| Visueller Editor mit KI | Onlook (Apache-2.0) | README, `docs/web-design-godmode/04_ONLOOK_ANALYSIS.md` | Dokumentation gelesen. Hängt an Cloud-Diensten, darum nur Vorbild für Verhalten |
| Web-Apps | bolt.diy (MIT), Bolt | README, Praxistest eines Einzeltesters | Dokumentation gelesen. Wird nur noch gepflegt; WebContainers haben eigene Bedingungen, darum nicht einbauen |
| KI-Arbeitsweise | Frontend-Design-Skill, oil-ui, Superdesign (MIT), Playwright MCP und Chrome DevTools MCP (Apache-2.0) | Listen und Repositories, 09.11.2026 | Nur Beschreibungen |
| Web-Techniken | Baseline-Angaben | Sekundärquellen, teils widersprüchlich | Vor Aufnahme gegen MDN oder caniuse prüfen |

## Anforderungen

Iststand: **fehlt**, **teilweise** oder **vorhanden**. Stufe gibt die vorgeschlagene Reihenfolge an.

### Webdesign

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| W1 | Geräte-Ansichten mit Anpassungen | Desktop ist die Basis. Einstellungen gelten für kleinere Ansichten weiter, bis eine kleinere Ansicht sie überschreibt; die Überschreibung wirkt nicht zurück nach oben. Überschriebene Werte sind sichtbar und zurücksetzbar. Breite der Ansichten wählbar. | Test: Wert auf Tablet ändern lässt Desktop gleich und wirkt auf Handy. Test: Export erzeugt passende Medienregeln. | teilweise; 3 feste Vorschaubreiten, keine Anpassungen je Ansicht | Webflow Hilfe | 2 |
| W2 | CMS | Sammlungen mit Feldern (Text, Bild, Datum, Verweis), Einträge bearbeiten, eine Vorlage je Sammlung erzeugt eine Seite je Eintrag, Listen zeigen Einträge. Export bleibt offline lauffähig. | Test: 3 Einträge ergeben 3 Seiten und eine Liste. Test: Pflichtfeld leer wird gemeldet. | fehlt; setzt mehrere Seiten voraus | Webflow | 3 |
| W3 | SEO je Seite | Titel, Beschreibung, Adresse und Vorschaubild je Seite, Weiterleitungen, Sitemap, robots-Datei. Inhalte stehen im exportierten HTML, nicht erst nach dem Laden von JavaScript. Prüfung läuft auf dem Export. | Test: Text einer Seite steht im HTML ohne Skriptausführung. Test: Sitemap listet alle Seiten. Test: doppelte Adresse wird gemeldet. | teilweise; Titel und Beschreibung vorhanden, Export des visuellen Editors rendert per Skript | Webflow | 3 |
| W4 | Animation im Editor | Effekte beim Hineinscrollen, beim Darüberfahren und Seitenübergänge je Baustein, mit Vorschau. Eigene Umsetzung über CSS und Web Animations API. Beachtet „weniger Bewegung“. Ohne Unterstützung bleibt der Inhalt sichtbar. | Test: Bei „weniger Bewegung“ läuft keine Animation. Test: Export enthält den Ersatzweg. | teilweise; Einblenden und Schweben je Baustein | Framer | 2 |
| W5 | Wiederverwendbare Komponenten | Eine Bausteingruppe wird zur Komponente. Änderungen am Original wirken auf alle Verwendungen, einzelne Verwendungen können Felder überschreiben. Eigene Vorlagen speicherbar. | Test: Original ändern ändert 2 Verwendungen, außer überschriebene Felder. | fehlt; 8 feste Bausteine | Framer | 2 |
| W6 | Zentrale Gestaltungswerte | Farben, Schriften, Rundungen und Abstände gelten projektweit über alle Seiten, benannt und als heller und dunkler Satz. Kontrast wird beim Wählen geprüft. | Test: Wert ändern wirkt auf alle Seiten. Test: zu geringer Kontrast wird gemeldet. | teilweise; Werte je Seite, geprüft beim Speichern | Onlook, Webflow | 2 |

### KI-Entwicklung

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| K1 | Klicken und per Satz ändern | Ein oder mehrere Elemente in der Vorschau auswählen und die Änderung beschreiben. Nur die Auswahl ändert sich, mit Vorschau und einem Rückgängig-Schritt. | Test: Auftrag ändert nur ausgewählte Bausteine. Test: Verwerfen lässt die Seite gleich. | vorhanden (T-0620); Auswahl im Editor unter „Auswählen“, KI ändert nur die Auswahl, Vorschau, ein Rückgängig-Schritt | Lovable | 1 |
| K2 | Text direkt ändern | Text in der Vorschau anklicken und ändern, ohne KI und ohne Kosten. | Klickweg mit erwartetem Ergebnis; Test: gespeicherte Daten enthalten den neuen Text. | vorhanden (T-0620); Doppelklick in „Auswählen“, gleiche Aktion wie die KI | Lovable | 1 |
| K3 | Skizze als Auftrag | Auf die Vorschau zeichnen, Skizze und Satz gehen gemeinsam an die KI. Ohne bildfähige KI steht dort ein klarer Hinweis. | Test: ohne bildfähige KI kein Fehler, Hinweis sichtbar. Test: Auftrag enthält das Bild. | fehlt | Lovable | 3 |
| K4 | Seite aus Text oder Bild | Aus einem Satz oder eigenem Bild entstehen bearbeitbare Editor-Daten statt fertiger Dateien. Ergebnis als Vorschau, Übernehmen als ein Schritt. Ungültige Antworten gelangen nicht in die Seite. | Test: ungültige KI-Antwort wird abgelehnt. Test: Ergebnis lässt sich im Editor öffnen und ändern. | vorhanden (T-0620); Editor-Daten aus Satz oder Bild, Vorschau, Übernehmen. Bild über echtes Claude und echte Bild-KI nicht live geprüft | Lovable, Onlook | 1 |
| K5 | Checkpoints und Varianten | Benannte Stände speichern und wiederherstellen, Varianten nebeneinander ausprobieren und eine übernehmen. Vor jeder KI-Änderung entsteht automatisch ein Stand. | Test: Wiederherstellen ergibt exakt den alten Inhalt. Test: Variante ändert das Original nicht. | vorhanden (T-0620); benannte und automatische Stände, Varianten mit Bildern nebeneinander | Onlook | 1 |
| K6 | Code und Design nebeneinander | Klick auf ein Element springt zur passenden Stelle. Änderungen im Code erscheinen im Editor und umgekehrt, oder Trendo sagt klar, was nicht abgeglichen werden kann. | Test: Änderung in einer Richtung erscheint in der anderen. Test: nicht abgleichbarer Code geht nicht verloren. | teilweise; Code-Editor vorhanden, kein Abgleich | Onlook | 3 |
| K7 | Web-Apps mit Daten | Datenbank und Anmeldung lokal zuerst (SQLite), Dienste wie Supabase nur mit eigenem Konto. Generierter Code läuft nur im Projektordner, Geheimnisse landen nie im Code. | Test: App speichert und lädt lokal. Test: Geheimnis im Code wird gemeldet. | fehlt nach Stichwortsuche | Bolt, Lovable | 3 |
| K8 | Veröffentlichen | Paket oder Hochladen mit eigenem Konto, nur nach Zustimmung im Einzelfall und nach bestandener Prüfung. Trendo meldet ehrlich, was hochgeladen wurde. | Test: Ohne Zustimmung findet kein Hochladen statt. | teilweise; Export und Zip, nichts wird hochgeladen | bolt.diy | 3 |

### KI-Nutzung: Techniken, Skills, Mods und Mechaniken

Alles Folgende soll Claude und lokale KI gleichermaßen nutzen können.

| ID | Funktion | Erwartetes Verhalten | Prüfung | Iststand | Quelle | Stufe |
|---|---|---|---|---|---|---|
| A1 | Editor-Aktionen als KI-Werkzeuge | Jede Editor-Funktion gibt es als benannte, geprüfte Aktion (Baustein einfügen, Element ändern, Ansicht anpassen, Gestaltungswert setzen, Animation setzen, Seite anlegen, Stand speichern). Die KI liefert einen Plan aus Aktionen statt rohem Code. Mensch und KI nutzen denselben Weg. | Test: Gleicher Plan ergibt das gleiche Ergebnis, egal welche KI ihn liefert. Test: unbekannte oder ungültige Aktion wird abgelehnt, nichts wird geschrieben. | vorhanden (T-0620); 11 Aktionen in `WebDesignActions`. Ziehen und Felder in Puck laufen weiter über Puck und `WebDesign.Validate`. „Ansicht anpassen“ wählt nur die Editor-Ansicht, Werte je Ansicht kommen mit W1 | Eigene Ableitung, `13_AI_AGENT_INTEGRATION.md` | 1 |
| A2 | Technik-Bibliothek | Verzeichnis moderner Web-Techniken mit Browser-Stand, Ersatzweg und Beispiel. Direkt nutzbar: Container Queries, Subgrid, View Transitions im Dokument, `:has()`, flüssige Schriftgrößen. Nur mit Ersatzweg: scrollgesteuerte Animationen, Anchor Positioning, Übergänge zwischen Seiten. Lokale KI wählt aus dem Verzeichnis. | Test: Jeder Eintrag hat Stand, Quelle und, wo nötig, Ersatzweg. Stichprobe gegen MDN. | teilweise; als Fließtext im Skill | Baseline-Angaben, Recherche Webdesign | 2 |
| A3 | Skills je Fachgebiet | Der vorhandene Web-Skill wird aufgeteilt: Layout und Typografie, Responsiv, Bewegung, Barrierefreiheit, SEO, Leistung, Sicherheit von Web-Apps. Trendo lädt passend zur Aufgabe und kürzt für lokale KI auf das Nötige. Eigene Formulierungen, fremde Skills nur mit Lizenz. | Test: Aufgabe zu Barrierefreiheit lädt diesen Skill. Test: Grenze für lokale KI wird eingehalten. | vorhanden (T-0620); Grundregeln plus 7 Fachgebiete, passend geladen, lokal höchstens 2400 Zeichen | Frontend-Design-Skill, `T-0317` | 1 |
| A4 | Mods | Erweiterungen bringen Bausteine, Aktionen oder Skills mit. Jede hat eine Beschreibung mit Rechten und Lizenz, läuft nur im Projektordner und wird nur nach Zustimmung installiert. Die KI sieht sie als weitere A1-Aktionen. | Test: Mod ohne Beschreibung oder Lizenz wird abgelehnt. Test: Mod kommt nicht aus dem Projektordner heraus. | noch zu prüfen | Figma- und Webflow-Erweiterungen, Puck | 3 |
| A5 | Mechaniken als feste Regeln | Stapel mit Abstand, „passt sich an“ oder „füllt aus“, Einrasten an Hilfslinien, Reihenfolge der Geräte-Ansichten, Überschreiben bei Komponenten. Der Editor erzwingt sie, damit KI und Mensch dasselbe Ergebnis bekommen und KI keine festen Pixelmaße erfindet. | Test: Stapel bleibt bei schmaler Breite im Rahmen. Test: Abstände stammen aus den Gestaltungswerten. | teilweise; Bereiche und Spalten vorhanden | Figma, Framer, Webflow | 2 |
| A6 | KI prüft ihr Ergebnis | Nach jeder Änderung: Bilder in drei Breiten, Prüfung auf Barrierefreiheit, Qualität, Konsole und defekte Links, dann gezielt nachbessern, mit Obergrenze an Runden. Mit Claude in der Stufe „Volle Funktionen“ auch über Playwright oder Chrome DevTools MCP, lokal über Trendos Vorschau. Bildbewertung durch KI gilt als Hinweis, nicht als Nachweis. | Test: eingebauter Fehler wird gefunden und gemeldet. Test: Obergrenze wird eingehalten. | vorhanden (T-0620); Export in echtem Chrome in 3 Breiten, Konsole, zu breiter Inhalt, Links, `AxeCheck`, `WebsiteQuality`, höchstens 3 Nachbesserungen. Playwright und Chrome DevTools MCP nicht eingebunden | Playwright MCP, Chrome DevTools MCP, `T-0316` | 1 |
| A7 | Passende KI je Aufgabe | Jede Aktion nennt, was sie braucht (Text, Bild, Code). Trendo nimmt Claude über das Abo, sonst eine lokale KI, und sagt klar, was gerade nicht geht. Nie die bezahlte API. | Test: ohne Claude läuft die Textaufgabe lokal. Test: Bildaufgabe ohne bildfähige KI ergibt einen Hinweis. | vorhanden (T-0620); Bedarf je Aktion, Claude über das Abo, sonst lokal, KI-Dienste mit API-Schlüssel ausgeschlossen. Bedarf „Code“ nutzt noch keine Aktion | `T-0316` | 1 |
| A8 | Gestaltungsrichtung zuerst | Vor dem Erzeugen schlägt die KI zwei bis drei deutlich verschiedene Richtungen vor (Stimmung, Schrift, Farbe, Aufbau) mit kleiner Vorschau. Erst nach der Wahl wird gebaut, damit Seiten nicht austauschbar wirken. | Test: Ohne Wahl wird nichts gebaut. Test: Richtungen unterscheiden sich in Schrift oder Farbe. | fehlt; 4 Stile vorhanden | Frontend-Design-Skill, oil-ui, Superdesign | 2 |

## Reihenfolge

1. **Stufe 1, KI-Grundlage:** A1, A3, A6, A7, K4, K1, K2, K5. Ohne A1 kann die KI den Editor nicht zuverlässig bedienen.
2. **Stufe 2, Design und Mechanik:** A5, W1, W6, W5, W4, A2, A8.
3. **Stufe 3, Inhalte und Web-Apps:** mehrere Seiten als Voraussetzung, dann W2, W3, K6, K3, A4, K7, K8.

## Noch offen

- Webflow, Framer und Lovable wurden nicht bedient, Teile stammen aus Sekundärquellen. Vor dem Bau je Funktion Handbuch oder Web-App vertiefen, soweit die Nutzungsbedingungen das erlauben.
- Browser-Stand der Techniken widerspricht sich in den Quellen. Vor Aufnahme in A2 gegen MDN oder caniuse prüfen.
- Die Einträge „fehlt“ und „teilweise“ beruhen vor allem auf Stichwortsuche und dem Status-Dokument. Vor Baubeginn je Funktion im Code und in der App nachprüfen.
- Stufe 3 enthält große Einzelthemen. K7 und A4 berühren die Sicherheit und brauchen eine Prüfung durch den Security-Agenten.
- `requirements.yaml` und `decisions.yaml` sind unverändert. Eine Übernahme braucht einen freigegebenen Auftrag.
