# Kleidung & Produkte – Testplan

Studio: Kleidung & Produkte entwerfen (SPEC-08) · Bereich: Kreativ & Medien · Seite: `mode-programm` · Stand: 0.9.36 (Stand bee36a7)

19 Tests sprechen dieses Programm an.

- Bedienstufen: Einfach blendet Fachwerkzeuge aus, Erweitert zeigt alle, Wechsel lässt das Dokument unverändert; Einstellung bleibt gespeichert
- Profi-Stufe: Befehlssuche führt Befehle aus, Verlauf springt zurück, Projektdaten – falsches JSON wird abgelehnt, richtiges übernommen und rückgängig machbar
- Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente
- Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt
- SPEC-08 F10: Kindergrößen
- SPEC-08 F11: BH-Größe
- SPEC-08 F12: Ringgröße
- SPEC-08 F13: Textilkennzeichnung
- SPEC-08 F14: Gradierung
- SPEC-08 F15: Garnbedarf
- SPEC-08 F16: EAN-13
- SPEC-08 F17: Staffelpreise
- SPEC-08 F18: Produktdatenblatt
- SPEC-08 UI: Etikett sofort geprüft, Größensprung ändert die Größentabelle, Rechner (Schuh, Kind, BH, Ring, EAN, Staffel vom Ladenpreis) rechnen beim Tippen
- SPEC-08 UI: kein Rechner-Formular mehr; Schnittteil umbenennen/vergrößern/entfernen ändert Zuschnitt und Stoffbedarf; Rückgängig
- SPEC-08: Größen aus Maßen, Schuhgrößen, Stoffbedarf, Varianten, Preiskalkulation
- Schnitt-Editor: Basisgröße bearbeitbar, andere Größe abgeleitet und automatisch gelegt (größer bei XL), Stoffbedarf je Größe, SVG speichern und als Export
- Schnittlage UI: Teil ziehen speichert die Lage (Rückgängig), Doppelklick dreht, Überlappung wird gemeldet, automatisch legen räumt auf, PDF
- Schnittlage-Kern: automatisch ohne Überlappung im Stoff, mindestens so sparsam wie das alte Reihenlegen, Ausnutzung, zu breit = Fehler, PDF
