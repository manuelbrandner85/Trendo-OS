# Bericht T-0733: Bildbearbeitung + Künstlerstudio

**Auftrag:** `tasks/T-0733-bild-malen.md` (Profi-Audit 16.11.2026, Rang 3 und 8). **Branch:** `claude/T-0733-bild-malen`, eigene Kopie von `main` (0.9.103) in `%TEMP%\trendo-auftrag-T-0733`.

## Punkte

| Punkt | Vorher (Beleg) | Jetzt | Nachweis | Urteil |
|---|---|---|---|---|
| WebP-Export | Nur PNG/JPG/SVG (`MainWindow.Bildbearbeitung.cs` Exports; Audit audit-2 CORE-08 Nr. 10) | Export „WebP (klein, fürs Web)“, `ImageRender.Export` kodiert `.webp` mit SkiaSharp (Qualität 90, Transparenz bleibt); leerer Kodierer meldet sich statt still zu scheitern | `T0733_BildMalen_Ui.Tiff_in_webp_out`: Klick auf `prog-export-webp`, `SKCodec` = Webp, 400×200, Pinselstrich grün; **echtes ffprobe** liest `webp,400,200`; Rohlog `reports/T-0733/webp-tiff-fremdpruefung.log` (ffprobe + Pillow 13.0.0, SHA-256) | VOLLWERTIG |
| WebP-Import | Dateiauswahl kannte `webp` schon, nie geprüft | unverändert (SkiaSharp liest WebP), jetzt geprüft | gleicher Test: exportierte WebP wieder als Ebene | VOLLWERTIG |
| TIFF-Import | Nicht vorhanden (Audit Nr. 4) | SkiaSharp 3.119.4 kann TIFF **nicht** (geprüft: `SKBitmap.Decode` → null, steht als Assert im Test). Trendo wandelt TIFF mit dem eingerichteten FFmpeg verlustfrei in PNG unter `<Daten>/bild/importe/<name>-<sha12>.png` (bleibt nach Neustart auffindbar) und legt es als Ebene an; ohne FFmpeg klare Meldung „TIFF öffnet Trendo über FFmpeg …“. Dateiauswahl und Feld nennen TIFF | gleicher Test mit **echtem FFmpeg** (Fremddatei aus FFmpeg), Eingabe über das Feld `img-file` + Knopf „Als Ebene hinzufügen“; Pixel rot/blau stimmen | VOLLWERTIG mit FFmpeg; ohne FFmpeg nur Meldung (BLOCKIERT-Fall, nicht simuliert) |
| Werkzeug-Tasten | V B E T U Q C P M G K J S existierten seit T-0728, aber **kein Zoom-Werkzeug / kein Z** | Neues Werkzeug „⌕ Zoom“ (Taste Z wie Photoshop): Klick vergrößert eine Stufe (1/16 … 3200 %) an der Stelle (die Stelle bleibt unter der Maus, `ImageCanvas.Pan`), Alt+Klick oder Rechtsklick verkleinert, „Einpassen“/„100 %“ setzen die Verschiebung zurück; Zoomen ist kein Bearbeitungsschritt. Hilfetext nennt die Tasten | `T0733_BildMalen_Ui.Tool_keys_and_zoom`: V/B/E/M/T/Z je im Menü „Werkzeuge“ und im Tooltip „(Z)“, `ProgramMenu.Conflicts` leer, echte Tasten Z→M→Z, Klick zoomt (Stelle wandert < 1,5 px), Alt+Klick zurück, Verlauf unverändert; `T0710_MenuBar_Ui` 8/8 (alle Hüllen-Programme × 3 Stufen ohne Konflikt) | VOLLWERTIG |
| Künstlerstudio Auswahl | „Auswahl, Transformieren, Text: nicht gefunden“ (audit-2 STUDIO-ART-01 Nr. 2) | Werkzeug „⬚ Auswahl“ (Taste M): Rechteck aufziehen oder Lasso umfahren; hineinklicken + ziehen verschiebt den Inhalt der gewählten Ebene, Eckgriffe skalieren (Anker gegenüberliegende Ecke), Optionsleiste mit Rechteck/Lasso, „Skalieren“ per %, „Inhalt löschen“, „Auswahl aufheben“; Live-Vorschau beim Ziehen. Rechnung im Core (`PaintSelection`: Maske gerade-ungerade, Ausschneiden, bilinear mit vormultiplizierter Deckkraft). Auswahl liegt im Dokument (`sel`), daher setzt Rückgängig Inhalt **und** Auswahl zurück; Auswahl selbst ist kein Schritt (wie Krita) | `T0733_BildMalen_Ui.Art_select_move_scale` (echte Maus): Rechteck → Verschieben (+600/+200, alte Stelle frei, Ziel schwarz) → Eckgriff auf 200 % → zweimal Rückgängig über den Knopf → 50 % per Zahl → Lasso-Dreieck → aufheben. Core `KuenstlerAuswahlTests` 4/4 | VOLLWERTIG (Ebene für Ebene; Drehen/Verzerren gibt es nicht) |

Nebenbei behoben (nötig für die Auswahl): `ArtEdit` rechnet das Gesamtbild nach dem Schritt neu – vorher blieb die Anzeige stehen, wenn sich nur die Pixel einer Ebene änderten (gleiche Ebenen-Kennung, z. B. „Leeren“). Malfläche lag bei wenig Höhe (Mindesthöhe 300) über der Optionsleiste: Leiste jetzt `ZIndex = 1`; im Auswahlmodus zeigt die Leiste nur Auswahl-Knöpfe (einzeilig). Bild im Render (1600×1000, Profi) angesehen: `reports/T-0733/art-auswahl.png`, `bild-zoom.png` (eine goldene Hauptaktion je Ansicht wie bisher, Dunkel-Gold).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- App (je Klasse, `Trendo.App.Tests.exe -class …`): `T0733_BildMalen_Ui` 3/3 · `Bildbearbeitung_Ui` 5/5 · `Kuenstlerstudio_Ui` 6/6 · `T0728_Bedienung_Ui` 7/7 · `T0710_MenuBar_Ui` 8/8 · `MediaBrowser_Ui` 3/3 · `ProgramHeadDesign_Ui` 6/6.
- Core `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "KuenstlerAuswahlTests|KuenstlerTests|ImageOps|ProgramMenu|NoFixedUser"`: Bestanden 43/43.
- Rohlogs: `reports/T-0733/tests.log`, `reports/T-0733/webp-tiff-fremdpruefung.log`; `export.webp` SHA-256 `aefa8d77f9ff6a4bf51903bda70617e7763815390c2d157af122eb966ae288a2`.
- Rot-Grün: die neuen Tests nutzen neue Schnittstellen (`ArtSelection`, `ImgZoom`-Werkzeug, Export `webp`, `PaintSelection`) – auf altem Code kompilieren sie nicht bzw. finden `prog-export-webp`/`tool-img-tool-zoom` nicht. Während der Arbeit schlugen sie zudem echt fehl (Auswahl beim Verschieben als Skalieren erkannt, Anzeige nicht neu gerechnet) und wurden erst nach den Korrekturen grün.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis: VOLLWERTIG (Bild: Vorlage „Leere Leinwand“ → Malen → WebP; Künstler: „Querformat“ → Malen → Auswahl umsetzen).
2. Fremddatei importieren/bearbeiten/exportieren, Fremdprogramm öffnet Export: VOLLWERTIG für TIFF→WebP (TIFF aus echtem FFmpeg, WebP von ffprobe und Pillow gelesen, Rohlog + SHA-256).
3. Prozessabbruch ≤ 30 s: NICHT GETESTET in diesem Paket (unverändert die Programm-Hülle mit Wiederherstellung; die Auswahl liegt im Dokument und wird mitgesichert).
4. Claude/KI-Fachaufgabe: NICHT Teil dieses Pakets – unverändert (Bild: keine eigene Prompt-Aktion; Künstler: `KunstPromptAdapter`).
5. Test vom Klick bis zur echten Datei: VOLLWERTIG (`Tiff_in_webp_out`: Feld + Knopf → Mausstrich → Export-Knopf → Datei, ffprobe).
6. Einfach/Profi, Tasten, Rechtsklick, Ziehen: TEILWEISE – Tasten VOLLWERTIG (Z, M), Ziehen auf der Fläche VOLLWERTIG (Auswahl, Zoom-Klick), Rechtsklick = Herauszoomen im Zoom-Werkzeug (im Test nur Alt+Klick geprüft); ein eigenes Rechtsklick-Menü der Leinwand und Ablegen aus dem Explorer gibt es weiterhin nicht. Stufe „Einfach“ nicht eigens geprüft (Werkzeuge erscheinen über die Hülle wie bisher).

## Nicht geprüft
- WebP mit Transparenz im Fremdprogramm (nur deckendes Bild geprüft); Farbprofil/Metadaten werden nicht geschrieben.
- TIFF-Sonderfälle (mehrseitig, 16 Bit, CMYK, Kacheln): FFmpeg nimmt die erste Seite; nicht einzeln getestet.
- Zoom-Werkzeug: kein Verschieben der gezoomten Ansicht mit Leertaste/Handwerkzeug in der Bildbearbeitung (nur über erneutes Klicken an anderer Stelle).
- Bedienung mit echtem Grafiktablett; Leistung der Auswahl bei sehr großen Leinwänden (64 MP) nicht gemessen.
- Kein Volltest (macht die Hauptsitzung).

## Offene Punkte
- PSD-Import und PSD/TIFF-Export in der Bildbearbeitung (Audit „Was fehlt“ 1) – nicht Teil dieses Auftrags.
- Künstlerstudio: Drehen/Spiegeln der Auswahl, Auswahl hinzufügen/abziehen, Text-Werkzeug fehlen weiter.
- Künstlerstudio öffnet TIFF (noch) nicht – `ArtDecode` nutzt nur SkiaSharp; der FFmpeg-Weg der Bildbearbeitung ließe sich übernehmen.

## Berührte Dateien
`src/Trendo.App/MainWindow.Bildbearbeitung.cs`, `src/Trendo.App/ImageCanvas.cs`, `src/Trendo.App/ImageRender.cs`, `src/Trendo.App/MainWindow.Kuenstlerstudio.cs`, `src/Trendo.App/ArtControls.cs`; neu `src/Trendo.Core/Kreativ/KuenstlerAuswahl.cs`, `tests/Trendo.App.Tests/T0733BildMalenUiTests.cs`, `tests/Trendo.Core.Tests/KuenstlerAuswahlTests.cs`, `reports/T-0733/*`, `tasks/T-0733-bild-malen.md`, dieser Bericht. **Keine gemeinsamen Dateien** (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer) geändert.

**Status:** umgesetzt und gezielt getestet; gepusht auf `claude/T-0733-bild-malen`, kein Merge, kein Release.
