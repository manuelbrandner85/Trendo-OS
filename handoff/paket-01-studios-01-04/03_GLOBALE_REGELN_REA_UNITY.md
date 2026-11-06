# VERBINDLICHE GLOBALE REGEL – in JEDEM künftigen Vier-Studio-Paket

Freigaben aus dem Nutzerdialog (2026-11-05):
**G1: REA / Clean-Room-Nachbau** – Trendo OS soll benötigte Fähigkeiten nach Möglichkeit selbst als echte native Funktionen besitzen, statt Nutzer zu unnötigen separaten Programmdownloads zu zwingen.
**G2: Unity** – Unity ist eine ausdrücklich genehmigte Engine-/Tool-Option; in **jedem Paket berücksichtigen, wo es für die jeweilige Studiofachlichkeit tatsächlich relevant ist** (nicht künstlich in rein fachfremde Studios einbauen).

## Technische REA-Entscheidungsmatrix
1. Vorhandene native Funktion im Trendo-Code: **wiederverwenden/verbessern**.
2. Feature fehlt: mit REA **zulässige Funktionen, sichtbare Workflows, Dateiformate und Interaktionen analysieren**, unabhängig selbst implementieren, eigene UI/Icons/Texte/Assets entwickeln, mit Tests beweisen.
3. Bibliothek, Codec oder quelloffene Komponente mit passender kommerzieller Lizenz: legal **integrieren** und Attribution/SBOM/Updatepolitik liefern.
4. Anspruchsvolle Drittengine oder Hardware-Abhängigkeit: **offiziellen Adapter nutzen** statt unvernünftigen Full-Clone. Gilt insbesondere Unity Editor/Engine, Unreal Engine, Blender (wo vollwertige DCC-Features externe Laufzeit voraussetzen), Device-Treiber und proprietäre Hardware-SDKs. Keine unautorisierte Installation.
5. Wenn überhaupt nicht möglich: `NOT_AVAILABLE` + Grund + sinnvoller Fallback, kein Fake-Button und keine vorgetäuschte Anbindung.

REA ist KEINE Freigabe zum Kopieren fremder Quellen, Assets, Logos, Marken, Original-UI-Bilder, geheimen Protokolle oder zum Umgehen von Lizenzen/DRM/Schutzmaßnahmen. Für riskante Reverse-Engineering-Methoden erst Rechtsprüfung/Freigabe. Nur Clean-Room-Verhaltensnachbau aus legalen Referenzen.

## Unity nach Fachgebiet
- **Studio 1 Spiele:** volles Engine-Ziel `Unity` inklusive Projekt, C#-Skripten, Szenen/Prefabs, Import, Shadern, Physik, Animation, Build und Edit-/Play-Mode-Tests. Gleichrangige Wahl zu Unreal/Godot, keine universelle Einheits-API vorspiegeln.
- **Studio 2 3D:** optionale Import-/Exportprofile für Unity (FBX/glTF je verfügbarer Pipeline), Material-/Rig-/Animationskonvertierung, echte Unity-Importprüfung. 3D-Editor nicht als Unity-Clone bauen.
- **Studio 3 Film/VFX:** optionale Unity-Timeline-/Cinemachine-/Recorder-/Kameradaten-Pipeline und Filmsequenz-Export, soweit Unity-Pakete/versioniert verfügbar und lokal installiert.
- **Studio 4 Musik/Audio:** optionale Unity-AudioSource/AudioMixer/Event-Anbindung, Loop-/Stem-/Raumklang-Export, E2E-Test in Unity-Projekt. Ohne Unity kein Zwang.

**Immer** Version, Lizenz, vorhandene Installation, Add-ons, Rechte, Buildziel, OS, GPU/Speicher, Paketabhängigkeiten checken. Nur mit Nutzerfreigabe systemweit installieren, Lizenzen akzeptieren oder externe Dienste verwenden. Verbindungsstatus ausschließlich aus echtem Healthcheck. Einheitliches Contract-Format `capability.json`, `job.json`, `artifact.json` + `license-provenance.json` projektlokal und keine Secrets in Dateien.

Diese Datei muss als unveränderte Policy-Version in Paket 02 (Studios 5–8), 03 (9–12) usw. enthalten sein, bis eine ausdrückliche neue Nutzerentscheidung sie ersetzt. Für reine Bild-/Office-Studios genügt „Unity nicht erforderlich“ mit Begründung; keine Dummy-Integration.
