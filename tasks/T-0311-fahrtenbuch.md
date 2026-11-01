# T-0311 – Auto als Fahrtenbuch

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Auto als Fahrtenbuch“).
- Vorher: Fahrten mit km, „dienstlich“ und Zweck; Löschen ohne Spur; keine Monate, keine Festschreibung.
- Jetzt (`MainWindow.Spec16.cs` › Fahrten, Kern `VehicleBook` + `W6/Logbook.cs`):
  - Fahrtart dienstlich / privat / Arbeitsweg; Ziel, Zweck, Geschäftspartner getrennt (dienstlich: Zweck Pflicht, fehlendes Ziel wird angemahnt).
  - Häufige Fahrten als Knöpfe: füllen Ziel, Zweck, Partner, Art und das km-Ende (übliche Strecke).
  - Jahr blättern, Anteile als Balken, Monatsgruppen mit Summen; „Monat festschreiben“ sperrt Änderungen und Nachträge.
  - Korrigieren nur mit Grund (Vermerk „alt → neu – Grund“ an der Fahrt), Stornieren statt Löschen (bleibt durchgestrichen mit Grund sichtbar, zählt nicht, gibt die km frei).
  - Fahrtenbuch als PDF (A4 quer) mit allen Pflichtangaben, Stornos/Korrekturen, Monats- und Jahressummen, Lücken, Unterschriftszeile; CSV erweitert.
  - Ältere Einträge ohne Art gelten weiter als dienstlich/privat.
- Tests: `LogbookTests` (Eintragen, Monatssummen, Korrektur, Storno, Festschreiben, häufige Fahrten, PDF); `SPEC-16`-Kerntests (9) und `VehicleUiTests` (2) grün.
- Offen: eigener Oberflächentest für Korrigieren/Stornieren/Festschreiben mit echten Klicks (folgt).
