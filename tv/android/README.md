# TRENDO OS – Premium TV Edition (Android TV)

Eigenständige Android-TV-App (Kotlin, Compose for TV). Auftrag und Backlog: `tasks/T-0500-tv-edition.md`.

**Stand:** M1-Quellcode geschrieben, **noch nicht gebaut** (Android SDK fehlt auf dem Entwicklungs-PC), nicht im Emulator, nicht auf dem CHiQ getestet.

## Was M1 kann (Code)
- Startseite mit farbigem T, „TRENDO OS · PREMIUM TV EDITION · by Manuel", Uhr
- Reihen „Favoriten" (Vorschlag beim ersten Start: TiviMate, Google Play – erkannt am Namen), „Alle Apps", „System"
- Apps werden über ihren eigenen Original-Starteintrag geöffnet (`getLeanbackLaunchIntentForPackage`)
- System: TV-Einstellungen, Original-Startseite
- `AppGuard`: nur „Starten" und „Infoseite öffnen" erlaubt – keine Funktion zum Löschen/Deaktivieren fremder Apps
- Home-Eintrag (`HomeAlias`) ab Werk aus

## Bauen (nach Einrichtung des SDK)
```
gradlew testDebugUnitTest
gradlew assembleDebug
```
`local.properties` (nicht im Repo) enthält `sdk.dir=…`. Keystore und `keystore.properties` bleiben lokal.
