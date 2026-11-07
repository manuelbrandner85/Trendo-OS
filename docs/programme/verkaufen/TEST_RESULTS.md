# Verkaufen – Testergebnisse

Studio: Online verkaufen (STUDIO-ONLINE-BUSINESS-01) · Bereich: Business & Geld · Seite: `verkaufen` · Stand: 0.9.36 (Stand 6da70dc)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 9 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Artikelformular: nach einer Fehlermeldung (falsche Prüfziffer, fehlender Preis) behalten alle Felder ihren Inhalt; nach erfolgreichem Anlegen sind sie leer | ✓ bestanden |
| Geld: Übersicht mit Saldo und Diagramm, Budget-Warnung, Kategorie in der Tabelle ändern wird gelernt, Steuerberater-ZIP | ✓ bestanden |
| Geschäftsidee U2/U3: Score als Ring mit Ampel, Gründungsschritte abhaken | ✓ bestanden |
| Kasse wie SumUp/Square: Strichcode scannen legt den Artikel in den Korb, Rabatt 10 % senkt die Summe, Auswertung zeigt Umsatz, Bestseller und Stoßzeit; Artikelnummer mit falscher Prüfziffer abgelehnt | ✓ bestanden |
| Online verkaufen: Startseite mit Kennzahlen; Kunde, Artikel, Bestellung, Rechnung – Umsatz und Geld stimmen | ✓ bestanden |
| STUDIO-ONLINE-BUSINESS-01: Artikelnummer/EAN: Prüfziffer EAN-13 und EAN-8, falscher Strichcode abgelehnt, freie Nummern erlaubt; Scanner findet zuerst die Nummer, dann den Namen | ✓ bestanden |
| STUDIO-ONLINE-BUSINESS-01: Auswertung: Umsatz, Bons, Durchschnittsbon, Rabatte, Bestseller, Wochentage, Stoßzeit; Stornos zählen nicht; Vergleich zum Zeitraum davor | ✓ bestanden |
| STUDIO-ONLINE-BUSINESS-01: Rabatt: 10 % oder 5 € auf den Bon, nie mehr als die Summe; bucht weniger Einnahme, kein Lagerposten; Storno bucht genau zurück; falsche Angaben werden abgelehnt | ✓ bestanden |
| T-0304: Kasse – Kacheln in den Korb, +/−, „passend“/Rückgeld, Kassieren bucht Geld und Bestand, Bon-PDF, Storno Nein/Ja, Tagesabschluss-PDF | ✓ bestanden |
