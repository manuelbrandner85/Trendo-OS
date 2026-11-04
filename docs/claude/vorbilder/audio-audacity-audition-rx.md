# Vorbild-Analyse Audio: Audacity, Adobe Audition, iZotope RX (Clean-Room)

- Programme: „Audio bearbeiten“
- Vorbilder: Audacity (frei), Adobe Audition (kostenpflichtig), iZotope RX (kostenpflichtig), dazu Ocenaudio und Descript (Funktionsumfang aus öffentlicher Doku)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 04.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

- ✓ Wellenform mit Zeitlineal, Auswahl mit der Maus, Marke, Abspielmarke (Audacity)
- ✓ Schneiden, Kopieren, Einfügen, Löschen, Zuschneiden, Duplizieren, Stille einfügen
- ✓ Lauter/Leiser, Normalisieren, Ein-/Ausblenden, Tiefen/Höhen weg, Mono, Umkehren, Tempo
- ✓ Effekte Echo und Hall, Rauschsperre
- ✓ Rauschen nach Profil entfernen: Profil aus einer stillen Stelle merken, dann Spektral-Abzug (Audacity Noise Reduction, RX Spectral De-noise)
- ✓ Brummen weg 50 Hz mit Oberwellen (RX De-hum)
- ✓ Stille kürzen: lange Pausen auf eine feste Länge (Audacity Truncate Silence, Descript)
- ✓ Lautheit nach EBU R128 / BS.1770: Sprache −16 LUFS, Musik −14 LUFS, Spitzen auf −1 dBFS (Audition Match Loudness); Messung mit FFmpeg ebur128 abgeglichen (±0,5 LU)
- ✓ Kompressor (Audition Dynamics)
- ✓ Spektrogramm-Ansicht mit Auswahl darüber (RX Spectral Editor, Audacity Spektrogramm)
- ✓ Aufnahme vom Mikrofon an der Marke (Audacity Record)
- ✓ Schritte-Liste mit Rückgängig, Original bleibt unverändert (nicht-destruktiv wie Audition)
- ✓ Export WAV und MP3
- ◐ Spektrales Malen/Retuschieren einzelner Frequenzbereiche (RX) – nur Anzeige, Bearbeitung auf die Zeitauswahl
- ✗ Mehrspur-Ansicht (Audition Multitrack) – dafür gibt es den Videoschnitt mit Tonspuren
- ✗ Plugins (VST/LV2) – bewusst nicht, keine fremden Programme laden

## Bedienkonzept (Profi-Bauplan A1–A3)
Menüband mit Gruppen (Bearbeiten, Effekte, Reparieren, Ansicht), Einstufen Einfach/Erweitert/Profi, Wellenform in der Mitte, Transportleiste darunter, Schritte rechts.

## Nächste Schritte
1. Spektrales Retuschieren (Frequenzbereich in der Auswahl absenken)
2. Klick- und Knackser-Entfernung (RX De-click)
