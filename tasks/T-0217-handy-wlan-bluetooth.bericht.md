# T-0217 – Handy per WLAN und Bluetooth

## Auftrag
Studio „Handy verbinden“ (X-08): neben dem Kabel auch WLAN (Android 11+, `adb pair` / QR im Android-Format, `adb connect`, mDNS) und Bluetooth (Suche, Koppeln, OBEX, Akku). Alles, was per Kabel geht, geht über dieselbe adb-Seriennummer auch per WLAN. Bluetooth zeigt ehrlich, dass der Bildschirm so nicht geht. Koppeln und Verbinden nur nach Klick und Haken. adb und scrcpy nur nach Frage über die vorhandene Werkzeugprüfung. Keine Treiber, keine Schlüssel im Code, kein offener Port von Trendo.

## Branch
`cursor/handy-wlan-bluetooth-56df`

## Geänderte Dateien
- `src/Trendo.Core/W7/Fusion.cs` – Verbindungsart Kabel/WLAN, längere Seriennummern, scrcpy suchen, Anleitung
- `src/Trendo.Core/W7/WirelessAdb.cs` – QR-Sitzung, mDNS, pair/connect, bekannte Handys ohne Code
- `src/Trendo.Core/W7/QrMatrix.cs` – QR Byte-Modus, Fehlerkorrektur M, Version 1–6, ohne Bibliothek
- `src/Trendo.Core/W7/PhoneActions.cs` – Bildschirm (scrcpy), Apps, Überschriften
- `src/Trendo.Core/W7/BluetoothLink.cs` – BlueZ und WinRT über PowerShell, OBEX / fsquirt
- `src/Trendo.App/MainWindow.Phone.cs` – Seite „Per WLAN verbinden“ und „Per Bluetooth“
- `src/Trendo.App/MainWindow.W7.cs` – Handy-Seite zeigt auf die neue Datei
- `src/Trendo.App/StudioCatalog.cs` – Satz zu Kabel, WLAN und Bluetooth
- `src/Trendo.Core/Dependencies.cs` – adb und scrcpy, winget ohne `--scope user`
- `src/Trendo.Core/ToolCheck.cs` – `adb version`, `scrcpy --version`
- `requirements.yaml` – Notiz zu den Nachbau-Tests, Status nicht angehoben
- `THIRD_PARTY.md` – Zeilen für adb und scrcpy
- `tests/Trendo.Core.Tests/W7PhoneLinkTests.cs`
- `tests/Trendo.Core.Tests/W7Tests.cs` – Kabel-Zeile bleibt Kabel
- `tests/Trendo.App.Tests/W7UiTests.cs`

## Testergebnis
`dotnet test Trendo.sln -c Release`. Die Ausgabe sagt „Passed!“:

- Trendo.Core.Tests: Passed 495, Skipped 10, Failed 0, Total 505. Dauer 7 s. Darin 7 neue Tests in `W7PhoneLinkTests`.
- Trendo.App.Tests: Passed 206, Failed 0, Total 206. Dauer 6 min 36 s. Darin 3 neue Oberflächen-Tests.
- Trendo.Api.Tests: Passed 15, Failed 0, Total 15. Dauer 481 ms.

Der feste QR-Text `WIFI:T:ADB;S:studio-AbCdEfGh12;P:1234567890;;` (Version 4, 33×33) und weitere Texte der Versionen 1–6 wurden mit zxing-cpp 3.1.1 wieder gelesen. Die Prüfsumme dieser Matrix steht im Test.

## Offene Punkte / Risiken
- Der QR-Code folgt dem Android-Text `WIFI:T:ADB;S:…;P:…;;`. Das Handy öffnet den Kopplungsdienst. Trendo öffnet keinen Port. `adb` selbst kann danach seinen Dienst auf `127.0.0.1:5037` starten, erst wenn der Nutzer einen adb-Knopf tippt.
- Windows-Bluetooth ist ein PowerShell-Skript mit WinRT (`DeviceInformation`, GATT 0x180F/0x2A19). Es wurde nicht auf einem Windows-PC ausgeführt. Koppeln gilt nur bei Status `Paired` oder `AlreadyPaired`.
- Dateien unter Windows öffnen `fsquirt` (der Dialog nimmt die Datei selbst). Unter Linux `obexftp` bzw. `obexd` für 45 Sekunden, danach Abbruch. Fehlt das Programm, sagt die Seite das. Keine Treiber.
- scrcpy bekommt `-s` mit der Kabel- oder WLAN-Seriennummer. Ob ein bestimmtes Handy das mitmacht, ist hier nicht geprüft.
- `requirements.yaml`: X-08 bleibt `IN_PROGRESS` / bisheriges `PC_VERIFIED`. Die Notiz sagt, dass WLAN und Bluetooth nur mit Nachbauten geprüft sind.

## Nicht geprüft
- Kein echtes Handy, kein echtes WLAN, kein Scannen des QR-Codes mit einer Handykamera, kein echtes `adb pair` / `adb connect` / `adb mdns`.
- Kein echtes Bluetooth-Gerät, kein BlueZ-Funk, kein WinRT, kein fsquirt, kein obexftp/obexd im Betrieb, kein echtes scrcpy.
- Kein Windows-PC. Keine Treiber. Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Kein neues PC_VERIFIED.
