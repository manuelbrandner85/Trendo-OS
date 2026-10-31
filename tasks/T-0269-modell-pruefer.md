# T-0269 – VR & AR als echtes Programm „Modell-Prüfer“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): glTF-Viewer/-Validator, Leistungsanzeigen von Standalone-Brillen; kein Code übernommen (siehe `docs/claude/vorbilder/modell-pruefer-gltf.md`)
- Umgesetzt: Modell öffnen (glTF/GLB, OBJ, STL) oder Beispielmodell, eigene 3D-Ansicht (schattiert, Drahtgitter, Ansichten, drehen/zoomen, Bodenraster), Zielgerät wählen mit Balken für Dreiecke/Zeichenaufrufe/Texturen/Größe, Ampel für alle Geräte, Maßstab-Urteil, Texturen mit Grafikspeicher, Erweiterungen, Prüfbericht, Rechner sofort (Bildzeit, Detailstufen mit Modellwerten, Texturschärfe, Bewegungskomfort).
- Gefunden und behoben: LiveCalc merkte sich die Vorbelegung schon beim ersten Anzeigen – spätere Vorbelegungen (z. B. Werte des gewählten Fahrzeugs oder Modells) griffen nicht mehr; Beispielmodell war 100-fach zu klein (Netze rechnen in cm)
- Tests: App 2 neu (echte Maus/Tastatur) ersetzen SPEC04_Ui/SPEC04_UiPlus; FachBedienungTests auf CORE-09 umgestellt
