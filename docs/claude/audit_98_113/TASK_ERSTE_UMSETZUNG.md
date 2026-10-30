# Erster Umsetzungsauftrag: T-0240 – TV-Zugangsdaten in den sicheren Speicher

**Warum zuerst:** echte Sicherheitslücke im heutigen Code (Audit-Befund 1, Verbesserung 98), klein, ohne neue Abhängigkeit, vollständig automatisiert testbar, berührt keine offene Entscheidung.

## Ist-Zustand (nachgewiesen)
- `src/Trendo.App/MainWindow.Tv.cs` `TvAddSource`: Xtream-Zugänge werden zu `Xtream.PlaylistUrl(server, user, pass)` zusammengesetzt und als `TvSource.Url` gespeichert.
- `src/Trendo.Core/Tv/IpTv.cs` `TvLibrary.AddSource` schreibt das in `<daten>/tv/tv.json` – Passwort im Klartext.
- Vorhanden und ungenutzt dafür: `src/Trendo.Core/Secrets.cs` (`ISecretStore`, `SecretStoreFactory`).

## Ziel
1. Für Xtream-Quellen speichert `tv.json` nur Server und Benutzernamen; das Passwort liegt im `ISecretStore` unter `tv:<quellen-id>`.
2. Die Wiedergabe- und Schnittstellenadressen werden erst beim Abruf zusammengesetzt (`Xtream.PlaylistUrl/Api` mit Passwort aus dem Speicher).
3. **Umzug alter Daten:** Beim ersten Start mit der neuen Version werden vorhandene Einträge mit `password=` in der Adresse umgezogen (Passwort in den Speicher, Adresse bereinigt) – mit Audit-Eintrag ohne Passwort.
4. Quelle entfernen löscht auch das gespeicherte Passwort.
5. Anzeige bleibt maskiert (`Xtream.Mask`).

## Dateien
`src/Trendo.Core/Tv/IpTv.cs`, `src/Trendo.App/MainWindow.Tv.cs`, `src/Trendo.App/MainWindow.TvVod.cs`; Tests in `tests/Trendo.Core.Tests/IpTvTests.cs` und `tests/Trendo.App.Tests/TvStudioUiTests.cs`.

## Abnahmetests (müssen echt laufen)
- Positiv: Xtream-Quelle anlegen → `tv.json` enthält kein `password=`; Sender, Filme, Serien und Folgen laden und abspielen weiter (nachgebildetes Netz wie heute).
- Umzug: altes `tv.json` mit Passwort in der Adresse → nach dem Start Passwort im Speicher, Datei ohne Passwort, Wiedergabe-Adresse unverändert.
- Negativ: Speicher liefert nichts (z. B. gelöscht) → klare Meldung „Bitte Passwort neu eingeben“, kein Absturz, kein Netzaufruf mit leerem Passwort.
- Entfernen: Quelle löschen → Eintrag im Speicher weg.
- Kein Passwort in Audit-Log, Absturzberichten oder Fehlermeldungen.
- Volltest: `dotnet build Trendo.sln -c Release`, `dotnet test Trendo.sln -c Release`, `python -m unittest discover -s tests/claude_handoff`.

## Ablauf
Branch `claude/T-0240-tv-zugangsdaten`, Auftrag `tasks/T-0240-tv-zugangsdaten.md`, Bericht `tasks/T-0240-tv-zugangsdaten.bericht.md`; Builder 03-security-permissions, QA 19-qa-verification. Danach Push und – nach deiner Vorgabe „jede Änderung als Release“ – Release 0.9.16.

## Risiko und Rückbau
Gering bis mittel (Datenumzug). Rückbau: Der Umzug schreibt vorher eine Sicherung `tv.json.vor-0240`; ohne Passwort im Speicher fragt Trendo neu.

## Nicht Teil dieses Auftrags
Zero-Config-Sender (C1), Mediatheken, weitere 98-Funktionen.
