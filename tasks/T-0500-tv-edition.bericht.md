# Bericht T-0500 – TV-Edition M1 (Stand 05.11.2026)

**Auftrag:** `tasks/T-0500-tv-edition.md` · **Branch:** `claude/T-0500-tv-edition` (lokal, nicht gepusht)

## Geänderte Dateien
`tasks/T-0500-tv-edition.md`, dieser Bericht, `tv/android/**` (neu), `reports/tv/**` (Belege). Windows-/Linux-/Pi-Code unverändert.

## Werkzeuge (nutzerlokal, keine Systeminstallation)
Android SDK unter `C:\Users\<user>\TrendoEntwicklung\android-sdk`: cmdline-tools 23.0.0 (SHA-1 geprüft), platform-tools 37.0.1, platforms;android-35, build-tools 35.0.0; Android-SDK-Lizenz nach Freigabe angenommen. Gradle 8.11.1 (SHA-256 geprüft) nur zum Erzeugen des Wrappers. Signaturschlüssel lokal außerhalb des Repos.

## Kommandos und Ergebnisse
| Schritt | Ergebnis |
|---|---|
| `gradlew testDebugUnitTest assembleDebug` | BUILD SUCCESSFUL; AppCatalogTest 4/4, AppGuardTest 3/3; Log `reports/tv/T-0500-m1-build-2026-11-05.log` (SHA-256 `1a7b997a…dcc597e`) |
| `gradlew testReleaseUnitTest assembleRelease` 0.1.0 | BUILD SUCCESSFUL, APK 1 549 850 Byte, SHA-256 `8082ac7b…d959fd20`, Zertifikat SHA-256 `a9039051…6a32f83` |
| Installation 0.1.0 auf CHiQ (nach Freigabe) | `Success`; Paketvergleich vorher/nachher: **genau +1** (`os.trendo.tv`), deaktivierte Pakete unverändert, Standard-Home unverändert `com.google.android.tvlauncher` |
| Start am CHiQ | `am start -W`: Status ok, TotalTime 1370 ms; kein Eintrag im Crash-Puffer; Screenshot `reports/tv/T-0500-chiq-start-2026-11-05.png` |
| Speicher | Prozess ca. 74 MB, nach Wechsel in den Hintergrund vom Hersteller-LMK beendet (normal bei 1,35 GB RAM) |
| 0.1.1 (Startfokus) | Release gebaut, SHA-256 `b9454989…f07a7a`; **noch nicht installiert** |

## Funktionsstatus
| Nr | Funktion | Status |
|---|---|---|
| 01 | TV & Streaming Hub (Basis) | REAL IMPLEMENTIERT, AUF CHiQ GESTARTET (Kacheln sichtbar; App-Start per Fernbedienung noch nicht geprüft) |
| 04 | Smart App Organizer (Basis) | REAL IMPLEMENTIERT, AUF CHiQ SICHTBAR (TiviMate + Play Store als Favoriten) |
| 10 | Recovery (Teil: „Alles rückgängig", Original-Startseite) | REAL IMPLEMENTIERT, nicht auf CHiQ ausgelöst |
| 11 | Ad-Free Launcher | REAL IMPLEMENTIERT (keine Werbe-/Analytics-SDKs) |
| 22 | Focus Navigation | 0.1.0 ohne Startfokus (Fehler gefunden), Korrektur in 0.1.1 gebaut |
| alle übrigen | GEPLANT |

## Offen / Risiken / nicht geprüft
- Emulator-Test BLOCKIERT (keine Hardware-Beschleunigung auf dem PC).
- Fernbedienungs-Durchlauf, Start von TiviMate/Play Store aus Trendo, Offline-Test, 60-min-Leerlauf, 5 Neustarts: NICHT geprüft.
- Paketliste nur lokal gesichert (Scratchpad), nicht im Repo.
- `dumpsys` am CHiQ eingeschränkt („Service dumps disabled").
- Kein Push, kein PR, kein Merge, kein Release.
- Löschwunsch Disney+/Kodi/AI Pont/Google Play Spiele: nicht ausgeführt (Datenverlust, Übergabe verbietet Fremd-App-Deinstallation); Anleitung zum Selbstmachen an Manuel gegeben.
