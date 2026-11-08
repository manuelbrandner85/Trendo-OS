# Objekte erkennen mit YOLOX – 08.11.2026

Alle Studios, Runde 7, CORE-09 „Kameras & Roboter steuern“.

- **Modell:** **YOLOX-S** (Megvii, Apache-2.0) aus dem offiziellen GitHub-Release 0.1.1rc0.
  - Datei: `yolox_s.onnx`, 35 858 002 Bytes, SHA-256 `c5c2d13e59ae883e6af3b45daea64af4833a4951c92d116ec270d9ddbe998063`.
  - Ermittelt aus dem echten Download am 08.11.2026. Trendo übernimmt die Datei nur mit genau dieser Prüfsumme.
- **Laufzeit:** onnxruntime 1.31.1 (MIT), numpy 2.2.6 und Pillow 12.3.0. Sie läuft in Trendos KI-Laufzeit „Objekte“ (eigene Python-Umgebung über uv, eigener Prozess) auf dem **Prozessor**, damit sie nicht um die Grafikkarte konkurriert.
- **Bewusst nicht gewählt:** Ultralytics-YOLO. Es steht unter AGPL-3.0.
- **Modellgröße:** Das kleine Modell „S“ ist schnell genug für Kamerabilder ohne Grafikkarte. Die großen Varianten (M 101 MB, L 217 MB) wären auf dem Prozessor zu langsam für den Alltag.

## Was die Nutzerin oder der Nutzer sieht
„Kameras & Roboter steuern“ → Kamera → **„Objekte erkennen“**. Fehlt die Einrichtung, steht dort „… einrichten“: ein Klick, einmalig etwa 120 MB.

Das erkennt die Funktion im aktuellen Bild (Livebild oder Foto):
- 80 Arten, zum Beispiel Personen, Fahrzeuge, Tiere und Alltagsgegenstände.
- Die Vorschau zeigt das Bild mit Rahmen und Sicherheit.
- Im Status steht die Zählung auf Deutsch mit Einzahl und Mehrzahl, etwa „Erkannt: 2 Personen, 1 Hund (40 ms, YOLOX-S, lokal).“

**Kein Bild verlässt den PC.** Das Skript hat keinen Netzzugriff.

## Umsetzung
- **Skript** `trendo_objekte.py` (eigener Code):
  - Letterbox auf 640 × 640 wie YOLOX.
  - Auflösen der Ausgabe mit Gitter und Schritten 8, 16 und 32.
  - Sicherheit = Objekt × Klasse.
  - Doppelte Treffer werden je Klasse entfernt (Überlappungsgrenze 0,45). Die Erkennungsschwelle steht standardmäßig auf 0,35.
  - Schutz: höchstens 40 Megapixel, Fehler bei kaputten Bildern werden verständlich gemeldet.
- **C#** `ObjectDetect`:
  - Antwort lesen.
  - 80 deutsche Namen mit Einzahl und Mehrzahl.
  - Modell-Download mit Prüfsumme; bei falschem Hash wird die Datei verworfen und nichts bleibt liegen.

## Tests
- **`ObjectDetectTests`** (6 Tests, grün):
  - Antwort lesen
  - Zählung als Text
  - Argumente und Schwelle
  - Download nur mit passender Prüfsumme
  - KI-Laufzeit mit festen Fassungen; das Skript nutzt den Prozessor und hat keinen Netzzugriff
  - kaputte Antworten werden abgelehnt
- **`CamObjects_Ui`** (1 Oberflächentest, grün):
  - Ohne Einrichtung erscheint der Einrichten-Knopf.
  - Ohne Bild kommt ein klarer Hinweis.
  - Mit Bild erscheinen das markierte Bild und die deutsche Zählung.
  - Ein Fehler wird verständlich gemeldet.
- Die bisherigen Kamera- und Roboter-Tests sowie die KI-Bibliothekstests bleiben grün.

## Nicht geprüft (NOT RUN)
- **Echter Lauf:** Modell und onnxruntime auf einem echten Foto. Dafür muss die Python-Laufzeit eingerichtet und das heruntergeladene Modell ausgeführt werden. Das geht nur mit Freigabe des Nutzers, wie bei axe-core. Der Download selbst ist geprüft (Größe, Prüfsumme, ONNX-Kopf).
- Erkennungsgüte auf eigenen Kamerabildern.
- Ein Dauerlauf auf dem Livebild. Bisher wird ein Bild nach dem anderen auf Knopfdruck erkannt.
