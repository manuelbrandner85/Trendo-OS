# Verkaufen – Testplan

Studio: Online verkaufen (STUDIO-ONLINE-BUSINESS-01) · Bereich: Business & Geld · Seite: `verkaufen` · Stand: 0.9.114 (Stand 99cbb5c)

13 Tests sprechen dieses Programm an.

- Artikelformular: nach einer Fehlermeldung (falsche Prüfziffer, fehlender Preis) behalten alle Felder ihren Inhalt; nach erfolgreichem Anlegen sind sie leer
- Kasse wie SumUp/Square: Strichcode scannen legt den Artikel in den Korb, Rabatt 10 % senkt die Summe, Auswertung zeigt Umsatz, Bestseller und Stoßzeit; Artikelnummer mit falscher Prüfziffer abgelehnt
- M1 (UI): Kasse zeigt jede Variante als Kachel, Verkauf senkt den Bestand der Variante
- N-e (UI): Import mit 8 gesperrten/ungültigen Zeilen – Meldung nennt „8 nicht übernommen“ und alle 8 Hinweise
- N1 (UI): „Artikel aktualisieren“ mit vorgemerkter Variante, die beim Anbieter wegfällt – Meldung steht im Shop-Bereich
- Online verkaufen: Startseite mit Kennzahlen; Kunde, Artikel, Bestellung, Rechnung – Umsatz und Geld stimmen
- STUDIO-ONLINE-BUSINESS-01: Artikelnummer/EAN: Prüfziffer EAN-13 und EAN-8, falscher Strichcode abgelehnt, freie Nummern erlaubt; Scanner findet zuerst die Nummer, dann den Namen
- STUDIO-ONLINE-BUSINESS-01: Auswertung: Umsatz, Bons, Durchschnittsbon, Rabatte, Bestseller, Wochentage, Stoßzeit; Stornos zählen nicht; Vergleich zum Zeitraum davor
- STUDIO-ONLINE-BUSINESS-01: Rabatt: 10 % oder 5 € auf den Bon, nie mehr als die Summe; bucht weniger Einnahme, kein Lagerposten; Storno bucht genau zurück; falsche Angaben werden abgelehnt
- T-0304: Kasse – Kacheln in den Korb, +/−, „passend“/Rückgeld, Kassieren bucht Geld und Bestand, Bon-PDF, Storno Nein/Ja, Tagesabschluss-PDF
- T-0753 ERSETZT: Anfragen an Printful, Printify, Stripe (Test), Netlify gegen lokalen Server nach offizieller Dokumentation – keine Abweichung, nichts Kostenpflichtiges
- T-0753: Klick bis zur Datei – Artikel mit Bild und Varianten anlegen, ändern, Kunde ändern, Bestellung ändern, CSV aus und wieder ein, Rückgängig
- T-0753: POD-Testlauf in der Oberfläche ohne eigene Schlüssel – BLOCKIERT mit Anleitung, keine Anfrage; Entwurf wird Artikel im Shop
