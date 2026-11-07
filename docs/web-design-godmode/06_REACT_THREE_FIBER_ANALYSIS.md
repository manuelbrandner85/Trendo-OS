# React Three Fiber (pmndrs/react-three-fiber, master d74622e, 2026-10-14; @react-three/fiber 9.8.1)
- Lizenz MIT. Pakete: `fiber` (Renderer), `test-renderer`, `shared`, `eslint-plugin`.
- Gebündelt mit three 0.180 (MIT). Baustein „3D-Szene“: Würfel/Kugel/Knoten, PBR-Material, gerichtetes Licht mit Schatten, Schattenfänger, Drehung, Höhe.
- Befund: In der skalierten iframe-Leinwand saß die Szene versetzt (Größenmessung mit CSS-Transform); ohne iframe korrekt, `resize={{ offsetSize: true }}` gesetzt.
- Offen: GLB/glTF-Modelle aus dem Projekt (Blender-Export) laden; WebGPU erst prüfen, wenn stabil.
