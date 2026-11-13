# Drittanbieter-Lizenzen für den Verkauf

Phase 1. Stand 13.11.2026. Grundlage: `THIRD_PARTY.md`, `THIRD_PARTY_NUGET.md`, die Stückliste `sbom-nuget.md` (aus den Paketdateien erzeugt mit `scripts/sbom.py`) und der Quelltext.

## Mitgelieferte Bibliotheken (im Setup)

52 NuGet-Pakete in den Produktprojekten: 40 MIT, 9 Apache-2.0, 3 mit eigener Lizenzdatei.

| Paket | Lizenz | Bewertung für den Verkauf |
|---|---|---|
| Avalonia, SkiaSharp, HarfBuzzSharp, Velopack, YamlDotNet, PDFsharp, Ical.Net, NLayer, SharpCompress, MQTTnet und weitere | MIT | kommerziell erlaubt, Lizenzhinweis mitliefern |
| ZUGFeRD-csharp, NodaTime, PDFtoImage-Teile und weitere | Apache-2.0 | kommerziell erlaubt, Hinweis und ggf. NOTICE mitliefern |
| WeCantSpell.Hunspell 7.0.1 | wahlweise MPL 1.1, GPL 2.0 oder LGPL 2.1 (vom Hunspell-Projekt übernommen) | mit MPL 1.1 kommerziell nutzbar. Bei Änderungen an dieser Bibliothek müssen diese offengelegt werden. Trendo nutzt sie unverändert |
| Avalonia.Angle.Windows.Natives | eigene Lizenzdatei (ANGLE, BSD-artig) | Lizenzdatei prüfen und mitliefern |
| Schrift Inter, Atkinson Hyperlegible | SIL OFL 1.1 | erlaubt, Lizenztext liegt bei (`Assets/schriften/OFL-Atkinson.txt`) |

Vor dem Verkauf nötig: eine Seite „Lizenzen“ im Produkt und im Setup, die alle Hinweise vollständig zeigt.

## Externe Programme (nicht im Setup, Installation beim Nutzer)

| Programm | Lizenz | Wie Trendo es nutzt | Bewertung |
|---|---|---|---|
| Blender | GPL-3.0-or-later | Herstellerinstaller nach Zustimmung, Aufruf als eigener Prozess | erlaubt, solange Trendo Blender nicht mitliefert oder einbindet |
| Unreal Engine | Epic EULA | Installation über den Epic Launcher durch den Nutzer | Trendo liefert nichts davon aus. Was Nutzer damit bauen, regelt deren eigener Vertrag mit Epic |
| FFmpeg | LGPL oder GPL je nach Build | externer Prozess | genaue Build-Variante und Herkunft prüfen, nicht mitliefern ohne Prüfung |
| LibreOffice | MPL-2.0 | Herstellerinstaller | erlaubt |
| Krita, Kdenlive | GPL | Herstellerinstaller | erlaubt als externer Prozess |
| Git für Windows, mpv | GPL-2.0 | Herstellerpaket | erlaubt als externer Prozess |
| Ollama | MIT | Herstellerinstaller | erlaubt |
| Qwen2.5 0.5B | Apache-2.0 | `ollama pull` beim Nutzer | erlaubt |
| **Qwen2.5 3B** | **Qwen Research License** (Lizenzdatei auf Hugging Face am 13.11.2026 geprüft: nur Forschung und Bewertung) | optional über `ollama pull` | **in einer verkauften Fassung nicht anbieten**. Vorschlag: durch Qwen2.5 1.5B oder 7B (Apache-2.0) ersetzen |
| Qwen2.5-VL 7B (`qwen2.5vl:7b`) | im Code genutzt, Lizenz nicht in `THIRD_PARTY.md` | Bildverstehen im Musikvideo-Studio | **Lizenz prüfen** |
| Weitere Modelle (Bild, Musik, Stimme) | je Modell verschieden | KI-Bibliothek, nach Zustimmung | Lizenzliste je Modell vollständig führen, nicht-kommerzielle Modelle in der Complete Edition kennzeichnen |

## Offene Punkte

- Die Stückliste erfasst nur NuGet-Pakete. JavaScript-Dateien unter `Assets` (zum Beispiel `ifc/assets/ifc.js`, Webdesign-Editor), Bilder, Icons und 3D-Modelle fehlen noch. Für jede Datei ist Herkunft und Lizenz nachzuweisen.
- `tools/` und `tests/` sind nicht Teil des Setups und deshalb nicht erfasst.
- Für Linux- und Pi-Pakete gibt es keine eigene Stückliste.
