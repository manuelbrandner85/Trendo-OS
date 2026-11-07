# Beleg S3-02 (Teil Multicam) – Kameras über den Ton synchronisieren und schneiden (07.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev (Ton ziehen), Synchronisation von Trendo (Kreuzkorrelation der Lautstärke-Hüllkurve, erst 10 ms, dann 1 ms genau)
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~MulticamTests` mit `TRENDO_MC_BELEG=<temp>/trendo-mcbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 1 s
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoMulticam_Ui` → Failed 0 (echte Kameradateien, 2 s Versatz gefunden, Wechsel 0:1, 3:2, 6:1 → drei Abschnitte, Ton von Kamera 1 durchgehend)

## Messung
```
Kamera B gefunden: 2,250 s später (Soll 2,250), Übereinstimmung 0,83
```
- Kamera ohne gemeinsamen Ton („fremd.mp4“) wird abgewiesen – Trendo rät nicht.
- Abschnitte, in denen eine Kamera noch nicht oder nicht mehr aufnimmt, und Wechsel, die nicht bei 0 s beginnen oder nicht aufsteigen, werden abgelehnt.

## Dateien (SHA-256)
```
e0562e8e355c00c62ef233f376cc598be8333a3557ef60b6a53e0ef1b45af5c8 *fremd.mp4
2c6e67847be1a814a7bd8ff4ad205d1d1ccc9901db7793441b6dbb5ad64a5b2b *kamera-a.mp4
cdccc7e66300d4c8842776b4dd8ef2eb23824477ae66a4f5bc342df68076b8f8 *kamera-b.mp4
```

## Nicht geprüft
- Mehr als zwei Kameras im echten Lauf (Weg ist gleich)
- Synchronisation über Timecode statt Ton; Kameras mit stark unterschiedlicher Bildrate
