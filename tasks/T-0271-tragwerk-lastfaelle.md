# T-0271 – Tragwerk: Trapezlast, Temperatur, Lagersetzung, schräges Loslager

- Freigabe: Manuel im Chat am 31.10.2026 („Erst offene Punkte“)
- Umgesetzt (Rechenkern `Statik/Frame.cs`): Streckenlast mit Endwert (Trapez/Dreieck) mit exakten Ersatzlasten und Schnittgrößen, Temperaturlast (gleichmäßige Erwärmung → Normalkraft, Unterschied unten/oben → Krümmung, Biegelinie mit Temperaturkrümmung), Lagersetzung als vorgegebene Verschiebung, Loslager auf geneigter Ebene (Strafsteifigkeit in Normalenrichtung, Lagerkraft senkrecht zur Ebene). Zeichnung: Trapez mit wachsenden Pfeilen, Temperaturhinweis am Stab, geneigte Gleitebene mit Winkel, Setzung am Lager. Bedienung: Felder in den Eigenschaften, „＋ Temperatur“ am Stab.
- Tests: Core 4 gegen Lehrbuchformeln (qL/6 und qL/3, qL²/(9√3), qL²/30 und qL²/20, EAαΔT, αΔT·L, EIαΔT/h, κL²/8, 6EIδ/L², geneigtes Lager), App 1 (echte Eingabe mit Rückgängig)
- Offen: Nachweise nach Eurocode (Teilsicherheitsbeiwerte, Lastkombinationen)
