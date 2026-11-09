# Realismus in Fräsen, Statik, Wohnung und 3D-Modellen (Freigabe 09.11.2026)

## Was neu ist
- **Fräsen & Fertigen:** „Werkstück in 3D“ zeigt das Rohteil mit dem Abtrag bis zum aktuellen Simulationsschritt, dazu Volumen und Späne-Gewicht. „Fertiges Teil als Foto“ rendert das Teil mit Cycles.
- **Statik:** „3D-Ansicht“ zeigt die Stäbe als Körper in der Farbe des Werkstoffs. Die Biegung ist stark überhöht, Rot zeigt die größte Belastung.
- **Wohnung:** Uhrzeit und Monat ergeben im Foto einen echten Sonnenstand (Breitengrad 51°, Nordpfeil berücksichtigt). Die Wandbeläge Fliesen und Tapete je Raum erscheinen jetzt im Foto.
- **3D-Modelle:** neues Material „Aluminium gefräst“, „Metall gebürstet“ hat statt einer spiegelnden Textur feine Schleifspuren, neues Licht „produkt“.

## Behobene Fehler im Render-Skript
- Importierte Netze behielten die Farbe aus der Datei, das gewählte Material wirkte nicht.
- Der Boden wurde aus veralteten Matrizen berechnet, importierte Teile schwebten dadurch.

## Prüfung am echten Bild
| Bild | Ergebnis |
|---|---|
| `realismus-fraesteil.png` | Liest sich als gefrästes Aluminium und liegt auf. |
| `realismus-bad-juli-13uhr.png` | Fliesen laufen sauber über die Wände, Sonnenfleck auf dem Boden. |
| `realismus-wohnung-8uhr.png`, `realismus-wohnung-19uhr.png` | Der Schatten wandert von einer Seite auf die gegenüberliegende. |
| `realismus-statik-ui.png` | Rahmen als Körper in Trendo, 41-fach überhöht, Riegel rot. |

## Grenzen
- Das Fräsbild hat einen sichtbaren Horizont im Hintergrund, die Kanten sind nicht gefast.
- Die Materialien wirken in der Vorschauqualität (64 Samples) noch glatt, die Fliesen sehr gleichmäßig.
- Der Abtrag nimmt einen Schaftfräser an, Kugel- und Radienfräser sind nicht nachgebildet.
- Der Breitengrad ist fest auf 51° gesetzt.
- Die echten Bilder entstanden mit Prüf-Kopien der Skripte in einem sichtbaren Blender-Fenster. Der Knopf in Trendo wurde nur mit nachgestelltem Blender getestet.
