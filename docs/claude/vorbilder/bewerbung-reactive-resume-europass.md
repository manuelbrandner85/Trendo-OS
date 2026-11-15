# Vorbild-Analyse Bewerbung: Reactive Resume, Europass, Canva-Lebenslauf (Clean-Room)

- Programme: „Bewerbung & Lebenslauf“
- Vorbilder: Reactive Resume (MIT, trotzdem nur Bedienkonzept), Europass-Lebenslauf (EU), Canva-Lebenslauf (proprietär)
- Werkzeug: Funktionsumfang aus öffentlicher Doku, README und Produktseiten; kein fremder Code, keine fremden Vorlagen-Grafiken. Anforderungen mit Prüfungen: `docs/nachbau/bewerbung/anforderungen.md`. Stand 15.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Lebenslauf als strukturierte Daten (Person, Stationen, Ausbildung, Kenntnisse, Sprachen mit GER-Stufe, Interessen)
- ✓ Editor links, Live-Vorschau rechts; drei Layouts (klassisch, modern mit Seitenleiste, kompakt), Akzentfarben, Foto
- ✓ Export PDF (eigener PDFsharp-Satz mit Seitenumbruch), Word (über „Trendo Text“), ATS-Textfassung
- ✓ Prüfung: Kontaktdaten, Datumsformat, Ende vor Beginn, Lücken ab 3 Monaten, Länge, GER-Stufen
- ✓ Stichwort-Abgleich mit der Stellenanzeige (schon drin / fehlt noch)
- ✓ Anschreiben: eigener Entwurf ohne KI oder mit dem vorhandenen KI-Kern (nichts erfinden), DIN-5008-Brief, Übergabe an „Briefe schreiben“
- ✓ Bewerbungsmappe als eine PDF (Deckblatt, Anschreiben, Lebenslauf, Anlagen aus PDF oder Bild)
- ✓ Übersicht der Bewerbungen mit Status, Fristen, Nachfassen und CSV
- ◐ Reihenfolge der Abschnitte fest (Stationen einzeln verschiebbar), keine frei ziehbaren Abschnitte
- ✗ Import (JSON Resume, LinkedIn), Lebenslauf auf Englisch, öffentliche Lebenslauf-Webseite

## Bedienkonzept (Profi-Bauplan A1–A3)
Reiter oben: Lebenslauf · Anschreiben · Mappe · Übersicht. Links die Eingabe in Karten, rechts die A4-Vorschau wie das PDF. Eigenschaften rechts: Layout, Akzentfarbe, Prüfung, Fristen. Menüband: Lebenslauf, Anschreiben, Mappe, Übersicht.

## Nächste Schritte
1. Abschnitte frei anordnen und ausblenden (wie Reactive Resume)
2. Import aus JSON Resume
3. Zweite Sprache des Lebenslaufs (Englisch) für Bewerbungen ins Ausland
