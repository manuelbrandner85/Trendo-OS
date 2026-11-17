# Bericht T-0715: B6 „Programm-Hülle für alle“ – Etappe E3 (Kreativ/Medien)

**Branch:** `claude/T-0715-huelle-e3` (vom Integrationszweig `claude/T-0685-b1-integration`, Stand 492505cb6), eigene Kopie in `%TEMP%\trendo-auftrag-T-0715`.
**Grundlage:** T-0708, T-0711, T-0714, Umzugsliste `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E3). Vom Nutzer freigegeben.

## Programmliste E3 (alle Kreativ/Medien-Programme außerhalb der Hülle) – was war → was ist jetzt

| Programm | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| **Medien bearbeiten** (`kreativ-medien`) | Arbeitsstand (Datei, Ausschnitt, Clipliste, Bildeinstellungen) nur im Speicher; Bildeinstellungen gingen beim Neuzeichnen verloren (Felder wurden erst bei „Bild speichern“ gelesen); kein Rückgängig; FFmpeg schrieb direkt unter den Ergebnisnamen, Bilder über `File.Create` | `MediaLink` (`LINK-MEDIEN`) über `LinkSlot`/`ProgramLink`: Rückgängig/Wiederholen 100 Schritte (Dateiwahl, Ausschnitt, Clipliste, jede Bildeinstellung; Regler/Tippen nach Pause ein Schritt), Notsicherung im 20-s-Takt (atomar, für das Fenster gesperrt), Karte `media-recovery`. Gespeichert = Ergebnisdatei entstanden. Ergebnisse atomar: `MediaStudio.RunInto` (FFmpeg schreibt in `<Name>.~xxxx.teil.<Endung>`, erst nach Erfolg umbenannt; Fehler hinterlässt nichts), Clipliste-Datei, Bilder (`ImageTools` über `AtomicFile`), Beispielbild | UI `T0715_HuelleE3_Ui.Media_image` (Helligkeit + getippter Text, 2× Rückgängig, 2× Wiederholen, Takt, zweites Fenster ohne Karte, Fenster zu, Karte, exakt der Stand, Felder zeigen ihn, „Bild speichern“ → echtes JPG 320 px, Sicherung weg), `Media_video` (**echtes FFmpeg**: Clipliste + Ausschnitt, Zurückholen, „Ausschnitt speichern“ → 1,5 s langes MP4, keine `.teil`-Reste); Core `Media_outputs_atomic` (echtes FFmpeg: Schnitt, Zusammenfügen, Fehlerfall) |
| **Trendo Musik – Spuren-Mischer** | Pegel/Stumm/Solo je Spur (`_msStemMix`) nur im Speicher, kein Rückgängig; Mischung/Einzelspuren als WAV direkt, Spur-MIDI, Unsicherheitsbericht, Karaoke-LRC mit `File.Write*Async` | `MsStemLink` (`LINK-MUSIK-SPUREN`): Rückgängig/Wiederholen 100 Schritte in der Kopfzeile des Mischers (keine zusätzliche Zeile), Notsicherung, Sperre, Karte `ms-stem-recovery` oben im Lied-Bereich (auch ohne gewähltes Lied); Zurückholen wählt das gemischte Lied und zeigt „Spuren“. Gespeichert = „Als Lied speichern“. WAV atomar (Zwischendatei + Verschieben), MIDI/Bericht/LRC über `AtomicFile` | UI `Music_stem_mixer` (Stumm, Solo über die Knöpfe, 2× Rückgängig/Wiederholen, Takt, zweites Fenster ohne Karte, Karte, exakt die Mischung, Mischer sichtbar, „Als Lied speichern“ → echte WAV, Sicherung weg) |
| **Trendo Musik – DAW-Ansicht** (`MusikDaw.cs`) | „ungeprüft“ | **geprüft: kein Dokument.** Transportleiste und Arrangement zeigen das gewählte Lied nur an; „Im Mischpult öffnen“ legt ein Mehrspur-Dokument der Hülle an (Audio bearbeiten – schon in der Hülle). Nichts anzuschließen | `MusikDaw_Ui` grün |
| **Live streamen** (`sendung`) | Ablauf und Szenen sofort gespeichert, aber **kein Rückgängig**; `szenen.json` über feste `.neu`-Datei (zwei Fenster konnten sich stören); Exporte (CSV, Kapitel, Moderationskarten, Wochenplan, versetzte SRT, OBS-JSON) mit `File.WriteAllText`; kein Versionsfeld | `BcLink` (`LINK-SENDUNG`): Rückgängig/Wiederholen 100 Schritte über Ablauf **und** Studio (Szenen, Einblendungen, Tonmischer, Sendeplan; Szenenwahl ist kein Schritt), Knöpfe in der vorhandenen Kopfzeile. **Keine Notsicherung/Karte – begründet:** jede Änderung (auch Rückgängig) wird sofort atomar gespeichert, es gibt nichts Ungesichertes (`Worth` = false). `szenen.json` über `AtomicFile`, alle Exporte atomar. `Version` in `ablauf.json` und `szenen.json` (fehlend = 1; neuere Fassung wird gezeigt, aber nicht überschrieben, rote Zeile `bc-newer`) | UI `Broadcast_undo` (alte `ablauf.json` ohne Version wird gelesen; zwei Programmpunkte getippt + Szene, 3× Rückgängig, Zwischenstand im zweiten Fenster schon auf der Platte, Wiederholen, Rückgängig über **Menü „Bearbeiten“**, keine Sicherungsdatei, Datei hat `"Version": 1`, CSV-Export, Fenster zu → neues Fenster hat den Stand, keine Karte); Core `Rundown_version`, `Scenes_version`, Abbruchtest unten |
| **TV & Live** (`tv`) | – | **geprüft: kein Dokument** (Abspieler, Senderlisten werden geladen, nicht bearbeitet) | – |
| **Freie Bilder & Klänge** (`assets`) | Download direkt unter den Zielnamen (`File.Create`), bei Abbruch blieb eine halbe Datei | **geprüft: kein Dokument.** Download jetzt als `.teil`, MD5-Prüfung, dann Verschieben; `LIZENZ.txt` atomar | `W4_X07_Ui` grün (Quelltext-Gegenprobe in `Creative_stores_atomic`) |

Menüleiste (T-0710): `sendung` und `kreativ-medien` haben jetzt „Datei“ (vorhandene Knöpfe: Exporte bzw. Ausschnitt/Bild/Clips/Medienordner) und „Bearbeiten › Rückgängig/Wiederholen“ (aus ihrem `LinkSlot`); bei Trendo Musik kommt „Rückgängig – Spuren-Mischung“ dazu. Nachweis `Menu_bar`, Bild `reports/T-0715/sendung.png`, `kreativ-medien.png` (Leiste in der Titelleiste, Rückgängig · Wiederholen nicht golden).

Keine Funktion entfällt, keine zweite Engine (alles über `LinkSlot` → `ProgramLink` → `ProgramDocument`). Kein Programm unverhältnismäßig groß – nichts abgegrenzt.

## Absturz (CrashProbe)
- Modus `sendung`: genau die Speicherwege der App (`BroadcastDesk.Save` + `LiveShow.Save`), 4000 Programmpunkte, 12 Szenen mit je 400 Einblendungen. Core `Kill_while_saving_broadcast`: **12 harte Abbrüche, 12 mitten im Schreiben**, beide Dateien jedes Mal lesbar, höchstens der laufende Schritt verloren.
- Modus `link-medien`: Notsicherung der Werkbank über `ProgramLink` mit Sperre, 80 000 Clips. Core `Kill_while_autosaving_media_link`: **12 harte Abbrüche, 9 mitten im Schreiben**, nie eine halbe Sicherung, verwaiste Sperre stört nicht, höchstens 1 Schritt verloren.

## Rot-Grün
Neue Tests nutzen `MediaState`, `MediaLink`, `MsStemLink`, `BcLink`, Knöpfe `media-/ms-stem-/bc-undo|redo|recovery|restore-0`, `Rundown.Version`, `LiveShow.Version/Newer`, `MediaStudio.RunInto`, CrashProbe-Modi `sendung`/`link-medien` – auf dem alten Stand nicht übersetzbar bzw. nicht vorhanden; `Creative_stores_atomic` prüft im Quelltext, dass die alten direkten Schreibstellen weg sind (auf altem Code rot). Zwischenstände rot und behoben: Quelltext-Prüfung zählte `AtomicFile.WriteAllText(` mit (jetzt Wortgrenze); `link-medien` schrieb zu schnell (3/12 im Schreiben) – Umfang erhöht; Spuren-Test: im neuen Fenster ohne eingerichtetes Demucs zeigt das Lied „Einrichten“ statt Mischer (vorbestehendes Verhalten, Test setzt „eingerichtet“ wie im ersten Fenster).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- App, ein Lauf mit 43 Klassen (Pflicht: `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket3_Ui`, `DesignPaket4_Ui`, `T0708_HuelleEtappe1_Ui`, `T0711_HuelleEtappe1b_Ui`, `T0714_HuelleE2_Ui`, `T0710_MenuBar_Ui`, `AssistChain_Ui`; neu `T0715_HuelleE3_Ui`; berührt: `SPEC15_Ui`, `SPEC15_LiveUi`, `LiveStudio_Ui`, `ClaudeLive*_Ui`, `MediaEditTests`, `W4_Core08_Ui`, `MediaPro_Ui`, `MediaBrowser_Ui`, `MediaIcons_Ui`, `W4_X07_Ui`, alle Musik-Klassen (Stems, Spuren, Studio, DAW, Noten, ProWerkzeuge, Mitsingen, Suno, Plus, Profi, Stimme/n, ClaudeMusik, MusicPlayer), `EveryButton_Alltag`, `T0203_Programme_Ui`, `ProgramFrameTests`, `WindowRebuildTests`, `Runde2ToolsUi`): **168 Tests, 0 rot, 0 übersprungen** (`reports/T-0715/app-tests.log`).
- Core gezielt (T0715, T0708, T0711, T0714, ProgramCrash, LiveShow, SPEC15_Broadcast/Plus, W4_Core08_Media, W4_X07_Assets, MusicPro, PromptAction, AtomicFile) **und QA-019**: **142 Tests, 141 grün, 1 rot** (`core-tests.log`) – rot: `T0708_ProgramLinkTests.Kill_while_autosaving_link` („6 von 16 Abbrüchen im Schreiben“, Zeitbedingung unter Last; schon in T-0711/T-0714 als lastempfindlich gemeldet, Modus `link` unverändert). Nachlauf allein T0708 + T0715: **11/11 grün** (`core-tests-nachlauf.log`). QA-019 einzeln: 3/3 grün.
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests OK** (`baseline.log`).
- Bilder 1600×1000 angesehen: `sendung.png`, `kreativ-medien.png`. Prüfsummen `reports/T-0715/SHA256SUMS.txt`. Kein Volltest.

## Abnahmekriterien (für die Programme dieser Etappe)
1. Aus leerer Vorlage ein echtes Ergebnis: **TEILWEISE** – nebenbei echte Dateien über die Oberfläche (JPG aus dem Bild-Editor, MP4-Ausschnitt mit echtem FFmpeg, WAV-Mischung, CSV-Ablaufplan), nicht als eigenes Ziel.
2. Fremddatei importieren/exportieren im Fremdprogramm: **NICHT GETESTET** (nicht Gegenstand; FFmpeg liest die erzeugten MP4 wieder ein – Dauer gemessen).
3. Prozessabbruch verliert höchstens 30 s: **VOLLWERTIG** für Medien bearbeiten (Takt 20 s, Abbruchtest `link-medien`) und Live streamen (sofort gespeichert, Abbruchtest `sendung`); Spuren-Mischer über denselben Sicherungsweg ohne eigenen Abbruchtest. Ausnahme Live streamen: Text in einer Zelle, die noch den Fokus hat, wird erst beim Verlassen/Enter übernommen. Harter Abbruch der App selbst nicht gefahren.
4. Claude/KI-Änderung rückgängig: **NICHT GETESTET** (Änderungen über die Seite werden beim Neuzeichnen ein Schritt; kein echter KI-Lauf).
5. Klick bis zur echten Datei: **TEILWEISE** – „Bild speichern“, „Ausschnitt speichern“ (echtes FFmpeg), „Ablaufplan (CSV)“ per Klick; Mischung über die öffentliche Methode.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Menüleiste mit „Bearbeiten“ auf den neuen Seiten (per Menü-Befehl geprüft); keine neuen Kürzel.

## Was NICHT geprüft wurde
- Kein Volltest; kein harter Abbruch des App-Prozesses selbst; kein eigener Abbruchtest für den Spuren-Mischer.
- Echte Demucs-Trennung (im Test ersetzt über `StemSplitOverride`; das Mischen/Speichern selbst ist echt).
- Bilder in Bedienstufe Einfach nicht eigens gerendert (Rendering in der Standardstufe).

## Offene Punkte
- Vorbestehend: `T0708…Kill_while_autosaving_link` lastempfindlich.
- Live streamen: Zelle mit Fokus (siehe 3).
- Vorschaubilder der Zeitleiste (`MediaStudio.Frames`) nicht atomar – Zwischenspeicher, wird neu erzeugt.
- Musik: ohne eingerichtetes Demucs zeigt ein getrenntes Ursprungslied „Einrichten“ statt des Mischers (vorbestehend; die Spuren liegen schon vor) – Vorschlag für später.

## Berührte Dateien (für die Zusammenführung)
- neu: `tests/Trendo.Core.Tests/T0715_HuelleE3Tests.cs`, `tests/Trendo.App.Tests/T0715HuelleE3UiTests.cs`, `tasks/T-0715-*`, `reports/T-0715/*`
- Core: `MediaStudio.cs` (`RunInto`, `TempOf`), `W6/Broadcast.cs` (`Rundown.Version`, `FormatVersion`, `Newer`), `W6/LiveShow.cs` (`Version`, `Newer`, Save über AtomicFile, gibt bool zurück), `Assets.cs` (LIZENZ.txt atomar)
- App: `MainWindow.W4Media.cs`, `ImageTools.cs`, `MainWindow.MusikStemsPlus.cs`, `MainWindow.MusikStudio.cs` (Karte im Lied-Bereich, 3 Zeilen), `MainWindow.Spec15.cs`, `MainWindow.LiveStudio.cs` (Kommentar), `MainWindow.W4Assets.cs`
- **gemeinsame Datei, nur additiv:** `src/Trendo.App/MainWindow.MenuBar.cs` – zwei neue Fälle `"sendung"` und `"kreativ-medien"` im `switch` von `ExternalMenus` direkt vor `_ => []` (3 Zeilen). `ExternalMenuPages` **nicht** geändert (die Seiten werden über ihren `LinkSlot` erkannt).
- **nicht** angefasst: `ProgramLink.cs`, `MainWindow.LinkSlots.cs`, `MainWindow.ProgramLinks.cs`, `MainWindow.ProgramShell.cs`, `FachBase.cs`; keine Programme von E4/E5.
- sonst: `tests/Trendo.CrashProbe/Program.cs` (+2 Modi `sendung`, `link-medien`), `docs/audit/2026-11-16-profi-audit/huelle-umzug.md`

**Status:** Etappe E3 umgesetzt und gezielt geprüft; nicht im Volltest.
