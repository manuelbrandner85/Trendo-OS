# Unity-Integration – fachlich notwendiger Standard für alle künftigen Pakete

Unity wurde ausdrücklich nachträglich genehmigt. Es steht für `CORE-06` auf einer Ebene mit Unreal/Godot und ist **kein** nachgebautes oder eingebettetes Unity-Binary.

## Capability Discovery (read-only)
- Prüfe lokal vorhandenen Unity Hub/Editor durch deklarierte Pfade/Installationsmetadaten – nur in Nutzerfreigegebenen Verzeichnissen. Editor-Version, Lizenz-/Aktivierungsstatus, Module/Build Targets, vorhandene Pakete und Plattformkompatibilität protokollieren.
- Für Unity-Tasks `engine_id=unity`, `version`, `projectPath`, `unityBinary`, `targetPlatform`, `scriptCompilation`, `testMode`, `offlineMode`, `requiresApproval` führen.
- Fehlende Unity-Installation → `BLOCKED: unity-not-installed`, nie automatisch herunterladen.
- Lizenz/Editor erfordert Anmeldung → ehrliche Handlungsaufforderung, keine Anmeldedaten auslesen, keine Umgehung.

## Kontrollierter CLI-Workflow (nur nach vorhandener Engine/Projekt/Arbeitsfreigabe)
Offizielle CLI-Unterstützung: `-projectPath`, `-batchmode`, `-quit`, `-logFile`, `-executeMethod`, `-buildTarget` oder Build Profiles; Syntax zur tatsächlichen Editor-Version prüfen.
1. Projektversion lesen; temporären Projektclone/Worktree verwenden; Package Lock und Assets ohne Überraschungsupgrade sichern.
2. Generierte Editor-C#-Scripts in `Assets/Editor/` oder passende Assembly Definition; Gameplay-C# getrennt von Editor-C#.
3. Import unter unveränderlichen GUID-/Meta-Dateien, verifizierte Szenen/Prefabs/Animator Controller.
4. EditMode- und PlayMode-Tests im Unity Test Framework; Logs, Exitcodes, Testcount, Artefakte speichern.
5. Plattform-Build nur bei vorhandener Buildunterstützung, nicht veröffentlichen. Resultat und Hash belegen.

## Trendo-spezifische Adapter-Capabilities
`unity.project.open`, `unity.project.create`, `unity.scene.edit`, `unity.prefab.edit`, `unity.import.3d`, `unity.animation.import`, `unity.material.map`, `unity.audio.import`, `unity.timeline.export`, `unity.build.local`, `unity.test.editmode`, `unity.test.playmode`, `unity.preview.local`.
Status je Capability separat `NOT_PRESENT | PLANNED | IMPLEMENTED | TESTED | BLOCKED` – keine vorgetäuschte Vollständigkeit.

## Film/3D/Musik
3D-Szenen können in Unity importiert werden, sofern passender Exporter/Package installiert. Filmschnitt bleibt in Trendo; Unity Timeline/Cinemachine/Recorder nur optional und abhängig von lokaler Unity-/Package-Version. Musik-/Audio-Export: WAV/OGG/Stems und Event-Mappings nach Unity-AudioSource/AudioMixer. Kein Unity-Build nur zur Audiowiedergabe.

## Test- und Sicherheitsfälle
- Positiv: installiertes kompatibles Unity erzeugt lokal eine Szene, importiert Modell+Audio, Editor-/PlayMode-Testlog, kontrollierten Testbuild ohne Veröffentlichung.
- Negativ: Unity fehlt, Lizenz ausstehend, Projektversion unpassend, Import verändert GUIDs, fehlerhafte Plugins, unfreigegebene Installation, unbekannte Pfade, Timeouts. In allen Fällen keine unautorisierten Side Effects.

Dokumentationsgrundlage: https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html sowie https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.test-framework.html (bei jeder Verwendung die aktuelle Version prüfen).
