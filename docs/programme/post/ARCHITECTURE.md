# E-Mail & Kontakte – Architektur

Studio: E-Mails & Termine (SYS-09) · Bereich: Arbeiten & Büro · Seite: `post` · Stand: 0.9.28 (Stand ab682fb)

## Präsentation (Trendo.App)
- `src/Trendo.App/MainWindow.Pim.cs`

## Bedienelemente der Arbeitsfläche
- Texteditor

## Fach-Engine (Trendo.Core)
- `CalEvent`
- `MailDraft`
- `ImapSession`
- `MailCenter`
- `MailDates`
- `Repeat`
- `TodoItem`
- `MailAccount`
- `MailRule`

## Dateiformate (im Code belegt)
- .eml
- .ics
- .jpg
- .json
- .mp3
- .mp4
- .ogg
- .png
- .txt
- .wav
- .webm

## Externe Programme und Dienste
- FFmpeg
- VLC (optional)
- mpv (optional)

## Bausteine (Masterprompt 131 / 5)
Eigene Arbeitsseite ohne die gemeinsame Programm-Hülle – Dokumentmodell, Rückgängig und Bedienstufen sind hier nicht einheitlich.
