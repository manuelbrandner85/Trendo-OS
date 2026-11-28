# Tragwerk & Mechanik – Testergebnisse

Studio: Statik nachrechnen (SPEC-03) · Bereich: Technik & Fachgebiete · Seite: `technik` · Stand: 0.9.113 (Stand 1978f1a)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 26 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert | ✓ bestanden |
| Nachfrage: Wirkung nach außen fragt, Nein bricht ab und nennt den Grund; Bearbeiten fragt nicht | ✓ bestanden |
| Nutzerweg: Ein harmloser Plan läuft von selbst, bei offener Freigabe bleibt er stehen | ✓ bestanden |
| Nutzerweg: Mit ausgeschaltetem „ohne Klicks“ bleiben Plan, Vergleich und Behalten wie zuvor | ✓ bestanden |
| SPEC-03 F08: Hohlprofil | ✓ bestanden |
| SPEC-03 F09: Knicken (Euler) | ✓ bestanden |
| SPEC-03 F10: Schrauben | ✓ bestanden |
| SPEC-03 F11: Zug & Druck | ✓ bestanden |
| SPEC-03 F12: Torsion Welle | ✓ bestanden |
| SPEC-03 F13: Druckfeder | ✓ bestanden |
| SPEC-03 F14: Zahnräder | ✓ bestanden |
| SPEC-03 F15: Wärmedurchgang Wand | ✓ bestanden |
| SPEC-03 F16: Gewicht | ✓ bestanden |
| SPEC-03 UI: Eurocode einschalten – Ausnutzung am Stab, Last als ständig markieren senkt η, Nachweis als Bericht | ✓ bestanden |
| SPEC-03 UI: Rechner rechnen sofort bei der Eingabe (Wärmedehnung, Einheiten, Schraube, Zahnrad, Gewicht) | ✓ bestanden |
| SPEC-03 UI: Streckenlast zum Trapez machen, Temperatur am Stab, Setzung und Neigung am Lager – Ergebnis rechnet mit | ✓ bestanden |
| SPEC-03 UI: Tragwerk – Stab mit der Maus zeichnen (Länge tippen), Lager und Streckenlast klicken, Nachweis, Vorschlag übernehmen | ✓ bestanden |
| SPEC-03 UI: bewegliches Tragwerk wird gemeldet; Rahmen-Vorlage, Ergebnisanzeigen, PDF/SVG/CSV-Export | ✓ bestanden |
| SPEC-03: Balken (4 Lagerungen), Querschnitte, Wärmedehnung, Einheiten | ✓ bestanden |
| Statik UI: 3D-Ansicht zeigt das Tragwerk als Körper mit Überhöhung, zweiter Klick führt zur Zeichnung zurück | ✓ bestanden |
| T-0728 C3: Claude ändert über die Brücke in drei Zügen – am Ende des Laufs ist es EIN Rückgängig-Schritt „Claude-Auftrag“; Strg+Z nimmt alles zurück (Claude selbst ersetzt durch die Werkzeugaufrufe) | ✓ bestanden |
| T-0748 UI: Stufe Einfach – Gegenprobe steht im Inspektor, Bericht über „Datei ▸ Exportieren“ in der Menüleiste | ✓ bestanden |
| T-0748 UI: Tragwerk – Vorlage Einfeldträger zeigt „Gegenprobe ✓ Abweichung“; Rahmen ebenso; Export „Gegenprobe (Bericht)“ mit Tabellenformel | ✓ bestanden |
| T-0748-fix UI: Temperaturlast – Statuszeile sagt „nur Gleichgewicht geprüft“, nicht „✓ < 0,01 %“ | ✓ bestanden |
| T-0748-fix UI: großes Tragwerk – „Gegenprobe läuft …“, Rechnung im Hintergrund, neue Änderung bricht ab; danach ✓ in Inspektor und Statuszeile | ✓ bestanden |
| T-0748-fix2 UI: Einzellast bei 1/3 – Inspektor zeigt M = F·a·b/L = 22,2 kNm (vorher 21,7), Gegenprobe ✓, Bericht-Export nutzt das Hintergrund-Ergebnis | ✓ bestanden |
