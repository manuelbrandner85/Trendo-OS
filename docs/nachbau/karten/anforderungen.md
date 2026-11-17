# Karten & Einladungen – Anforderungen (Clean-Room)

Freigabe: 79 neue Studios am 09.11.2026, „Ja, weiter“ am 15.11.2026 (T-0671). Kategorie: Arbeiten & Büro. Id `STUDIO-CARDS-01`, Programm `karten-programm`.

## Vorbilder (nur Bedienkonzept, keine Grafiken, keine Vorlagen übernommen)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Canva | Vorlage wählen, Text auf der Seite anklicken und rechts gestalten | proprietär | canva.com |
| Greetings Island | eingebaute Zu-/Absagen (RSVP), Frist, Gästeliste | proprietär, Produktseite/App-Store | greetingsisland.com, apps.apple.com (Invitation Maker: Cards & RSVP) |
| Paperless Post | Gäste in Gruppen, Antworten bestätigen | proprietär | paperlesspost.com/blog/paperless-post-product-features |

Druckvorgaben: 3 mm Beschnitt, Schnittmarken außerhalb des Beschnitts, Falzmarke, TrimBox/BleedBox im PDF (übliche Druckerei-Vorgaben). Wiederverwendet: PDFsharp wie `Trendo.Core.Kreativ.PrintFile` (Beschnitt-Rechnung), Schriften über `PdfTools.EnsureFonts`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| CARDS-01 | Formate | Klappkarte A6 (Bogen A5 quer), Klappkarte A5, Postkarte, quadratische Klappkarte; Bögen: außen Rückseite\|Vorderseite, innen links\|rechts | Core `Cards_formats` | Druckpraxis | 1 |
| CARDS-02 | Vorlagen | Geburtstag, Hochzeit, Einladung, Danke, Weihnachten in jedem Format; Postkarte übernimmt Innentexte auf die Rückseite | Core `Cards_formats`, UI `Pages` | Canva | 1 |
| CARDS-03 | Seiten gestalten | Seite wählen (Vorschau), Text anklicken, Text/Schrift/Größe/Farbe/Ausrichtung/Höhe/fett/kursiv, neuer Text, Seitenfarbe, Bild (ganze Seite, obere Hälfte, eingepasst) | UI `Cards` | Canva | 1 |
| CARDS-04 | Platzhalter | {Name}, {Personen}, {Datum}, {Ort}, {AntwortBis} – in Vorschau und Druck eingesetzt | Core `Cards_serie_and_check`, UI `Cards` | Serienbrief | 1 |
| CARDS-05 | Gästeliste (RSVP) | schnell eintragen („Familie Muster, 4, ja“), Antwort offen/zugesagt/vielleicht/abgesagt, Zählung inkl. Personen, CSV hin und zurück (; oder ,) | Core `Cards_guests`, UI `Cards` | Greetings Island | 1 |
| CARDS-06 | Serienkarten | je Gast ohne Absage eine Karte mit Namen; ohne {Name} oder ohne Gäste klare Meldung | Core `Cards_serie_and_check`, `Cards_pdf`, UI `Cards` | Paperless Post | 1 |
| CARDS-07 | Druck-PDF | je Bogen eine Seite, Größe = Endformat + Beschnitt + 10 mm Markenrand, Hintergrund/Bild bis in den Beschnitt, Schnitt- und Falzmarken, TrimBox/BleedBox | Core `Cards_pdf` | Druckerei-Vorgaben | 1 |
| CARDS-08 | Prüfung | Text kleiner 7 pt, Kontrast < 3:1, Text am Rand, Bild unter 150 dpi, Bild fehlt, Platzhalter ohne Angabe, Postkarte mit Innenseiten, Beschnitt 0–10 mm | Core `Cards_serie_and_check`, `Cards_pdf`, UI `Cards` | WCAG-Kontrast, Druckpraxis | 1 |
| CARDS-09 | Nichts senden | Trendo druckt und verschickt nichts; Adapter ohne Nebenwirkung | Adapter-Test `Karte_Adapter` | Projektregel | 1 |

## Nicht gebaut (nächste Schritte)
- Antwort-Webseite für Gäste (bräuchte einen Dienst), Umschläge mit Anschrift, eigene Schriften, Ornamente/Rahmen.
