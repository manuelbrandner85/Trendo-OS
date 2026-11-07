# Trendo Musik ↔ Suno – Abgleich jeder Funktion und der Oberfläche (07.11.2026)

Auftrag: „bei trendo musik prüfe exakt jede einzelne funktion was suno hat im bestenfall auch die ui und anordnung usw und gleiche ab … fehlende funktionen und die ui von suno übernehmen … baue exakt nach mit rea“.

## Arbeitsweise (Clean-Room)

**Quellen:** Ausgewertet wurden nur öffentliche Informationen. Dazu gehören Sunos eigene Versionshinweise (suno.com/release-notes, Stand 6.11.2026) und Hilfeseiten (help.suno.com: Studio, Export). Ergänzend dienten öffentliche Bildschirmfotos der Erstellen-Seite (saasui.design) und Anleitungen Dritter zur Oberfläche von v6 (Stand September 2026). Die Erstellen-Seite selbst verlangt eine Anmeldung und wurde nicht geöffnet.

**REA:**
- Der REA-Server ist in dieser Sitzung nicht verbunden.
- Suno ist ein Webdienst ohne Programmdatei auf dem PC. REA könnte höchstens eine eigene, angemeldete Browsersitzung beobachten.
- Nach der Trendo-Regel „REA nur Clean-Room“ übernimmt Trendo Funktionen, Abläufe und die Anordnung. Code, Logo, Grafiken, Texte und Markennamen von Suno übernimmt Trendo nicht; das Aussehen bleibt im Trendo-Stil (dunkel-gold).

**Technik:** Erzeugt wird lokal mit ACE-Step 1.5 (MIT), dazu kommen Demucs, Applio, librosa, Piper und die eigenen Werkzeuge. Was Sunos Server-Modelle können, schafft ein lokales Modell auf 12 GB nicht immer. Das steht unten ehrlich je Punkt.

## Oberfläche und Anordnung

| Bereich | Suno (Web, 2026) | Trendo Musik heute | Abstand |
|---|---|---|---|
| Navigation | schmale Leiste links: Start, Entdecken, Erstellen, Studio, Bibliothek; unten Meldungen, Labs, Mehr | keine eigene Navigation; Modi als Knöpfe (Einfach, Profi, Beat, Noten) | **fehlt** → V1 |
| Erstellen-Spalte | Reiter „Songs / Speech“; Umschalter Simple/Advanced; Modellwahl oben rechts; „+ Audio / + Voice / + Image“; Beschreibung; Knopf „Create“ | Einfach/Profi/Beat/Noten; Beschreibung; Instrumental; Sprache; Länge; Varianten; „Erstellen“ | ◐ → V2, V3 |
| Mittlere Spalte | „Workspaces › Mein Arbeitsbereich“, Suche, Filter, Sortierung, Ansicht, Schnellfilter (Liked, Public, Uploads), Seiten; Zeilen mit Cover, Titel, Stil-Tags, Remix-Knopf, „…“-Menü | „Meine Lieder“: eigene Aufnahme, Suche, Favoriten | ◐ → V4, V5 |
| Rechte Spalte | Details zum gewählten Lied (Cover, Text, Aktionen) | „Lied“ mit Reitern Lied, Bearbeiten, Spuren & Mitsingen, Musikvideo, Profi | ✓ (Inhalt ist eher reicher) |
| Abspielleiste | unten, über die ganze Breite: Transport, Fortschritt, Lautstärke | oben im DAW-Stil: Transport, Takt·Zeit, Tempo, Takt, Tonart, Fortschritt | ◐ Position → V1 |
| Studio | eigene Seite: Mehrspur-Zeitleiste, Take-Spuren, Clip-Einstellungen, MIDI, Synth, Effekte, Automation, Chat-Leiste, Export | DAW-Ansicht des Lieds und Übergabe an das Mischpult (Audioeditor, Mehrspur) | ◐ → V14 |

## Funktionen – jede einzelne

✓ vorhanden · ◐ teilweise · ✗ fehlt · ⊘ nicht sinnvoll oder nicht machbar (mit Grund)

### Erstellen
| Suno | Trendo | Vorschlag |
|---|---|---|
| Simple: ein Satz → Lied | ✓ Einfach-Modus | – |
| Mehrere Richtungen und Referenzen kombinieren (Lieder, Playlists, Audio, Bild, Video) | ◐ Klang-Referenz (Datei oder Lied); Bild und Video fehlen | V2 |
| Modellwahl (v6, v6-wild, v6-mini) | ◐ intern Turbo oder Grundmodell, nicht wählbar | V2 |
| Advanced: Liedtext, Stile mit Tag-Vorschlägen | ✓ Profi-Modus, über 180 Genres, Gesangsarten, Instrumente | – |
| Exclude styles | ✗ | V3 |
| Vocal Gender | ◐ über Gesangsart/Tonlage, kein eigener Schalter | V3 |
| Duration (Schieberegler) | ✓ Länge bis 10 Minuten (Auswahlliste) | V3 (Schieberegler) |
| Max Mode | ✗ (gäbe es lokal: Grundmodell mit mehr Schritten) | V3 |
| Weirdness, Style Influence, Audio Influence, Variety | ✗ (lokal über Zufalls- und Führungsstärke, Referenzstärke) | V3 |
| My Taste | ✗ | V13 |
| Liedideen / Zufallsstil | ◐ | V2 |
| Sounds: One-Shot und Loop mit Tonart/BPM | ◐ Beat-Modus, Geräusch-Werkstatt (Audio), MMAudio | V7 |
| Speech (Sprache mit Musik in einem) | ✗ (Piper vorhanden) | V8 |
| Cover (anderer Stil) | ✓ | – |
| Audio hochladen (bis 8 min) und weiterbauen | ✓ eigene Aufnahme, alle Formate | – |
| Instrumental | ✓ | – |
| Zwei Fassungen je Auftrag | ✓ (1–4) | – |
| Sprachen | ✓ 50 Sprachen | – |

### Liedtext
| Suno | Trendo | Vorschlag |
|---|---|---|
| Lyricist (eigene Beispieltexte als Schreibstil) | ✗ | V6 |
| Natürliche Sprache („Zeile lustiger“) | ◐ Claude als Produzent, Refrain eingängiger | V6 |
| Reim- und Zeilenvarianten per Markieren | ◐ Reime (Wörterbuch), Silben | V6 |
| Vollbild-Editor, Abschnitts-Etiketten, Autosave | ◐ Gliedern, Etiketten im Text | V6 |
| Einzelnes Wort oder einzelne Zeile im fertigen Lied ändern | ✗ (lokal über Abschnitt neu mit neuem Text – erst messen) | V6 |

### Lied bearbeiten (Remix/Edit)
| Suno | Trendo | Vorschlag |
|---|---|---|
| Extend | ✓ Verlängern (Grundmodell) | – |
| Replace Section | ✓ Abschnitt neu erzeugen | – |
| Teil per Sprache ändern (v6) | ◐ über Claude-Plan | V5 |
| Crop / Remove Section | ◐ im Audioeditor, nicht im Lied-Menü | V5 |
| Remaster (mit Variation) | ◐ Mastern; „neu mit Variation“ fehlt | V5 |
| Reuse Prompt | ✗ als ein Knopf | V5 |
| Mashup (zwei Lieder) | ✗ | V10 |
| Use as Sample | ✗ | V10 |
| Song Editor (Abschnitte umstellen an der Welle) | ◐ DAW-Ansicht ohne Umstellen | V14 |

### Spuren, MIDI, Stimmen
| Suno | Trendo | Vorschlag |
|---|---|---|
| Auto Split (12 Kategorien) | ◐ 4 Spuren + Begleitung (Demucs, schnell/genau) | V9 (6 Spuren mit Gitarre und Klavier, mehr ist lokal nicht verlässlich) |
| Split from Mix (ein Instrument + Rest) | ◐ Gesang + Begleitung | V9 |
| Advanced Split (fast 100 Instrumente) | ⊘ kein freies lokales Modell dafür | – |
| Get MIDI je Spur | ◐ Melodie → Noten im Audioeditor | V9 |
| Voices (eigene Stimme singen lassen) | ✓ V1–V8 mit Einwilligung, echt geprüft | – |
| Personas (Stimme und Stil aus einem Lied merken) | ◐ Klang-Referenz/Persona | V11 |
| Custom Models (≥ 6 eigene Lieder trainieren) | ✗ | V12 (erst auf 12 GB messen) |

### Studio (Mehrspur)
| Suno | Trendo | Vorschlag |
|---|---|---|
| Zeitleiste mit Spuren, Take-Spuren, Comping | ◐ Mischpult/Mehrspur, keine Takes | V14 |
| Clip-Einstellungen (Lautstärke, Tonhöhe in Halbton und Cent, Loop, Rückwärts, Stretch), Fade-Kurven, Warp-Marker | ◐ teils im Audioeditor | V14 |
| MIDI-Spur, Wavetable-Synth, Computer-Tastatur, Arpeggiator | ◐ Piano-Roll, Beat-Modus mit Arpeggiator | V14 |
| Effekte (Sidechain, Faltungshall), Automation | ✓ Busse, Sends, Sidechain, Automation (S4-01/09), Plugins (S4-06) | – |
| Chat-Leiste („mach eine Basslinie“) | ◐ Studio-Assistent oben | V14 |
| Instrument zu einem Zeitbereich erzeugen | ✗ (ACE-Step „lego“ 04.11. gemessen: folgt den Akkorden nicht) | V14 (ehrlich: nur wenn eine Messung besteht) |
| Export ganzes Lied, Bereich, Mehrspur | ◐ WAV/FLAC/MP3, Spuren-Ordner | V14 |
| Versionen, automatisch speichern | ✓ (Programm-Hülle) | – |

### Bibliothek, Teilen, Extras
| Suno | Trendo | Vorschlag |
|---|---|---|
| Workspaces, Playlists, Alben | ✗ (nur eine Liste) | V4 |
| Filter, Sortierung, Listen- und Rasteransicht | ◐ Suche und Favoriten | V4 |
| Cover-Art als Bild oder Video, per Text verfeinern | ◐ Bild mit 4 Layouts | V15 |
| Hooks (Kurzvideo mit eigenem Clip) | ◐ Musikvideo 9:16 | V16 |
| Tägliche Liedidee und Kalender | ✗ | V17 |
| Mitsingen, Karaoke, Analyse, Video, Mastering-Ziele | ✓ (fehlt bei Suno, bleibt erhalten) | – |
| Entdecken/Community, Kommentare, Folgen, Credits, Abos | ⊘ braucht einen Server; Trendo ist lokal | – |
| MCP-Anbindung | ◐ Trendo-Agenten/Claude im Studio | – |

## Vorschläge (Ja/Nein je Punkt)

**V1 – Anordnung wie Suno (Clean-Room, Trendo-Stil):**
- Schmale Navigation links in Trendo Musik: Erstellen, Studio, Bibliothek, Stimmen, Ideen.
- Drei Spalten: Erstellen | Arbeitsbereich | Lied.
- Die Abspielleiste läuft unten über die ganze Breite (Welle, Like, Lautstärke). Takt, Tempo und Tonart wandern in diese Leiste.

**V2 – Erstellen-Spalte wie Suno:**
- Reiter „Lieder | Sprache“ und Umschalter Einfach/Erweitert.
- Modellwahl oben rechts: Schnell (Turbo), Gründlich (Grundmodell), Max.
- Knöpfe „+ Audio“, „+ Stimme“, „+ Bild“. Ein Bild wird von der lokalen Bild-KI oder von Claude in Stimmung und Stil übersetzt.
- Ein Würfel für Liedideen.

**V3 – „Mehr Optionen“ im Erweitert-Modus:**
- Stile ausschließen, Stimme weiblich/männlich, Dauer-Schieberegler.
- Max-Modus (Grundmodell, mehr Schritte, länger).
- Schieberegler Verrücktheit, Stil-Treue, Audio-Treue und Vielfalt, abgebildet auf ACE-Step-Parameter (Zufall, Führungsstärke, Referenzstärke). Je Regler wird gemessen, ob er wirkt; was nicht wirkt, kommt nicht in die Oberfläche.

**V4 – Bibliothek wie Suno:**
- Arbeitsbereiche, Playlists und Alben.
- Filter: Favoriten, eigene Aufnahmen, mit Stimme, Instrumental, Sprache. Sortierung, Listen- und Rasteransicht, Seiten.

**V5 – Remix-/Bearbeiten-Menü je Lied** (Zeilen-„…“ und Knopf „Remix“):
- Verlängern, Cover, Abschnitt ersetzen, Abschnitt entfernen, Zuschneiden.
- Remaster mit Variation, Beschreibung wiederverwenden.
- Spuren holen, MIDI holen, als Persona merken, Mashup, als Sample.
- Teil per Sprache ändern („Refrain energiegeladener“).

**V6 – Liedtext-Editor wie Suno:**
- Vollbild, Abschnitts-Etiketten, Autosave.
- „Texter“ aus eigenen Beispieltexten (lokal gespeicherter Schreibstil).
- Markieren → Reim- oder Zeilenvarianten; Änderungswünsche in eigenen Worten.
- Einzelne Zeile im fertigen Lied neu singen lassen. Das kommt nur, wenn die Messung zeigt, dass der Rest unverändert bleibt.

**V7 – Sounds:** One-Shot und Loop mit Tonart und BPM. Loops werden taktgenau geschnitten und lassen sich in den Beat-Modus oder ins Studio legen.

**V8 – Sprache:** Text sprechen (Piper, lokal) mit passender Musik darunter, erzeugt und mit automatischem Absenken der Musik gemischt (Hörbuch, Rede, Gute-Nacht-Geschichte).

**V9 – Spuren wie Suno:**
- 6 Spuren: zusätzlich Gitarre und Klavier (Demucs 6s, MIT).
- „Aus der Mischung holen“ (ein Instrument + Rest).
- „MIDI holen“ je Spur.
- Ehrlich: Sunos Trennung in fast 100 Instrumente gibt es lokal nicht.

**V10 – Mashup und Sample:**
- Mashup: Zwei Lieder in Tonart und Tempo angleichen (Tempo/Tonhöhe mit Messung) und Gesang A über Begleitung B legen.
- Sample: Ein Ausschnitt dient als Startpunkt für ein neues Lied (ACE-Step Audio zu Audio).

**V11 – Stimmen und Personas zusammenführen:** Unter „Stimmen“ stehen eigene Stimmen (Applio, mit Einwilligung) und Personas („Klang dieses Lieds merken“). Bekannte Künstler bleiben gesperrt.

**V12 – Eigenes Modell:** ACE-Step mit mindestens 6 eigenen Liedern nachtrainieren (LoRA), nur mit Rechte-Bestätigung. Zuerst wird gemessen, ob das auf 12 GB geht; geht es nicht, sagt Trendo das ehrlich.

**V13 – Mein Geschmack:** Trendo lernt lokal aus Favoriten, Bewertungen und Wiederholungen (Genres, Tempo, Stimmung) und nutzt das beim Würfel und bei Vorschlägen. Abschaltbar, die Daten bleiben auf dem PC.

**V14 – Studio wie Suno Studio** (baut auf dem Mischpult auf, das schon Busse, Automation und Plugins hat):
- Zeitleiste mit Take-Spuren (beide Fassungen untereinander) und Comping.
- Clip-Einstellungen: Lautstärke, Tonhöhe in Halbton und Cent, Loop, Rückwärts, Stretch. Dazu Fade-Kurven und Warp-Marker.
- Abschnitte an der Welle umstellen. MIDI-Spur mit Synth und Computer-Tastatur.
- Chat-Leiste (Claude oder lokale KI) für Befehle.
- Export: ganzes Lied, Bereich, Mehrspur.

**V15 – Cover-Art:** Bild oder kurzes Video (Wan 2.2) und per Text weiter verfeinern.

**V16 – Kurzvideo (Hooks):** eigener Clip plus Liedausschnitt im Hochformat 9:16, mit mitlaufendem Text.

**V17 – Tägliche Liedidee:** eine Idee am Tag auf der Startseite von Trendo Musik, dazu ein Kalender der eigenen Lieder.

Nicht vorgeschlagen:
- Community, Feed, Kommentare, Credits und Abos – dafür braucht es einen Server, und das passt nicht zu „alles lokal“.
- Advanced Split mit fast 100 Instrumenten – kein freies lokales Modell.
