# T-0282 – Modell-Prüfer: Beispielmodell aus echten Möbeln statt Kisten

- Freigabe: Manuel im Chat am 31.10.2026 („alle 3D-Objekte … fotorealistisch und proportional“, „Ja, weiter“)
- `XrSample` neu: Leseecke aus den in Blender gebauten Katalogmöbeln (Sessel 80 × 85 × 90 cm, Zimmerpflanze 40 × 40 × 120 cm, Teppich 200 × 140 cm), Z oben in cm, auf dem Boden stehend, Sessel leicht zur Mitte gedreht, Lehne hinten. Vorher: Quader-Sessel, Quader-Tisch, Kugelleuchte, Y oben.
- Die drei GLB-Dateien liegen nur einmal unter `src/Trendo.App/Assets/moebel/` und werden in `Trendo.Core` als eingebettete Ressource verlinkt (keine Kopie).
- Modell-Prüfer: Datei heißt `beispiel-leseecke.glb`, Meldung nennt den Inhalt; Größe 2,00 × 1,15 × 1,40 m „plausibel“, 24 392 Dreiecke, 7 Materialien – alle Zielgeräte grün.
- Reality Check (Bildschirmfoto Modell-Prüfer): Proportionen, Standfläche, Schatten und Materialfarben stimmig; Sessel mit Holzbeinen und Polstern, Pflanze im Topf.
- Andere 3D-Programme geprüft: 3D-Studio, CAD, 3D-Druck haben keine kastigen Beispielmodelle (Teile baut der Nutzer selbst). Der Beispielraum im Scan-Programm (Boden, Wand, Kiste, Kugel) bleibt bewusst aus Grundformen – er prüft die Erkennung von Ebenen, Quadern und Kugeln in Punktwolken.
- Tests: Core 1 neu (Maße, Bodenkontakt, Lehne hinten, Farben, glTF-Höhe); App `SPEC04_Ui` angepasst (Dreiecke aus dem Modell statt fester Zahl, eigene feine Kugel für den Schwerlast-Test). Grün: SPEC04_Ui, Library3D_Ui, PlanPhoto_Ui, Shading_3D (12) und Core T028x/Xr/W6 (9).
