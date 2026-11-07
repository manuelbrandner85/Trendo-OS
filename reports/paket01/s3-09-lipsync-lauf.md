# S3-09 Lippensynchron (Mundbilder) im Videoschnitt – Lauf 07.11.2026

## Was gebaut ist

Aus einer Sprachaufnahme und den Wortzeiten entsteht eine Mundbild-Spur mit Zeiten. Die Wortzeiten liefert Whisper; ohne Whisper wird der Text über die Sprechpausen verteilt.
- Die Spur nutzt den Preston-Blair-Satz wie Papagayo: AI, E, O, U, L, WQ, etc, FV, MBP, rest.
- Sie wird als JSON und als Moho/Papagayo-Datei gespeichert. Diese Datei lesen Blender, Moho, OpenToonz und Synfig.

Prüfung gegen die Aufnahme:
- Verglichen werden Mundöffnung und Lautstärke per Kreuzkorrelation, Raster 10 ms, geglättet ±50 ms.
- FAIL, wenn der Versatz größer als ±80 ms ist oder der Zusammenhang unter 0,3 liegt.

Vergleichsvorschau als MP4:
- Ein gezeichneter Mund je Mundbild, darunter die Tonwelle, Ton dabei.
- Optional steht daneben ein Gesichtsvideo. Dessen Mund misst MediaPipe je Bild (Blendshape `jawOpen`) und vergleicht ihn mit dem Ton.

Bei FAIL kommt nichts in die Zeitleiste. Spur und Vorschau bleiben zur Kontrolle im Ordner „Lippensynchron“.

**Code:**
- `src/Trendo.Core/Media/LipSync.cs`
- `src/Trendo.App/MainWindow.VideoLipSync.cs`: Reiter „Medien“ → „Lippensynchron“
- `src/Trendo.App/Assets/ki/trendo_pruefung.py`: neuer Schalter `--mund`

**Tests:**
- `tests/Trendo.Core.Tests/LipSyncTests.cs` (6 Tests, davon 2 echt)
- `tests/Trendo.App.Tests/VideoLipSyncUiTests.cs` (1 Test)

Ergebnis: `dotnet test … --filter FullyQualifiedName~LipSyncTests`: **Bestanden, 6/6, Exitcode 0** (`s3-09-beleg/dotnet-test.log`).

## Positiv (S3-09-P): echte Sprache, echte Wortzeiten

- **Sprache:** Piper mit der Stimme „thorsten-high“ (Thorsten-Voice-Datensatz, CC0), deutscher Text, 12 s.
- **Wortzeiten:** whisper.cpp b5454 mit ggml-base. Die SHA-256 des Programmpakets stimmt mit Trendos Katalog überein (`6ba69e34…`). Ergebnis: 36 Wörter (`echt/woerter.json`).

| Variante | Ergebnis |
|---|---|
| Whisper-Zeiten roh | FAIL – Zusammenhang 0,14 |
| **Wortgrenzen am Ton ausgerichtet (eingebaut)** | **OK – Versatz +10 ms, Zusammenhang 0,42** |
| Nur Text, über Sprechpausen verteilt | OK – Versatz +40 ms, Zusammenhang 0,43 |

**Befund:** Bei Whisper (base) kleben die Wörter aneinander und schließen Pausen ein. Deshalb wird jedes Wort jetzt auf den hörbaren Teil seines Zeitfensters gekürzt (`SnapToVoice`).

**Dateien:** `echt/mundbilder.json`, `echt/mundbilder-25fps.dat`, `echt/vergleich-echt.mp4` und die Mundformen `echt/mund-*.png`.

## Negativ (S3-09-N)

- **Ton 200 ms später, Spur unverändert:** „FAIL: Mund und Ton sind um −180 ms versetzt (erlaubt ±80 ms)“ (echter Lauf).
- **Stille:** „FAIL: In der Aufnahme ist keine Sprache zu hören“ (Komponententest).
- **Fehlende Aufnahme:** „FAIL: Sprachaufnahme fehlt“ (App-Test).
- **Unpassende Spur:** „FAIL: Die Mundbilder passen nicht zur Aufnahme“ (Komponententest).

**Echtes KI-Gesichtsvideo** (`video/`):
- Quelle ist Wan 2.2 S2V vom 04.11.2026 in Vorschau-Qualität, 48 Bilder mit 16 Bilder/s. In allen 48 Bildern hat MediaPipe den Mund gemessen.
- Ergebnis: „FAIL: Die Mundbewegung im Video folgt der Sprache nicht (Zusammenhang 0,28)“.
- Das deckt sich mit der Artefakt-Prüfung G5 vom 04.11. („Mund folgt dem Gesang nicht“).
- Verschiebt man denselben Mund um 3 Bilder, misst Trendo eine Versatzänderung von etwa 200 ms.
- Ein Gesichtsvideo mit gutem Lippensynchron konnte ich nicht echt erzeugen. Dafür wäre ein S2V-Lauf in Endqualität nötig, das dauert auf 12 GB lange.

## Nicht geprüft

- Ein OK-Ergebnis im Video-Vergleich mit einem echten Gesicht, siehe oben. Das ist nur mit synthetischen Werten im Komponenten- und App-Test belegt.
- Whisper „small“ und Sprachen außer Deutsch.
- Der Import der Moho-Datei in Blender wurde nicht ausprobiert.
