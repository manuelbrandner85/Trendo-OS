# v0.6: Datenordner, Qwen2.5 3B, Hintergrund-Blur – PC-Test 21.10.2026 (MESZ)

| Prüfpunkt | Ergebnis | Beleg |
|---|---|---|
| Benutzerdaten in %APPDATA%\TrendoOS, getrennt vom Programmordner %LOCALAPPDATA%\TrendoOS | ✅ GETESTET | mig.txt |
| Migration alter Daten | ⚠️ Daten aus v0.3–v0.5.1 waren schon vorher weg. Der Velopack-Setup leert bei Neuinstallation/Update den Programmordner (Audit, Projekte und config.json gingen dabei verloren; nur Testdaten). Migriert wurde nur der Velopack-Starter „Trendo OS.exe“ (Fehler, manuell zurückgelegt; behoben in 0.6.1: .exe-Dateien werden nicht migriert). Die Migration greift nur, wenn alte Daten noch vorhanden sind. | mig.txt |
| Deinstallation (Update.exe --uninstall) behält Benutzerdaten | ✅ GETESTET: 6 Dateien / 22 Audit-Zeilen vorher = nachher; Neuinstallation übernimmt sie | uninstall.txt |
| Qwen2.5 3B pro Benutzer geladen (1,9 GB, 158 s), Freigabe im Audit | ✅ GETESTET | pull3b.txt |
| 3B ist Standard bei 32 GB RAM, umschaltbar unter KI | ✅ GETESTET | ki-settings.png |
| Deutsche Antwort im KI-Panel (3B) | ✅ „Die Hauptstadt von Deutschland ist Berlin.“ | ki-3b.png |
| Vergleich 0.5B / 3B | 3B erklärt korrekt und knapp. 0.5B ist fehlerhaft („eine Systeme“). Die Hauptstadt-Frage beantworten diesmal beide richtig (0.5B früher „München“). | compare.txt |
| Hintergrund-Blur | ✅ echte Unschärfe (unscharfe Wallpaper-Kopie, maskiert mit der Panel-Transparenz). Kein OS-Acrylic: Das Wallpaper wird in der App gezeichnet. | ki-3b.png |

Lizenz: Qwen2.5 3B steht unter der Qwen Research License und ist nur nicht-kommerziell frei. Die kommerzielle Nutzung braucht eine gesonderte Lizenz. 0.5B steht unter Apache-2.0.
