# Tragwerk & Mechanik – Arbeitsabläufe

Studio: Statik nachrechnen (SPEC-03) · Bereich: Technik & Fachgebiete · Seite: `technik` · Stand: 0.9.113 (Stand 1978f1a)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- SPEC-03 UI: Eurocode einschalten – Ausnutzung am Stab, Last als ständig markieren senkt η, Nachweis als Bericht
- SPEC-03 UI: Rechner rechnen sofort bei der Eingabe (Wärmedehnung, Einheiten, Schraube, Zahnrad, Gewicht)
- SPEC-03 UI: Streckenlast zum Trapez machen, Temperatur am Stab, Setzung und Neigung am Lager – Ergebnis rechnet mit
- SPEC-03 UI: Tragwerk – Stab mit der Maus zeichnen (Länge tippen), Lager und Streckenlast klicken, Nachweis, Vorschlag übernehmen
- SPEC-03 UI: bewegliches Tragwerk wird gemeldet; Rahmen-Vorlage, Ergebnisanzeigen, PDF/SVG/CSV-Export
- Statik UI: 3D-Ansicht zeigt das Tragwerk als Körper mit Überhöhung, zweiter Klick führt zur Zeichnung zurück
- T-0748 UI: Stufe Einfach – Gegenprobe steht im Inspektor, Bericht über „Datei ▸ Exportieren“ in der Menüleiste
- T-0748 UI: Tragwerk – Vorlage Einfeldträger zeigt „Gegenprobe ✓ Abweichung“; Rahmen ebenso; Export „Gegenprobe (Bericht)“ mit Tabellenformel
- T-0748-fix UI: Temperaturlast – Statuszeile sagt „nur Gleichgewicht geprüft“, nicht „✓ < 0,01 %“
- T-0748-fix UI: großes Tragwerk – „Gegenprobe läuft …“, Rechnung im Hintergrund, neue Änderung bricht ab; danach ✓ in Inspektor und Statuszeile
- T-0748-fix2 UI: Einzellast bei 1/3 – Inspektor zeigt M = F·a·b/L = 22,2 kNm (vorher 21,7), Gegenprobe ✓, Bericht-Export nutzt das Hintergrund-Ergebnis
