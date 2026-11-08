# Echter Lauf: assimp – 3D-Dateien umwandeln (3D-Modelle erstellen) – 08.11.2026

Freigabe des Nutzers: „Ja, alle testen“. assimp 6.0.5 (BSD-3-Clause) aus der offiziellen GitHub-Zip `windows-x64-v6.0.5.zip` (17953058 Bytes, SHA-256 1ab3ac83e75e64bf3fb2b67e43c3b8436b49e25a212aa0c65813415a098d7c41 – Wert im Abhängigkeitskatalog).
Die Zip enthält nur die Bibliothek `Release/assimp-vc143-mt.dll`, kein Kommandozeilenprogramm.

## Aufbau
- Trendo lädt die DLL erst zur Laufzeit (`NativeLibrary`, ohne `unsafe`) und ruft `aiImportFile`, `aiExportScene`, `aiReleaseImport` und `aiGetErrorString` auf.
- Die Umwandlung läuft **nie im Trendo-Fenster**, sondern in einem eigenen Prozess `TrendoOS --assimp-convert <dll> <ein> <aus>`. Ein Absturz von assimp bei einer kaputten oder bösartigen Datei trifft nur diesen Prozess, Trendo meldet ihn.

## Läufe (Test `AssimpConvertTests`, echte DLL über `TRENDO_ASSIMP_DLL`)
- Würfel als OBJ → **GLB** (beginnt mit „glTF“) → zurück zu OBJ: **8 Ecken, 12 Dreiecke**. Die Vierecke wurden trianguliert, nichts ging verloren.
- Kaputte FBX-Datei → Exit 3 mit der Meldung: „assimp kann die Datei nicht lesen: FBX-Tokenize (offset 0x23) block offset is out of range“. Kein Absturz.
- Über den gebauten `TrendoOS.exe --assimp-convert` (eigener Prozess): GLB geschrieben.
- Ergebnis: alle 32 Kern-Tests im Filter grün (assimp und Abhängigkeitskatalog).

## Oberfläche
- 3D-Studio, Karte „In GLB umwandeln (assimp)“: ohne Einrichtung der Knopf „assimp einrichten“, sonst „→ GLB“ je Datei im 3D-Ordner. Das Ergebnis landet in `3D/umgewandelt`. Ein Absturz wird gemeldet (Test `AssimpUi`, grün).

## Nicht geprüft
- Echte FBX-, DAE- oder 3DS-Dateien aus anderen Programmen (nur Testwürfel und eine kaputte Datei).
- Einrichtung über den Trendo-Knopf (Download in `%LOCALAPPDATA%\TrendoWerkzeuge\assimp`).
- Linux-Edition (Pfade zu libassimp eingetragen, nicht geprüft).
