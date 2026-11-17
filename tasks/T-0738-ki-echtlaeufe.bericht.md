# Bericht T-0738: Echtläufe – Bilder mit KI und Trendo Musik

**Branch:** `claude/T-0738-ki-echtlaeufe-2` (ersetzt `claude/T-0738-ki-echtlaeufe`, der wegen eines Benutzernamens im Beleg gelöscht wurde; eigene Kopie von `main` in `%TEMP%\trendo-auftrag-T-0738-fix`, danach gelöscht)
**Status:** beide Echtläufe auf diesem PC bestanden, Belege unter `reports/T-0738/`. Zwei echte Fehler im Weg behoben. Kein Volltest (macht die Hauptsitzung).

## Vorhanden auf diesem PC (geprüft, nichts installiert, nichts geladen)
- Grafikkarte: NVIDIA GeForce RTX 5070, 12 GB (nvidia-smi).
- Bild-KI: `TrendoWerkzeuge\ki\laufzeit\stablediffusioncpp\sd-cli.exe` (Vulkan-Build), Modell `ki\modelle\sdxl-turbo\sd_xl_turbo_1.0_fp16.safetensors` (6 938 081 905 Bytes = Größe aus der KI-Bibliothek). SD Turbo ist nicht eingerichtet.
- Musik-KI: `ki\laufzeit\acestep` (ACE-Step 1.5, `python_embeded`, `checkpoints\acestep-v15-turbo`, `acestep-5Hz-lm-0.6B`, VAE).
- Fremdwerkzeug zum Gegenlesen: ffprobe 9.0.2 (im PATH).

## STUDIO-IMAGEAI-01 „Bilder mit KI“
**Was war:** sd-cli-Weg nie echt gelaufen, UI-Test immer mit `ImageAiOverride` (audit-4, Kriterium 14). Fehlte die Bild-KI, sagte die App nur „nicht eingerichtet“ – nicht, ob Programm oder Modell fehlt. Lieferte sd-cli kein Bild, kam immer „Ist die Grafikkarte gerade mit etwas anderem beschäftigt?“ – die Ausgabe von sd-cli ging verloren.

**Was ist jetzt:**
- Echtlauf über den Weg der Oberfläche: Vorlage „Neues Bild“ → Beschreibung ins Feld `img-prompt` getippt → Klick (echte Zeigerereignisse) auf „Bilder malen“ → `ImgGenerate` → `RunWithInput(sd-cli.exe …)` → PNG im Bildordner des Dokuments (`<Daten>\ki-bilder\<dir>\`), Galerie zeigt es, Herkunftsnachweis daneben. Nichts ersetzt. Das Testfenster bekommt dafür nur den echten Werkzeugordner (`Deps.ToolsRoot`) und `KiRealToolsInTests = true` (gleiches Muster wie `CodeRealToolsInTests`).
- Ehrliche BLOCKIERT-Meldung: `ImgAiMissing()` prüft Programm und Modell einzeln. Hinweis an der Hauptaktion (kurz, damit das Menüband einzeilig bleibt – V-01): „Bild-KI fehlt“ / „Bildprogramm fehlt“ / „Bildmodell fehlt“ mit „Jetzt einrichten“ (→ KI-Bibliothek); im Bild nachgeprüft (1600 × 1000, Einfach, eine goldene Aktion). Beim Klick auf „Bilder malen“: „Es fehlt das Programm stable-diffusion.cpp (sd-cli.exe) und ein Bildmodell (SDXL Turbo, 6,9 GB, oder SD Turbo, 5,2 GB). So richtest du sie ein: …“.
- Fehler beim Malen: die Rohausgabe jedes sd-cli-Aufrufs (Befehl, Exitcode, Ausgabe, Fehlerausgabe) steht in `ImgAiLastLog`; `ImageAiKit.ErrorHint` macht aus Grafikspeicher voll / Modell nicht ladbar / Zeitüberschreitung einen Was-tun-Satz statt immer derselben Vermutung.

**Beleg (echt):** `reports/T-0738/bild-ki-lauf.log` (Rohausgabe sd-cli, Laufzeit, ffprobe), `bild-ki-leuchtturm.png` (1024 × 1024, 1 981 524 Bytes, SHA-256 `b354a269dfaed488c5e6850ea1f64387a2421ead729dceefa061d6a962d0607d`), `bild-ki-leuchtturm.png.json` (Herkunftsnachweis, gleicher SHA-256). Laufzeit vom Klick bis „1 Bild(er) fertig“: 20,2 s (sd-cli selbst: 17,99 s, SDXL Turbo, 4 Schritte, ganz auf der Grafikkarte). ffprobe: `codec_name=png, width=1024, height=1024`. Sichtprüfung: Leuchtturm auf Klippe bei Sonnenuntergang, Brandung – passt zur Beschreibung. Auffällig: unten rechts eine kleine Pseudo-Signatur, obwohl „signature, watermark“ im Negativtext steht (Eigenheit von SDXL Turbo bei cfg 1,0 – Negativtext wirkt dort kaum).

## STUDIO-MUSIK-01 „Trendo Musik“
**Was war:** Lied-Erzeugung in UI-Tests ersetzt (`AceClientOverride`), echter Lauf nur mit `TRENDO_ACE_LIVE` über einen eigenen Testweg (nicht die Oberfläche); kein Beleg „Klick auf Erstellen → Lied“ (audit-2, Kriterium 12/14). Der Hinweis unter „Erstellen“ versprach „Ein Lied dauert meist unter einer Minute“.

**Was ist jetzt:**
- Echtlauf über die Oberfläche: Trendo Musik → „Mit eigenem Songtext“ → Titel, Stil, Liedtext getippt, Länge 30 s, 1 Fassung, Deutsch → Klick auf „Erstellen“ → `MsCreateWithVoice` → `MusicStudioCreate` → `EnsureAceServer` startet Trendos eigenen ACE-Step-Server (127.0.0.1:8001) wie in der App → Auftrag, Abfragen, Download → MP3 in `Projekte\Musik\Probe-T-0738.mp3` und in der Liederbibliothek. Beim Schließen des Fensters beendet Trendo den Server.
- Gemessen (Einzelmessung: ein Lauf, RTX 5070 mit 12 GB, ein 30-s-Lied, 1 Fassung): Server-Start 115 s (Modelle laden), Erzeugen 142 s, zusammen 262,2 s vom Klick bis zum Lied. Andere Grafikkarten, längere Lieder oder mehrere Fassungen sind nicht gemessen; der Hinweis ist eine Richtgröße. Der Hinweis „unter einer Minute“ war damit falsch und lautet jetzt: „Beim ersten Lied lädt die Musik-KI erst ihre Modelle (etwa 2 Minuten), ein kurzes Lied braucht dann meist 2 bis 3 Minuten.“

**Beleg (echt):** `reports/T-0738/musik-lauf.log` (Ablauf, Meldung der App „✓ Fertig.“, Laufzeit, ffprobe, Rohlog des ACE-Step-Servers nur für diesen Lauf, Farbcodes entfernt), `trendo-musik-probe.mp3` (535 248 Bytes, SHA-256 `bc16c521ba8cf471594c4916027d9681ccd6aa3680ac18286dd0967476e805b3`). ffprobe: `codec_name=mp3, sample_rate=48000, channels=2, duration=30.000000`.
- Nicht im Echtlauf: Mastern (−14 LUFS) und automatisches Cover – im isolierten Testfenster sucht Trendo FFmpeg und die Bild-KI absichtlich nicht im echten System; das Lied ist daher ungemastert (`gemastert=False` im Log).

Alle Prüfsummen: `reports/T-0738/SHA256SUMS`. Belege mit LF-Zeilenenden, UTF-8 ohne BOM; `.gitattributes` `reports/T-0738/** -text`, damit die Commit-Blobs byte-gleich sind.

## Tests (Zahlen aus der Ausgabe)
| Lauf | Ergebnis |
|---|---|
| App `T0738_KiEchtlaeufe_Ui` ohne Umgebungsvariable (erster Stand) | 2 bestanden, 2 Echtläufe kehrten ohne `TRENDO_ECHT_KI` mit `return;` zurück und wurden dadurch fälschlich als „bestanden“ gezählt (Befund der Gegenprüfung, siehe unten – jetzt sichtbar übersprungen) |
| App `T0738_KiEchtlaeufe_Ui.ImageAi_echt` mit `TRENDO_ECHT_KI` | 1/1 bestanden (23,3 s) |
| App `T0738_KiEchtlaeufe_Ui.Musik_echt` mit `TRENDO_ECHT_KI` | 1/1 bestanden (265,4 s) |
| Core `--filter KreativKits2` | Bestanden: 5 erfolgreich, 0 Fehler, 0 übersprungen |
| App-Sammellauf, 41 Klassen, die die geänderten Dateien berühren (ClaudeMusik_Ui, DesignPaket3_Ui, Kie_Ui, KreativStudios2_Ui, KreativStudios3_Ui, Kuenstlerstudio_Ui, Modern_Ui, 23 Musik*-Klassen, StudiosKreativ3D_Ui, T0710_MenuBar_Ui, T0711_HuelleEtappe1b_Ui, T0715_HuelleE3_Ui, T0727_Offen_Ui, T0738_KiEchtlaeufe_Ui, UebersichtVorschlaege_Ui, VisionVideoT0699_Ui, FilmSchnitt_Ui, FilmStudio_Ui, ProgramHeadDesign_Ui) | Total 203, 0 Fehler, 2 übersprungen. Ein erster Lauf hatte 1 Fehler: `ProgramHeadDesign_Ui` V-01 „Arbeitsfläche beginnt bei 323 px“ – der lange Hinweistext „Bild-KI fehlt: Programm und Bildmodell“ brach das Menüband um; gekürzt auf „Bild-KI fehlt“, danach grün |

Rot-Grün: `Musik_hint_matches_measurement` auf dem alten `MainWindow.MusikStudio.cs` rot (1 Fehler), auf dem neuen grün. Die Tests zu Bild-KI und `ErrorHint` nutzen `ImgAiMissing`, `KiRealToolsInTests`, `ImageAiKit.ErrorHint` – auf dem alten Code kompilieren sie nicht (rot).

## Gegenprüfung behoben (17.11.2026)
Neuer Zweig `claude/T-0738-ki-echtlaeufe-2` von `main`, Inhalt des alten Zweigs ohne dessen Geschichte übernommen, ein Commit. Der alte Zweig wurde auf GitHub gelöscht, weil seine Geschichte den Benutzernamen enthielt. Kein neuer Echtlauf mit sd-cli oder ACE-Step.

| Befund | Was war | Was ist jetzt | Nachweis |
|---|---|---|---|
| 1 HOCH | `musik-lauf.log` Zeile 274: Benutzerordner URL-kodiert (`GET /v1/audio?path=C%3A%5CUsers%5C…`) – `Anon()` ersetzte nur den Klartextpfad | Zeile im Beleg auf `C%3A%5CUsers%5C%3Cuser%3E%5C…` gesetzt (nur diese Stelle, sonst Rohlog unverändert). `Anon()` ersetzt jetzt den Benutzerordner mit `\` und `/`, URL-kodiert mit `%5C` und `%2F` (groß/klein egal), den bloßen Benutzernamen als ganzes Wort und den Rechnernamen (vor dem Benutzernamen). `SHA256SUMS` neu berechnet, `sha256sum -c` gegen die Dateien: 5× OK | neuer Test `Beleg_anonymisiert_auch_url_kodiert` (Beispielname „Erika“); Datenschutz-Grep auf `git log -p main..` leer |
| 2 MITTEL | `ImageAi_echt`/`Musik_echt` ohne `TRENDO_ECHT_KI` mit `return;` → als „bestanden“ gezählt; Bericht sagte „übersprungen“ und „4/4 bestanden“ | `Assert.Skip("Übersprungen – Echtlauf nur mit TRENDO_ECHT_KI=<Belegordner>.")`; Testtabelle berichtigt | Testlauf: beide `[SKIP]` mit diesem Text |
| 3 NIEDRIG | Musik-Hinweiszeit ohne Einordnung | als Einzelmessung gekennzeichnet (ein Lauf, RTX 5070, 30-s-Lied, 1 Fassung) | Abschnitt STUDIO-MUSIK-01 |
| 4 NIEDRIG | `bild-ki-lauf.log` enthielt 7 Zeilen mit `ESC[K`; `NoAnsi` entfernte nur `ESC[…m` | ESC-Folgen im Beleg entfernt (0 ESC-Zeichen übrig; die Fortschritts-Wagenrückläufe von sd-cli bleiben als Rohausgabe). `NoAnsi` entfernt alle CSI-Folgen (`ESC[` Parameter, Zwischenzeichen, Endzeichen `@`–`~`) und läuft jetzt in `Beleg()` für jeden Beleg | Test `Beleg_anonymisiert_auch_url_kodiert` (`ESC[32m`, `ESC[0m`, `ESC[K`, `ESC[2K`, `ESC[1;5H`) |
| 5 NIEDRIG | Kurztext „sd-cli fehlt“ (Fachwort) | „Bildprogramm fehlt“ | `ProgramHeadDesign_Ui`, `DesignPaket3_Ui` (V-09 „Bild-KI fehlt“) grün. Der Zweig „nur Programm fehlt, Modell da“ ist nicht im UI-Test (bräuchte eine 6,9-GB-Modelldatei) |
| 6 NIEDRIG | ffprobe in `ImageAi_echt` optional (`if`) | `Assert.NotNull(fp)` wie im Musik-Test | Code; greift nur im Echtlauf |

Tests nach der Behebung (ohne `TRENDO_ECHT_KI`, kein Volltest):
- App-Sammellauf `T0738_KiEchtlaeufe_Ui`, `KreativStudios2_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket3_Ui`, `UebersichtEinfach_Ui` und 25 `Musik*`-Klassen: Total 100, 1 Fehler, 3 übersprungen – der Fehler war der neue Prüftest selbst (`Assert.DoesNotContain("\u001b", …)` vergleicht kulturabhängig und ignoriert das Steuerzeichen; zudem musste der Rechnername vor dem Benutzernamen ersetzt werden). Nach der Korrektur `T0738_KiEchtlaeufe_Ui` allein: Total 5, 0 Fehler, 2 übersprungen (die beiden Echtläufe). Übersprungen im Sammellauf: die zwei Echtläufe und der vorhandene „Musik-KI live“-Test.
- Core `--filter KreativKits2`: 5 erfolgreich, 0 Fehler, 0 übersprungen.

## Abnahmekriterien
1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – Bild: leere Vorlage → PNG 1024², Musik: leeres Formular → 30-s-MP3; beide echt belegt.
2. Fremddatei importieren/bearbeiten/exportieren, im Fremdprogramm öffnen: **TEILWEISE** – Ergebnisse von ffprobe (Fremdwerkzeug) gelesen; Import einer Fremddatei war nicht Teil des Auftrags (Bild-zu-Bild fehlt weiter, Musik-Import existiert und wurde hier nicht erneut geprüft).
3. Prozessabbruch verliert höchstens 30 s: **NICHT GETESTET** in diesem Auftrag (Hülle bzw. `MsLink` unverändert).
4. KI führt eine Fachaufgabe aus, Ergebnis im offenen Dokument, rückgängig: **TEILWEISE** – das Bild landet als Eintrag „Bild gemalt“ im Dokument (rückgängig machbar, PNG bleibt liegen); Claude-Aktion schreibt weiter nur `bildauftrag.txt` (nicht angefasst).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** – `ImageAi_echt`, `Musik_echt` (nur mit `TRENDO_ECHT_KI`, im normalen Lauf sichtbar übersprungen über `Assert.Skip`).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **NICHT GETESTET** / nicht Teil des Auftrags (unverändert).

## Was NICHT geprüft wurde
- Echter Lauf mit SD Turbo (nicht eingerichtet) und der zweite Versuch mit `--offload-to-cpu` (nicht nötig geworden).
- Mastern und Cover nach dem echten Lied (siehe oben), „Nur beschreiben“ mit Text-KI, Varianten > 1, Längen > 30 s.
- Einrichten über „Jetzt einrichten“ bis zum Download (kein Download ohne Freigabe) – nur der Sprung zur KI-Bibliothek ist getestet.
- Der Umschalter „Mit eigenem Songtext“ wurde im Echtlauf über das Klick-Ereignis des Knopfs ausgelöst (nicht über die Bildschirmstelle): im Testfenster ragt der Knopf mit der Testschrift über seine Spalte, die Trefferprüfung landet in „Meine Lieder“. Im gerenderten Bild der App bei 1100 × 800 und 1440 × 900 passt er. Alle anderen Klicks (Vorlage, Bilder malen, Erstellen) liefen über die Bildschirmstelle.

## Offene Punkte
- Claude-Aktion `bild.auftrag` malt weiter nicht (nur `bildauftrag.txt`), kein Bild-zu-Bild, kein Abbrechen beim Malen (audit-4, unverändert).
- Musik: Prompt-Aktion `musik.lied.planen` schreibt nur einen Liedplan (audit-2, unverändert).
- Pseudo-Signatur im SDXL-Turbo-Bild trotz Negativtext – ggf. später Hinweis oder Nachbearbeitung.
- Der Echtlauf hängt Zeilen an `TrendoWerkzeuge\ki\acestep-server.log` an und nutzt den Cache von ACE-Step (wie die App selbst).

## Berührte Dateien
- `src/Trendo.App/MainWindow.KreativStudios2.cs` – `KiRealToolsInTests`, `ImgAiLastLog`, `ImgAiMissing()`, Rohausgabe/Was-tun bei Fehler, Blocker/Status
- `src/Trendo.App/MainWindow.MusikStudio.cs` – Hinweistext unter „Erstellen“ (eine Zeile)
- `src/Trendo.Core/Kreativ/KreativKits2.cs` – `ImageAiKit.ErrorHint`
- `tests/Trendo.App.Tests/T0738KiEchtlaeufeUiTests.cs` (neu), `tests/Trendo.Core.Tests/KreativKits2Tests.cs`
- `reports/T-0738/*` (neu), `tasks/T-0738-ki-echtlaeufe.md`, `tasks/T-0738-ki-echtlaeufe.bericht.md`
- gemeinsam: `.gitattributes` (eine Zeile `reports/T-0738/** -text`, nötig für byte-gleiche Belege)
