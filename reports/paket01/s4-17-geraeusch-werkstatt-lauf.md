# Beleg S4-17 – Geräusch-Werkstatt und Bibliothek mit Lizenzprüfung (06.11.2026)

- Werkzeug: Trendo-eigene Klangerzeugung (Rauschen, Filter, Sinus), keine fremden Samples
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~FoleyTests` mit `TRENDO_FOLEY_BELEG=<temp>/trendo-foleybeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 467 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioFoley_Ui` → Failed 0 (Schritte erzeugen, mit Tag „Schritt.Kies“ in die Bibliothek; Fremdgeräusch ohne Lizenz im Paket ausgeschlossen und genannt)

## Messung (Realismus-Prüfung)
```
Spektraler Schwerpunkt: Regen 6985 Hz, Wind 708 Hz, Maschine 591 Hz
Schritte Kies (8 s): 16 Schläge erkannt
```
- Wind war zuerst zu hell (6 kHz) – auf drei Filterstufen umgebaut, jetzt dunkel wie echter Wind; Regen von 11,6 kHz auf rund 7 kHz gedämpft (Bereich echter Regenaufnahmen).

## Paket (Positiv S4-17-P und Negativ S4-17-N)
```
--- Lizenzen.csv
Datei;Name;Lizenz;Quelle;SHA-256
Regen leicht.wav;Regen leicht;selbst erzeugt (Trendo) – frei verwendbar;Trendo Geräusch-Werkstatt;fcc010b6d1445bf32f0428ce4e2050a78f7c9fdc5a404ccf8ebbf55b7d408d08
Schritte Kies.wav;Schritte Kies;selbst erzeugt (Trendo) – frei verwendbar;Trendo Geräusch-Werkstatt;dc0c4f410c8ea78d3f010cdba2ef1cb3eb44df9e8b6d8c78aae1ea2b2a8dc77f
--- Ereignisse.json
{
  "Wetter.Regen": [
    "Regen leicht.wav"
  ],
  "Schritt.Kies": [
    "Schritte Kies.wav"
  ]
}--- Ausgeschlossen.txt
Feuer aus dem Netz: Lizenz „unbekannt“ erlaubt keine Veröffentlichung
Wind: Datei wurde seit der Aufnahme verändert (Prüfsumme)
```

## Dateien (SHA-256)
```
fcc010b6d1445bf32f0428ce4e2050a78f7c9fdc5a404ccf8ebbf55b7d408d08 *Regen leicht.wav
dc0c4f410c8ea78d3f010cdba2ef1cb3eb44df9e8b6d8c78aae1ea2b2a8dc77f *Schritte Kies.wav
```

## Nicht geprüft
- Hörtest durch einen Menschen (nur Messung von Klangfarbe, Pegel und Einzelschlägen)
- Fahrzeuge und Umgebungen aus mehreren Schichten; Import großer Fremdbibliotheken
- Lizenzangaben werden geprüft wie angegeben, nicht online nachgeschlagen
