# Beleg S1-01 – Trendo-Spiel als Unity-Projekt, echter Lauf (06.11.2026)

- Unity: 6000.6.8f1 (Unity 6.6) mit Windows Build Support, Lizenz über die eigene Anmeldung des Nutzers im Unity Hub
- Projekt: Inhalte aus EngineTargets.CreateUnityProject (2 Level: Wiese, Burg)
- Befehle: Unity.exe -batchmode -nographics -projectPath <Projekt> -runTests -testPlatform EditMode|PlayMode -testResults … -logFile …; Build: -batchmode -quit -executeMethod TrendoBuild.BuildWindows

| Schritt | Exitcode | Ergebnis |
|---|---|---|
| EditMode-Tests | 0 | result="Passed" total="3" passed="3" failed="0" |
| PlayMode-Tests | 0 | result="Passed" total="1" passed="1" failed="0" |
| Windows-Build | 0 | TRENDO-BUILD Succeeded 95937094 Bytes |
| Gegenprobe (Level „Burg“ ohne Ziel), EditMode | 2 | result="Failed(Child)" total="3" passed="2" failed="1" – Meldung „Ziel fehlt in Burg“ |

## SHA-256
- EditMode-ergebnis.xml: 59b66a7a102e416c9c8868f93333bf644447c82465ca73094f5552c3c873ac54
- PlayMode-ergebnis.xml: ee35b4c088aef5d0464297185358281994898a34038551925acdc956bc54c300
- Neg-ergebnis.xml: 11b727dfafb1f05b08bbee2ca06475306250f616899653cf76f57cf280804aee
- Build/Windows/Testspiel-Unity.exe: 8060790b33cf3ca520e289bf961094e2ed5473c7fad5d6148d8489e96055d27b
- Assets/Scripts/TrendoLevel.cs: 375d398dae487d8609c05e49a98859503c57ba9257ae8d00b5117f9f6dfd1c07
- Assets/Scripts/TrendoPlayer.cs: 0429d66008281de9c7e07c80623501e635eb194c26c2320a49d35a34c39bc56b
- Assets/Scripts/TrendoLevelData.cs: 9c90d384a53d10d918f27b451d11df0932501a8a9d7623b1146d5c16c2a24b94
- Assets/Editor/TrendoBuild.cs: 68a5d9d392dc0438423883a413806a7aed8a2518e10ca0447286930d26d29eef
- Assets/Tests/EditMode/TrendoLevelTests.cs: 30f630cceed21c9038d83bba6b199f0592074f95743456f6d7cc9a76bf311e4f
- Assets/Tests/PlayMode/TrendoPlayTests.cs: 6c56dfd78a8d230df6d17079e7c2a5da020a05f67b9dc65f1a5610854df221fb

## Rohlog-Auszüge

### EditMode
```
[Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-<user>" (connect: 0.00s, validation: 0.00s, handshake: 0.22s)
Test run completed. Exiting with code 0 (Ok). Run completed.
```

### PlayMode
```
[Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-<user>" (connect: 0.00s, validation: 0.00s, handshake: 0.06s)
Test run completed. Exiting with code 0 (Ok). Run completed.
```

### Build
```
Build Finished, Result: Success.
TRENDO-BUILD Succeeded 95937094
```

Nicht geprüft: Spielen von Hand im gebauten Spiel, Steuerung, Darstellung im Fenster.
