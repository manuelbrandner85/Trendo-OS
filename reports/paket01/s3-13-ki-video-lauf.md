# S3-13 KI-Video im Videoschnitt – Lauf 07.11.2026

**Was:** Clip aus einer Beschreibung erzeugen, mit Zufallswert (Seed) und Herkunftsnachweis. Das lokale Modell ist Wan 2.2 TI2V-5B (Apache-2.0) über stable-diffusion.cpp (MIT, Fassung master-1015-e41fbcf). Der Clip kommt in die Zeitleiste und kann rückgängig gemacht werden. Daneben liegt `…​.herkunft.json` mit Beschreibung, Ausschluss, Seed, Modell samt Lizenz und Dateien, Programm sowie den Prüfsummen von Roh-Datei und Clip. Der Clip ist ausdrücklich als „KI-erzeugt“ gekennzeichnet.

**Code:**
- `src/Trendo.Core/Media/GenVideo.cs`
- `src/Trendo.App/MainWindow.VideoGenKi.cs`: Reiter „Medien“ → „KI-Video (lokal)“

**Tests:**
- `tests/Trendo.Core.Tests/GenVideoTests.cs` (3 Tests, einer davon echt)
- `tests/Trendo.App.Tests/VideoGenUiTests.cs` (1 Test)

## Positiv (S3-13-P): echter Lauf mit dem eingerichteten Modell

Befehl: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~GenVideoTests` mit gesetzten Variablen `TRENDO_GENVIDEO_ECHT` und `TRENDO_GENVIDEO_BELEG`.
Ergebnis: **Bestanden, 3/3, Exitcode 0** (`s3-13-beleg/dotnet-test.log`).

| Clip | Zufallswert | Größe | Dauer | Rechenzeit (RTX 5070) |
|---|---|---|---|---|
| clip-a-seed42 | 42 | 1280 × 704, 24 Bilder/s | 0,5 s (13 Bilder) | 107 s |
| clip-b-seed42 | 42 | 1280 × 704, 24 Bilder/s | 0,5 s | 116 s |
| clip-c-seed4711 | 4711 | 1280 × 704, 24 Bilder/s | 0,5 s | 106 s |

**Wiederholbarkeit:** Bei gleichem Seed beträgt die mittlere Bildabweichung **1,22** von 255, bei anderem Seed **52,53** (`messung.txt`). Die Grafikkarte rechnet über Vulkan nicht bitgenau gleich. Der Unterschied ist mit bloßem Auge nicht zu sehen. Die Prüfgrenze im Test liegt bei „unter 3,0“ und „mindestens 10× so groß bei anderem Seed“.

**Herkunftsnachweis:** Für jeden Clip ist er vorhanden. `VerifyProvenance` bestätigt die Prüfsumme, und eine Veränderung am Clip wird erkannt (Komponententest).

## Qualitätsmessung (Befund, danach geändert)

Gleiche Beschreibung, Seed 4711, 1 s. Die Bilder liegen unter `qualitaet-*.png`, die Zeiten in `qualitaet-zeiten.txt`.

| Einstellung | Gesamtzeit | Bild |
|---|---|---|
| 640 × 352, 20 Schritte | 35 s | verschmiert, zerfallende Formen |
| 832 × 480, 25 Schritte | 75 s | verschmiert, Blockmuster |
| 1280 × 704, 12 Schritte | 332 s* | glaubwürdig, teils Kachel-Artefakte (Seed 42) |
| 1280 × 704, 30 Schritte | 245 s | fotoreal (Tasse, Holztisch, Gegenlicht) |

\* Dekodieren 282 s, weil parallel andere Tests liefen. Das Rechnen selbst dauerte 43 s statt 120 s.

**Folge:** Die erste Vorschau-Stufe (480 × 288) war unbrauchbar und ist entfernt. Wan 2.2 5B ist auf 704p trainiert, deshalb läuft auch die Vorschau jetzt in voller Größe mit 12 Schritten. Standard ist die **Endfassung** mit 30 Schritten.

## Negativ (S3-13-N)

- **Ohne Modell:**
  - Das Feld zeigt nur einen Hinweis und den Weg zur KI-Bibliothek. Einen Knopf zum Erzeugen gibt es nicht (App-Test).
  - `Problem()` meldet „Kein Video-Modell eingerichtet … Ohne Modell erzeugt Trendo keine Bilder.“
- **Liefert die KI nichts:** Es kommt die Meldung „Die Video-KI hat keinen Clip geliefert – nichts eingefügt.“ Die Zeitleiste bleibt unverändert (App-Test).
- **Falsche Eingaben werden vor dem Start abgewiesen:** leere Beschreibung, Seed außerhalb von 0 bis 2³²−1, Länge außerhalb von 0,5 bis 5 s, fehlende Modelldatei, fehlendes Startbild.

## Nicht geprüft

- Clips mit Startbild (`-i`) liefen nicht echt, das ist nur im Komponententest abgedeckt.
- Clips länger als 0,5 s wurden in der Endfassung nicht echt erzeugt. Der KI-Musikvideo-Lauf vom 04.11. hatte 2 s geschafft.
- Grafikkarten außer der RTX 5070 wurden nicht geprüft.
