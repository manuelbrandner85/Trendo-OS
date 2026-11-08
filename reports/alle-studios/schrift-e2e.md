# Gut lesbare Schrift – 08.11.2026

Alle Studios, Runde 10, SYS-12 „Schrift & Farben anpassen“.

**Atkinson Hyperlegible** (Braille Institute of America, SIL OFL 1.1). Zwei Dateien (Normal und Fett, 110 KB) liegen unverändert im Setup, der Lizenztext daneben. Die Schrift wurde für Menschen mit Sehschwäche entworfen: leicht verwechselbare Zeichen wie `I`, `l` und `1` sehen klar verschieden aus.

## Was die Nutzerin oder der Nutzer sieht
„Einstellungen → Sehen & Bedienhilfen“ → Bereich **„Gut lesbare Schrift“**: Standard (Inter) oder Atkinson Hyperlegible. Die Wahl gilt sofort für die ganze Oberfläche und bleibt gespeichert. Festbreiten-Schriften in Code-Ansichten bleiben unverändert.

## Geprüft
- **Gerendert** über `tools/Trendo.Render` (`TRENDO_READFONT`): Bild `schrift-atkinson.png`. Die Schrift ist aktiv, `I`, `l` und `1` sind klar verschieden, Unterlängen vollständig, das Layout bleibt intakt.
- **`ReadFont_Ui`:** Wahl wird gespeichert und angewandt, zurück zum Standard geht, unbekannte Werte werden ignoriert.

## Verworfen: OpenDyslexic
Zunächst ebenfalls ausgewählt (OFL-1.1). Die Sichtprüfung zeigte:
- Unterlängen werden abgeschnitten, aus „g“ wird fast „a“ („Unabhänaia“).
- Der Text läuft so breit, dass die Seitenleiste abschneidet.

Eine Schrift für bessere Lesbarkeit, die Buchstaben verstümmelt, schadet mehr, als sie nützt; ihr Nutzen ist in Studien ohnehin umstritten. Sie ist nicht im Setup.

## Nicht geprüft (NOT RUN)
- Rückmeldung von Menschen mit Sehschwäche.
- Alle 56 Studios einzeln mit dieser Schrift.
