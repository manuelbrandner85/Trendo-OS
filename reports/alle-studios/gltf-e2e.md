# Echter Lauf: Khronos glTF-Validator und glTF-Transform (VR & AR gestalten) – 08.11.2026

Freigabe des Nutzers: „Ja, testen“.

## Downloads
- **glTF-Validator 2.0.0-dev.3.10** (Khronos, Apache-2.0): offizielle GitHub-Zip `gltf_validator-2.0.0-dev.3.10-win64.zip`, 2320898 Bytes, SHA-256 c5068f51205deedc28acc3529ee7e11ee60e853454f673093398eba80142202c. Eingetragen im Abhängigkeitskatalog.
- **glTF-Transform 4.5.1** (MIT): `npm install @gltf-transform/cli@4.5.1 --ignore-scripts`, 173 Pakete. Trendo startet es mit `node …/bin/cli.js`, nicht über die `.cmd`-Datei; deren `cmd.exe` würde Zeichen wie `&` in Dateinamen auswerten.

## Läufe (Trendo-Möbel `regal.glb`)
| Schritt | Ergebnis |
|---|---|
| Prüfen | 0 Fehler, 0 Warnungen – 9 840 Dreiecke, 19 680 Ecken, 8 Materialien, 9 ms |
| Prüfen einer abgeschnittenen Datei (200 000 Bytes) | Exit 1, Fehler `GLB_UNEXPECTED_END_OF_CHUNK_DATA` |
| Packen für Web & Brille (`optimize --compress meshopt --texture-compress webp`) | 532 KB → **105 KB** (−80 %) in 0,8 s |
| Prüfen des gepackten Modells | 0 Fehler, 9 300 Dreiecke, Erweiterungen `EXT_meshopt_compression`, `EXT_texture_webp` |
| Profil „Austausch“ (ohne Kompression) | 656 KB, also größer als das Original, dazu GPU-Instancing → **nicht übernommen** |

**Wichtig:** Blender und Unreal lesen meshopt-komprimierte Dateien nicht. Die Oberfläche sagt das, und das Original bleibt unverändert neben der neuen `…-web.glb`.

## Tests
- `GltfToolsTests`: 7 Tests mit Validator-Berichten in echter Form, dazu 2 Echtläufe (Prüfen und Packen), die die Programme automatisch finden. Alle grün.
- `GltfUi`: Einrichten-Knöpfe ohne Programme; mit ihnen prüfen und packen, danach wird gleich wieder geprüft. Neue Datei, Original bleibt.

## Nicht geprüft
- Einrichten über die Trendo-Knöpfe: Download in `TrendoWerkzeuge` und npm-Installation.
- Anzeige der gepackten Datei auf einer echten Brille.
