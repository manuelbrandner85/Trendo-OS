# Beleg S3-11 – LUT je Stück, Scopes und Farbvergleich (06.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~ColorScopesTests|FullyQualifiedName~VideoProTests|FullyQualifiedName~GreenscreenTests"` mit `TRENDO_LUT_BELEG=<temp>/trendo-lutbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     6, übersprungen:     0, gesamt:     6, Dauer: 871 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.ColorScopes_Ui` → Failed 0 (beschädigte LUT abgelehnt, Stück unverändert; Scopes mit Vergleich)
- Eingaben: szene.mp4 = testsrc2 320×180, 2 s; neutral.cube = Einheits-LUT 2³; falsch.cube = Farbkanäle vertauscht (R←B, G←R, B←G)

## Messung
```
ohne LUT: Stats { YAvg = 121,897, YMin = 26, YMax = 219, SatAvg = 107,426, HueAvg = 189,729 }
neutrale LUT: Stats { YAvg = 121,971, YMin = 27, YMax = 215, SatAvg = 106,279, HueAvg = 189,827 }
  → Farblich gleichwertig – Helligkeit 122,0/121,9, Sättigung 106,3/107,4, Farbton 190°/190°
falsche LUT: Stats { YAvg = 121,579, YMin = 20, YMax = 216, SatAvg = 106,335, HueAvg = 187,715 }
  → Mittelwerte: Farblich gleichwertig – Helligkeit 121,6/121,9, Sättigung 106,3/107,4, Farbton 188°/190°
  → bildgenau neutral: Bildgleich (SSIM 0,996, PSNR 43,8 dB).
  → bildgenau falsch: Nicht gleichwertig: SSIM 0,472, PSNR 8,6 dB – die LUT verändert das Bild deutlich.
Export mit falscher LUT: Quality { Ssim = 0,486835, Psnr = 8,545947 }
```

- Positiv S3-11-P: Scopes-Bild (Bild · Waveform · Vektorskop, 1440×270) entsteht mit der Farbe/LUT des Stücks; Export mit LUT läuft und die LUT wirkt im fertigen Film.
- Negativ S3-11-N: Die falsche LUT sieht in den Mittelwerten gleich aus – deshalb prüft Trendo bildgenau (SSIM/PSNR) und meldet „nicht gleichwertig“. Beschädigte, unvollständige oder 1D-LUTs und ICC-Profile werden abgelehnt, ohne das Stück zu ändern.

## Dateien (SHA-256)
```
8154f0bacd13dfc89a4493d71a01d66b6f5941284af809445974a9a9275dc7e1 *export.mp4
a2cea29158035b5dd74065480fce37931159fde579985587f0f77f2c48b97d58 *falsch.cube
ef8679c33aee4d841afe65e155e1d78cbce576309c2e40e22721f282a62a3cff *messung.txt
524dbe0d98d27b9f251af96dd299fde14535854dee4334f8fd22aca148432fc9 *neutral.cube
ffc343a8da884f6807526345f8a883f7fdfa11b1146a49bb958a47bbeeda64e8 *scopes.png
ae710a6514927b821a953bb95d835ab8a1524236f88d3fa5ebe453d6d0f4efa7 *szene.mp4
```

## Nicht geprüft
- ACES/OpenColorIO: nicht installiert; Trendo arbeitet mit .cube-LUTs (Resolve/Adobe), kein OCIO-Config
- HDR (PQ/HLG) und 10-Bit-Ausgabe
- echte Kamera-LUTs (Log → Rec.709) mit echtem Log-Material
