# Trendo Musik – echte Prüfung von Stimmtrennung und Stimm-Lernen (07.11.2026)

Bisher war das nur mit Testdaten geprüft. Jetzt liefen die echten KI-Programme auf diesem PC (RTX 5070, 12 GB). Sie wurden genau so eingerichtet, wie Trendo es tut (`KiPyApps`, gleiche Fassungen und Pakete), und mit Trendos eigenen Skripten aufgerufen. Gearbeitet wurde in einem Temp-Ordner, denn Installationen aus Claude heraus landen sonst im unsichtbaren App-Container.

## Stimmtrennung (Demucs 4.0.1, `trendo_spuren.py`) – `spuren/`

**Testlied mit bekannter Wahrheit:**
- Gesang: Piper „thorsten-high“, CC0.
- Begleitung: Bass, Akkorde und Schlagzeug, synthetisch erzeugt.
- Maß ist SI-SDR, also wie sauber eine Spur ist; je höher, desto besser.

| | Gesang | Begleitung | Dauer |
|---|---|---|---|
| ohne Trennung (Mischung) | −5,6 dB | 5,6 dB | – |
| schnell (htdemucs) | **14,5 dB** | 19,9 dB | 22 s* |
| genau (htdemucs_ft) | **16,1 dB** | 19,8 dB | 120 s* |

\* Beim ersten Mal ist das Laden des Modells mitgezählt. htdemucs_ft lädt einmalig etwa 320 MB.

**Echtes KI-Lied** (60 s, mit Trendo Musik/ACE-Step erzeugt):
- Trennung in 6 s (schnell) bzw. 10 s (genau).
- Gesang und Begleitung zusammen ergeben wieder das Original: Rekonstruktion 35,1 dB (schnell) bzw. 21,4 dB (genau).
- Hörproben liegen als `ki-lied-*-10s.mp3` bei.

## Stimm-Lernen und Singen (Applio, Stand 140404bd, `trendo_stimme.py`) – `stimme/`

- **Einrichten:**
  - Der Stand ist per Commit geprüft.
  - PyTorch 2.11.0+cu128 läuft, CUDA ist aktiv.
  - Die Grundmodelle sind beim ersten Lernen 1,9 GB groß.
- **Lernen:**
  - Daten: 24 Sätze (ca. 1,5 min) der freien Stimme „thorsten-high“. Der Thorsten-Voice-Datensatz ist CC0 und ausdrücklich für Sprachsynthese freigegeben, es ist keine Künstlerstimme.
  - 30 Durchgänge in 719 s.
  - Ergebnis: `thorsten_30e_270s.pth` und `thorsten.index`.
- **Singen:** 15 s KI-Gesang (aus dem getrennten KI-Lied) in der gelernten Stimme, −12 Halbtöne, Dauer 38 s.

| Messung (`messung.json`) | Wert |
|---|---|
| Länge | 15,00 s → 14,98 s |
| Melodie erhalten (Zusammenhang der Tonhöhe) | **0,84** |
| Tonhöhe | **−1200 Cent**, genau die gewählten −12 Halbtöne |
| Klangfarbe näher an der gelernten Stimme | **77 %** (Abstand 104,6 → 24,4) |

Hörprobe: `eingang-ki-gesang-15s.mp3` → `ausgang-gelernte-stimme-15s.mp3`

## Behobener Fehler

Python schrieb seine Meldungen in Trendo bisher im Windows-Zeichensatz cp1252, Trendo liest aber UTF-8. Aus „…“, „ä“ und „–“ wurde deshalb „�“, zum Beispiel bei „Modell laden: htdemucs …“. Jetzt setzt `KiPyApps.UvEnv` die Schalter `PYTHONUTF8=1` und `PYTHONIOENCODING=utf-8`. Der Test dazu steht in `KiPyAppsTests`.

## Nicht geprüft

- Die Einrichtung über den Knopf in Trendo auf diesem PC. Das muss der Nutzer selbst anklicken, weil Installationen aus Claude im App-Container landen.
- Eine echte Mikrofon-Aufnahme der eigenen Stimme.
- Lernen mit weniger als 1,5 min oder mehr als 30 Durchgängen.
- Ein Hörurteil: Gemessen sind nur die Werte oben.
