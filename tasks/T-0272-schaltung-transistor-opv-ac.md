# T-0272 – Schaltung: Transistor, Operationsverstärker, Frequenzgang

- Freigabe: Manuel im Chat am 31.10.2026 („Erst offene Punkte“)
- Umgesetzt: Dreipole mit drittem Anschluss aus der gezogenen Linie (`Part.Pin3`). NPN/PNP nach Ebers-Moll (Newton mit Spannungsbegrenzung, Kollektorstrom und Uce als Messwerte), Operationsverstärker als gesteuerte Quelle mit Sättigung an ±Versorgung (tanh), SPICE-Export (Q-Modelle, B-Quelle), DIN-Schaltzeichen, Werkzeugleiste, Eigenschaften (β, Versorgung). Frequenzgang (`AcAnalysis`): Arbeitspunkt, komplexe Knotenanalyse 10 Hz…100 kHz, Bode-Diagramm mit −3-dB-Grenze in den Eigenschaften. Neue Vorlagen „Transistor als Schalter“ und „OPV-Verstärker“.
- Tests: Core 4 (RC-Tiefpass fg = 1/(2πRC), −20 dB/Dekade, −45°; OPV ×10 und Begrenzung; NPN Ic = β·Ib und Sättigung; PNP), App 1 (Vorlagen, LED-Strom, Bode an der Oberfläche)
- Offen: Platinen-Layout (PCB), MOSFET, Logik-ICs
