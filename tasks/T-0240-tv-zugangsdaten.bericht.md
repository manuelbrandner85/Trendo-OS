# Bericht T-0240 – TV-Zugangsdaten in den sicheren Speicher

- **Auftrag:** `tasks/T-0240-tv-zugangsdaten.md` (Freigabe im Chat am 30.10.2026)
- **Branch:** `claude/T-0240-tv-zugangsdaten` (Basis `main` @ 7e1b863)
- **Status:** IMPLEMENTED, automatisierte Tests grün. NICHT PC_VERIFIED (Umzug mit echten Daten am PC noch nicht gelaufen).

## Geänderte Dateien
- `src/Trendo.Core/Tv/IpTv.cs`: neue `TvCredentials` (Platzhalter `{trendo-pass:<id>}`, `Scrub` nur an `/<benutzer>/<passwort>/` und `password=`, `Reveal` URL-kodiert, null bei fehlendem Passwort); `TvLibrary(dataRoot, ISecretStore?)` mit `PasswordOf/SetPassword/Reveal/MigrateSecrets`; `AddSource` mit Passwort; Platzhalter beim Speichern von Liste, Filmen, Serien, Folgen, EPG-Adresse; `RemoveSource` löscht das Passwort.
- `src/Trendo.App/MainWindow.Tv.cs`: `Tv` mit `_core.Secrets`; Einsetzen in `TvFetch`, `TvPlayUrl`, Aufnahme; Meldung bei fehlendem Passwort; Umzug beim Start im Hintergrund (nur wenn nötig), Ladeanzeige währenddessen.
- `src/Trendo.App/MainWindow.TvVod.cs`: Xtream-Zugang aus der eingesetzten Adresse.
- Tests: `tests/Trendo.Core.Tests/IpTvTests.cs` (+2), `tests/Trendo.App.Tests/TvCredentialsUiTests.cs` (neu, 2), `tests/Trendo.App.Tests/TvStudioUiTests.cs` (Dateien ohne Passwort).
- Version 0.9.16, `installer/neuerungen/0.9.16.md`.

## Kommandos und Ergebnisse
- `dotnet test Trendo.sln -c Release` → Exit 0: Api 15/15, Core 847 bestanden/1 übersprungen, App 434 bestanden/3 übersprungen
- `python -m unittest discover -s tests/claude_handoff` → 8 Tests OK
- Rohlog: `reports/T-0240/volltest.log`, SHA-256 `4b4febb3a7b42f7a309336973b8b699ab303a5935aa150c57cf394de617a9c89`
- Probelauf ohne Änderung an echten Daten: Ersetzung erfasst alle 14 164 Passwortstellen einer vorhandenen Senderliste, 0 übrig (nur gezählt, nichts ausgegeben).

## Offene Punkte / Risiken
- Umzug am PC beim ersten Start von 0.9.16 noch nicht beobachtet.
- Die Sicherung `tv.json.vor-0240` wird ebenfalls bereinigt; eine Rückkehr auf 0.9.15 würde Platzhalter sehen und nicht abspielen (kein Absturz).
- Passwörter in bereits vorhandenen Absturzberichten früherer Versionen werden nicht nachträglich bereinigt (dort war das Passwort laut Prüfung nicht enthalten).

## Nicht geprüft
Echte Wiedergabe beim Anbieter (bewusst), Linux/Pi.
