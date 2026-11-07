# Beleg S3-17 – Abspann mit Keyframes und Schrift-Lizenzprüfung (07.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~CreditsTests` mit `TRENDO_CREDITS_BELEG=<temp>/trendo-creditsbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 648 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoCredits_Ui` → Failed 0 (Abspann aus „Rolle: Name“ als Clip ans Ende, rückgängig machbar; Schrift mit Einbett-Verbot abgewiesen, nichts gerendert)

## Messung am fertigen Clip (6 s, 640×360, rollend)
```
Helligkeit (YMAX) bei 0,8 s: unten 251, oben 16
Helligkeit bei 4,8 s: oben 253, unten 17
Schrift: arial.ttf → Systemschrift – zum Rendern ins Video erlaubt (wird nicht weitergegeben).
```

- Positiv S3-17-P: Keyframes (Zeit, Lage, Deckkraft) werden linear dazwischen gerechnet und als FFmpeg-Ausdruck gesetzt; der Text steht am Anfang unten und am Ende oben (Helligkeit gemessen). Satz wie im Kino: Titel groß, Rolle klein und grau, Namen mittelgroß, jede Zeile zentriert.
- Negativ S3-17-N: Schrift mit Einbett-Verbot → rot, kein Abspann; Schrift ohne Lizenzangabe → gelb „vor der Veröffentlichung prüfen“; OFL-Schrift → grün; keine Schriftdatei → rot.

## Dateien (SHA-256)
```
3b890073c6458664c6b76049880379bec69a1f551f7494feb5f2018d570993f6 *abspann.mp4
c66afc93579fff8e8a84e594bacbce9b5fcd60368df6ddfac723ebdabdd50fbf *bild-anfang.png
b7182f22e5d3a5e4d5981be9b70a98752d3145d1cf3118d4aff0a8b4e48c9f72 *bild-ende.png
fee829cc6acf7e27ee48ac964bc05fc5968cba322e32fb4817b2d2d6f36f884a *bild-mitte.png
4beb8f186d8077c3517ef8666f76ee02058f7b5af05593140fb0882aedf61c7b *abspann.txt
bad8cd66742bfe6b3c6b29ea295de3c07780b4a63e316e0aeaf345436ae12d36 *messung.txt
```

## Nicht geprüft
- Keyframes in der Oberfläche einzeln verschieben (Vorlage „rollend“ mit vier Keyframes; im Kern frei änderbar)
- Animierte Titel mitten im Film und Logos/Infografiken (Titel je Stück gibt es schon)
