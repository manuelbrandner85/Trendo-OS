# CNC-Werkstatt – Arbeitsabläufe

Studio: Fräsen & Fertigen (SPEC-14) · Bereich: Technik & Fachgebiete · Seite: `cnc` · Stand: 0.9.114 (Stand 99cbb5c)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- Fräsen UI: Werkstück in 3D zeigt den Abtrag mit Volumen, das Foto entsteht und erscheint, Zurück zur Bahnansicht geht
- SPEC-14 UI: G-Code eintippen → Prüfung findet Fehler, Klick auf den Hinweis markiert die Zeile; Simulation über Regler und Abspielen
- SPEC-14 UI: Maße eintippen und Kontur erzeugen, Datei laden, Nullpunkt verschieben, Export .nc/SVG/Bericht, Rechner rechnen sofort
- T-0747 UI: CNC – „Aus DXF/SVG …“ → Bahnen mit Versatz; Haltestege eintippen; Rückgängig; Rechtsklick-Menü; Export .nc
- T-0747 UI: Grundriss – „DXF einlesen …“ → Wände, Tür, Räume; Rückgängig; Wand ändern; Export DXF liest sich wieder
- T-0747-fix UI: Fräser-Ø/Vorschub geändert → Bahnen aus der Zeichnung neu (ein Schritt); geht es nicht, ist der Export gesperrt
- T-0747-fix UI: große DXF (12 000 Linien) im Hintergrund – Karte mit Abbrechen, Ergebnis als ein Schritt; Abbrechen ändert nichts
- T-0747-fix2 UI: Trendo CAD zeigt, dass eine Zoll-DXF in mm umgerechnet wurde
