# T-0727 – Offene Probleme Gruppe B: Leistung und Ansicht (16./17.11.2026)

PC: AMD Ryzen 7 5700X (8 Kerne), 32 GB RAM, RTX 5070 (11,9 GB), Windows 11 Pro 10.0.26340, FFmpeg 9.0.2, Defender-Echtzeitschutz an.
Während der Messungen liefen auf dem PC parallel andere Sitzungen (Builds/Tests) – deshalb immer **abwechselnd vorher/nachher** gemessen und Mediane genannt.
Pfade mit Benutzernamen sind durch `C:\Users\<user>` ersetzt. Prüfsummen in `SHA256SUMS`.

| Punkt | Ursache (Messung) | Behebung | vorher → nachher | Urteil |
|---|---|---|---|---|
| B1 Kaltstart | 50–75 % der Kaltzeit liegen **vor `Main`** (2,4–7,5 s, warm 0,1 s), der Rest beim ersten Laden von Avalonia/Skia (app.init ≈ 1,1 s statt 0,45 s). Gleicher Inhalt in neuem Ordner ist wieder kalt → Prüfung frisch geschriebener Programmdateien durch Windows/Virenschutz beim ersten Laden, nicht Trendo-Code. Ohne ReadyToRun ist es in beiden Fällen langsamer. | keine Code-Änderung (kein Hebel im Programm ohne Risiko) | kalt 4,9–5,1 s (R2R) ↔ 5,5–10,3 s (ohne R2R); warm 1,24–1,60 s ↔ 2,44–2,71 s | Ursache belegt, nicht in Trendo behebbar; R2R bleibt |
| B2 3D-Aufbau | Profil (dotnet-trace): `JsonSerializer` über das ganze Netz als Zwischenspeicher-Schlüssel (`PartSolid`, `PreviewBytes`, ≈ 530 ms), je Fläche neue `SolidColorBrush`/`Pen` mit Eigenschaftssystem (`DrawFace` ≈ 700 ms), Kanten je neuer Ansicht neu gerechnet (≈ 120 ms) | Schlüssel = Teilwerte + SHA-256 des Netzes; unveränderliche Pinsel/Stifte; Kanten je Körper schwach gemerkt | Median 3 273 → 1 906 ms bis Ansicht steht (Import 699 → 488 ms), je 5 Läufe | verbessert (−42 %), 3D-Bild pixelgleich |
| B3 3D-Höhe | Im Programmfenster (90 % der Fläche) drückt der Kopf die Ansicht zusammen: 1366×768 ≈ 150 px, 1600×900 ≈ 180 px sichtbar (Testfenster 1440×900: ≈ 40 px), Seitenleisten unten abgeschnitten | Seite füllt die Höhe; bliebe weniger als 200 px Ansicht sichtbar (Grenze aus der Abnahme `W4_Core05_Ui.Preview_and_export_fit`), lässt sich die Seite senkrecht verschieben; Klick in die Ansicht verschiebt die Seite nicht | sichtbar nie unter 200 px, Seitenleisten erreichbar; ab 200 px (1600×1000 im Fenster, alle maximierten Größen) Bild wie vorher | verbessert; ganz gelöst erst mit kompaktem Kopf (Audit S-01/V-01) |
| B4 Überblendung | FFmpeg muss beide Stücke ab ihrem Schlüsselbild dekodieren (GOP 2 s): nur Stück b bis zur Stelle 294 ms, nur a 189 ms, beide ohne Effekte 357 ms, mit Effekten 433 ms; FFmpeg-Start allein 57 ms | keine Änderung – geprüfte Varianten ohne Gewinn: Hardware-Dekodieren (d3d11va 847 ms, cuda 626 ms), Auswahl vor Skalieren (428 ms), PNG-Stufen, Filter-Threads | unverändert: 0,43 s unter Last, 0,32–0,36 s auf ruhigem PC | Ziel 0,3 s knapp verfehlt; ohne andere Pixel kein Hebel gefunden |
| B5 Mischer ohne Demucs | `MsStemsPanel` prüfte zuerst „Demucs eingerichtet?“ und kehrte dann zurück | liegen die Spuren vor, gibt es Mischer und Stimme trotzdem; „Einrichten“ nur fürs Neu-Trennen | rot → grün (`T0727_Offen_Ui.Mixer_without_demucs`) | behoben |
| B6 Modellwahl Testumgebung | Die App fragt seit T-0712 Ollama (/api/tags), sonst Manifeste in den echten Ordnern – qwen2.5:0.5b nur, wenn gar kein allgemeines Modell da ist (dann richtig: Standardmodell zum Laden). Nur das **isolierte Testfenster** kennt absichtlich keine Modelle; zwei Echtläufe lasen /api/tags selbst | gemeinsame Hilfe `Ui.UseRealModels(w)` (Weg der App), in den Echtläufen Assist und Code-KI genutzt | echt: 9 Modelle von Ollama, 11,9 GB → Standard **qwen3:14b** über den App-Weg | sauber gemacht |
| B7 Roter Test | Seit der Menüleiste (T-0710) liegen Einträge geschlossener Untermenüs („Datei ▸ Neu“) logisch in der Seite; der Test nahm diesen unsichtbaren Text, das Ereignis erreichte das Fenster nie | Test nimmt nur Text, der im Fenster hängt (nach dem Anordnen), keine Menüeinträge | rot → grün | Test war falsch, Code richtig |

## B1 Kaltstart – `b1-kaltstart/`
`kaltstart.ps1`: veröffentlicht wie das Paket (`dotnet publish -c Release -r win-x64 --self-contained true`), in einen **frischen Ordner** kopiert (neue Dateien wie nach dem Auspacken),
Start mit eigenem Datenordner und `--startup-log`; „warm“ = zweiter Start im selben Ordner. `kaltvergleich.ps1`: abwechselnd mit und ohne ReadyToRun, je 3 Runden (`vergleich.log`).

| Lauf | main | app.init | window.ctor | first.frame |
|---|---|---|---|---|
| R2R kalt (3 Runden) | 2 452 / 2 355 / 2 560 | 3 508 / 3 463 / 3 673 | 4 275 / 4 280 / 4 541 | **5 001 / 4 882 / 5 113** |
| R2R warm | 91 / 102 / 141 | 515 / 551 / 703 | 760 / 841 / 1 017 | **1 239 / 1 337 / 1 598** |
| ohne R2R kalt | 2 769 / 1 848 / 5 045 | 4 367 / 3 089 / 6 948 | 5 415 / 4 089 / 8 863 | 6 830 / 5 474 / 10 341 |
| ohne R2R warm | 121 / 128 / 153 | 752 / 760 / 840 | 1 275 / 1 247 / 1 392 | 2 437 / 2 463 / 2 707 |

Einzelläufe davor (`start-a…d2`): kalt 5,3–17,5 s, davon vor `main` 2,6–7,5 s; **gleicher Inhalt in neuem Ordner** (`d2`) wieder 7,5 s vor `main`; alle Dateien vorher einmal lesen (`b-vorgelesen`, 5,5 s für 423 Dateien) half nicht (12,5 s vor `main`).
Feinere Marken im Fenster-Aufbau (`d1`, `d2`: `d.w1 … d.dedupe`) zeigen: die Init-Wellen brauchen kalt wie warm < 40 ms – der Kaltanteil liegt nicht in Trendos Start-Code.
Folgerung: Der Kaltstart ist das erste Laden frisch geschriebener, unsignierter Programmdateien durch Windows (Echtzeitschutz/Reputation) – vor Trendos erstem eigenen Befehl.
Hebel außerhalb dieses Pakets: **Codesignatur** (offen, nicht als bestanden markiert). Ohne ReadyToRun wäre es schlechter (kalt und warm). Rust: kein Vorteil, die Zeit liegt nicht in Trendos Code.
Nicht gemessen: ein Setup.exe-Lauf (BLOCKIERT wie in T-0724) und ein PC ohne Defender.

## B2 3D-Ansicht: Aufbau nach dem Import – `b2-3d-aufbau/`
Messung: `LeistungT0724Ui.Szene_40000_Dreiecke` (OBJ 1 MB, 40 000 Dreiecke, über die Oberfläche, headless), abwechselnd alter Stand (eigene Kopie des Integrationszweigs) und neuer Stand, je 5 Läufe (`vorher-rohlog.txt`, `nachher-rohlog.txt`):

| | Import fertig (ms) | bis Ansicht aufgebaut (ms) |
|---|---|---|
| vorher | 562 / 655 / 699 / 1125 / 716 – Median 699 | 2714 / 2767 / 3273 / 3877 / 3471 – **Median 3 273** |
| nachher | 439 / 449 / 488 / 577 / 531 – Median 488 | 1651 / 1906 / 1881 / 1906 / 2130 – **Median 1 906** |

Ursache (dotnet-trace, Stichproben des UI-Threads): Seitenaufbau `Studio3DView` 3× je Import (Meldung + Anzeige), darin `JsonSerializer.Serialize(Part3D)` mit 60 000 Koordinaten und 120 000 Indizes als Schlüssel (`PartSolid` ≈ 50 ms je Aufruf, 4 Aufrufe) und `Scene3D.ToJson()` als Vorschau-Schlüssel (≈ 70 ms je Aufbau); `CadView.Render` ≈ 530 ms je Bild, davon `DrawFace` ≈ 700 ms für zwei Bilder – je Fläche ein neuer `SolidColorBrush` und `Pen` (AvaloniaObject, Eigenschaftssystem); `SharpEdges` je neuer Ansicht ≈ 120 ms.
Behebung: `PartKey` (Werte als JSON ohne Netz + SHA-256 von Punkten und Indizes), Vorschau-Schlüssel ebenso; `ImmutableSolidColorBrush`/`ImmutablePen`; Kanten in einer schwachen Tabelle je Körper (Flächenzahl mitgeprüft).
Bildgleichheit: Render-Werkzeug (`TRENDO_3D_DEMO`, `TRENDO_3D`, neu `TRENDO_3D_OBJ` mit dem 40 000-Dreiecke-Netz), 1600 × 1000: **in der 3D-Ansicht 0 abweichende Pixel**; außerhalb höchstens 5/255 (Seiten-Rahmen) – `b3-3d-hoehe/bildvergleich.txt`.

## B3 3D-Ansicht im niedrigen Fenster – `b3-3d-hoehe/`
`fenster-3d-*-vorher/nachher.png`: Programmfenster auf dem Desktop (Standardgröße 90 % der Fläche). Vorher war die Ansicht bei 1366 × 768 rund 150 px, bei 1600 × 900 rund 180 px hoch und die Seitenleisten unten abgeschnitten.
Nachher bleibt immer mindestens **200 px** Ansicht sichtbar; reicht die Höhe dafür nicht, lässt sich die Seite senkrecht verschieben (Bildlaufleiste rechts), die Seitenleisten sind ganz erreichbar.
Warum 200 und nicht 320 px: Die vorhandene Abnahme `W4_Core05_Ui.Preview_and_export_fit` verlangt „bei üblichen Fenstergrößen ohne Scrollen“ und eine Arbeitsfläche ≥ 200 px – mit 240/320 px wurde sie rot (gemessen). Die Ansicht selbst ist wie bisher mindestens 320 px hoch angeordnet.
Ab 200 px sichtbarer Höhe ist alles wie vorher: `fenster-3d-1600x1000` unterscheidet sich nur in Uhrzeit/Kopfzeile, die maximierten Seiten `3d-<größe>-einfach|profi.png` (1366 × 768, 1600 × 900, 1600 × 1000) sind bis auf eine 1-px-Linie bei 1366 × 768 Profi gleich (`bildvergleich.txt`).
Test `T0727_Offen_Ui.Viewport_keeps_min_height` (1366×768, 1600×900, 1600×1000): auf altem Code rot (`rot-gruen-alter-code.txt`, dort noch mit Grenze 320 px: „Ansicht nur 4 px / 90 px hoch“), neu grün.
`Studio3DViewport_Ui.Viewport_edit` klickt jetzt wie eine Person erst nach dem Verschieben der Seite nach unten auf die Teile (Hilfe `ShowStage`), sonst lag ein Klickpunkt auf dem Fensterrand; Prüfungen unverändert.
Ganz gelöst ist die Höhe erst mit einem kompakteren Kopf (Audit S-01/V-01 „Schmaler Kopf“) – nicht Teil dieses Pakets.

## B4 Videoschnitt: Standbild in der Überblendung – `b4-ueberblendung/`
Graph der Stelle 3,5 s aus `VideoLookTests.Preview_equals_export_real` (1080p, Schieben 1 s, Look, Titel, Keyframes, Vignette), echtes FFmpeg 9.0.2, Rohlog `messung.txt`.
Erster Lauf (PC unter Last durch andere Sitzungen), Median je 7 Läufe: Gesamtaufruf 433 ms · nur Stück b bis zur Stelle 294 ms · nur Stück a 189 ms · beide nur mit Überblendung (ohne Effekte) 357 ms · ohne Effekte von b 343 ms · ohne Effekte von a 403 ms · ohne Ton 407 ms · leerer FFmpeg-Aufruf 57 ms.
Zweiter Lauf (ruhiger PC, `messung.txt`): Gesamtaufruf 336–359 ms (1080p), mit 960 px wie in der Oberfläche 324 ms · beide ohne Effekte 257 ms · nur b 211 ms · nur a 139 ms · ohne Effekte von b 277 ms.
Varianten ohne Gewinn (Bild jeweils pixelgleich geprüft): Auswahl der Bilder vor dem Skalieren 428/340 ms · `-filter_complex_threads 1` 428/330 ms · Hardware-Dekodieren d3d11va 847/633 ms, cuda 626/502 ms · PNG-Kompression 0/1 (960 px) 402–413 / 323–325 ms.
Ursache: Beide Stücke müssen ab ihrem letzten Schlüsselbild dekodiert werden (Quellen mit 2 s GOP), dazu ≈ 60–90 ms für die Keyframe-Bewegung und Vignette des zweiten Stücks und ≈ 55 ms FFmpeg-Start. Auf ruhigem PC liegt die Überblendung bei 0,32 s (960 px), unter Last bei 0,4–0,5 s – also knapp über dem Ziel. Eine weitere Verkürzung ginge nur über andere Pixel (Proxy, Vorschau ohne Effekte) oder einen dauerhaft laufenden FFmpeg – das erste widerspricht „Vorschau = Export“, das zweite ist ein Umbau. **Keine Änderung, Ziel 0,3 s knapp verfehlt.**

## B5–B7
- B5: `T0727_Offen_Ui.Mixer_without_demucs` – Trennen (Demucs ersetzt, schreibt echte WAV-Spuren), dann Demucs „nicht eingerichtet“ (leerer Werkzeugordner des Testfensters): Mischer und „Einrichten“ sichtbar, kein „Neu trennen“, Stumm ändert die Mischung, „Als Lied speichern“ mischt eine echte WAV. Alter Code: „Mischer fehlt“.
- B6: `T0727_Offen_Ui.Test_helper_models` – Testfenster ohne Hilfe: Quelle „keine“, Standard qwen2.5:0.5b mit „noch kein allgemeines Sprachmodell“; mit `Ui.UseRealModels` (echtes Ollama): `T-0727 B6 echt: 9 Modelle von Ollama, Grafikspeicher 11,9 GB → Standard qwen3:14b`.
- B7: `ClaudeOperate_Ui` 3/3 grün (vorher 2/3).
