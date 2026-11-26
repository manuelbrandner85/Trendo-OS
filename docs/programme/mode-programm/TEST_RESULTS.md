# Kleidung & Produkte – Testergebnisse

Studio: Kleidung & Produkte entwerfen (SPEC-08) · Bereich: Kreativ & Medien · Seite: `mode-programm` · Stand: 0.9.110 (Stand 0eb9d52)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 46 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 1

| Test | Ergebnis |
|---|---|
| E11: Studio-Übersicht mit einem Suchfeld (Tippen filtert, Enter sucht überall); Hilfetext zu Strg+K stimmt | ✓ bestanden |
| E12: „Einfach“ blendet rechte Spalte, Erdkugel und Schnellaktionen aus (einblendbar unter „Anpassen“); Einstellungen zeigen die Hauptseiten, Rest unter „Mehr“ | ✓ bestanden |
| E1: Ein Knopf „KI ▾“ neben „Los“ – Claude, Markierung, kie.ai und Hinzufügen im Klappmenü; kein zweites Claude im Programmband | ✓ bestanden |
| E2: „Einfach“ klappt Stufenwahl, Eigenschaften und Rand-Schalter ein; „Mehr Werkzeuge“ holt sie, die Stufe bleibt im „Mehr“-Menü wählbar | ✓ bestanden |
| E3: Automatisch speichern – unverändert keine Datei, nach einer Änderung im Studio-Ordner, danach bei jeder Änderung; Anzeige „Gespeichert“; Schließen verliert nichts | ✓ bestanden |
| E4 + E5: Speichern unter und Öffnen mit der Dateiauswahl; „Weitermachen“ öffnet das zuletzt bearbeitete Dokument – auf der Startansicht und auf dem Desktop | ✓ bestanden |
| E6: „Was möchtest du machen?“ startet die Aufgabe – fehlt eine Angabe, fragt Trendo kurz und startet dann mit der Antwort | ✓ bestanden |
| E7: Fehler im Programm als Alltagssatz mit „Was tun?“-Knopf und aufklappbarem technischem Text; Benachrichtigung mit ausdrücklichem Ton | ✓ bestanden |
| E8: Kurze Einführung beim ersten Öffnen (drei Schritte, überspringbar, gemerkt) und über „Einführung“ wieder abrufbar | ✓ bestanden |
| E9 + E10: Eine Wahl „Einfach / Erweitert / Profi“ (größere Knöpfe getrennt); Alltagswörter in der linken Leiste mit Fachwort als Hinweis | ✓ bestanden |
| Gegenprüfung T-0745 (1, 4): Ecke genau auf die Nachbarecke gesetzt wird mit Erklärung abgelehnt; Export nennt sich überschneidende Teile | ✓ bestanden |
| Gegenprüfung T-0745 (2): keine Tastatur-Falle – ohne Auswahl wandert Tab aus der Fläche; nach Enter im Zahlenfeld bleibt der Fokus im Feld | ✓ bestanden |
| Gegenprüfung T-0745 (7): Pfeil-Schritte am selben Punkt innerhalb 1 s sind EIN Rückgängig-Schritt; Pause oder andere Änderung trennt | ✓ bestanden |
| Kleidung: Anprobe schreibt einen vollständigen Auftrag, zeigt Bilder; Fehler von Blender werden gemeldet | ✓ bestanden |
| Kleidung: Farbe aus der Palette und eigene Farbe; Muster mit Größe und zweiter Farbe landen im Auftrag; falsche Farbe wird gemeldet | ✓ bestanden |
| Kleidung: Produktionsdateien werden geschrieben | nicht im Lauf |
| Kleidung: Schnittteile mit Kurven, Zuschnitt und Kosten erscheinen; Stoff und Schnitt lassen sich wechseln | ✓ bestanden |
| Profi-Stufe: Befehlssuche führt Befehle aus, Verlauf springt zurück, Projektdaten – falsches JSON wird abgelehnt, richtiges übernommen und rückgängig machbar | ✓ bestanden |
| Programm: Startseite → Vorlage → bearbeiten → Rückgängig → Speichern → Zuletzt → wieder öffnen; Reiter für zwei Dokumente | ✓ bestanden |
| Programm: ungespeichertes Dokument wird automatisch gesichert und auf der Startseite wiederhergestellt | ✓ bestanden |
| SPEC-08 F10: Kindergrößen | ✓ bestanden |
| SPEC-08 F11: BH-Größe | ✓ bestanden |
| SPEC-08 F12: Ringgröße | ✓ bestanden |
| SPEC-08 F13: Textilkennzeichnung | ✓ bestanden |
| SPEC-08 F14: Gradierung | ✓ bestanden |
| SPEC-08 F15: Garnbedarf | ✓ bestanden |
| SPEC-08 F16: EAN-13 | ✓ bestanden |
| SPEC-08 F17: Staffelpreise | ✓ bestanden |
| SPEC-08 F18: Produktdatenblatt | ✓ bestanden |
| SPEC-08 UI: Etikett sofort geprüft, Größensprung ändert die Größentabelle, Rechner (Schuh, Kind, BH, Ring, EAN, Staffel vom Ladenpreis) rechnen beim Tippen | ✓ bestanden |
| SPEC-08 UI: kein Rechner-Formular mehr; Schnittteil umbenennen/vergrößern/entfernen ändert Zuschnitt und Stoffbedarf; Rückgängig | ✓ bestanden |
| SPEC-08: Größen aus Maßen, Schuhgrößen, Stoffbedarf, Varianten, Preiskalkulation | ✓ bestanden |
| Schnitt-Editor: Basisgröße bearbeitbar, andere Größe abgeleitet und automatisch gelegt (größer bei XL), Stoffbedarf je Größe, SVG speichern und als Export | ✓ bestanden |
| Schnittlage UI: Teil ziehen speichert die Lage (Rückgängig), Doppelklick dreht, Überlappung wird gemeldet, automatisch legen räumt auf, PDF | ✓ bestanden |
| Schnittmuster: ohne Einrichtung Einrichten-Knopf; mit FreeSewing → T-Shirt-Schnitt als SVG mit Maßen in mm, Herrengröße, ergänzte Maße auf Deutsch genannt | ✓ bestanden |
| T-0620: Von selbst angelegte Datei folgt dem neuen Titel (samt Versionen); selbst benannte Dateien bleiben, wie sie sind | ✓ bestanden |
| T-0680 Absturz, gespeichertes Dokument: Die neuere Notsicherung neben der Datei wird beim Öffnen angeboten (Wiederherstellen / Verwerfen, mit Zeitpunkt); Wiederherstellen bringt die Änderung zurück und lässt sich rückgängig machen | ✓ bestanden |
| T-0680 Absturz, nie gespeichert: Dokument ändern, Sicherung läuft, Fenster stürzt ab – das neue Fenster bietet „Wiederherstellen“ mit Zeitpunkt an und bringt die Änderung zurück; die Sicherung verschwindet erst nach dem Speichern | ✓ bestanden |
| T-0680 Beschädigte Datei: verständliche Meldung statt leerem Dokument, der letzte gesicherte Stand wird angeboten und geöffnet; eine Datei aus einer neueren Fassung wird mit Hinweis abgelehnt; kaputte Sicherungen auf der Startseite werden gemeldet | ✓ bestanden |
| T-0680 Verlust nach Absturz gemessen: Mit automatischem Speichern steht eine Änderung nach der kurzen Pause von selbst in der Datei; das neue Fenster macht dort weiter | ✓ bestanden |
| T-0680 Von außen geändert: Weder das automatische Speichern noch „Speichern“ überschreibt – es wird gefragt (meine speichern / als Kopie / die andere laden); die andere Fassung bleibt als Version | ✓ bestanden |
| T-0680 Zweites Fenster: dieselbe Datei öffnet dort nur zum Lesen – nichts wird still überschrieben; angeboten wird „Nur lesen“ oder „Als Kopie“; nach dem Schließen im ersten Fenster ist sie frei | ✓ bestanden |
| T-0745: Eckpunkt mit Naht lässt sich nicht löschen (Hinweis statt Absturz); Teil zurücksetzen holt die Vorlage; Profi setzt Punkte genau per Zahl | ✓ bestanden |
| T-0745: Einfach bearbeitet mit Maus und Rechtsklick ohne Leiste, zeigt Maße; Profi zeigt Leiste, Nahtvergleich und Zahlenfelder | ✓ bestanden |
| T-0745: Klick bis Datei – bearbeiteter Saum landet in DXF, SVG, PDF, Datenblatt und im Anprobe-Auftrag | ✓ bestanden |
| T-0745: Teil anklicken, Saumecke ziehen, Doppelklick setzt Punkt, K macht eine Kurve, Entf löscht, Strg+Z nimmt je Schritt zurück – Maße und Nahtzugabe folgen | ✓ bestanden |
| V-08: Speicherstand nur an einer Stelle und verständlich – „Nicht gespeichert –“ mit Knopf „Speichern“ (ohne automatisches Speichern), „Wird automatisch gespeichert“, „Gespeichert um HH:MM“ | ✓ bestanden |
