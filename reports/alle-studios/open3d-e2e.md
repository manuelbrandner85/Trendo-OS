# Echter Lauf: Open3D – Scan-Netze glätten und vereinfachen (Gegenstände einscannen) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. Eigene Python-Umgebung (Python 3.12) mit `open3d==0.20.0` (MIT) von PyPI, nur Binärpakete, unter `C:\Users\<user>\AppData\Local\Temp\trendo-tools-b\py-open3d`.

## Lauf mit dem Trendo-Skript `trendo_open3d.py`
Testkörper: Kugel mit 10 cm Radius, 57 120 Dreiecke, 3 mm Rauschen entlang der Normalen (wie ein verrauschter Scan).

| Schritt | Dreiecke | Radius (Mittel) | Streuung (Rauschen) |
|---|---|---|---|
| verrauscht (Eingabe) | 57 120 | 0,1000 m | 0,0030 m |
| Glätten (Taubin, 10 Durchgänge) | 57 120 | **0,1000 m** (kein Schrumpfen) | **0,0015 m** |
| Vereinfachen (Ziel 2 000) | **2 000** | 0,0999 m | 0,0013 m |

- 99 Durchgänge (außerhalb 1–50): Meldung „Zahl außerhalb des erlaubten Bereichs“, Exit 2.

## Tests
- `Open3dTests.Real_smooth_and_simplify` (echt, nur mit `TRENDO_OPEN3D_PYTHON`): Kugel aus C#, 32 400 Dreiecke. Glätten senkt die Streuung auf unter 70 %, der Radius bleibt innerhalb ±2 mm. Vereinfachen trifft das Ziel von 2 000 Dreiecken. Unsinnige Zahl und unbekannter Modus enden mit Exit 2. Grün.
- `Open3dTests` übrige 3 (Einrichtung, Lizenz, Stimmigkeit aller Python-Laufzeiten): grün.
- `Open3dUi` (Scan-Studio: Einrichten-Knopf, Glätten ersetzt das Modell, „Rückgängig“, Fehler lässt das Modell unverändert): grün.

## Nicht geprüft
- Einrichtung über den Trendo-Knopf „Open3D einrichten“ (uv): Die Schritte sind geprüft, aber nicht ausgeführt.
- Echter COLMAP-Scan als Eingabe (nur Testkörper).
