# Präsentationen gestalten – Anforderungen (Clean-Room)

Studio `STUDIO-DECKDESIGN-01`, Seite `praesentation-gestalten-programm`, Kategorie Arbeiten & Büro. Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0673).

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Vorlagen, keine Grafiken)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Beautiful.ai | „Smart Slides“: Layout ordnet, skaliert und richtet sich beim Tippen selbst aus; Themen mit Schrift, Farben, Logo | proprietär, nur Produktseiten | beautiful.ai/smart-slides, beautiful.ai/pro |
| Gamma | Gliederung/Notizen rein, gestaltetes Deck raus; Karten statt freier Fläche | proprietär | gamma.app (Produktseite) |
| Pitch | Design-Themen, Sprechernotizen, Zusammenarbeit | proprietär | pitch.com (Produktseite) |
| PowerPoint Designer | schlägt je Inhalt Layouts vor (Zeitleiste aus Daten, Bild + Text) | proprietär | support.microsoft.com „Designer“ |

## Grundsatz: kein zweites Präsentationsformat
Das Studio speichert nur die Gliederung und die Gestaltungswahl (Design, Farben, Schrift, Folienart je Folie, Regeln, Redezeit). Erzeugt wird immer ein `SlideDoc` des vorhandenen Kerns von „Trendo Präsentation“ (`src/Trendo.Core/Office/SlideDoc.cs`). PPTX über `SlideFiles`, PDF über `SlidePdf`; „In Trendo Präsentation bearbeiten“ übergibt dasselbe `SlideDoc`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| DD-01 | Gliederung lesen | Zeile = Folie, „- “/Einrückung = Punkt, „> “/„Notiz:“ = Sprechernotiz, „Bild:“ = Bild, „[Zitat]“ = Folienart fest | Core `DeckDesignTests.Parse_outline` | Gamma (Notizen → Deck) | 1 |
| DD-02 | Folienart je Inhalt | Titel, Abschnitt, Stichpunkte, Zwei Spalten, Bild + Text, Zitat, Kennzahlen, Zeitleiste automatisch | Core `Layout_by_content` | PowerPoint Designer, Beautiful.ai Smart Slides | 1 |
| DD-03 | Regeln | höchstens 6 Punkte je Folie (einstellbar), Rest auf „(Forts.)“-Folien; Punkte über 90 Zeichen an Satzgrenzen geteilt, sonst gekürzt – voller Text in die Notizen | Core `Rules_split` | Beautiful.ai (Gestaltungsregeln) | 1 |
| DD-04 | Design-Themen mit Kontrast-Prüfung | 10 Themen (Farben, Schrift, Hintergründe); WCAG-Kontrast Text ≥ 4,5 : 1, Titel/Akzent ≥ 3 : 1; „Kontrast reparieren“ | Core `Contrast`, UI `Theme_and_contrast` | WCAG 2.x (frei) | 1 |
| DD-05 | Schrift passt sich an | größte Schrift, die passt, nie unter 14 pt; Hinweis, wenn auch 14 pt nicht reicht | Core `Fit_size` | Beautiful.ai | 1 |
| DD-06 | Sprechernotizen und Zeitplan | eigene Notizen bleiben, fehlende werden vorgeschlagen; Redezeit je Folie aus Inhalt geschätzt bzw. auf Gesamtzeit verteilt, Startzeiten „m:ss“ | Core `Notes_and_schedule` | Pitch (Notizen), Redecoach-Praxis | 1 |
| DD-07 | Export | PPTX (vorhandener Weg), PDF, Handzettel-PDF mit Notizen, Folienplan als Text | Core `Export_pptx_pdf`, UI `Build_export_handover` | PowerPoint | 1 |
| DD-08 | Von Hand ändern | Folienart je Folie wählen/zurücksetzen; Gliederung aus offener Präsentation holen | Core `Overrides_and_roundtrip`, UI `Layout_override` | PowerPoint Designer | 1 |
| DD-09 | Übergabe | „In Trendo Präsentation bearbeiten“ öffnet die gestalteten Folien dort | UI `Build_export_handover` | – | 1 |
| DD-10 | KI-Entwurf | Thema → Gliederung über den vorhandenen KI-Kern (`Intel.AskAsync`), im Test ersetzt; Ergebnis als prüfbarer Vorschlag | UI `Ai_draft` | Gamma | 2 |
| DD-11 | KI-Adapter | „Gliederung:“ → PPTX + Folienplan im Projekt, ohne Nebenwirkung | Core `Adapter` | Trendo-Kette | 1 |
| DD-12 | Bilder suchen/erzeugen | ◐ nicht gebaut – Bilder kommen per „Bild:“ aus eigenen Dateien | – | Gamma/Beautiful.ai | 3 |

## Bewusst nicht gebaut
- Keine Online-Bildsuche, kein Teilen-Link, keine Zusammenarbeit in Echtzeit, kein Hochladen.
- Keine Animationen über das hinaus, was der Präsentationskern kann (Überblenden zwischen Folien).
