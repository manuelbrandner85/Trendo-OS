# Beleg S3-16 und S4-18 – Untertitel SRT/WebVTT, Sprachfassungen, LRC (07.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~TimedTextTests` mit `TRENDO_SUBS_BELEG=<temp>/trendo-subsbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     4, übersprungen:     0, gesamt:     4, Dauer: 371 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoSchnittPlus_Ui` → Failed 0 (Sprachfassung mit falscher Zeilenzahl abgelehnt, richtige übernommen; nach dem Export liegen .srt, .vtt, .it.srt und .it.vtt neben dem Video)

## Drei Sprachen im Video (Positiv S3-16-P)
```
ffprobe Untertitelspuren:
subrip|deu
subrip|ita
subrip|eng

italienische Spur aus dem MKV zurückgelesen:
1
00:00:00,500 --> 00:00:02,250
Buongiorno, Erika.

2
00:00:02,500 --> 00:00:04,000
Andiamo al mare.

3
00:00:04,200 --> 00:00:06,000
Vieni con noi?

```

## LRC (Positiv S4-18-P)
```
[ti:Sommerlied]
[la:de]
[length:00:06]
[00:00.50]Guten Morgen, Erika.
[00:02.50]Wir fahren ans Meer.
[00:04.20]Kommst du mit?
[00:06.00]
```

## Negativfälle
- Übersetzung mit anderer Zeilenzahl → „nicht synchron“, keine Datei; überlappende, rückwärts laufende, leere oder über das Ende hinausgehende Zeiten werden gemeldet und nicht geschrieben (Test `Rejects_bad_timing`).
- S3-16-N (Stimmenklon ohne Rechte): Sperre greift über die Audio-Freigabe – KI-Stimme ohne Einwilligung blockiert (Beleg `s4-20-audio-pruefung-lauf.md`). Eine Synchronfassung mit Stimmen baut Trendo nicht.
- S4-18-N (Liedtext ohne Rechte): Liedtexte kommen nur von der Person selbst (Regel), Timing-Fehler werden gemeldet.

## Dateien (SHA-256)
```
e4a907fa11fff1ba8a30003337749dcbc18bbc17735e44b877567e7109b0b608 *film.de.vtt
9dd8e83baa5db06fbc920729921d0d6860d32417245689f8c1c5211100934513 *film.it.vtt
21bc645b9dd2f05720f984f7451257dd9edd69f94b06b02c67e5d7e736cf191d *film.en.srt
433c928fe4fbaad8d23e8066bcfe13785c0ee491c0cecc8be77b2220eae7d787 *film-untertitel.mkv
970107e8996405e5683a3a81b395ae692978e212fde2fba86ff1960cd10382ab *lied.de.lrc
```

## Nicht geprüft
- Übersetzung durch Claude direkt aus dem Videoschnitt (Text wird eingefügt; Claude kann ihn im Programm füllen)
- LRC-Knopf in Trendo Musik: Zeiten kommen aus der Liedanalyse (Musikvideo); der Export als Knopf fehlt noch
