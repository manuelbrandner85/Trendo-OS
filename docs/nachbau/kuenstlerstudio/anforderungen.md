# Künstlerstudio – Anforderungen (Clean-Room)

Freigabe: K1–K8 alle Ja am 15.11.2026. Kategorie: Kreativ & Medien.

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Grafiken)

| Vorbild | Warum | Lizenz / Zugang | Quelle (Stand 15.11.2026) |
|---|---|---|---|
| Krita | stärkste freie Pinsel-Engine, Zeichenhilfen (Fluchtpunkte, Spiegeln), ORA/PSD | GPL-3 → nur Vorbild, kein Code | docs.krita.org (Handbuch), toolradar.com/compare/rebelle-vs-krita |
| Procreate | einfachste Bedienung, Zeitraffer, StreamLine-Glätten, QuickShape | proprietär, nur Produktseite/Handbuch | procreate.com/handbook, softstribe.com/alternatives/procreate-alternatives |
| Clip Studio Paint | Lineal-/Perspektivwerkzeuge, Comics | proprietär | g2.com/compare/clip-studio-paint-vs-krita |
| Rebelle | Natur-Farbmischung (Aquarell, Öl) | proprietär | toolradar.com/compare/rebelle-vs-clip-studio-paint |

Dateiformat ORA nach der offenen OpenRaster-Spezifikation (openraster.org/baseline/file-layout-spec.html, layer-stack-spec.html): ZIP, `mimetype` zuerst und unkomprimiert (`image/openraster`), `stack.xml`, `data/*.png`, `mergedimage.png`, optional `Thumbnails/thumbnail.png`.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Stufe |
|---|---|---|---|---|---|
| K1-01 | Pinselstrich | Strich zwischen Punkten lückenlos (Abstand ≤ 25 % der Größe), weiche Kante, Deckkraft | Core-Test: Strich von (10,10) nach (90,10) färbt alle x dazwischen, Rand weicher als Mitte | Krita Handbuch „Brush Engines“ | 1 |
| K1-02 | Druckstärke | Stiftdruck steuert Größe und/oder Deckkraft; Maus = voller Druck | Core-Test: Druck 0,2 → schmaler als Druck 1,0 | Krita „Tablet settings“ | 1 |
| K1-03 | Pinselsatz | mindestens Bleistift, Tusche, Airbrush, Marker, Aquarell, Radierer mit eigenen Kennwerten | Core-Test: alle Voreinstellungen vorhanden; Radierer senkt Alpha | Procreate Pinselbibliothek | 1 |
| K1-04 | Ebenen | anlegen, löschen, verschieben, ausblenden, Deckkraft, sperren | Core-Test je Vorgang | Krita „Layers“ | 1 |
| K1-05 | Mischarten | Normal, Multiplizieren, Negativ multiplizieren, Überlagern, Aufhellen, Abdunkeln, Addieren | Core-Test mit bekannten Pixelwerten | Formeln nach W3C Compositing (frei) | 1 |
| K1-06 | Rückgängig | Strich und Ebenenänderungen rückgängig/wiederholen, mind. 50 Schritte | Core-Test | allgemein | 1 |
| K1-07 | Speichern/Export | PNG (flach), ORA (Ebenen, Namen, Deckkraft, Sichtbarkeit, Mischart) laden und speichern; PSD-Export flach mit Ebenen | Core-Test: ORA-Rundreise gleich; PNG lesbar; PSD-Kopf gültig | OpenRaster-Spezifikation | 1 |
| K2-01 | Farbrad | Farbton-Ring + Sättigung/Helligkeit, Hex-Eingabe, zuletzt benutzte Farben | UI-Test: Farbe wählen setzt Pinselfarbe | Procreate „Colors“ | 1 |
| K2-02 | Paletten | Palette anlegen, Farbe hinzufügen, speichern/laden (GPL-Palettenformat `.gpl`, offen) | Core-Test Rundreise | Krita „Palettes“ | 1 |
| K2-03 | Farbe aus Foto | aus einem Bild 5–8 Hauptfarben (k-Means) als Palette | Core-Test: Bild aus 3 Farbflächen → genau diese 3 Farben gefunden | eigenes Verfahren | 1 |
| K2-04 | Harmonien | Komplementär, Analog, Triade, Teiltriade | Core-Test: Triade = Farbton +120°/+240° | Farbenlehre allgemein | 1 |
| K3-01 | Symmetrie | senkrecht, waagrecht, beide, radial (n Teile) | Core-Test: Strich links → Spiegelpixel rechts | Krita „Mirror Tools“, Procreate „Drawing Guide“ | 1 |
| K3-02 | Perspektive | 1, 2, 3 Fluchtpunkte; Strich rastet auf Linie zum Fluchtpunkt ein | Core-Test: Punkt wird auf Linie projiziert | Krita „Assistant Tool“ | 1 |
| K3-03 | Raster/Lineal | Raster einblenden, gerade Linie mit Lineal, Umschalt = gerade Linie | Core-Test Lineal-Projektion | allgemein | 1 |
| K3-04 | Glätten | Stabilisierung der Eingabe (Stärke 0–100) | Core-Test: zittrige Punkte → geringere Abweichung | Procreate „StreamLine“ | 1 |
| K4-01 | Referenzbild | Bild als schwebende Referenz (nicht im Bild), Farbe daraus aufnehmen | UI-Test | Procreate „Reference“ | 2 |
| K4-02 | Posenpuppe | Pose aus dem 3D-Studio als Referenz-/Skizzenebene | UI-Test: Puppe rendert in Ebene | Krita/Clip-Studio 3D-Modell | 2 |
| K4-03 | Ideenskizze | lokale Bild-KI (ComfyUI) erzeugt Vorschlag als Referenz, nie als fertiges Bild | Test mit nachgebildeter KI: landet als Referenz | eigene Vorgabe | 2 |
| K5-01 | Zeitraffer | Strichfolge wird aufgezeichnet, als Bildfolge/Video an den Videoschnitt | Core-Test: n Bilder, letztes = Endbild | Procreate „Time-lapse“ | 2 |
| K6-01 | Skizze säubern | Linien aus Skizze herauslösen (Schwelle/Glätten) als neue Ebene | Core-Test: graue Skizze → schwarze Linienebene, Original bleibt | eigenes Verfahren | 2 |
| K6-02 | Vorfärben | geschlossene Flächen der Linienebene auf eigene Ebene füllen | Core-Test: geschlossener Kreis wird gefüllt | Clip Studio „Colorize“ (nur Idee) | 2 |
| K7-01 | Portfolio | ausgewählte Bilder → statische Portfolio-Webseite | Core-Test: index.html + Bilder | eigene Vorgabe | 3 |
| K7-02 | Druckdatei | Export in DPI-Größe (z. B. A4 300 dpi) mit Beschnitt | Core-Test: Maße stimmen | Druckvorgaben allgemein | 3 |
| K7-03 | Print on Demand | Übergabe an das POD-Studio | UI-Test | vorhandenes POD-Studio | 3 |
| K8-01 | Malkurse | geführte Lektionen mit Schritten, Prüfung je Schritt (z. B. „Ebene angelegt“) | Core-Test: Lektion schreitet bei erfüllter Bedingung fort | Procreate „Beyond the Basics“ (nur Idee) | 3 |
