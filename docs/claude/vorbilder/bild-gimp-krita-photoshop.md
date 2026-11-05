# Vorbild-Analyse Bild: GIMP, Krita, Photoshop (Clean-Room)

- Programme: „Bildbearbeitung“, „Medien bearbeiten“
- Vorbilder: GIMP, Krita, Inkscape (frei), Adobe Photoshop, Affinity Photo (kostenpflichtig), Photopea (Funktionsumfang aus öffentlicher Doku)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 04.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Werkzeugleiste links, zweispaltig wie in Photoshop/GIMP (Verschieben, Pinsel, Radierer, Text, Formen, Zuschneiden, Vektorpinsel, Auswahl, Verlauf, Füllen, Reparieren, Klonen)
- ✓ Optionsleiste über der Fläche zeigt nur die Regler des gewählten Werkzeugs (Farbe, Pinselgröße, Deckkraft, Toleranz, kreisförmig, Maske rund)
- ✓ Ebenen mit Sichtbarkeit, Deckkraft, Reihenfolge, Mischmodi (Multiplizieren, Negativ multiplizieren, Ineinanderkopieren, Abdunkeln, Aufhellen, Differenz)
- ✓ Helligkeit, Kontrast, Sättigung, Unschärfe, Schwarzweiß, Sepia, Invertieren
- ✓ Tonwerte (Schwarzpunkt, Weißpunkt, Mitteltöne/Gamma) und Auto-Tonwerte mit 0,5 % Beschnitt (Photoshop „Auto-Tonwert“)
- ✓ Kurven als fertige Vorlagen (S-Kurve, Aufhellen, Abdunkeln, Matt) – monotone Kurve ohne Überschwinger
- ✓ Auswahl Rechteck; daraus Ebenenmaske eckig oder rund mit weicher Kante, umkehren, entfernen
- ✓ Verlauf linear oder kreisförmig (Farbe → durchsichtig) als eigene Ebene
- ✓ Füllen (Farbeimer) mit Toleranz, füllt nur die zusammenhängende Fläche
- ✓ Reparieren (Bereichsreparatur): Fleck, Kratzer, Staub werden aus der Umgebung aufgefüllt
- ✓ Klonen (Kopierstempel): erst Quelle anklicken, dann übermalen
- ✓ Retusche nicht-destruktiv auf einer eigenen Ebene, jeder Schritt einzeln rückgängig; Original bleibt unverändert
- ✓ Export PNG/JPG/SVG, SVG öffnen
- ✓ Freie KIs: Bilder verbessern, Bilder erzeugen
- ✓ Auswahl-Gruppe wie in Photoshop (05.11.2026): Rechteck, Ellipse, Lasso (freihändig) und Zauberstab (Farbfläche mit Toleranz, schaut auf das sichtbare Gesamtbild) – umkehrbar, weiche Kante, als Ebenenmaske oder zum Füllen
- ✓ Inhaltsbasiert füllen: Auswahl wird aus der Umgebung neu aufgebaut – Farbverlauf vom Rand, Feinstruktur aus der am besten passenden Nachbarstelle (ohne KI, lokal, rückgängig machbar)
- ✓ Kurven mit frei ziehbaren Punkten (Klick setzt, Rechts-/Doppelklick entfernt), Histogramm dahinter; Vorlagen bleiben als Startpunkt
- ◐ Generative Füllung per KI (Photoshop Generative Fill) – nur über die freie KI „Bilder verbessern“

## Bedienkonzept (Profi-Bauplan A1–A3)
Bauplan erfüllt (Werkzeuge links, Optionsleiste über der Fläche, Leinwand, Ebenen und Eigenschaften rechts).

## Nächste Schritte
1. Auswahl hinzufügen/abziehen (Umschalt/Alt wie in Photoshop)
2. Generative Füllung mit lokaler Bild-KI, sobald ein Modell installiert ist
