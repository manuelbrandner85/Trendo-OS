# Verkaufen – Testplan

Studio: Online verkaufen (STUDIO-ONLINE-BUSINESS-01) · Bereich: Business & Geld · Seite: `verkaufen` · Stand: 0.9.36 (Stand 5e46a94)

9 Tests sprechen dieses Programm an.

- Artikelformular: nach einer Fehlermeldung (falsche Prüfziffer, fehlender Preis) behalten alle Felder ihren Inhalt; nach erfolgreichem Anlegen sind sie leer
- Geld: Übersicht mit Saldo und Diagramm, Budget-Warnung, Kategorie in der Tabelle ändern wird gelernt, Steuerberater-ZIP
- Geschäftsidee U2/U3: Score als Ring mit Ampel, Gründungsschritte abhaken
- Kasse wie SumUp/Square: Strichcode scannen legt den Artikel in den Korb, Rabatt 10 % senkt die Summe, Auswertung zeigt Umsatz, Bestseller und Stoßzeit; Artikelnummer mit falscher Prüfziffer abgelehnt
- Online verkaufen: Startseite mit Kennzahlen; Kunde, Artikel, Bestellung, Rechnung – Umsatz und Geld stimmen
- STUDIO-ONLINE-BUSINESS-01: Artikelnummer/EAN: Prüfziffer EAN-13 und EAN-8, falscher Strichcode abgelehnt, freie Nummern erlaubt; Scanner findet zuerst die Nummer, dann den Namen
- STUDIO-ONLINE-BUSINESS-01: Auswertung: Umsatz, Bons, Durchschnittsbon, Rabatte, Bestseller, Wochentage, Stoßzeit; Stornos zählen nicht; Vergleich zum Zeitraum davor
- STUDIO-ONLINE-BUSINESS-01: Rabatt: 10 % oder 5 € auf den Bon, nie mehr als die Summe; bucht weniger Einnahme, kein Lagerposten; Storno bucht genau zurück; falsche Angaben werden abgelehnt
- T-0304: Kasse – Kacheln in den Korb, +/−, „passend“/Rückgeld, Kassieren bucht Geld und Bestand, Bon-PDF, Storno Nein/Ja, Tagesabschluss-PDF
