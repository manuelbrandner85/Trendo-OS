# Bericht T-0611: Etappe „Kreativ & KI“ – Teil 2

**Zweig:** `claude/T-0611-kreativ-ki-2` (Worktree `studios-aufraeumen`), Basis `main` 7f0f9e3fa. Nicht auf `main`.

## Inhalt

- **Bücher schreiben** (STUDIO-BOOK-01): Manuskript in Kapiteln („# Titel“), Kapitelübersicht mit Wortzahl, Wortziel mit Fortschritt, E-Book im offenen EPUB-3-Format (mimetype zuerst und unkomprimiert, Inhaltsverzeichnis, ein XHTML je Kapitel, **fett**/*kursiv*), Lektorat und Weiterschreiben über Claude mit dem Zugang der Person.
- **Animation & Motion** (STUDIO-ANIM-01): Bühne 1280 × 720, Ebenen (Rechteck, Kreis, Text), Schlüsselbilder für Position, Größe, Drehung und Deckkraft, fünf Übergänge, fertige Bewegungen (einblenden, einfliegen, pulsieren, drehen), Abspielen und Zeitregler, Export als animiertes SVG und als Video (MP4 über FFmpeg).
- **Fotos verwalten** (STUDIO-PHOTOS-01): baut auf dem vorhandenen Fotoalbum und dem EXIF-Datum auf. Einsortieren nach Jahr/Monat, Jahr oder Tag, wahlweise nach Datum umbenennen, Kopieren oder Verschieben; Probelauf zuerst, ausgeführt erst nach zweitem Klick; Doppelte und bereits einsortierte Fotos werden übersprungen; nichts wird überschrieben; Rückgängig.
- **Bilder mit KI** (STUDIO-IMAGEAI-01): nutzt die Bild-KI der KI-Bibliothek (stable-diffusion.cpp mit SDXL oder SD Turbo, wie die Musik-Cover). Stile, Formate, 1–4 Varianten mit wiederholbarem Startwert, Galerie, Herkunftsnachweis je Bild (KI-erzeugt, Modell, Beschreibung, Startwert, SHA-256), Export als PNG oder ZIP. Sperre für anstößige Beschreibungen.
- Gemeinsames Textfeld der Kreativ-Studios: übernimmt nur noch Getipptes. Vorher konnte ein altes Feld nach einem Befehl (z. B. „Neues Kapitel“) beim Neuzeichnen den neuen Stand überschreiben – betraf auch Teil 1.
- Studio-Bilder lokal mit der vorhandenen SDXL-Turbo-KI erzeugt (ComfyUI ist nicht mehr installiert); Beschreibungen in `tools/studio_bilder.py`.

## Unabhängige Prüfung

Behoben:
- hoch: Ein zweiter Kopierlauf legte alles noch einmal als „(2)“ an – jetzt wird gleicher Inhalt erkannt und übersprungen.
- hoch: Verschieben über das bestehende Umbenennen arbeitete mit Zwischennamen; bei einem Fehler (z. B. volle Zielplatte) blieben Fotos unter „.trendo-…“ liegen, ohne Rückgängig. Jetzt eigener Ablauf ohne Zwischennamen, Protokoll auch bei Abbruch, halbe Kopie wird gelöscht.
- mittel: „***fett-kursiv***“ und verschachtelte Sternchen ergaben ungültiges XHTML im E-Book – jetzt gültig, im Zweifel ohne Hervorhebung.
- mittel: Rückgängig beim Kopieren verglich nur die Größe – jetzt SHA-256; geänderte Dateien bleiben.
- mittel: Eine fremde Projektdatei konnte beliebige Dateien in den Bild-Export ziehen – jetzt nur eigene PNG im Bildordner des Dokuments, höchstens 25 MB. Rückgängig-Protokolle nur aus dem eigenen Ordner.
- niedrig: Startwert −2147483648 brach still ab; Sperrliste erkannte gebeugte Formen nicht; Netzwerkpfade „//rechner“ als Ziel; Video-Ausgabe hielt die Oberfläche zu lange an; SVG-Zeitpunkte ab etwa 33 s ungenau.

## Testergebnis

Volltest `dotnet test Trendo.sln -c Release` und Paket-Tests, Exitcode 0:
Api 16 bestanden; Core 2229 bestanden, 5 übersprungen; App 1220 bestanden, 4 übersprungen; Paket-Tests 11 OK. Auszug in `reports/volltest-T-0611/`.

Vorher ein Lauf mit 2 Fehlern: Studio-Bilder fehlten noch (entstanden während des Laufs) und zwei Claude-Profile hatten kein Lieferformat – beides behoben.

## Nicht geprüft

- Bild-KI-Studio mit echter Grafikkarte über die Oberfläche (die Studio-Bilder liefen mit demselben Programm und Modell direkt).
- Video-Export mit echtem FFmpeg (im Test ersetzt).
- E-Book mit epubcheck und in Apple Books.
- Verschieben auf eine echte zweite Platte.

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
