# T-0218 – Blender ohne Konsole, Live und Kameras geprüft

## Auftrag
Windows-Befund zu 0.9.7 (Blender 5.2.1): „In Blender öffnen“ startet `blender.exe` und ein Konsolenfenster geht auf. Unter Windows soll `blender-launcher.exe` benutzt werden, wenn die Datei neben `blender.exe` liegt, sonst `CreateNoWindow`. Dazu ein Test. Außerdem: Live streamen ohne Live-Vorschau, Kameras & Roboter ohne Gerätesuche (nur Farbsuche im Bild). Prüfen, ob #227 und #228 auf `main` sind und in der Oberfläche hängen. Vorschau nur nach einem Klick, Stream nicht ohne Freigabe.

## Branch
`cursor/blender-launcher-c7fc`

## Geänderte Dateien
- `src/Trendo.Core/Integrations.cs` – `BlenderGui.ForWindow`. Oberflächenstart von `blender.exe` (Studio, Dock, externe Programme) nimmt unter Windows `blender-launcher.exe` im selben Ordner. Fehlt die Datei, bleibt `blender.exe` mit `CreateNoWindow`, ohne `UseShellExecute`.
- `tests/Trendo.Core.Tests/ProcessStartTests.cs` – Test mit echtem Temp-Ordner: Launcher vorhanden, Launcher fehlt, kein Windows, anderes Programm bleibt unangetastet.

Nicht geändert: `Cli.Run` und `Studio3D.ExportBlend` (`--background` bleibt bei `blender.exe`). Erkennung und Versionsabfrage bleiben bei `blender.exe`. Die Seiten `sendung` und `roboter` waren schon verdrahtet.

## Befund Live und Kameras
#227 (`9cc87d3`) und #228 (`53e20b4`) sind Vorfahren von `origin/main`. Die Knöpfe stehen in der ersten Ansicht (1536×1024, Fenster maximiert):

- Live streamen: „Vorschau starten“, Hinweis „Noch kein Bild“. Der Stream kommt erst über „Live anfragen …“ und danach „Ja, jetzt senden“ bzw. „Jetzt senden“. Eine Freigabe allein startet nichts.
- Kameras & Roboter steuern: „Kameras suchen“, „Kameras im Netzwerk suchen“, „Livebild an“. Die Farbsuche „Objekt im Bild finden“ liegt darunter und ist nicht die einzige Suche.

Die Zeile oben („nicht eingebaut“ / „Keine Kamerasteuerung … aus einem Prompt“) ist der Auftrags-Prompt. Der startet keine Kamera und keinen Stream. Die Studio-Knöpfe sind davon getrennt.

Im Projekt steht Version 0.9.5. Ein Tag `v0.9.7` liegt nicht im Repo. Ein Installer, der vor den Merges vom 22.10.2026 gebaut wurde, zeigt die Knöpfe nicht.

## Testergebnis
`dotnet test Trendo.sln -c Release`, SDK 10, Linux:

- Trendo.Api.Tests: Bestanden 15, Fehler 0, gesamt 15. Dauer 540 ms.
- Trendo.Core.Tests im selben Lauf: Bestanden 531, übersprungen 10, Fehler 1, gesamt 542. Dauer 7 s. Der Fehler ist `Companion_http`: `HttpListenerException` „Address already in use“ in `Dispose`, nachdem die Prüfungen durch waren. Einzellauf danach: Bestanden 1. Danach das ganze Core-Projekt: Bestanden 532, übersprungen 10, Fehler 0, gesamt 542. Dauer 9 s. Darin die zwei Blender-Starttests.
- Trendo.App.Tests: Bestanden 221, Fehler 1, gesamt 222. Dauer 13 min 41 s. Der Fehler ist `SPEC-10 UI: diesen PC live messen`: der Text war „● Dieser PC – Trendo-Arbeitsspeicher: Warnung“, der Test sucht „Ok“. Die Schwellen 1500/3000 MB sind unverändert. Einzellauf danach: Bestanden 1. Die Live- und Kamera-UI-Tests liegen in den 221 bestandenen.

Filter `Programmstart` vor dem Gesamtlauf: Bestanden 2, Fehler 0.

## Offene Punkte / Risiken
- Kein echtes Blender 5.2.1 und kein Windows in diesem Lauf. Ob `blender-launcher.exe` das Konsolenfenster auf dem Prüf-PC wirklich weglässt, sieht man erst dort.
- `CreateNoWindow` allein hat auf dem Prüf-PC nicht gereicht, sonst wäre der Befund nicht gekommen. Der Launcher ist deshalb der eigentliche Weg.
- Der Begleiter-Test scheitert im parallelen Gesamtlauf am Aufräumen des HttpListener. Einzeln ist er grün. Der Companion-Code ist nicht angefasst.
- SPEC-10 wird im Gesamtlauf rot, sobald der Prozess über 1500 MB Arbeitsspeicher liegt. Einzeln grün. Keine Produktänderung.

## Nicht geprüft
- Kein echter Blender-Start, keine echte Kamera, kein Mikrofon, kein RTMP, kein winget. Kein PC_VERIFIED. QA-017, QA-019 und die Codesignatur sind nicht bestanden.
