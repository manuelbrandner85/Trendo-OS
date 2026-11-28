# Tragwerk & Mechanik – Testplan

Studio: Statik nachrechnen (SPEC-03) · Bereich: Technik & Fachgebiete · Seite: `technik` · Stand: 0.9.114 (Stand 99cbb5c)

26 Tests sprechen dieses Programm an.

- Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert
- Nachfrage: Wirkung nach außen fragt, Nein bricht ab und nennt den Grund; Bearbeiten fragt nicht
- Nutzerweg: Ein harmloser Plan läuft von selbst, bei offener Freigabe bleibt er stehen
- Nutzerweg: Mit ausgeschaltetem „ohne Klicks“ bleiben Plan, Vergleich und Behalten wie zuvor
- SPEC-03 F08: Hohlprofil
- SPEC-03 F09: Knicken (Euler)
- SPEC-03 F10: Schrauben
- SPEC-03 F11: Zug & Druck
- SPEC-03 F12: Torsion Welle
- SPEC-03 F13: Druckfeder
- SPEC-03 F14: Zahnräder
- SPEC-03 F15: Wärmedurchgang Wand
- SPEC-03 F16: Gewicht
- SPEC-03 UI: Eurocode einschalten – Ausnutzung am Stab, Last als ständig markieren senkt η, Nachweis als Bericht
- SPEC-03 UI: Rechner rechnen sofort bei der Eingabe (Wärmedehnung, Einheiten, Schraube, Zahnrad, Gewicht)
- SPEC-03 UI: Streckenlast zum Trapez machen, Temperatur am Stab, Setzung und Neigung am Lager – Ergebnis rechnet mit
- SPEC-03 UI: Tragwerk – Stab mit der Maus zeichnen (Länge tippen), Lager und Streckenlast klicken, Nachweis, Vorschlag übernehmen
- SPEC-03 UI: bewegliches Tragwerk wird gemeldet; Rahmen-Vorlage, Ergebnisanzeigen, PDF/SVG/CSV-Export
- SPEC-03: Balken (4 Lagerungen), Querschnitte, Wärmedehnung, Einheiten
- Statik UI: 3D-Ansicht zeigt das Tragwerk als Körper mit Überhöhung, zweiter Klick führt zur Zeichnung zurück
- T-0728 C3: Claude ändert über die Brücke in drei Zügen – am Ende des Laufs ist es EIN Rückgängig-Schritt „Claude-Auftrag“; Strg+Z nimmt alles zurück (Claude selbst ersetzt durch die Werkzeugaufrufe)
- T-0748 UI: Stufe Einfach – Gegenprobe steht im Inspektor, Bericht über „Datei ▸ Exportieren“ in der Menüleiste
- T-0748 UI: Tragwerk – Vorlage Einfeldträger zeigt „Gegenprobe ✓ Abweichung“; Rahmen ebenso; Export „Gegenprobe (Bericht)“ mit Tabellenformel
- T-0748-fix UI: Temperaturlast – Statuszeile sagt „nur Gleichgewicht geprüft“, nicht „✓ < 0,01 %“
- T-0748-fix UI: großes Tragwerk – „Gegenprobe läuft …“, Rechnung im Hintergrund, neue Änderung bricht ab; danach ✓ in Inspektor und Statuszeile
- T-0748-fix2 UI: Einzellast bei 1/3 – Inspektor zeigt M = F·a·b/L = 22,2 kNm (vorher 21,7), Gegenprobe ✓, Bericht-Export nutzt das Hintergrund-Ergebnis
