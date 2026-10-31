# T-0278 – Möbelmodelle in echten Maßen (Blender) und vollständiger glTF-Szenengraph

- Freigabe: Manuel im Chat am 31.10.2026 („alle 3D-Objekte fotorealistisch, proportional und organisch korrekt“, „Ja, weiter“)
- Umgesetzt: `Assets/blender/trendo_moebel.py` baut alle 26 Katalogmöbel in Normmaßen (Sitzhöhe 42–45 cm, Arbeitshöhe 90 cm, Tischhöhe 75 cm …) mit Fasen an allen Kanten, gewölbten Polstern mit Kedernaht-Rundung und leichter Unregelmäßigkeit, eingelassenen Becken, Griffen, Büchern unterschiedlicher Größe, Pflanze mit Blättern; Cycles-Materialien physikalisch (Holz mit Jahresringen und Faserrichtung längs zum Bauteil, Stoff mit Webstruktur, Sheen und Farbvariation, Chrom, gebürsteter Edelstahl, Sanitärkeramik mit Lack, Glas, Blatt mit Lichtdurchgang). Zwei Qualitäten: „render“ für Fotos, „preview“ für die Vorschau. `Assets/moebel/*.glb` (2,2 MB, höchstens 17 000 Dreiecke) für die 3D-Vorschau.
- GLB-Leser (`MeshIO`): wertet jetzt den Szenengraph aus (Verschiebung/Drehung/Skalierung, Elternknoten, Mehrfachverwendung) – vorher lagen Teile aus Blender/Unreal/Sketchfab-Exporten übereinander.
- Reality Check im sichtbaren Blender (Cycles, RTX 5070/OptiX): erste Fassung überbelichtet und mit „Grabstein“-Rückenkissen und quer gemaserten Holzbeinen – korrigiert und erneut geprüft.
- Tests: Core 1 (alle 26 Modelle vorhanden, Breite/Tiefe/Höhe ±12 % zum Katalog, Bodenkontakt)
