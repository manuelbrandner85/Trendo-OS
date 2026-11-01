# T-0304 – Verkaufen als Kasse

- Freigabe: Manuel im Chat am 01.11.2026 (Auswahl „Verkaufen als Kasse“).
- Vorher: Verkaufen kannte nur Online-Bestellungen (Kunde → Bestellung → Rechnung → Zahlung).
- Jetzt: Verkaufen hat die Bereiche „🧾 Kasse“ (Start) und „🛒 Online & Artikel“ (bisheriger Inhalt). Die Kasse (`MainWindow.Kasse.cs`, Kern `CashRegister`) nutzt dieselben Artikel, denselben Bestand (`Shop.Available`, Lagerbewegung „Kasse“) und bucht in „Geld“:
  - Artikel als Kacheln mit Bruttopreis und freiem Bestand; Antippen legt in den Warenkorb, nicht Vorrätiges ist gesperrt; Suche.
  - Warenkorb mit − / + / ✕, Summe, enthaltene MwSt (außer Kleinunternehmer).
  - Bar mit gegebenem Betrag, Schnelltasten („passend“, 10 €, 20 € …) und Rückgeld bzw. „Es fehlen …“; oder Karte.
  - Kassieren: fortlaufende Bon-Nummer je Tag, Bestand sinkt sofort, Einnahme mit MwSt-Satz in „Geld“; Bon als PDF (80 mm).
  - Heute: Umsatz, bar, Karte, Meistverkauftes, Liste der Bons; Storno nur nach „Ja“ (Bestand und Geld zurück, nur einmal); Tagesabschluss als PDF.
  - Hinweis: kein Ersatz für eine zertifizierte Registrierkasse (TSE).
- Test angepasst: `Teil4BusinessUiTests` wechselt zuerst auf „Online & Artikel“ (die Seite startet jetzt mit der Kasse).
- Vorschaubild: Render-Schalter `TRENDO_KASSE=1`.
- Tests: `CashRegisterTests` (Kassieren mit MwSt und Rückgeld, Bon-Nummern, Bestand, Ablehnungen, Kleinunternehmer, Storno, Tagesabschluss, PDFs), `Kasse_Ui` (echte Klicks: Kacheln, +/−, gesperrter Artikel, zu wenig Geld, Schnelltaste, Kassieren, Bon-PDF, Karte, Storno Nein/Ja, Tagesabschluss-PDF, Wechsel zu Online); `Teil4BusinessUiTests`, `W5BusinessUiTests` grün.
