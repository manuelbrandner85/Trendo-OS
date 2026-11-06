# Beleg S3-12 – Bild verbessern mit Messung (06.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~VideoEnhanceTests|FullyQualifiedName~FilmDeliveryTests"` mit `TRENDO_ENH_BELEG=<temp>/trendo-enhbeleg` → Exitcode exit 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     6, übersprungen:     0, gesamt:     6, Dauer: 2 s
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoEnhance_Ui` → Total 1, Failed 0 (FFmpeg ersetzt: berechnen, verwenden, rückgängig; Artefakte ohne Übernehmen)
- Eingaben: sauber.mp4 = testsrc2 320×180, 25 fps, 2 s; verrauscht.mp4 = sauber + noise=alls=28:allf=t; kaputt-gross.mp4 = 2× Nachbarpixel + starkes Rauschen + Überschärfen

## Messung (SSIM/PSNR gegen das saubere Original)
```
vorher (verrauscht gegen sauber): Quality { Ssim = 0,40558, Psnr = 24,32707 }
nachher (entrauscht gegen sauber): Quality { Ssim = 0,418883, Psnr = 24,693886 }
Urteil: Verdict { Light = gruen, Text = Messbar besser: SSIM 0,406 → 0,419, PSNR 24,3 → 24,7 dB, MayApply = True }
Hochskaliert: Verdict { Light = gelb, Text = Hochskaliert ohne erkennbare Artefakte (Rückrechnung SSIM 0,983, PSNR 38,2 dB) – mehr Pixel heißt nicht mehr Details, bitte selbst vergleichen., MayApply = True }
Kaputt hochskaliert: Verdict { Light = rot, Text = Artefakte beim Hochskalieren (Rückrechnung SSIM 0,299, PSNR 17,2 dB) – nicht übernommen., MayApply = False }
```

## Positivfall S3-12-P
Entrauschen (hqdn3d, Stärke 0,8) macht den verrauschten Clip gegen das saubere Original messbar besser – Urteil grün.

## Negativfall S3-12-N
Absichtlich kaputtes Hochskalieren: Rückrechnung auf 320×180 weicht stark ab → rot, nicht übernommen. Sauberes 2×-Hochskalieren (Lanczos) bekommt nur gelb („mehr Pixel heißt nicht mehr Details“), nie „besser“.

## Dateien (ffprobe, SHA-256)
```
sauber.mp4  h264|320|180|25/1 2.000000 
verrauscht.mp4  h264|320|180|25/1 2.000000 
entrauscht.mp4  h264|320|180|25/1 2.000000 
gross.mp4  h264|640|360|25/1 2.000000 
kaputt-gross.mp4  h264|640|360|25/1 2.000000 
5538202f5f36ad261966c157a94d6009e3858f65e7d2171e63924d20347bf005 *entrauscht.mp4
f29c79c7ebc2d04e4b52bfe7202c6694cbff21c492db9f81972b5ea507d7b3c6 *gross.mp4
a32a387379c2636631bb2ca5b16a25241d5b0a9c8d7fd7a00e3e1e3f0c45c570 *kaputt-gross.mp4
1aa483dcfd3ebee84aa16500ea5dbb5ad912d2a31a1701644baeec7be1877737 *sauber.mp4
f379159ec0cd1e11e8dcb0b02a42d64e9ca63a18de93a8178652e9dfae3b6818 *verrauscht.mp4
```

## Nicht geprüft
- echte Kameraaufnahmen mit Wackeln (deshake nur im Filteraufbau geprüft, nicht gemessen)
- KI-Hochskalieren (Real-ESRGAN o. ä.): nicht eingebaut, bräuchte Modell und Freigabe
- Laufzeit bei langen 4K-Clips
