# Funktionsliste: Online verkaufen

Studio STUDIO-ONLINE-BUSINESS-01 · Online verkaufen · Bereich Business & Geld

> Entwurf aus dem read-only Audit vom 09.11.2026. Zahlen sind aus dem Repository erzeugt. „Implementiert“ heißt nicht „abgenommen“.

| Nr | Funktion | Stand laut Katalog | Test |
|---|---|---|---|
| VK-01 | Kassieren bar mit Rückgeld oder Karte, fortlaufende Bon-Nummer, MwSt | umgesetzt | CashRegisterTests |
| VK-02 | Kasse in der Oberfläche: Kacheln, Korb, Schnelltasten, Bon-PDF, Storno nach Ja | umgesetzt | Kasse_Ui |
| VK-03 | Tagesabschluss als PDF | umgesetzt | CashRegisterTests |
| VK-04 | Rabatt in Prozent oder Euro | umgesetzt | SalesStatsTests |
| VK-05 | Artikelnummer/EAN mit Prüfziffer, Scanner-Eingabe | umgesetzt | SalesStatsTests |
| VK-06 | Auswertung: Umsatz, Vergleich, Durchschnittsbon, Bestseller, Wochentage, Stoßzeit | umgesetzt | SalesStatsTests |
| VK-07 | Scannen, Rabatt und Auswertung in der Oberfläche | umgesetzt | Kasse_Ui |
| VK-08 | Kunden, Artikel, Bestellung, Rechnung, Umsatz | umgesetzt | Teil4BusinessUiTests |
| VK-09 | Lager, Nachbestellpunkt | umgesetzt | W5_Bus_Ops |
| VK-10 | Geschäftsidee bewerten und planen | umgesetzt | W5_Business_Ui |
| VK-11 | Bestellungen aus fremden Online-Shops lesen | offen | – |
| VK-12 | Zertifizierte Kasse (TSE) | bewusst nicht | – |

## Fehlende Funktionen laut Vorbild-Analyse

- ✗ Bestellungen aus fremden Online-Shops abrufen – nur nach Zustimmung und mit eigenem Zugang, noch nicht gebaut
- ✗ Zertifizierte Kasse mit TSE (KassenSichV) – bewusst nicht, Hinweis auf der Seite

## Teilweise vorhanden

- keine
