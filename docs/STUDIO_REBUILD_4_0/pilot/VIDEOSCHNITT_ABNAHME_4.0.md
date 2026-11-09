# Abnahme Pilot 4.0: Videoschnitt

Stand 09.11.2026. Sammelzweig `claude/T-0603-sammel`.

| Punkt | Inhalt | Ergebnis | Nachweis |
|---|---|---|---|
| Sicherung | Altes Projektformat lädt, Spuren bleiben, Export identisch | erfüllt | `VideoBaselineTests`, Referenz in `tests/Trendo.Core.Tests/Pilot40/` |
| V1 | Flüssige Vorschau mit Ton | erfüllt die geprüften Punkte | `PreviewStreamTests`, `VideoPreview_Ui` |
| V2 | Beliebig viele Bild- und Tonspuren | erfüllt die geprüften Punkte | `VideoLanesTests`, `VideoLanes_Ui` |
| V3 | Framegenau, Zeitcode | erfüllt die geprüften Punkte | `VideoLanesTests`, `VideoLanes_Ui` |
| V4 | Einrasten, Ripple, Slip, Rolling | erfüllt die geprüften Punkte | `VideoLanesTests`, `VideoLanes_Ui` |
| V5 | Keyframes | erfüllt die geprüften Punkte, echter ffmpeg-Lauf | `VideoAnimTests`, `VideoPilotPanels_Ui` |
| V6 | Effektstapel | erfüllt die geprüften Punkte, echter ffmpeg-Lauf | `VideoAnimTests`, `VideoPilotPanels_Ui` |
| V7 | Farbräder; Waveform und Vektorskop waren schon vorhanden | erfüllt die geprüften Punkte | `VideoLanesTests`, `VideoPilotPanels_Ui` |
| V8 | Mischpult | erfüllt die geprüften Punkte | `VideoLanesTests`, `VideoPilotPanels_Ui` |
| V9 | Rechtsklick, Höhe der Zeitleiste | teilweise: Seitenleiste der Programm-Hülle noch nicht ziehbar | `VideoPilotPanels_Ui` |
| V10 | Mindestens 20 eigene Testfälle | erfüllt: 37 neue Testfälle | siehe oben |

## Messung

1080p-Testvideo auf freiem PC in Vorschaugröße 640 × 360: 240 Bilder in 0,34 s, also rund 23-mal schneller als Echtzeit. Unter Volllast des Volltests wurden 15 Bilder pro Sekunde gemessen.

## Volltest

Sammelzweig ohne die spätere Studio-Ordnung: Api 16 bestanden; App 939 bestanden, 4 übersprungen; Paket-Tests 9 bestanden. Core im Volltest: 2143 bestanden, 5 übersprungen, 1 Fehler. Der Fehler (`T-0241`, Erweiterungen installieren) war eine Dateisperre von Windows beim Umbenennen eines Ordners und liegt nicht im Videoschnitt. Einzeln grün, Core danach komplett: 2144 bestanden, 5 übersprungen.

## Nicht geprüft

- Am echten Bildschirm mit hörbarem Ton. Die Tests laufen ohne Fenster und mit nachgestellter Tonausgabe.
- Einblendungen, Überblendungen, Keyframes und Effekte zeigt die flüssige Vorschau nicht, nur der Export.
- Vor- und Rücklauf in anderem Tempo nutzen weiter Einzelbilder.
- Andock-Panels: warten auf die Entscheidung zum Umbau der Programm-Hülle.
- Eine echte alte Nutzerdatei; die Referenz ist eine von Hand nachgebildete Projektdatei im alten Format.
- Keine Abnahme durch eine Person.
