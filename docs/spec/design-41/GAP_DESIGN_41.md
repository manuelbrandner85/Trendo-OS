# Design-Lückenliste Verbesserung 41 (UI-041-PREMIUM-EXPERIENCE)
**Stand:** 2026-10-22 · Vergleich gegen main `0c0525b` · IST-Bilder: Box-Build (Linux, Xvfb 1920×1080, `--offline --no-first-run --page …`), **nicht** Windows-PC. Vergleichsbilder: `reports/design-41/vergleich/`.
Paket unverändert in `docs/spec/design-41/` (Prüfsummen `SHA256SUMS`). Bilder sind KI-Entwürfe, keine Screenshots, keine Daten/Logos übernehmen.

| Ref | Bereich | IST (main) | SOLL (Ref) | Lücke | Task |
|---|---|---|---|---|---|
| 01 | Desktop | Feste Fläche ~1536×800 mit Gesamt-`ScaleTransform`; links 12 Bereiche + Schnellzugriff, Mitte Studio-Kachelraster + Logo + Ringe, rechts KI/Systemstatus/Projekte/Schnell-Tools; oben Menüleiste | Ruhiger Naturhintergrund mit freier Mitte, zentrales Suchfeld, schlanke linke Leiste (Start/Studios/KI/Dateien/Einstellungen + „Zuletzt verwendet“), rechtes Widget-Panel (Datum/Uhr, Metriken, Netzwerk), schlankes Dock | Responsives Layout statt Zoom; deutlich weniger Elemente auf dem Desktop; Suche zentral; Navigation auf 5 Einträge + Zuletzt | T-0102, T-0103 |
| 02 | Startmenü | vorhanden (Suche, Anheften, Zuletzt, A–Z), nicht per CLI fotografierbar | Glas-Panel: Suche, angeheftete Apps-Raster, Empfohlen/Kürzlich, Kategorien links, Nutzer/Power | Visuelle Modernisierung, Kategorienleiste; IST-Foto fehlt (UI41-0) | T-0104 |
| 03 | Studioübersicht | „Alle Studios“ im Fenster, 9 Bereiche, kleine Icon-Chips, Text-Karten | große Bildkarten mit Motiv, Filter-Chips (Alle/Kreativität/…), Suchfeld, Pfeil-Aktion | Kartenkomponente mit großem Symbolbereich, Filterchips, Suche in der Übersicht | T-0105 |
| 04 | Briefstudio | Ein langes Formular (Schritte 1–4), Registerkarten Schnell/Akten/Briefköpfe, **keine Live-Vorschau** | Links Navigation (Neu/Vorlagen/Meine Dokumente/Empfänger/Einstellungen), Mitte Formular + Editor mit Formatleiste, rechts **echte DIN-5008-Vorschau**, Aktionen Speichern/Exportieren/Als PDF/Senden oben | Dreiteiliges Layout, Live-Vorschau, Kopfaktionen; „Senden“ nur mit Freigabe-Gate | T-0106 |
| 05 | KI | KI-Fenster mit Modellwahl + rechte KI-Spalte „Nicht verbunden“ | Chat-Layout mit Verlauf links, Vorschlagskarten, Eingabe unten, rechts Modell & Status | Chat-Layout; Status nur echt (Nicht verbunden/Prüfen/Bereit/Antwort läuft/Fehler/Offline); „GPT-4o“ im Bild **nicht** übernehmen | T-0104 |
| 06/10 | Fenster/Dateimanager | MDI-Fenster mit Titelleiste, Min/Max/Close, Taskleisten-Eintrag, Resize/Snap (#199) | Weiche mehrstufige Schatten, aktiv/inaktiv deutlich, Glas-Titelleiste | Tiefe/Fokuszustände; Funktion nicht verändern | T-0107 |
| 07 | Einstellungen | Ein Fenster: Aussehen, Akzent, Hintergrund-Thumbnails, Bedienung | Navigationsliste links (System/Design/Anzeige/Audio/Netzwerk/Sicherheit), Theme-Karten (Obsidian/Aurora/Sunset/Light), Schalter „Dynamischer Hintergrund“ | Unterteilung in Bereiche, Theme-Presets | T-0107 |
| 08 | Systemmetriken | Ringe CPU/RAM/SSD, GPU „n. v.“ (echt) | Ringe + Netzwerk-Durchsatz, Leistungsoverlay | Netzwerk-Messung ergänzen; GPU weiter ehrlich „Nicht verfügbar“ wenn keine Quelle | T-0103 |
| 09 | Wallpaper/Themes | 4 eigene Hintergründe, Akzent folgt Bild | Presets, optionale dezente Bewegung, Pi-Fallback | Theme-Presets + Leistungsprofile (High-End/Standard/Pi) | T-0107 |
| – | Boot/First-Run (Fkt. 2, 17) | Einrichtungsassistent vorhanden | sanfte Überblendung, einfacher Willkommensdialog | Feinschliff ohne künstliche Wartezeit | T-0107 |
| – | Tokens (Fkt. 1, 14) | Farben/Brushes teils im Code erzeugt | zentrale ResourceDictionary-Tokens + Styles | Token-/Style-Architektur | T-0101 |
| – | QA (Fkt. 19, 20) | Klicktests 78 Kacheln (PC), Headless-Tests | Viewport-Matrix 1024×768…3840×2160, DPI 100–200 %, Kontrast, Tastatur, Vorher/Nachher-Messung | Matrix-Screenshots, Messungen | T-0100, T-0108 |

## Konflikte / Entscheidungen
1. **„Design belassen“ (Manuel, Startmenü-Entscheidung t25) ↔ Design 41:** Ref 01 ersetzt das bisherige Desktop-Bild (Kachelraster in der Mitte, Ringe, Schnell-Tools) durch eine ruhige Mitte mit Suchfeld. Das ist ein sichtbarer Umbau des von Manuel per „1000 % wie Designkonzept“ abgenommenen Desktops. Paket sagt selbst: bei Konflikt gilt funktionierende Architektur; globaler Rollout erst nach JA. → Manuel muss entscheiden, welches Bild gilt (Vorschlag: Design 41 ist neuer, gilt; altes Konzept wird abgelöst).
2. **Einfache Namen ↔ Referenzbilder:** Bilder zeigen „Briefstudio“, „3D-Studio“, „Video-Studio“, „Unreal Studio“, „Blender Studio“, „Gaming-Center“. Projektregel: Namen aus `StudioCatalog` („Briefe schreiben“, „3D-Modelle erstellen“ …). Paket §1 selbst: zentrale Katalognamen verwenden → **Katalognamen bleiben**, Bildtexte nicht übernehmen.
3. **„Unreal Studio“/„Blender Studio“ als eigene Karten:** gibt es nicht als Module; Blender/Unreal sind externe Programme. Nicht als Studios anlegen, nur als Programme mit echtem Status.
4. **Logo:** Referenzbilder zeigen einen „T“-Kreis; der gilt nicht. Gewählt und eingebaut ist Logo A „Obsidian-Kachel“ (`src/Trendo.App/Assets/logo/trendo-logo-a.svg`, 22.10.2026). Die Bildmotive nicht übernehmen.
5. **Weniger Elemente auf dem Desktop** (5 statt 12 Navigationspunkte) ↔ „jede Einzelheit, übersichtlich“: Funktionen bleiben über Studios/Startmenü erreichbar.
6. **Dock zeigt Fremd-Apps** (Firefox, Steam, Epic): nur echte, installierte Programme anzeigen.
7. **„Senden“ im Briefstudio:** nur über bestehendes Freigabe-Gate, keine Direkt-Sendung.
8. **Prüfstopp:** nach Desktop + Briefstudio (UI41-6) zwingend JA/NEIN von Manuel vor Rollout auf alle Studios (UI41-7).
