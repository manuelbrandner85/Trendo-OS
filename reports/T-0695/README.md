# T-0695 – Beleg: Vorschau = Export im Videoschnitt

Stand 16.11.2026, Windows 11, FFmpeg 9.0.2 (gyan.dev full build), echtes FFmpeg – nichts ersetzt.

## Was geprüft wurde

**Kerntest** `tests/Trendo.Core.Tests/VideoLookTests.cs` → `Preview_equals_export_real` (Rohlog `kern-rohlog.txt`, Projekt `projekt.json`):
1080p-Projekt, 30 Bilder/s, aus echten FFmpeg-Quellen (Testbild mit Bewegung und Ton, Fraktal, Farbbalken, Logo):
Stück a mit Look „Schwarzweiß“, Titel „Sommer 2026“ und 1 s Übergang „Schieben“ → Stück b mit Keyframes (Größe 1→0,5, Lage 0→0,2) und Vignette → harter Schnitt → Standbild c;
Logo 5–9 s auf der zweiten Bildspur mit bewegter Lage (Keyframes). Export über `VideoEdit.Export` (unveränderter Export-Aufruf),
Vorschau über `VideoLook.RenderFrameAsync` (derselbe Filtergraph, auf den Ausschnitt um die Stelle begrenzt).

| Stelle | was dort zu sehen ist | Vorschau ↔ Export (mittlere Abweichung, 0–255) | Gegenprobe Nachbarbild | Gegenprobe rohes Stück (alte Vorschau) |
|---|---|---|---|---|
| 1,0 s | Schwarzweiß-Look + Titel | 0,275 | 2,257 | 108,479 |
| 3,5 s | mitten im Übergang „Schieben“ | 0,802 | 25,762 | 105,926 |
| 5,2 s | Keyframes Größe/Lage, Vignette, Logo mit Bewegung | 1,258 | 5,024 | 111,598 |
| 6,6 s | dto., spätere Keyframe-Stelle | 0,816 | 3,583 | 105,580 |
| 8,0 s | Standbild nach hartem Schnitt + Logo | 0,203 | 0,371 | 7,776 |

Bilder je Stelle: `vergleich-<zeit>s.png` – links das rohe Stück (so sah die Vorschau bisher aus), Mitte die neue Vorschau, rechts das Bild aus dem exportierten MP4.

**Schwelle 2,5 von 255 (je Farbkanal gemittelt), begründet:** Der Export ist H.264 (verlustbehaftet, 4:2:0); die Vorschau ist das unkomprimierte Bild aus demselben Graphen.
Gemessen liegt die Abweichung bei 0,2–1,3. Ein Bild daneben (eine 1/30 s später) weicht bei bewegtem Inhalt 2,3–25,8 ab, das rohe Stück ohne Effekte 7,8–111,6.
2,5 liegt damit über dem Codec-Verlust, aber unter jeder Gegenprobe mit Bewegung – ein falsches Bild oder ein fehlender Effekt fällt auf.
Nur bei 8,0 s (Standbild, nur das Logo bewegt sich langsam) ist das Nachbarbild mit 0,371 kaum anders; dort trennt die Gegenprobe „rohes Stück“ (7,8).

**Oberflächentest** `tests/Trendo.App.Tests/VideoLookUiTests.cs` → `Look_click_to_file_real` (Rohlog `ui-rohlog.txt`):
Dateien ablegen (Länge/Ton per ffprobe) → Look „Schwarzweiß“ + Titel → Abspielkopf 1,0 s → fertiges Bild nach 254 ms, 0,00 % farbige Pixel →
Scrubben über 5 Stellen → Abspielen mit Effekten (15 fertige Bilder in 686 ms) → F öffnet/schließt das Vollbild →
Klick auf die goldene Hauptaktion „Video exportieren“ → MP4 1920×1080 mit Ton, 8,5 s → gezeigtes Vorschaubild ↔ MP4-Bild bei 1,0 s (960 px): 0,055.
`ui-vorschau-1.0s.png` ist genau die Datei, die die Oberfläche angezeigt hat; `ui-export-1.0s.png` das Bild aus dem MP4.

**Bild der Oberfläche** `oberflaeche-schnitt-vorschau.png`: Render-Werkzeug (`TRENDO_SHOT=seite TRENDO_SCHNITT=look`, eigener `TRENDO_RENDER_ROOT`) –
die Vorschau zeigt das fertige Bild (Look „warm“, Titel im Bild gerechnet, nicht mehr als Text darübergelegt), „Vollbild“ oben rechts, golden nur „Video exportieren“.

## Wartezeit (1080p-Projekt, Bild 960 px breit, kalter Zwischenspeicher, je 5 Läufe)

| Stelle | Median | Höchstwert |
|---|---|---|
| 1,0 s (Look + Titel) | 173 ms | 190 ms |
| 3,5 s (in einer 1-s-Überblendung) | 416 ms | 503 ms |
| 5,2 s (Keyframes, Vignette, Logo) | 240 ms | 257 ms |
| 6,6 s | 239 ms | 246 ms |
| 8,0 s (Standbild + Logo) | 164 ms | 166 ms |

Zwischenspeicher-Treffer: 0–1 ms. Ziel < 300 ms erreicht außer **in Überblendungen** (≈ 0,4–0,5 s): dort muss FFmpeg ab Blendenbeginn rechnen,
weil `xfade` den Blendenfortschritt am ersten Bild festmacht; die Bilder dazwischen werden früh verworfen (Auswahl vor Farbe/Effekten), das halbierte die Zeit von ≈ 0,8–1,0 s.
Währenddessen zeigt die Vorschau das letzte fertige Bild mit dem Hinweis „Vorschau wird berechnet …“.

Wiedergabe mit Effekten: fertige Bilder 640×360 aus dem 1080p-Graphen, **81 Bilder/s** (Ziel 30), erstes Bild nach 301 ms.

## Prüfsummen (SHA-256)

```
d1b5eec6da43b6d0b388c80127bda6f7ddb675b2274a9311d6d6094b65f3cae3  kern-rohlog.txt
938b906d30d25d1749f1ee6f45c30460b67027eb46808eb74f3a234602af9fee  ui-rohlog.txt
79f4c698d44d91bca5b5b6cfd21aacf797bd8c7bcb7623ea80dafff94e5c9bbe  projekt.json
1d54fb7c2f62a1ab8f71a63cfefcffad28c075957ceb67bedf90ef445447b410  vergleich-1.0s.png
07abd15520b7e790777132ada08c513b8cce820a5904adeebde452214bdf67a0  vergleich-3.5s.png
abdfb0a7934bca57a35d7b4f1eaa022c0d869a8c17c93c59a2ea2551e5b894d7  vergleich-5.2s.png
ba19d49c0ce810da5bdb0d7bccec8beab9116fc5c8958200edf5029cc07267b3  vergleich-6.6s.png
5d4d79bf479dbeacc0065a505cbadf288f8b431098bf167134e2ae49f2979371  vergleich-8.0s.png
0ad144c83bd5b83e39b6a6f5989faeb156a823e9fb5cb0be516233fae81a1e66  ui-vorschau-1.0s.png
f1b5480fbda4ad0214029c8200b06864e8438228893c4f5a148fd2658529069b  ui-export-1.0s.png
771407646135b95fd2744d8c00af4a2090e8037c2042f7da985f8a1b4afa717d  oberflaeche-schnitt-vorschau.png
```

Die exportierten MP4 selbst liegen nicht im Repo (Testordner werden gelöscht); ihre SHA-256 stehen in den Rohlogs.
Pfade mit Benutzernamen sind in `projekt.json` durch `C:\Users\<user>` ersetzt.

Neu erzeugen: `TRENDO_T0695_BELEG=<ordner>` setzen, dann
`dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~VideoLookTests"` und
`tests/Trendo.App.Tests/bin/Release/net10.0/Trendo.App.Tests.exe -class Trendo.App.Tests.VideoLook_Ui`.
