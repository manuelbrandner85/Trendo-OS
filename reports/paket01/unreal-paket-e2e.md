# Beleg – Ein-Klick-Veröffentlichung: fertiges Windows-Spiel aus Unreal, echter Lauf mit UE 5.8 (07.11.2026)

## Was gebaut ist
- „📦 Pakete bauen“ mit Ziel Windows ruft für Unreal-Spiele Unreals offiziellen Paketierer auf: RunUAT BuildCookRun (Shipping, kochen, .pak, ablegen, ohne Rückfragen) und packt den Windows-Ordner als ZIP in „Veröffentlicht“. Hochladen bleibt ein eigener Schritt nach Ja.
- Fehlschlag nennt die Fehlerzeile des Paketierers (Protokoll paket.txt).

## Zwei echte Fehler im ersten Paket, gemessen und behoben
1. Zu groß: 1006 Dateien, 4 GB – der ganze Ordner Content/Trendo wurde lose mitgeliefert (rohe Quell-Texturen und noch einmal alle Assets). Jetzt liegen nur level.json und natur.json lose (Content/TrendoDaten); ältere Projekte werden beim nächsten „Bauen“ umgestellt.
2. Nur Platzhalter-Formen: Menschen, Pflanzen, Felsen, Wagen und Bewegungen lädt das Spiel zur Laufzeit über ihren Pfad – der Paketierer kochte sie deshalb nicht mit. Jetzt stehen /Game/Trendo, die Mover-Figuren und der Sportwagen als „immer kochen“ in den Paketeinstellungen.

## Messung (Endstand)
- Paketierer: Exitcode 0, BUILD SUCCESSFUL, 3 min; Paket 32 Dateien, 2620 MB (Waldlaeufer.exe + Shipping-Binary).
- Gepacktes Spiel gestartet (Probelauf -trendocheck -trendoshot): beendet mit Exitcode 0, 7 Bilder (Start, Objekte, Gegner, Kampf, Nahaufnahme, Blut 5 min später, Wagen) – echte Menschen, Pflanzen, Felsen, Blut, Wagen sichtbar.
- Shipping-Builds schreiben kein Protokoll; Beleg für das gepackte Spiel sind daher Exitcode und Bilder. Der Editor-Prüflauf mit allen Messzeilen lief vorher grün (Wegfindung um Fels, Wagen 47 km/h).

Bild: unreal-paket-spiel.png (sechs Bilder aus dem gepackten Spiel)

## Ehrlich offen
- Nur Windows; Mac/Linux/Konsolen-Pakete nicht gebaut. Kein Installer, ZIP zum Entpacken. Code-Signatur fehlt (Windows SmartScreen kann warnen).

## SHA-256
- unreal-paket-spiel.png: 9ded3e1d4c1b35d4b7dd4464127bd9c18ad50489df278786547a0e96d5d86902

## Rohausgabe – erstes Paket (vor den Korrekturen)
```
Exitcode 0, Dauer 4 min
BUILD SUCCESSFUL
AutomationTool exiting with ExitCode=0 (Success)
Dateien 1006, 4030 MB
C:\Users\<user>\AppData\Local\Temp\trendo-ue-e2e\paket\Windows\Waldlaeufer.exe
C:\Users\<user>\AppData\Local\Temp\trendo-ue-e2e\paket\Windows\Waldlaeufer\Binaries\Win64\Waldlaeufer-Win64-Shipping.exe
```

## Rohausgabe – Endstand
```
+DirectoriesToAlwaysCook=(Path="/ChaosModularVehicleExamples/Models/SportsCar")
+DirectoriesToAlwaysCook=(Path="/MoverExamples/Characters/Mannequins")
+DirectoriesToAlwaysCook=(Path="/Game/Trendo")
+DirectoriesToAlwaysStageAsNonUFS=(Path="TrendoDaten")
Exitcode 0, Dauer 3 min
BUILD SUCCESSFUL
AutomationTool exiting with ExitCode=0 (Success)
Dateien 32, 2620 MB
C:\Users\<user>\AppData\Local\Temp\trendo-ue-e2e\paket\Windows\Waldlaeufer.exe
C:\Users\<user>\AppData\Local\Temp\trendo-ue-e2e\paket\Windows\Waldlaeufer\Binaries\Win64\Waldlaeufer-Win64-Shipping.exe
Gepacktes Spiel beendet=True Exitcode=0
trendo-blut-alt.png 1738 KB
trendo-gegner.png 1542 KB
trendo-glied.png 1772 KB
trendo-kampf.png 1735 KB
trendo-objekte.png 1570 KB
trendo-start.png 1567 KB
trendo-wagen.png 1158 KB
```
