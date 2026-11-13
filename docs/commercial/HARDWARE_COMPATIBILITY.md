# Hardware Compatibility Manager

Stand 13.11.2026.

## Wo der Systemcheck läuft

| Wann | Wie | Datei |
|---|---|---|
| **Vor der Installation** | Skript `systemcheck.ps1` von der Verkaufswebseite oder aus dem Paket, nur lesend | `installer/systemcheck.ps1` |
| **In Trendo** (auch in der Free Edition) | Einstellungen › Zustand & Leistung › **Hardware & Leistung** | `src/Trendo.App/MainWindow.Hardware.cs` |
| **Vor dem Kauf** | Einstellungen › System › **Konto, Lizenz & Vollversion**, Karte „Vor dem Kauf: Was läuft auf deinem PC?“ | `src/Trendo.App/MainWindow.Lizenz.cs` |
| Programme und Werkzeuge (wie bisher) | Einstellungen › Zustand & Leistung › Systemcheck | `ToolCheck` |

## Was geprüft wird

| Prüfung | Quelle in Trendo | Quelle im Vorab-Skript |
|---|---|---|
| Windows-Version, Windows 11 ja/nein | `RuntimeInformation`, Build ab 22000 | `Win32_OperatingSystem` |
| Architektur | `RuntimeInformation.OSArchitecture` | `PROCESSOR_ARCHITECTURE` |
| Prozessor und Kerne | Registrierung (nur lesen), `ProcessorCount` | `Win32_Processor` |
| Arbeitsspeicher | `GC.GetGCMemoryInfo` | `Win32_OperatingSystem` |
| Freier Platz | `DriveInfo` des Datenordners | `Win32_LogicalDisk` des Systemlaufwerks |
| SSD oder Festplatte | **nicht ermittelt**, Hinweis „SSD empfohlen“ | `Get-PhysicalDisk` MediaType |
| Grafikkarte, Grafikspeicher, Treiber | Registrierung der Anzeigeadapter (nur lesen), bei NVIDIA zusätzlich `nvidia-smi` | Registrierung (nur lesen), sonst `Win32_VideoController` |
| Bildschirmauflösung | Avalonia `Screens.Primary` | `Win32_VideoController` |
| Laufzeit (.NET) | im Setup enthalten | – |
| Optionale Programme (Ollama, Blender, FFmpeg, Unreal) | Abhängigkeitskatalog | – |
| Internet | nur auf Knopfdruck, eine HEAD-Anfrage an github.com (in der App, der Core bleibt netzfrei nach QA-019) | – |

## Datenschutz

- Erfasst werden nur technische Werte, keine Benutzernamen, Seriennummern oder Gerätenamen.
- Nichts wird gesendet. Einzige Ausnahme ist der Knopf „Internet prüfen“: Er schickt eine leere Anfrage an github.com, über das auch die Updates laufen.
- Die Registrierung wird nur gelesen, nicht verändert. Die Projektregel „keine Registry-Änderungen“ ist eingehalten.
- Ein Test prüft, dass das Vorab-Skript keine Befehle zum Schreiben, Herunterladen oder Starten enthält und keine Seriennummern ausliest.

## Grenzen

- SSD oder HDD erkennt nur das Vorab-Skript, Trendo selbst nicht.
- Grafikspeicher kann bei manchen Treibern fehlen und erscheint dann als „unbekannt“.
- Ein Leistungstest (Benchmark) läuft nicht. Das Profil richtet sich nach den Hardwaredaten, nicht nach gemessener Geschwindigkeit. Siehe die offene Messreihe in `PERFORMANCE_PROFILES.md`.
- Unter Linux liest Trendo nur Prozessor, Kerne, RAM und Platz. Die Linux-Edition gehört vorerst nicht zum Verkaufsangebot.
