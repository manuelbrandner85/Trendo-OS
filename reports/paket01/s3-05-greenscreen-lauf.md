# Beleg S3-05 – Greenscreen und Maske auf der zweiten Bildspur (06.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~GreenscreenTests` mit `TRENDO_KEY_BELEG=<temp>/trendo-keybeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 1 s
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoSchnittPro_Ui` → Greenscreen blau + Maske rechts gesetzt und in der Karte wieder angezeigt (Failed 0)
- Eingaben: hintergrund.mp4 = rot 320×180, 2 s; greenscreen.mp4 = Grün 0x00FF00 mit blauem Quadrat (x 200–260, y 60–120)
- Export über den normalen Videoschnitt-Export (`VideoEdit.ExportArgs`), Greenscreen #00FF00, Toleranz 0,3

## Prüfung am fertigen Bild (Bild bei 1 s, RGB)
```
ergebnis (20,20) = 253,0,0
ergebnis (230,90) = 0,0,254
maske (230,90) = 253,0,0
```
- Positiv S3-05-P: Grün ist durchsichtig (Hintergrund rot sichtbar), die blaue Figur bleibt.
- Mit Maske „linke Hälfte“ verschwindet die Figur in der rechten Hälfte – dort ist wieder Rot.
- Negativ S3-05-N: Maske außerhalb des Bildes (X+Breite > 100 %, negative Lage, Breite 0) und Farbe nicht als #RRGGBB werden abgelehnt; die Einblendung bleibt unverändert (Test `Key_and_mask_args`).

## Dateien (SHA-256)
```
bb05cfd2f43418f0daef9a277fe5f8e1152a008341f86a196cb48f78307b0dde *ergebnis-maske.mp4
3a78a6ba16e2c3694f842c2d16d6b820bdccaf603fe3549ee53f6c65bdd216de *ergebnis.mp4
a5a5007c8b9a59ba1b3f508ad143640ab4f361f07b93f25e748ec54381ef5d7f *greenscreen.mp4
b6ddad934b5429ef3a97116017a593d5a10ee14cf188401f9cc5fd2955740027 *hintergrund.mp4
ba153d89dca7fa46e008e6a173d8930915c2f634ae208e7359e6fcc9fe4ab1bd *vergleich-ergebnis.png
3e64eda58529aff10d628175793ba4a8611bb929e81c7bfe868ee427630f2c75 *vergleich-maske.png
b5d1ec881f28f1c222c3f8e3ea29f4ba90a2a537184198f5045aa006f60c6cae *vergleich-quelle.png
```

## Nicht geprüft
- Rotoskopie und Bewegungs-Tracking der Maske (Maske ist fest, nicht animiert)
- Node-Graph-Oberfläche: Reihenfolge ist fest (Greenscreen → Maske → Größe → Deckkraft)
- echte Kameraaufnahmen mit ungleichmäßigem Grün und Spill-Unterdrückung
