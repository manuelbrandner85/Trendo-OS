# Blender-Fenster ohne Konsole, Live und Kameras geprüft

- 22.10.2026, Linux-Testrechner, Branch `cursor/blender-launcher-c7fc`
- Oberflächenstart von `blender.exe` nimmt unter Windows `blender-launcher.exe` im selben Ordner. Fehlt sie, startet `blender.exe` mit `CreateNoWindow` und ohne Shell. Export mit `--background` bleibt bei `blender.exe`.
- #227 und #228 sind auf `main`. „Vorschau starten“ und „Kameras suchen“ sind in der ersten Ansicht. Vorschau und Livebild erst nach dem Klick. Der Stream erst nach „Ja, jetzt senden“ bzw. „Jetzt senden“.
- Tests: Api 15 bestanden. Core im Gesamtlauf 531 bestanden / 10 übersprungen / 1 Fehler (Begleiter-Dispose, einzeln und im zweiten Core-Lauf grün: 532). App 221 bestanden / 1 Fehler (SPEC-10 zeigt „Warnung“ bei hohem Arbeitsspeicher, einzeln grün). Schwellen unverändert.
- Nicht geprüft: echtes Blender, echte Kamera, Windows, winget. Kein PC_VERIFIED. QA-017, QA-019 und Codesignatur nicht bestanden.
