# T-0688 – Vorlagen mit echten Werkzeugen

Lauf: 2026-11-16 07:28, Rechner: Windows 10.0.26340.0, win-x64

- dotnet --version: 10.0.404
- python --version: Python 3.13.15
- node --version: v24.22.2

| Vorlage | Bauen | Testen | Paket | Größe ZIP | SHA-256 ZIP | Startprobe | Rohlog SHA-256 |
|---|---|---|---|---|---|---|---|
| Web-App (HTML, CSS, JavaScript) | entfällt | ✓ 4 bestanden | ✓ Web-App als Ordner und ZIP (für jeden Webspeicher) | 2 KB | `595a7b4bdf2e11cc9a4ce9ab85045951c431e8239147a305715f5adea1e7ac50` | ✓ aus leerem Ordner über den Server des Projekts ausgeliefert (http://127.0.0.1:63326): Seite „Beleg web“ und 3 verknüpfte Dateien antworten mit 200. | `c2cdbc1ecc01bfc2f565ca1ea45de3e3a3c9b7c08b9de8ab33b49074d531178c` |
| Python-Programm | ✓ | ✓ 6 bestanden | ✓ ZIP mit Startskript (braucht Python auf dem Ziel-PC) | 2 KB | `1d83e2e0a3f2e295e2b031c8a63981c902ecd1ba916362b3b6697d5a8598968e` | ✓ Startskript aus leerem Ordner gestartet, Exit 0. Ausgabe: Hallo Welt! / Zahlen: 2; 3; 5 / Summe: 10 / Mittelwert: 3.33 / Median: 3 | `15c720d7af6d94fd8e45d9883bb340ee466738f18b87ac9f571a1e01a8f9a3dd` |
| C#-Programm für die Konsole (.NET) | ✓ | ✓ 5 bestanden | ✓ Eigenständiges Programm (win-x64), läuft ohne installiertes .NET | 31,5 MB | `a67e3071a885912d87a3795b4bb7caab44166b05c4c2521ed8a994f2f7d314a7` | ✓ gestartet aus leerem Ordner, Exit 0. Ausgabe: Hallo aus Trendo OS! / Zahlen: 2; 3; 5 / Summe: 10 / Mittelwert: 3,33 / Minimum: 2, Maximum: 5 | `fb8295a3fbd8b52cc8962ed9d291af0c33af234158828220097ac6d5a5730d5c` |
| C#-Programm mit Fenster (.NET, Avalonia) | ✓ | ✓ 5 bestanden | ✓ Eigenständiges Programm (win-x64), läuft ohne installiertes .NET | 68,0 MB | `016b416e07f9f8b5464ab8a5fffa995661f8a81c165a8bfee9d6ca9052353afc` | ✓ gestartet aus leerem Ordner, Fenster „Beleg dotnet-desktop“ nach 1,8 s offen, danach beendet. | `601d663ad5d8f52e89995d29f5aea3532d91306d49b20f3410a9ca09cb3f0548` |
| JavaScript-Programm (Node) | ✓ | ✓ 3 bestanden | ✓ ZIP mit Startskript (braucht Node.js auf dem Ziel-PC) | 2 KB | `326d0a908e0d2a49cc64d8f7ef65a1a296738a2e768a7e9494631913207dccb6` | ✓ Startskript aus leerem Ordner gestartet, Exit 0. Ausgabe: Hallo Welt! / Text: Ein kurzer Satz zum Ausprobieren / Woerter: 5, Zeichen: 32, laengstes Wort: Ausprobieren | `71847823c95b6523fbdc56d03966a4a739aefec0e8dd0573135a3cef3290304a` |

Rohlogs: `vorlage-<id>.log` in diesem Ordner. Die Pakete selbst liegen nicht im Repo (Größe); ihre Prüfsummen stehen oben.
