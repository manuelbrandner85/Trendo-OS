# T-0275 – Tragwerk: Nachweis nach Eurocode (Vorbemessung)

- Freigabe: Manuel im Chat am 31.10.2026 („Erst die Restpunkte“)
- Umgesetzt: Lastart je Last (ständig G / veränderlich Q), Grundkombination 1,35·G + 1,5·Q (Eigengewicht und Setzung als G), Durchbiegung charakteristisch ≤ L/300. Stahl/Edelstahl/Aluminium vereinfacht nach EC 3 (f_y/γM0, Biegeknicken Knicklinie b mit γM1 = 1,1), Holz C24 nach EC 5 (k_mod 0,8, γM 1,3, Knickbeiwert k_c), lineare Interaktion N + M (auf der sicheren Seite), Beton ehrlich abgelehnt (Bewehrung nötig). Bedienung: „Eurocode“ im Menüband, Ausnutzungsbalken je Stab, Lastart in den Eigenschaften, Export „Eurocode-Nachweis“ (Markdown-Tabelle).
- Grenzen (im Bericht genannt): Vorbemessung, keine k-Beiwerte der Interaktion, Knicklänge = Stablänge, nur eine Leiteinwirkung, kein Biegedrillknicken – ersetzt keine geprüfte Statik.
- Tests: Core 3 gegen Handrechnung (Biegung G/Q, χ nach Knicklinie b, EC 5), App 1
