# T-0281 – 3D-Vorschau: Licht, Schatten, Kontaktverdunklung; Modell-Prüfer zeigt Z oben

- Freigabe: Manuel im Chat am 31.10.2026 („alle 3D-Objekte fotorealistisch …“, „Ja, weiter“)
- `Shading` (neu): Licht linear gerechnet – Hauptlicht vorn links oben (warm, schräg zur üblichen Kamera, damit Schatten sichtbar fallen), Himmel/Boden-Umgebung je Flächenneigung, kühles Fülllicht, feiner Glanz, Kontaktverdunklung zum Boden hin, weiche Kompression der Lichter, sRGB.
- Modell-Prüfer (`MeshView`): **Fehler behoben** – Trendo-Netze haben Z oben, die Ansicht zeigte Y oben (Möbel lagen auf dem Rücken; das kastige Beispielmodell war selbst falsch herum gebaut und fiel deshalb nicht auf). Neu: Studio-Boden, weicher Bodenschatten (Halbschatten aus drei leicht versetzten Lichtrichtungen, vereinfachtes Netz, deckend in einer Deckkraft-Ebene), neue Lichtrechnung.
- CAD und Grundriss-3D (`CadView`): neue Lichtrechnung, Kontaktverdunklung, Schlagschatten auf den Boden (Wände auf Böden, Möbel, Bauteile); Bodenflächen werden vor den Schatten gezeichnet.
- Modell-Prüfer-Größe: **Fehler behoben** – Maße mehrteiliger Modelle kamen aus den Rohgrenzen der Teile ohne Knoten-Verschiebung (Sofa 2,20 × 0,65 × 0,90 m statt 2,20 × 0,85 × 0,90 m); jetzt aus dem zusammengesetzten Modell.
- Funktionskataloge: vier Verweise auf seit T-0262/T-0265/T-0267 ersetzte Tests (SPEC-06 F06, SPEC-10 F08, SPEC-15 F01/F09) auf die heutigen Tests umgestellt – Funktionen bestehen weiter und werden geprüft.
- Lehrreich: eine Geometrie mit 17 000 Teilfiguren wurde nicht gefüllt (nur die Kontur) und Ober-/Unterseiten heben sich bei NonZero auf – daher Dreieck für Dreieck in einer Deckkraft-Ebene.
- Tests: Core 1 (Sofa-Größe), App 3 (Licht, Schattenwurf, Z oben im Modell-Prüfer); betroffene App-Klassen (CAD, Grundriss, Bogen, Modell-Prüfer, Bibliothek, Foto: 18) und Core (W6, Xr, T-0278…T-0281) grün.
