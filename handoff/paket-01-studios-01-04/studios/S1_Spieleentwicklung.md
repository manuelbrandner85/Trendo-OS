# STUDIO 1 – Spieleentwicklung – genehmigte Verbesserungen

Trendo-Katalog: `CORE-06` · **18 Nutzer-JA** · Stand: **APPROVED_SPEC** (Status der einzelnen Funktionen per Live-Audit verifizieren)

## Bewährte Steuerung
Reihenfolge für jede Funktion: vorhandenes Trendo prüfen → REA-Clean-Room/Native Integration bewerten → Schnittstellen/Dateien/Versionierung definieren → echten vertikalen Slice bauen → positive/negative Tests → Performance/Security/License/QC → Berichte/Revision. Unity-Relevanz je Studio steht in `03_GLOBALE_REGELN_REA_UNITY.md`.

| Nr. | Verbindliche Verbesserung | Konkreter Funktionsumfang | Claude-Agent | Ziel-Artefakt |
|---|---|---|---|---|
| 01 | **Universal Game Engine** | Unreal Engine, Unity, Godot und bei Bedarf weitere Engines über echte Adapter steuern. | `13-unreal-games` | Projektvorlage, Engine-Capability, Prüfbericht |
| 02 | **AI Game Director** | Claude koordiniert Spielentwicklung, Aufgaben, Assets, Programmierung und Qualität. | `08-agent-workflow` | Versionierter Produktionsplan und Task-IDs |
| 03 | **Ultimate Character Creator** | Menschen, Tiere, Kreaturen, Kleidung, Haare, Gesichter, Rigging und Animationen. | `12-blender-assets` | Charakterprojekt, Rig, Material, Export |
| 04 | **Living Open World** | Große Spielwelten mit Städten, Natur, Verkehr, NPCs, Wetter und Tageszeiten. | `13-unreal-games` | Szenen, Streamingdaten, Spawn-Logik |
| 05 | **Advanced Gameplay Creator** | Quests, Inventar, Fähigkeiten, Kampf, Fahrzeuge, Interaktionen und Spielregeln. | `13-unreal-games` | Spielregeln, Zustände, Prüfszenario |
| 06 | **AAA Cinematic Director** | Prologe, Zwischensequenzen, Kamerafahrten, Regie, Dialoge und filmische Beleuchtung. | `14-media-vfx` | Abspielbare Sequenz mit Timeline |
| 07 | **AI NPC Intelligence** | NPC-Verhalten, Erinnerungen, Routinen, Beziehungen, Dialoge und Reaktionen. | `07-ai-local-models` | Deterministische NPC-Baseline + optionale KI |
| 08 | **Ultimate Animation Studio** | Motion Capture, Gesichtsanimation, Körperanimation, Retargeting und Zustandsmaschinen. | `12-blender-assets` | Rig- und Clip-Bibliothek, Retargetingbericht |
| 09 | **World & Level Architect** | Visueller Level-Editor, Landschaften, Gebäude, Innenräume und prozedurale Generierung. | `09-one-workspace-studios` | Bearbeitbarer Szenengraph und Export |
| 10 | **Game Audio & Music Director** | Soundeffekte, Musik, Stimmen, Dialoge, Raumklang und dynamische Musiksysteme. | `14-media-vfx` | Event-Audio-Zuordnung, Mix, Lizenzliste |
| 11 | **Visual Scripting & Code** | Blueprints, C++, GDScript, C# und andere geeignete Entwicklungswerkzeuge. | `13-unreal-games` | Engine-spezifische Skripte/Graphen plus Tests |
| 12 | **Physics & Destruction** | Kollisionen, Fahrzeuge, Flüssigkeiten, Stoffe, Zerstörung und Physiksimulation. | `13-unreal-games` | Wiederholbarer Physik-Testlevel |
| 13 | **Multiplayer & Online Systems** | Netzwerkspiel, Server, Lobby, Synchronisation und optional kooperative Spielmodi. | `05-core-api` | Autorisierte Testserver-Konfiguration und Testprotokoll |
| 14 | **Ultimate Game Interface** | Spielmenüs, HUD, Karte, Inventar, Controller, Einstellungen und Barrierefreiheit. | `04-ux-avalonia` | Echte Engine-UI und Navigationstests |
| 15 | **Save & Progression System** | Spielstände, Checkpoints, Charakterentwicklung, Entscheidungen und Spielverlauf. | `06-data-memory` | Versionierte Save-Datei und Migrationstest |
| 16 | **Autonomous Game QA** | Automatisierte Gameplay-Tests, Fehlererkennung, Performance, Regression und Testberichte. | `19-qa-verification` | Laufprotokolle und Spielbarkeitsbericht |
| 17 | **Game Optimization Director** | CPU-, GPU-, RAM-, Streaming- und Asset-Optimierung für verschiedene Geräte. | `18-performance-reliability` | Messwerte pro Zielplattform/Hardwareklasse |
| 18 | **Game Release & Publishing** | Builds, Export, Installationspakete, Versionsverwaltung und Veröffentlichungsfreigabe. | `20-release-compliance` | Reproduzierbarer Testbuild, Freigabe-Gate |

## Technische Grenzen und QA-Trigger
- **Unity ausdrücklich zusätzlich bestätigt** zu den 18 oben gezählten Verbesserungen. Nicht als 19. externes Studio; als feste Engine-Option innerhalb Nummer 01 und als Adapter in zugehörigen Funktionen.
- Unity: C#-Runtime, Editor-Skripte, Prefabs/Szenen, Unity Test Framework (EditMode/PlayMode), BuildPipeline/Build Profiles. `-batchmode -quit -projectPath` und `-executeMethod` nur nach Editor-Version-Prüfung. Unity Hub/Editor nicht automatisch nachinstallieren.
- Unreal: C++ und Blueprints für Gameplay; Python nur Editorautomation. Godot GDScript/C# je Engine-Build. Keine erdachten MCP-Server.
- Erst Level spielbar, dann Character/AI/OpenWorld/Multiplayer. Multiplayer nur autorisiertes isoliertes Testnetz; kein offener Server per Default.
- Negativtest: fehlende Lizenz/Editor, ungültige Szenen-Datei, fehlender Shader, nicht vorhandene Assets, korrupte Save-Datei, Netzwerkoffline, ungenehmigter Publish.

## Spezifische Testfälle
Zu allen 18 Verbesserungen stehen in `qa/TESTFAELLE.csv` jeweils ein überprüfbarer Positiv- und Negativfall (insgesamt 36). Die Tests sind **nicht** durchgeführt; Claude muss automatisierte Testprogramme nach Implementierung hinterlegen.

## Ergebnisbericht
Jede Teilaufgabe erzeugt: veränderte echte Projektdatei, Vorschaubild oder Audio, Build/Test-Log mit Exitcode, Performance-/Lizenzbericht, Git-Diff, verbleibende Einschränkungen. Kein TESTED ohne Beleg.
