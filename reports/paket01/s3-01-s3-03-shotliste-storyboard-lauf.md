# Beleg S3-01 und S3-03 – Drehbuch → Shotliste, Storyboard-Tafeln (07.11.2026)

- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~ShotPlanTests` mit `TRENDO_SHOT_BELEG=<temp>/trendo-shotbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 305 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoShotPlan_Ui` → Failed 0

## Shotliste (CSV aus dem Testdrehbuch)
```
ID;Szene;Innen/Außen;Ort;Tageszeit;Einstellung;Beschreibung;Dauer (s)
SZ01-E01;SZ01;Innen;KÜCHE;MORGEN;Totale;Erika betritt die Küche;4
SZ01-E02;SZ01;Innen;KÜCHE;MORGEN;Nah;Kaffee läuft in die Tasse;3
SZ01-E03;SZ01;Innen;KÜCHE;MORGEN;Detail;Ihre Hand greift nach der Tasse;3
SZ02-E01;SZ02;Außen;STRASSE;TAG;Halbtotale;Erika geht los;5
SZ02-E02;SZ02;Außen;STRASSE;TAG;Fahrt;Kamera folgt ihr bis zur Ecke;6.5
```

- Positiv S3-01-P: Szenenköpfe (Nummer, Innen/Außen, Ort, Tageszeit) und Einstellungen (Größe, Beschreibung, Dauer) werden zu Codes SZ01, SZ01-E01 …; CSV für Drehplan und Tabellen.
- Negativ S3-01-N: Szene 2 nach Szene 3, doppelte Nummern, Einstellung vor dem ersten Szenenkopf, Szene ohne Einstellung und unbekannte Einstellungsgröße werden gemeldet – die Reihenfolge bleibt wie geschrieben.
- Positiv S3-03-P: Je Einstellung eine Tafel (Code, Einstellung, Ort, Beschreibung) in Plan-Länge, als Stück mit der Shot-ID als Titel in die Zeitleiste (echte Tafel: 4,00 s MP4).
- Negativ S3-03-N: Kann eine Tafel nicht erzeugt werden, steht das mit Grund im Fehlprotokoll; die übrigen Tafeln werden trotzdem übernommen.

## Dateien (SHA-256)
```
b4e0c3c0a404f9f2e2e0759c73cb08e78fab61cbe06533adc583f740a93100be *SZ01-E01.mp4
527236fbefe577bf3adeca924c076f6825c67739662592e2415cd43905f515b8 *tafel.png
```

## Nicht geprüft
- Storyboard-Bilder mit der lokalen Bild-KI (statt Tafeln) – braucht das Bildmodell; die Tafeln sind der Platzhalter
- Drehbücher im Fountain- oder Final-Draft-Format (nur die einfache Schreibweise oben)
