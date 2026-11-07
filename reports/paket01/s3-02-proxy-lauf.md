# Beleg S3-02 (Teil Proxy) – Proxy-Dateien im Videoschnitt (07.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~VideoProxyTests` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 982 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoProxy_Ui` → Failed 0 (Proxy aus echtem 720p-Clip, Vorschau mit Proxy, Export-Aufruf mit Original, Schalter „Mit Proxys arbeiten“)

- Positiv: 1920×1080-Kameradatei → Proxy 960×540, kleiner, gleich lang; wird wiederverwendet, solange das Original gleich bleibt; Vorschau (Standbild und Abspielen) nutzt den Proxy.
- Export: Der FFmpeg-Aufruf enthält immer das Original, nie den Proxy. Fehlt das Original, bricht der Export mit „Datei fehlt“ ab – der Proxy wird nicht still ausgeliefert.
- Negativ: Proxy mit anderer Länge (2 s statt 3 s) wird verworfen und gelöscht; ändert sich das Original, entsteht ein neuer Proxy-Name; Bilder bekommen keinen Proxy.

## Nicht geprüft
- Multicam (mehrere Kameras synchron schneiden) – offen
- Geschwindigkeitsgewinn an echten 4K-Kameradateien gemessen
