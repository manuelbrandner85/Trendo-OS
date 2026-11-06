# Beleg S3-18 – ProRes 422 HQ, EXR- und PNG-Bildsequenz, fehlender Encoder (06.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~ProExportTests|FullyQualifiedName~VideoExport|FullyQualifiedName~VideoEdit|FullyQualifiedName~VideoPro|FullyQualifiedName~FilmDelivery|FullyQualifiedName~Greenscreen|FullyQualifiedName~ColorScopes"` mit `TRENDO_PRO_BELEG=<temp>/trendo-probeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:    22, übersprungen:     0, gesamt:    22, Dauer: 9 s
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoSchnittPlus_Ui` → Failed 0 (ProRes ohne Encoder: kein Export, Alternative genannt; EXR geht in einen Ordner)
- Eingabe: szene.mp4 = testsrc2 320×180, 25 fps, 1 s, Sinus 440 Hz

## ffprobe der Ausgaben
```
film.mov video: prores|HQ|320|180|yuv422p10le|25/1
film.mov audio: pcm_s24le|48000|2
EXR: 25 Bilder, erstes: exr|320|180|gbrpf16le
EXR-WAV: pcm_s24le|48000 · 1.000000 s
PNG: 25 Bilder
```

- Positiv S3-18-P: ProRes 422 HQ in .mov mit 10 Bit 4:2:2 und 24-Bit-PCM; EXR-Sequenz (25 Bilder, half float, ZIP) mit WAV gleicher Länge; PNG-Sequenz mit 25 Bildern. Dieses FFmpeg enthält prores_ks und exr.
- Negativ S3-18-N: Fehlt ein Encoder (z. B. prores_ks), startet Trendo keinen Export und nennt eine Alternative, die mit dem vorhandenen FFmpeg wirklich geht (ProRes → YouTube 1080p/H.264, EXR → PNG, H.265/AV1 → Wie das Projekt); ohne Alternative der Hinweis zum Einrichten.

## Dateien (SHA-256)
```
77406a3a49f9e015c2e000d8d60a837974e846e485bc74b227b787ff4008fbb2 *Film-EXR.wav
3d31a05a266d073d54b0810cfbc5e9d209843c52df4054d9f720ca0ddc2c999f *Film-EXR_00001.exr
3b76b04fa718f9b028bd6509f067bb407d4f40d1076fc35431dfeb908f3bc6e3 *Film-EXR_00025.exr
4b3789e8701d4b4cc5d990b6f45bed8dcc7f8719c2d0fd61f8246cb239a34c1c *Film-PNG_00001.png
1c8ad69f301d5eef4b9960a43c65cda21212983202a696419780eeb87bc64d91 *film.mov
a77e8a3b6cefdfa03720482f82093036b69543abb400af155f5026e5bd32cc9e *probe.txt
```

## Nicht geprüft
- Render-Warteschlange mit mehreren Fassungen nacheinander (ein Export läuft im Hintergrund, weitere erst danach) – offen
- 8K und HDR-Ausgabe; DNxHR als ProRes-Alternative
- Farbraum-Kennzeichnung im Container: bewusst nicht gesetzt, weil die RGB→YUV-Wandlung von FFmpeg standardmäßig BT.601 nutzt und ein BT.709-Etikett dann falsch wäre
