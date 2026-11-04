# Trendo Musik – Vorbilder und was davon lokal machbar ist (Stand 04.11.2026)

Recherchiert: Suno (v5/v5.5), Udio, ElevenLabs Music v2, Google Lyria 3 / ProducerAI, Stable Audio, AIVA, Soundraw, Beatoven, Mubert,
Moises, LANDR, Synthesizer V, ACE Studio und das quelloffene ACE-Step 1.5 (läuft in Trendo). Nachgebaut werden nur Funktionen, keine Inhalte oder Klänge.

## Echt gemessen auf dieser Maschine (RTX 5070, 12 GB)

| Funktion (Vorbild) | ACE-Step-Weg | Ergebnis |
|---|---|---|
| Cover mit gleicher Melodie (Suno Cover, Udio Remix) | `cover`, Nähe 0,90 | Tempo, Tonart, Takt, Länge gleich; Melodie auf der Gesangsspur 55 % (Zufall abgezogen) – grün |
| Cover mit Nähe 0,70 | `cover` | zu wenig Gesang für ein Melodie-Urteil → Standard-Nähe erhöht |
| Klang-Referenz (Suno „Inspo“) | `text2music` + `reference_audio` | wirkt: Klangabstand zur Referenz im Mittel 101 statt 139 ohne Referenz (schwankt je Lauf) |
| Gesang herauslösen (ACE `extract`) | Grundmodell | unbrauchbar (Ergebnis ≈ ganzes Lied) – Trendo nutzt weiter Demucs |
| Instrument dazu (Suno Studio, ACE `lego`) | Grundmodell, Streicher | folgt den Akkorden nicht (0,40 = Zufall) – nicht eingebaut |
| Begleitung zum Gesang (Suno „Add Instrumental“, ACE `complete`) | Grundmodell | folgt dem Gesang nicht (0,06) – nicht eingebaut |

Die Mehrspur-Aufgaben (extract/lego/complete) brauchen laut ACE-Step das XL-Grundmodell mit mindestens 24 GB Grafikspeicher.

## Machbar und noch nicht in Trendo (Reihenfolge des Einbaus)

1. Klang-Referenz und „Persona“ (gleicher Klang wie eines meiner Lieder) – Suno Inspo/Persona.
2. Ablaufplan je Abschnitt (Länge, Stimmung, Stil je Teil) – ElevenLabs „composition plan“, Soundraw/Beatoven-Abschnitte.
3. Üben wie Moises: Tempo ohne Tonhöhe, Tonart ±, Klick im Takt, Akkorde im Takt, Kapodaster-Vorschlag.
4. Mastering wie LANDR: Stile (warm, ausgewogen, offen), Lautheit je Plattform, Mastern nach Referenz.
5. MIDI-Export von Melodie und Akkorden (AIVA).
6. Loops mit Tempo, nahtlos (Stable Audio).
7. Kurzclip für soziale Netzwerke aus dem Refrain (Suno Hooks).

## Später / nicht lokal

- Eigenes Stil-Modell aus eigenen Liedern (Suno v5.5 Custom Model) – ACE-Step-LoRA geht laut Projekt mit 12 GB, braucht aber eine eigene Vorbereitungskette.
- Gesangssynthese aus Noten und Text (Synthesizer V, ACE Studio) – kein offenes Modell in brauchbarer Qualität geprüft.
- Endlos-Stream (Mubert) – wenig Nutzen.

Quellen: github.com/ace-step/ACE-Step-1.5 (README, docs/en/API.md), suno.com/release-notes, elevenlabs.io/docs (Music), moises.ai, Testberichte zu LANDR, Udio, Stable Audio, AIVA, Soundraw.
