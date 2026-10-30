# Verbesserung 113 – Audit gegen den Repo-Stand (read-only)

**Gegenstand:** `VERBESSERUNG_113_GODMODE.md` (Ultimate VR, AR & Spatial Computing Native Suite), `V113_FUNKTIONSREGISTER.json` (50 Funktionen, alle `APPROVED_SPEC`), `V113_QA_100_EINZELTESTS.csv` (100 Tests: 50 positiv, 50 negativ, alle `NOT_RUN`).
**Repo:** `C:\Users\<user>\Desktop\Trend Os`, HEAD `7e1b8634378a424a2b69cc9e4d70c0dbe5bfb322` (identisch mit dem `read_only_head` im Register). Arbeitsbaum: nur `?? TrendoOS-Setup-0.9.10.exe` (nicht versioniert).
**Methode:** Nur gelesen und gesucht. Nichts gebaut, keine Tests gestartet, kein Netz. Testbelege sind Dateien und DisplayNames. Der letzte volle Lauf war grün (Api 15/15, Core 845, App 432) auf Commit `c062bb3` vom 30.10.2026. Das ist **kein** PC_VERIFIED für 113.
**Detailtabelle:** `V113.csv` (50 Zeilen, `;`, UTF-8).

## 1. Funktionen je Status

| Status | Anzahl | Funktionen |
|---|---|---|
| IMPLEMENTIERT_MIT_TEST | **0** | – |
| IMPLEMENTIERT_OHNE_TEST | **0** | – |
| TEILWEISE | **14** | VR-02, VR-09, VR-10, AR-12, AR-14, SPI-21, SPI-24, SPI-28, SIM-35, SIM-36, SIM-38, QA-44, QA-46, QA-48 |
| BLOCKED_HARDWARE | **14** | VR-03, VR-06, AR-13, AR-16, AR-17, AR-18, AR-20, SPI-22, SPI-23, QA-41, QA-42, QA-43, QA-45, QA-47 |
| NUR_SPEZIFIZIERT | **22** | VR-01, VR-04, VR-05, VR-07, VR-08, AR-11, AR-15, AR-19, SPI-25, SPI-26, SPI-27, SPI-29, SPI-30, SIM-31, SIM-32, SIM-33, SIM-34, SIM-37, SIM-39, SIM-40, QA-49, QA-50 |

**Wie „TEILWEISE“ zustande kommt:** Fast alles stammt aus SPEC-04 „VR/AR/MR 2.0“. Dieses Modul steht in `requirements.yaml` als IMPLEMENTED/PC_VERIFIED. Der Beleg ist `reports/pc-0.9.2/tests-pc-4e6b81d.txt`. Die wichtigsten Teile:
- `src/Trendo.Core/W6/XrModels.cs`: `XrInspector.Inspect/Check/FrameBudget`
- `src/Trendo.Core/W6/XrModels2.cs`: `XrPlus.Detail/ScaleVerdict/TexelDensity/Comfort/LodPlan/Report`
- `src/Trendo.App/MainWindow.Spec04.cs`: Tab „VR & AR“, FachTool-Formulare

Dazu kommen vorhandene Bausteine:
- 3D-Editor: `Scene3D`, `Mesh3D`, `MeshIO`, `Studio3D`, `MainWindow.W4Studio3D.cs`
- `WindowManager`
- Kameras: `Cameras.cs`
- `TwinHub` und `TwinPlus`
- `SystemMetrics`
- A11y (`Sys12.cs`)
- Whisper-Diktat (`MainWindow.TextPro.cs`)

Keiner dieser Bausteine erfüllt eine der 50 Funktionen vollständig.

**Nicht gefunden (in `src/`, `tools/`, `tests/`):** OpenXR, ActiveRuntime, WebXR (`navigator.xr`, `isSessionSupported`), ARCore/ARKit, Physik-Solver, Hand-, Blick- und Haptik-Code, Spatial Audio, Snapshot-Ringpuffer. Die geforderten Schemas (`trendo-xr-project.v1`, `xr-device-snapshot.v1`, `xr-test-run.v1` usw.) gibt es ebenfalls nicht. Den Ordner `reports/verbesserung-113/` gibt es noch nicht.

## 2. Abdeckung der 100 QA-Einzeltests

| Kategorie | Anzahl |
|---|---|
| Durch vorhandene Tests vollständig abgedeckt | **0 / 100** |
| Teilweise bzw. analog berührt (gleiche Logik, aber nicht der Q113-Test) | **10 / 100** |
| Ohne jeden Bezug | **90 / 100** |

Die 10 teilweisen Fälle mit Belegen:
1. **Q113-02-N:** `Scene3DTests` „3D: Text wird zu Flächen, OBJ/STL/GLB kommen wieder herein“. Ungültige Dateien werfen eine Ausnahme. Nicht geprüft werden nicht-endliche Werte, Quaternionen und Einheitenverdopplung.
2. **Q113-09-N:** `W6Tests` „SPEC-04: GLB lesen, Dreiecke zählen, Geräte-Grenzen und Bildzeit prüfen“. Ein Modell über Budget wird erkannt. Ein READY/RELEASE-Block fehlt.
3. **Q113-14-N:** `W6Tests` „SPEC-04 F08: Maßstab prüfen“. Erkannt werden 0,002 m und 2000 m. Ein Faktor 100 zwischen 1 cm und 500 m bleibt unerkannt.
4. **Q113-21-P:** `WindowManagerTests` „Fenster: Zustand sichern und auf neuer Verwaltung wiederherstellen …“. Das deckt nur den Desktop-Teil ab, nicht den XR-Raum.
5. **Q113-25-N:** `EveryButtonAllPagesTests` „T-0202: Kein Klick auf irgendeiner Seite stürzt ab (Klick-Audit, Bericht der stummen Knöpfe)“. Gilt nur für 2D.
6. **Q113-36-P:** `W6Tests` „SPEC-10 F10: Ereignisprotokoll“ und „SPEC-10 F11: Hysterese“. Es geht um Schwellenereignisse aus Messdaten, nicht aus einer Simulation.
7. **Q113-38-P:** `W6Tests` „SPEC-10 F12: Zeitraum-Kennzahlen“ und „SPEC-10 F16: Export“.
8. **Q113-44-N:** `PromptActionTests` „Bildzeit rechnet das Budget und startet keine Brille“ und `W6UiTests` „SPEC-04 UI: GLB-Modell prüfen und Bildzeit“. Eine Überschreitung ergibt „nicht flüssig“. Fehlende GPU-Zeit und eine zu kurze Messung werden nicht geprüft.
9. **Q113-46-P:** `W6Tests` „SPEC-04 F10: Bewegungs-Komfort“. Nur Heuristik, keine Checkliste, kein Exit.
10. **Q113-48-N:** `MusikUiTests` mit `Assert.Null(SystemMetrics.ParseGpuMemory("[N/A]"))`. Ein fehlender Wert wird null statt 0. Es geht um den PC, nicht um ein XR-Gerät.

**Tests, die echte XR/AR-Hardware oder Runtime brauchen (meine Einordnung aus den Testtexten):** etwa 25 Positivtests.
- VR-03, 04, 05, 06, 08, 09, 10
- AR-12 bis AR-20
- SPI-22, 23, 29
- QA-41, 42, 43, 44, 45, 47

Die meisten Negativtests (BLOCKED, UNSUPPORTED, N. V., keine Fake-Erfolge) lassen sich ohne Gerät mit Fake-/Simulator-Adaptern prüfen.

## 3. Wichtigste Lücken

1. **Es gibt keine XR-Capability-Schicht.** Es fehlen `IXrDeviceProbe` und `IXrRuntimeSession` sowie ein Fake-Adapter mit `source=SIMULATOR`, der nie `DEVICE_TESTED` meldet. Davon hängen VR-03/06, AR-11..20, SPI-22/23/30 und QA-41..49 ab.
2. **Der Koordinaten- und Einheitenvertrag fehlt.**
   - `MeshIO.WriteGlb` (`src/Trendo.Core/MeshIO.cs`, Z. 80–81 und 108) rechnet nur cm→m (×0,01). Es gibt keinen Achswechsel von Z-oben (Trendo) auf Y-oben (glTF), und der Node hat keine `rotation`/`matrix`.
   - Der Rundreisetest prüft nur `Sx` eines symmetrischen Würfels und kann so einen Achsfehler nicht finden.
   - Das betrifft VR-02, AR-14, SIM-31 und Q113-X11.
3. **Es gibt keine XR-Projekt- und Testlauf-Schemas.** `trendo-xr-project.v1`, `xr-test-run.v1` und die Ablage unter `reports/verbesserung-113/` fehlen. Ohne sie ist keine Evidenzkette (SHA-256, Rohlog) möglich.
4. **Die Ehrlichkeit der Texte ist nicht gesichert.**
   - `MainWindow.Spec04.cs` Z. 22 meldet bei Budget-PASS „läuft flüssig“. Spec Abschnitt 0 verbietet genau diese Gleichsetzung.
   - Das Feld „Gemessen (ms)“ (Z. 25–27) ist eine Nutzereingabe ohne Herkunftsangabe. Der Adapter (`StudyPromptAdapter.cs` Z. 485) schreibt dagegen korrekt „Keine Brille ist gelaufen.“
5. **Native Arbeitsflächen fehlen.** SPEC-04 besteht aus FachTool-Formularen mit Textausgabe. Spec Abschnitt 0 lehnt genau das ab. Die fünf Studios (VR, AR, Spatial, Simulation, XR-QA) mit Viewport, Inspector und Undo existieren nicht. Ausbaufähig ist nur der 3D-Editor `MainWindow.W4Studio3D.cs`.

## 4. Hardware- und Gerätebedarf (nichts davon im Repo festgestellt)

- **PC-VR:** OpenXR-Runtime (aktive Runtime erst auf dem PC feststellen) und ein Headset mit Controllern. Nötig für VR-03..06/08..10, QA-41..45/47 und 120 s Frametime-Messung (QA-44).
- **Hand- und Eye-Tracking:** Headset mit beworbenen Extensions (VR-06, SPI-22/23).
- **Android-AR:** kompatibles ARCore-Gerät mit Depth-API, Android SDK und Debug-Signatur (AR-11..18, AR-20). **iOS:** auf Windows immer BLOCKED.
- **WebXR:** unterstützter Browser mit immersive-ar im Secure Context auf einem Gerät (AR-19).
- **Messaufbau:** Motion-to-Photon (QA-45) und Drift-Referenzpunkt (QA-47) brauchen externe Messmittel.
- **Software (optional):** Unreal Engine mit OpenXR-Plugin (Lizenz prüfen), Blender (für die Rundreise), ein deterministischer Physik-Solver (SIM-31). Installation jeweils nur mit Zustimmung.
- **Grundsatz:** Ein geräteloser Simulator ist kein Hardwarepass. Latenz und FPS werden nur gemessen, nie geschätzt.

## 5. Konflikte und Entscheidungsbedarf (Decision-Delta, nicht selbst entscheiden)

1. **113 steht in keinem Register.** In `requirements.yaml` und `docs/spec/decisions.yaml` gibt es keinen Eintrag zu IMP-113. Auch die in Spec Abschnitt 16 genannte Datei `docs/claude/TRENDO_VERBESSERUNG_113_VR_AR_SPATIAL_GODMODE.md` fehlt im Repo. Den Eintrag kann nur Manuel freigeben.
2. **Doppelung mit IMP-65.** „Ultimate VR, AR & Mixed Reality Studio GODMODE“ hat 50 Funktionen, steht auf `APPROVED` / `NOT_STARTED` und hat keine Funktionsliste in `improvement_functions` (`requirements.yaml` Z. 1324; `decisions.yaml` IMP-65). Ob 113 IMP-65 konkretisiert oder ersetzt, muss entschieden werden. Sonst droht ein zweites XR-Studio (Regel: keine zweiten Kerne).
3. **SPEC-04 ist PC_VERIFIED.** 113 muss den bestehenden Tab `xr` (`MainWindow.Spec04.cs`) und `XrInspector`/`XrPlus` erweitern, nicht duplizieren.
4. **Designwiderspruch.** Spec Abschnitt 0 verlangt „kühle blaue Premium-Optik, keine Gold-Neugestaltung“. `CLAUDE.md` sagt dagegen: Logo C „Gold-Orbit“ (27.10.2026), „Gold-Blau ist die Leitfarbe“. Manuel muss klären, welche Vorgabe für die XR-Studios gilt.
5. **Übrige Sperren:**
   - 86 NEIN: kein Bezug gefunden.
   - 97 OFFEN (Invention, Prototyping & Product Validation): SIM-31..40 nicht zu einer Produktvalidierung ausbauen.
   - SPEC-07 abgelehnt: SPI-24 (Sprache) darf keine Übersetzungs- oder Lokalisierungsfunktionen bringen.
   - 114+ pausiert: Spec Abschnitt 17 bestätigt das.
   - Kosten und Installationen: OpenXR-Runtime, Android SDK, Unreal, Solver und Whisper-Modelle nur mit Zustimmung. Kein Store-Upload, kein ADB-Install, keine Aktoransteuerung aus der Simulation (Spec Abschnitt 9.2: DENY).
   - AION/GENESIS: nur Kopien als Fixtures, keine Story- oder Gameplayänderung.

## 6. Vorschläge für kleine erste Arbeitspakete (ohne XR-Hardware testbar)

**AP-1: Ehrliche Texte für SPEC-04 und den GLB-Achsvertrag** (Builder `05-core-api` + `12-blender-assets`, QA `19-qa-verification`)
- `src/Trendo.Core/MeshIO.cs` (`WriteGlb`): Z-oben→Y-oben ausdrücklich umrechnen (Node-Rotation oder Vertex-Umrechnung) und im Manifest dokumentieren.
- `src/Trendo.App/MainWindow.Spec04.cs`: „läuft flüssig“ ersetzen durch „innerhalb Modellbudget (keine Messung)“. „Gemessen (ms)“ als „eingegeben“ kennzeichnen.
- Tests in `tests/Trendo.Core.Tests/Scene3DTests.cs` und `W6Tests.cs`:
  - asymmetrische L-Form exportieren und wieder einlesen, Lage, Achsen und Maße mit Toleranz prüfen (Teil von Q113-02-P/N und X11)
  - Negativfall NaN/∞ im Teil wird abgelehnt
  - UI-Text enthält kein „flüssig“ ohne Messung
- Abhängigkeit: Manuel muss klären, ob 113 überhaupt umgesetzt wird (siehe Konflikte 1 und 2).

**AP-2: XR-Capability-Probe mit Fake-Adapter** (Builder `05-core-api` + `15-device-fusion`, QA `19-qa-verification` + `03-security-permissions`)
- Neu: `src/Trendo.Core/W6/XrCapability.cs` mit `XrDeviceSnapshot` (`source=HARDWARE|SIMULATOR`, `observed_at_utc`, Runtime/Version nullable) und `IXrDeviceProbe`.
- `SimulatorXrProbe` meldet nie `DEVICE_TESTED`. `NoRuntimeProbe` liefert `NICHT_VERFÜGBAR` mit Grund.
- Tests in `tests/Trendo.Core.Tests/XrCapabilityTests.cs` decken Q113-03-N, Q113-41-N und Q113-45-N (N. V. statt 0) vollständig ohne Gerät ab.
- Eine echte OpenXR-Abfrage (Loader/ActiveRuntime) folgt erst danach als eigenes, read-only Paket nach Freigabe.
