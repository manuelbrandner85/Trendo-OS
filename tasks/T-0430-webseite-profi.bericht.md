# T-0430: Profi-Webseite im bestehenden Studio (Verbesserung 43, Punkte 3–8)

**Auftrag:** Punkte 3 bis 8 aus `docs/spec/IMPROVEMENT_43.md` im vorhandenen Studio CORE-07 („Webseite bauen“, Seite `dev`). Kein zweites Website-Studio. Punkte 1, 2, 9 und 11 bleiben beim gemeinsamen Prompt-Kern (auf diesem Stand noch nicht in `main`).

**Branch:** `cursor/webseite-profi-6cbf`

**Geänderte Dateien:**
- `src/Trendo.Core/Website/` – Plan, HTML/CSS/JS, Stile, Qualität, Blender-GLB, Unreal-Status, Paket
- `src/Trendo.Core/WebAssets/` – `trendo-scroll.js`, gebündeltes `trendo-three.js` (three.js 0.180.0, MIT), `seite.js`, Lizenzen
- `src/Trendo.Core/Trendo.Core.csproj` – die Bibliotheken als eingebettete Ressourcen
- `src/Trendo.App/MainWindow.W4Web.cs`, `MainWindow.W4Dev.cs`, `MainWindow.Look.cs` – Oberfläche im bestehenden Software-Tab
- `src/Trendo.App/WebsitePreview.cs` – lokale Bildvorschau (Chrome). Liegt in der App, damit `Trendo.Core` ohne `System.Net.Http` bleibt (QA-019)
- `tests/Trendo.Core.Tests/WebsiteStudioTests.cs`
- `tests/Trendo.App.Tests/WebsiteStudioUiTests.cs`, `WebsitePreviewTests.cs`

**Testergebnis** (`dotnet test Trendo.sln -c Release`, 22.10.2026):
- Trendo.Api.Tests: 15 bestanden, 0 übersprungen, 0 Fehler
- Trendo.Core.Tests: 502 bestanden, 10 übersprungen (Windows-Prüfung), 0 Fehler
- Trendo.App.Tests: 210 bestanden, 0 übersprungen, 0 Fehler

**Was gebaut ist:**
- Aus einem Satz wird ein Ordner mit `index.html`, `stil.css`, `seite.js`, `inhalt.json`, `stil.json`. Arten: Landingpage, Shop, Portfolio, Blog, Firmenwebseite. Vite nur als optionale Dateien, die Seite läuft auch ohne Node.
- Technik automatisch oder gewählt: Story-Scrolling, Parallax, 3D, Partikel/Shader, Video, Mikro-Animationen. Bibliotheken lokal, kein CDN.
- GLB/glTF wird kopiert. Andere 3D-Formate nur über den vorhandenen Blender-Adapter. Fehlt Blender, steht das klar da. Es wird kein Ersatzmodell als Blender-Export ausgegeben. Der Beispielwürfel ist als solcher beschriftet.
- Unreal: Status, Video oder Bildfolge für den Scroll. Pixel Streaming nur als Notiz in `PIXEL-STREAMING.md`. Kein Server, kein Render ohne Datei.
- Stile Klar, Verspielt, Luxuriös, Technik, plus eigene Schrift und Farben.
- Prüfung lokal (Handy, Ladezeit, Barrierefreiheit, SEO, Links), Note in Alltagssprache, Knopf „Jetzt beheben“. Kaputte eigene Links werden nicht erfunden.
- Paket nur nach zweitem Klick und Freigabe, Zip neben dem Ordner, Datei `NICHT-HOCHGELADEN.txt`. Nichts wird hochgeladen.
- Anschluss für den späteren Prompt-Kern: `WebsiteStudio.StudioId`, `PageId`, `ActionBuild`, `ActionRevise`, `ActionCheck`, `ActionPack`.

**Offene Punkte / Risiken:**
- Avalonia 12.1.4 in diesem Repo hat kein WebView. Die Vorschau ist ein echtes Chrome-Bild (1280×800 und 390×844), dazu „Im Browser öffnen“. Sie aktualisiert sich nach Bau, Stil und Änderung, ist aber kein eingebettetes Live-Fenster.
- Die Qualitätsnote ist eine lokale Dateiprüfung, kein Lighthouse-Lauf gegen ein Netz.
- GSAP ist seit der Preis-Seite vom 22.10.2026 kostenlos, die Dateien sind aber nicht MIT. Deshalb liegt `trendo-scroll.js` bei, nicht GSAP.
- Eine Änderung schreibt die erzeugten Dateien neu. Eigene Extra-Dateien bleiben. Versionen und Rückgängig (Punkt 9) sind nicht Teil dieses Auftrags.

**Nicht geprüft:**
- Blender ist auf diesem Linux-Image nicht installiert. Die Wandlung einer Blend-Datei wurde nicht ausgeführt, nur die ehrliche Meldung und das Kopieren einer GLB.
- Unreal ist nicht installiert. Es wurde kein Render gestartet und Pixel Streaming nicht verbunden.
- Keine lokale KI war angebunden. Der Test prüft harmloses JSON und die Ablehnung von HTML und Adressen. Der stille Weiterbau ohne Modell ist im Code, ein echter Modell-Lauf nicht.
- Kein Windows-PC, kein Explorer, keine Codesignatur. Kein `PC_VERIFIED`. `requirements.yaml` ist unverändert.
- QA-017 nicht angefasst. SYS-03-P14 lief im grünen App-Lauf mit, das PDF selbst habe ich nicht extra geöffnet.
