# Bericht T-0696 – Animation & Motion und Logos & Marken: Bühne zum Anfassen

**Zweig:** `claude/T-0696-animation-logos` (von `claude/T-0685-b1-integration`), eigene Kopie in `%TEMP%`.

## Was war → was ist

| Punkt | Vorher (Audit) | Jetzt | Beleg |
|---|---|---|---|
| Vektor-Editor | keiner; Logo = Formular, Animation = Zahlenfelder | `VectorEditor` (ein Steuerelement für beide): Rechteck, Ellipse, Vieleck, Stern, Linie, Zeichenstift mit Bézier-Punkten, Text (Schrift/Größe/Fett/Ausrichtung), Bilder; Auswahl/Rahmen, Verschieben, Drehen (runder Griff, 15°-Raster), Skalieren (8 Griffe, Umschalt = Seitenverhältnis, Alt = aus der Mitte), Punkte und Griffe ziehen, Doppelklick Ecke ↔ Rundung, „In Pfad umwandeln“, Ausrichten/Verteilen, Gruppieren, Reihenfolge, Füllung/Kontur/Verlauf, Raster/Einrasten, Kürzel (V A R E S L P T, Entf, Pfeile, Strg+D/C/X/V/G/A, [ ]), Rechtsklick-Menü, SVG/PNG/JPEG hineinziehen oder einsetzen. Jeder Schritt = ein Eintrag im Rückgängig der Programm-Hülle | `VectorStudios_Ui.LogoDraw`, `LogoImport` (echte Maus/Tastatur im Headless-Fenster) |
| Vektor-Kern | – | `Trendo.Core/Kreativ/VectorKit.cs`: Geometrie (nur M/L/C/Z), Treffer, Drehen, Normalisieren ohne Sprung, Anordnen, JSON, SVG schreiben (mit verlustfreier Rundreise über Metadaten), fremdes SVG über `ImageVector.FromSvg`, PDF (PDFsharp, Vektor), ICO, Logo-Vorschlag, Varianten hell/dunkel/einfarbig, Markenpaket; `MotionKit` (Schlüssel, Farbe, Pose, animiertes SVG, altes Ebenenformat lesen) | `VectorKitTests` (13 Tests) |
| Logos | Generator mit 5 Formen, Export SVG + MD | Formular bleibt als „Vorschlag erzeugen“ (golden) / Form-Knöpfe; Ergebnis ist das bearbeitbare `vector`-Dokument; Name/Untertitel/Farben im Formular ändern Texte bzw. färben um, ohne Handarbeit zu verlieren. Exporte: Logo-Paket (.zip: SVG, hell/dunkel/einfarbig/negativ, PNG 512/1024/2048, PDF, favicon.ico, markenpaket.md), PNG 1024, PDF, ICO, SVG, MD. Alte Logo-Dokumente ohne `vector` werden aus den Feldern aufgebaut | `LogoExports`; `reports/T-0696/logo-exporte.log` |
| Fremdleser Logo | – | Exportiertes SVG in **Chrome headless** (vorhanden) gerendert, Pixelvergleich 1280×360 mit dem PNG-Export: mittlere Abweichung **0,55/255**, 0,28 % Kanäle > 60. PDF mit **PDFium** gezeichnet (Zeichen sichtbar, Text gefunden) | `logo-chrome.png`, `logo-vorschau.png`, Log + SHA256SUMS |
| Animation | Bühne nur zum Ansehen, Schlüssel per Zahlenfeld, nur 3 Formen | Bühne = derselbe Editor; Ziehen/Drehen/Skalieren/Umfärben setzt bei „Schlüssel automatisch“ ein Schlüsselbild zur aktuellen Zeit (erster Schlüssel nach 0 s hält den Startwert); `KeyTimeline`: Zeilen je Form, gewählte Form mit Position/Größe/Drehung/Deckkraft/Farbe, Rauten ziehen, Doppelklick setzt Schlüssel, Rechtsklick Kurve (gleichmäßig/weich/federnd/…) oder Löschen, Lineal-Klick setzt Zeit; Abspielen; Bühnengröße (Quer/Full HD/Hoch/Quadrat). Zahlenfelder bleiben im Inspektor (Profi). Alte Dokumente (rechteck/kreis/text) öffnen weiter | `AnimRealVideo`, `AnimLegacyAndFormat`, alter Test `KreativStudios2_Ui.Anim` grün |
| Video | MP4 über FFmpeg, nie echt getestet | Bilder zeichnet jetzt `VectorSkia` (SkiaSharp, im Hintergrund, parallel) statt Fenster-Rendering; MP4 (H.264) und **GIF** neu. **Echter FFmpeg**: ffprobe 1280×720, h264, 61 Bilder, 2,03 s; Standbild t=0 Formmitte (299,5|359,5) erwartet (300|360), Ende (979,5|359,5) erwartet (980|360) | `anim-video.log`, `anim-t0.png`, `anim-ende.png` |
| Erster Schritt / Gold | Logo: „Namen eingeben“; Animation: keiner | Animation: „Erste Form auf die Bühne“ im Leerzustand; je Ansicht eine goldene Aktion (Schalter in der Leiste sind Kästchen statt goldener Umschalter) | Bilder `reports/T-0696/bild-*.png` (Render-Werkzeug, angesehen) |

## Tests (gezielt, kein Volltest)
- Core: `dotnet test … --filter VectorKitTests|KreativKits` → **Bestanden, 52/52**.
- App: `VectorStudios_Ui` (neu) **5/5**; Lauf mit `UebersichtEinfach_Ui`, `VectorStudios_Ui`, `KreativStudios_Ui`, `KreativStudios2_Ui`, `FilmSchnitt_Ui`: 22, davon 1 rot (`FilmSchnitt_Ui.Render_click_to_file_real`, „nicht sichtbar: prog-cmd-film-import-music“). **Gleicher Fehler auf dem unveränderten Integrationszweig** in derselben Klassen-Kombination nachgeprüft; einzeln grün (auf Basis und hier) → reihenfolgeabhängig, nicht aus diesem Paket.
- Vorher-Lauf über alle 31 Klassen, die `MainWindow.Programs` durchgehen (+ `ProgramHeadDesign_Ui`, `NoFixedUserTests`): 162 Tests, 2 rot – `UebersichtEinfach` (von mir behoben: Logo zeigt in „Einfach“ Paket/PNG/PDF/ICO, Bühnengröße steht in der Zeitleiste) und der FilmSchnitt-Fall oben.
- Altes Test-Verhalten: keine Tests geändert oder geschwächt. `KreativStudios_Ui.Logo` und `KreativStudios2_Ui.Anim` laufen unverändert grün (Namen `logo-light`, `anim-slider`, `anim-val-*` … bleiben).

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (Logo: Stern/Pfad mit Maus gezeichnet; Animation: erste Form → zwei Schlüssel per Ziehen → MP4).
2. Fremddatei importieren, bearbeiten, exportieren, Fremdprogramm öffnet Export: **VOLLWERTIG** für SVG (fremdes SVG eingesetzt/umgefärbt; Export in Chrome gerendert), PDF (PDFium), MP4/GIF (ffprobe/FFmpeg). Einschränkung: fremde SVG-Kurven kommen als Punkte (Leser der Bildbearbeitung), `transform`-Attribute fremder SVGs werden nicht ausgewertet.
3. Abbruch ≤ 30 s: **TEILWEISE/geerbt** – Hülle (Autosicherung 20 s, AtomicFile) unverändert genutzt, jeder Editor-Schritt ist eine Dokumentänderung; kein eigener Absturztest in diesem Paket.
4. Claude/KI-Fachaufgabe im offenen Dokument, rückgängig: **NICHT GETESTET** – Knöpfe „Ideen mit Claude“ / „Bewegung mit Claude planen“ schicken über den vorhandenen Claude-Weg (Abo); dass Claude das Vektor-Dokument ändert, ist nicht geprüft (braucht Anmeldung).
5. Test vom Klick bis zur Datei: **VOLLWERTIG** (`AnimRealVideo`, `LogoExports`).
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen-und-Ablegen: **VOLLWERTIG** für Kürzel, Rechtsklick, Maus-Ziehen auf der Fläche und in der Zeitleiste; Ablegen von Dateien: derselbe Code-Weg wie der Drop-Handler (`DropFiles`) getestet, das echte Betriebssystem-Ziehen nicht.

## Nicht geprüft / ehrlich
- In Tests zeichnet Avalonia ohne echte Grafik (Headless): die Editor-Ansicht (Avalonia) wurde nur über Bildschirmfotos des Render-Werkzeugs geprüft; der Pixelvergleich mit Chrome läuft gegen den PNG-Export (SkiaSharp). Beide nutzen dieselbe Geometrie, Text wird aber von zwei Zeichenwegen gesetzt.
- Animiertes SVG nur als gültiges XML geprüft, nicht im Browser abgespielt.
- Lottie, durchsichtiges Video, Ton/Musik in der Animation: nicht gebaut.
- In kleinen Programmfenstern (Erweitert, nicht maximiert) ist die Bühne knapp; die Logo-Vorschau-Leiste blendet sich unter 470 px Höhe aus. Tests laufen maximiert („wie im Alltag“).

## Berührte Dateien
- `src/Trendo.Core/Kreativ/VectorKit.cs` (neu)
- `src/Trendo.App/VectorEditor.cs`, `VectorSkia.cs`, `KeyTimeline.cs`, `MainWindow.KreativVektor.cs` (neu)
- `src/Trendo.App/MainWindow.KreativStudios.cs` (nur Abschnitt Logos & Marken)
- `src/Trendo.App/MainWindow.KreativStudios2.cs` (nur Abschnitt Animation & Motion)
- `tests/Trendo.Core.Tests/VectorKitTests.cs`, `tests/Trendo.App.Tests/VectorStudiosUiTests.cs` (neu)
- `reports/T-0696/*`, `tasks/T-0696-animation-logos.md`, `tasks/T-0696-animation-logos.bericht.md`

ENTWICKLUNGSBERICHT – Sprachen: C# (Avalonia, SkiaSharp, PDFsharp). Rust eingesetzt: NEIN (UI-/Vektorlogik im bestehenden C#-Projekt; Bildberechnung über SkiaSharp, kein messbarer Vorteil durch Rust).
