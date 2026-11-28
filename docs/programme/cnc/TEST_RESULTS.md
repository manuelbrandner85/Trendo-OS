# CNC-Werkstatt – Testergebnisse

Studio: Fräsen & Fertigen (SPEC-14) · Bereich: Technik & Fachgebiete · Seite: `cnc` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 26 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Claude-Zugang: Seite lesen, Feld füllen, auswählen, Knopf drücken über Name oder Beschriftung, Geheimnisse bleiben draußen | ✓ bestanden |
| Fräsen UI: Werkstück in 3D zeigt den Abtrag mit Volumen, das Foto entsteht und erscheint, Zurück zur Bahnansicht geht | ✓ bestanden |
| SPEC-14 F04: Bögen R | ✓ bestanden |
| SPEC-14 F14: Nullpunkt verschieben | ✓ bestanden |
| SPEC-14 F15: Rechtecktasche | ✓ bestanden |
| SPEC-14 F16: Lochkreis | ✓ bestanden |
| SPEC-14 F17: Außenkontur | ✓ bestanden |
| SPEC-14 F18: Zerspanleistung | ✓ bestanden |
| SPEC-14 F19: Teilekosten | ✓ bestanden |
| SPEC-14 F20: Kernloch | ✓ bestanden |
| SPEC-14 UI: G-Code eintippen → Prüfung findet Fehler, Klick auf den Hinweis markiert die Zeile; Simulation über Regler und Abspielen | ✓ bestanden |
| SPEC-14 UI: Maße eintippen und Kontur erzeugen, Datei laden, Nullpunkt verschieben, Export .nc/SVG/Bericht, Rechner rechnen sofort | ✓ bestanden |
| SPEC-14: G-Code (G0/G1/G2/G3, G20/G91), Wege, Zeit, Grenzen, Warnungen, Schnittwerte | ✓ bestanden |
| Symbole: jede Karte unter „Alle Studios“ und jeder Fach-Reiter zeigt sein Studio-Symbol | ✓ bestanden |
| T-0747 UI: CNC – „Aus DXF/SVG …“ → Bahnen mit Versatz; Haltestege eintippen; Rückgängig; Rechtsklick-Menü; Export .nc | ✓ bestanden |
| T-0747 UI: Grundriss – „DXF einlesen …“ → Wände, Tür, Räume; Rückgängig; Wand ändern; Export DXF liest sich wieder | ✓ bestanden |
| T-0747-fix UI: Fräser-Ø/Vorschub geändert → Bahnen aus der Zeichnung neu (ein Schritt); geht es nicht, ist der Export gesperrt | ✓ bestanden |
| T-0747-fix UI: große DXF (12 000 Linien) im Hintergrund – Karte mit Abbrechen, Ergebnis als ein Schritt; Abbrechen ändert nichts | ✓ bestanden |
| T-0747-fix2 UI: Trendo CAD zeigt, dass eine Zoll-DXF in mm umgerechnet wurde | ✓ bestanden |
| T-0747-fix3 N4: Trendo CAD meldet eine DXF ohne Einheit („als mm gelesen“) | ✓ bestanden |
| T-0747-fix3 R1/N3: Wert während der Rechnung noch einmal geändert – keine Ausnahme, der letzte Wert gilt und die Bahnen passen dazu | ✓ bestanden |
| T-0747-fix3 R2: A einlesen, B einlesen, Ø ändern, Strg+Z während der Rechnung – G-Code passt danach zur Zeichnung im Dokument | ✓ bestanden |
| T-0747-fix3 R3: G-Code von Hand geändert, während „Bahnen neu“ rechnet – die Handänderung bleibt | ✓ bestanden |
| T-0747-fix4 N4: zweite Datei während eines laufenden Einlesens wird gemerkt und danach eingelesen | ✓ bestanden |
| T-0747-fix4 P2a: Fräsart in A gemerkt, dann A geschlossen – B bekommt nichts, keine Ausnahme | ✓ bestanden |
| T-0747-fix4 P2b: Fräsart in A gemerkt, dann Reiter B gewählt – die Änderung landet in A, B bleibt unverändert | ✓ bestanden |
