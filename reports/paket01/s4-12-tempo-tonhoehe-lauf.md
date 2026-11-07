# Beleg S4-12 – Tempo und Tonhöhe mit Messung und Warnung (07.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev mit Rubber Band (Formanten erhalten, Tonhöhen-Qualität „quality“)
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~TimePitchTests` mit `TRENDO_TP_BELEG=<temp>/trendo-tpbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 2 s
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.MusikProWerkzeuge_Ui` → Failed 0 (Üben 75 %/−2 Halbtöne; 300 % wird abgelehnt, nichts gerechnet)

## Messung (Referenz: Ton 220 Hz + 440 Hz, Stereo, 4 s)
```
langsam-hoeher.wav: Tempo 0,8, +2 Halbtöne → Vorab gruen, gruen: Im Toleranzbereich: Länge 1,250×, Tonhöhe +200 Cent, Phase 1,00.
intonation-plus30.wav: Tempo 1, +0,3 Halbtöne → Vorab gruen, gruen: Im Toleranzbereich: Länge 1,000×, Tonhöhe +30 Cent, Phase 1,00.
extrem.wav: Tempo 0,55, +9 Halbtöne → Vorab gelb, gruen: Im Toleranzbereich: Länge 1,817×, Tonhöhe +900 Cent, Phase 1,00.
```

- Positiv S4-12-P: Länge ±1 %, Tonhöhe ±10 Cent, Phase L/R stabil – auch Feinstimmung um +30 Cent für die Intonation.
- Negativ S4-12-N: Starke Änderung (z. B. 55 %, +9 Halbtöne) bekommt vorab eine Artefakt-Warnung; über 50–200 % oder ±12 Halbtöne wird abgelehnt statt – wie früher – still begrenzt.

## Dateien (SHA-256)
```
bd86dbeaffa070f692259165ec0299b1c5b6de40cf5b10e9160d84b95a86f097 *extrem.wav
cbed2ebf276fa03253a08893441424d20289c876820fd447df8290c6d9a9e05f *intonation-plus30.wav
3e533192b00579fdf8e3f80d7ebcea7d18e1df11b31f1eb46a0edaa502781cd4 *langsam-hoeher.wav
b54f5e5275c00e50b2870e998a1dc738f670ce4ddb4622521b3ee102defc477b *ton.wav
```

## Nicht geprüft
- Automatische Intonationskorrektur Note für Note (wie Melodyne) – hier nur gleichmäßige Feinstimmung
- Hörtest auf Artefakte bei echtem Gesang
