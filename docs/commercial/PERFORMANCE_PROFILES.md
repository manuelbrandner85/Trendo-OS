# Leistungsprofile

Stand 13.11.2026. Der Code steht in `src/Trendo.Core/Compat/HardwareCompat.cs` (`PerformanceProfiles.Specs`), das Vorab-Skript in `installer/systemcheck.ps1`. Ein Test hält beide gleich.

> **Vorläufig.** Die Grenzwerte sind eine begründete Einstufung, aber **keine Messwerte**. Bisher lief Trendo nur auf einem Entwicklungs-PC (Ryzen 7 5700X, 32 GB RAM, RTX 5070 mit 12 GB, Windows 11). Sie dürfen erst als offizielle Systemanforderungen veröffentlicht werden, wenn die Messreihe unten abgeschlossen ist.

| Profil | Wofür | Kerne (logisch) | RAM | Grafikspeicher | freier Platz (Empfehlung) |
|---|---|---|---|---|---|
| **Basic** | Desktop, Büro, einfache kreative Aufgaben | ab 4 | ab 8 GB | keine Vorgabe | ab 20 GB |
| **Standard** | normaler Betrieb der meisten Studios | ab 6 | ab 16 GB | ab 4 GB | ab 50 GB |
| **Professional** | 3D, Video, Spiele, lokale KI | ab 8 | ab 32 GB | ab 8 GB | ab 200 GB |

Regeln:
- Das Profil ist das höchste, dessen Werte für Kerne, RAM und Grafikspeicher alle erreicht sind. Bei RAM und Grafikspeicher gelten 6 % Spielraum, weil Geräte etwas weniger melden als verbaut ist.
- Freier Speicherplatz zählt nicht zum Profil. Ein voller Datenträger macht den PC nicht schwächer, im Systemcheck gibt es dafür eine eigene Warnung. Auslöser war der Entwicklungs-PC: Mit nur 21 GB frei wäre er sonst als „Basic“ eingestuft worden.
- Unbekannter Grafikspeicher zählt als 0.

## Eignung je Studio

`StudioNeeds` legt für jedes Studio fest:
- **Mindestprofil:** Das Studio läuft, eventuell langsam.
- **Empfohlenes Profil:** Das Studio läuft flüssig.
- **Zusatzprogramme**, die das Studio braucht.

Studios, die dort nicht stehen, brauchen nur Basic. Das Ergebnis je Studio ist eine von vier Stufen:

| Ergebnis | Bedeutung |
|---|---|
| Läuft | Profil und Programme reichen |
| Eingeschränkt | Mindestprofil erreicht, empfohlenes nicht. Dazu kommt ein Tipp mit der Alternative |
| Braucht Zusatzprogramm | es fehlt ein kostenloses Programm, Installation nach Zustimmung |
| Zu schwach | unter dem Mindestprofil. Das Studio **bleibt nutzbar** und wird nur gewarnt, dazu kommt eine Alternative |

Kein Studio wird wegen schwacher Hardware gesperrt oder entfernt.

## Leistungsanpassung (nach Zustimmung)

| Vorschlag | Auslöser | Stand |
|---|---|---|
| Kleines KI-Modell (Qwen2.5 0.5B) | unter 15 GB RAM | **umgesetzt**, Knopf „Übernehmen“ stellt das Modell um, das große bleibt installiert |
| Vorschau in geringerer Qualität | Profil Basic oder darunter | nur Hinweis, noch keine automatische Umstellung |
| Weniger Aufgaben gleichzeitig | unter 6 Kernen | nur Hinweis. `ParallelLimit` berechnet den Wert, ist aber noch nicht angebunden |
| Grafikbeschleunigung nicht nachgewiesen | keine Grafikkarte mit eigenem Speicher erkannt | nur Hinweis |

Die drei Hinweise stehen in der Oberfläche ausdrücklich als „Trendo stellt das noch nicht automatisch um“.

## Messreihe vor der Veröffentlichung (offen)

Für jedes Profil sind mindestens zwei echte Geräte nötig, eines an der Untergrenze und eines darüber. Zu prüfen sind:
1. Installation per Setup.exe auf frischem Windows 11
2. Startzeit
3. Bedienung (Leistungswächter `qual-leistung`)
4. je Studio die Hauptaufgabe mit Zeitmessung:
   - Briefe und Office
   - 3D-Vorschau
   - Videoexport 1080p
   - Antwortzeit der lokalen KI
   - Musik-Erzeugung
   - Unreal-Projekt öffnen
5. die Grafikkarten-Hersteller NVIDIA, AMD und Intel (integriert)

Die Ergebnisse kommen mit Rohlog und SHA-256 unter `reports/`. Erst danach wird `PerformanceProfiles.Provisional` auf `false` gesetzt.
