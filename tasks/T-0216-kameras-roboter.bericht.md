# T-0216 – Kameras & Roboter steuern

## Auftrag
Das Studio „Kameras & Roboter steuern“ bleibt beim Namen und bekommt die fehlende Funktion dazu. Webcams und IP-Kameras (RTSP, ONVIF) werden erkannt, ein Livebild, ein Foto und eine Aufnahme bleiben lokal. Der Roboter simuliert und macht einen Probelauf, ohne etwas zu senden. Eine echte Bewegung geht nur nach Freigabe und nie bei Not-Aus. Der Not-Aus-Knopf bleibt immer sichtbar. Die bisherigen Rechenwerkzeuge (Objekt finden, Arm-Stellung, Fahrprogramm) bleiben mit denselben Knöpfen.

## Branch
`cursor/kameras-roboter-db3a`

## Geänderte Dateien
- `src/Trendo.Core/W6/CameraRobot.cs` – Kameras, ONVIF-Kette, Bewegungszeile, Freigabe, Anschluss
- `src/Trendo.App/MainWindow.Cameras.cs` – Oberfläche im Studio
- `src/Trendo.App/MainWindow.Core09.cs` – neue Fläche über den bestehenden Werkzeugen
- `src/Trendo.App/MainWindow.axaml.cs` – Freigabe startet nur noch Ordner und https-Seiten selbst
- `tests/Trendo.Core.Tests/CameraRobotTests.cs`
- `tests/Trendo.App.Tests/CameraRobotUiTests.cs`
- `docs/build-log/2026-10-22-1821-kameras-roboter.md`

`requirements.yaml` ist unverändert. Der Studioname kommt weiter aus `StudioCatalog`.

## Testergebnis
`dotnet test` mit SDK 10, Konfiguration Release, 22.10.2026 gegen 18:21 Uhr (Europe/Berlin):

- Trendo.Core.Tests: Bestanden 448, übersprungen 10, Fehler 0, gesamt 458. Dauer 6 s. Die vier neuen Tests sind darin. Die 10 Übersprungenen sind `WinRealTests` (kein Windows).
- Trendo.App.Tests: erster Gesamtlauf Bestanden 186, Fehler 1, gesamt 187, Dauer 4 min 50 s. Der Fehler ist `X-06: Live-Suche bei GitHub` (leere Antwort, kein Kamera-Code). Derselbe Test sofort danach: Bestanden 1, Fehler 0. Die drei neuen UI-Tests und die beiden bestehenden CORE-09-UI-Tests waren im Gesamtlauf grün.
- Trendo.Api.Tests: Bestanden 14, Fehler 0, gesamt 14. Dauer 379 ms.

## Offene Punkte / Risiken
- Keine echte Webcam, kein RTSP-Gerät und kein serieller Roboter auf diesem Rechner. Tests ersetzen FFmpeg und ONVIF durch Aufrufe, der Befehl selbst ist der echte.
- Eine ONVIF-Suche, die keine RTSP-Adresse liefert, zeigt die gefundene http-Adresse. Foto und Aufnahme sagen dann ehrlich, dass FFmpeg scheitert.
- Die Freigabe-Seite führt unbekannte Aktionen nicht mehr selbst aus. Ordner und https bleiben „Freigeben und ausführen“. Sendung (anderer Branch) und Bewegung starten der Knopf im Studio.
- Der Anschluss wird nur beschrieben, wenn die Datei existiert (oder ein COM-Name). Ein fehlender Anschluss wirft, es wird nichts als Erfolg gemeldet.

## Nicht geprüft
- Kein echtes Kamerabild, kein UDP-Broadcast im LAN, kein COM-Port, kein FFmpeg auf diesem Lauf gegen eine Kamera.
- Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Status in `requirements.yaml` nicht angehoben.
