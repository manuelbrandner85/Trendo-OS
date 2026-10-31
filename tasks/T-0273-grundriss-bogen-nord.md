# T-0273 – Grundriss: Bogenwände und Nordpfeil

- Freigabe: Manuel im Chat am 31.10.2026 („Erst offene Punkte“)
- Umgesetzt: Werkzeug „Bogen“ (Anfang und Ende klicken), Bogen als zusammengehörige Kette kurzer Wandstücke (bis 40 cm, 6–48 Stücke) – Räume, 3D, DXF/SVG/PDF und Materialliste funktionieren ohne Sonderfall; Stich (Wölbung) in den Eigenschaften, Radius und Bogenlänge, Löschen als Ganzes; Nordrichtung im Projekt mit Nordpfeil im Plan und in allen Exporten.
- Gefunden und behoben: ungültiger Stich (mehr als ein Halbkreis) löschte den Bogen vor der Prüfung; nach Enter + „Rückgängig“ schrieb das alte, schon abgelöste Eingabefeld den Wert beim Fokusverlust erneut zurück (galt für alle Programme – jetzt zentral gesperrt)
- Tests: Core 2 (Halbkreis π·r, Kreispunkte, Radius (c²/4+s²)/2s, Seitenwahl, Speichern), App 1 (Maus, Eintippen, Rückgängig, Nordrichtung); 27 Oberflächentests der Programme erneut grün
- Offen (Schritt 3D): virtueller Rundgang, Texturen
