# Beleg – f3d-Vorschaubilder, echter Lauf (07.11.2026)

Anlass: Alle Studios, Runde 1 – CORE-05 „3D-Modelle erstellen“. Freigabe der Person für den Testlauf: „Ja, f3d testen“.

## Werkzeug
- f3d 4.0.1, offizielle Windows-Zip `F3D-4.0.1-Windows-x86_64.zip` von github.com/f3d-app/f3d (BSD-3-Clause)
- SHA-256 der Zip: `7c36b7abc2929dafb7d93fc132354d69dbc1ea05c6bf858e7c70d75676ca56f8` (gleich dem Katalogeintrag „f3d“)
- Modell: Poly Haven `boulder_01_2k.gltf` (CC0) aus dem Unreal-Prüfprojekt

## Läufe
| Lauf | Argumente | Ergebnis |
|---|---|---|
| 1 | `--output --resolution=768,768 --anti-aliasing --tone-mapping --ambient-occlusion` | Exit 0, 29,7 s; Bild 2304 × 2304 mit Dateiname, Achsenkreuz, Gitter und Spiegelboden – stammt aus der mitgelieferten f3d-Grundeinstellung, nicht aus den Optionen |
| 2 | wie 1 plus `--no-config` | Exit 0, 0,8 s; 768 × 768, sauber, aber dunkel und flach (kein Umgebungslicht) |
| 3 | wie 2 plus `--hdri-ambient` (übernommen) | Exit 0; 768 × 768, glaubwürdige Farben, Materialglanz, Verdeckung in den Spalten |

Daraus: `Model3DPreview.Args` = `--no-config --output=… --resolution=N,N --anti-aliasing --tone-mapping --ambient-occlusion --hdri-ambient <Datei>` (Optionen gegen `doc/user/03-OPTIONS.md` v4.0.1 geprüft).

Bild: `f3d-vorschau-fels.png` (Lauf 3) – SHA-256 `6443c98e2a6c923b19e48416e184be4d87c583c2a52ede1eca2fd944e1662e94`

## Ehrlich offen
- Einrichtung über den Katalog (Download + Prüfsumme + Entpacken in TrendoWerkzeuge) im Trendo-Fenster nicht durchgeklickt; geprüft wurde der Lauf der Programmdatei und der Adapter mit Tests.
- Kamera ist die f3d-Standardansicht; eine feste Schrägansicht je Modellart folgt bei Bedarf.
