# Gegenprobe der Schaltungssimulation mit SpiceSharp – 08.11.2026

Studio „Schaltungen berechnen“ (SPEC-02). SpiceSharp 3.2.3 (MIT) rechnet dieselben Schaltungen wie Trendos eigene Simulation, mit denselben Bauteilwerten und bei 300 K. Das Paket steckt nur im Testprojekt und wird nicht mit Trendo ausgeliefert.

## Ergebnis
Trendos Simulation stimmt in allen Fällen mit SpiceSharp überein.

| Schaltung | Fälle | größte Abweichung |
|---|---|---|
| Spannungsteiler | 2 | unter 0,001 mV |
| Diode mit Widerstand, auch in Sperrrichtung | 5 | 0,022 mV |
| LED mit Vorwiderstand | 2 | 0,059 mV |
| NPN (gesättigt und im Verstärkerbereich) | 4 | 0,021 mV |
| PNP | 2 | 0,020 mV |
| Frequenzgang RC-Tiefpass, 61 Frequenzen | 1 | unter 0,000001 dB und 0,000001° |
| Frequenzgang RLC-Schwingkreis, 61 Frequenzen | 1 | unter 0,000001 dB und 0,000001° |
| Frequenzgang Emitterschaltung, 61 Frequenzen | 1 | 0,00027 dB und 0,0009° |

Fehler in der eigenen Simulation hat die Gegenprobe nicht gefunden, geändert wurde dort nichts.

Die Tests bleiben als `SpiceCrossCheckTests` im Testprojekt und fallen künftig auf, wenn sich die Simulation verschlechtert.

## Nicht geprüft
- Operationsverstärker: Trendo rechnet ihn als vereinfachtes Modell, das SpiceSharp nicht eins zu eins hat.
- Zeitverläufe (Einschalten, Gleichrichter).
- Vergleich mit echten Bauteilen: Beide Programme rechnen mit denselben Modellen, die Gegenprobe zeigt also richtige Rechnung, nicht reale Bauteilstreuung.
