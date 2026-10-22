# T-0215 – Live streamen: Vorschau, Aufnahme, Sendung

## Auftrag
Das Studio „Live streamen“ (Seite `sendung`, SPEC-15) war nur ein Sendeplan. Es soll wirklich senden: Quelle wählen (Bildschirm, Fenster, Kamera, Mikrofon), live eine Vorschau zeigen und lokal in eine Datei aufnehmen. Dafür wird FFmpeg über die vorhandene Werkzeugprüfung benutzt; installiert wird nur nach Nachfrage. Ein Livestream zu YouTube, Twitch oder einem eigenen RTMP-Server nutzt einen Stream-Schlüssel, den die Nutzerin oder der Nutzer eingibt. Der Schlüssel liegt im vorhandenen Secret-Store. Jeder Start einer Sendung läuft durch die vorhandene Freigabe und startet nie von allein. Es gibt einen deutlichen Stopp-Knopf. Der Sendeplan bleibt.

Dieser Lauf legt den Branch `cursor/live-stream-3e3d` an, weil der PR-Ablauf dieses Namensschema verlangt.

## Branch
`cursor/live-stream-3e3d`

## Geänderte Dateien
- `src/Trendo.Core/W6/LiveStream.cs` – Quellen, FFmpeg-Befehl, Schlüssel, RTMP-Adresse, Sitzung, Freigabe `live.start`
- `src/Trendo.App/DesktopCapture.cs` – Fenster, Kameras und Mikrofone vom Rechner lesen
- `src/Trendo.App/MainWindow.Live.cs` – Oberfläche: Vorschau, Aufnahme, Stopp, Schlüssel, Freigabe, FFmpeg-Nachfrage
- `src/Trendo.App/MainWindow.Spec15.cs` – Live-Teil über dem bestehenden Sendeplan
- `src/Trendo.App/MainWindow.axaml.cs` – Freigabe „live.start“ führt nicht aus und startet die Sendung nicht
- `src/Trendo.Api/LocalApi.cs` – `POST /api/approvals/{id}/approve` startet bei `live.start` nichts
- `tests/Trendo.Core.Tests/LiveStreamTests.cs`
- `tests/Trendo.App.Tests/LiveStreamUiTests.cs`
- `tests/Trendo.Api.Tests/ApiTests.cs` – Freigabe über die API startet den Stream nicht

Der Studioname bleibt „Live streamen“. `requirements.yaml` ist unverändert. SPEC-07 ist nicht angefasst. Das Studio öffnet auch ohne FFmpeg, damit der Sendeplan weiter geht.

## Testergebnis
`dotnet test Trendo.sln -c Release`, SDK 10, Linux:

- Trendo.Core.Tests: Bestanden 452, übersprungen 10, Fehler 0, gesamt 462. Dauer 6 s. Darin die 8 neuen Live-Tests.
- Trendo.Api.Tests: Bestanden 15, Fehler 0, gesamt 15. Dauer 535 ms. Darin der neue Test, dass eine Live-Freigabe nichts startet.
- Trendo.App.Tests: Bestanden 188, Fehler 1, gesamt 189. Dauer 6 min 9 s. Die 5 neuen Live-UI-Tests sind bestanden (Vorschau, Aufnahme, Stopp, Quellen, Freigabe und Schlüssel, FFmpeg-Nachfrage, Offline sperrt nur die Sendung).

Der eine Fehler ist `X-06: Live-Suche bei GitHub` in `tests/Trendo.App.Tests/W4OssUiTests.cs` Zeile 25: `Assert.NotEmpty` auf eine leere Liste. Dieser Test ruft die GitHub-Suche auf und ist von diesem Auftrag nicht angefasst. Ein einzelner Lauf desselben Tests direkt danach: Bestanden 1, Fehler 0, Dauer 706 ms. Die leere Liste kommt von der GitHub-Antwort, nicht vom Live-Code.

## Offene Punkte / Risiken
- Auf diesem Linux-Rechner installiert die Nachfrage FFmpeg über winget (`Gyan.FFmpeg`). winget ist hier nicht das Installationsprogramm. Die Nachfrage entsteht, eine stille Installation gibt es nicht. Ein echtes winget bleibt dem Windows-PC.
- Ein einzelnes Fenster auf dem Mac lehnt der Befehl ab (avfoundation kennt kein Fensterrechteck). Bildschirm und Kamera auf dem Mac sind im Befehl enthalten, hier nicht gestartet.
- Es läuft immer nur ein FFmpeg-Prozess. Vorschau, Aufnahme und Sendung teilen sich ihn. Stopp schreibt `q` und beendet den Prozessbaum nach drei Sekunden.
- Die Bitrate kommt aus dem vorhandenen Sendeplan (Mitte der Spanne). Eigene RTMP-Ziele nutzen die YouTube-Stufen. Twitch hat keine 1440p- und 2160p-Stufe.

## Nicht geprüft
- Kein echtes FFmpeg, kein Bildschirm, keine Kamera und kein Mikrofon auf diesem Rechner gestartet. Die Tests bauen den Befehl und einen Ersatzprozess.
- Kein echter Stream zu YouTube, Twitch oder einem RTMP-Server. Der Schlüssel bleibt im Test im Speicher-Store und steht nicht in den Dateien.
- Kein Windows-Credential-Manager, kein winget, kein Explorer.
- Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Status in `requirements.yaml` nicht angehoben. Kein PC_VERIFIED.
