# Bericht T-0248 – Shop: Bestand bei Lieferung, Umsatz bei Zahlung

- **Status:** IMPLEMENTED, automatisierte Tests grün. NICHT PC_VERIFIED.

## Änderung
- `src/Trendo.Core/FinanceExtra.cs`: `ShopOrder` mit `StockOut`, `Delivered`, `InvoiceGross`, `Payments` (null = Altdaten), `Paid`, `Open`; `ShopPayment`, `StockMove`; `Shop.Reserved/Available`, `Deliver` (Bestand und Lagerbewegung), `Cancel` (vor Lieferung ohne Bestand, danach Gegenbuchung, mit Zahlung gesperrt), `Pay` (Referenz idempotent, Teilzahlung, Überzahlung abgewiesen, bucht in `Ledger`); `Invoice` bucht nicht mehr; Kennzahlen und Monatsumsatz nach Zahlungseingang; `OpenReceivables`.
- `src/Trendo.App/MainWindow.W5Sell.cs`: „Geliefert“, „Zahlung erfassen“ mit Betrag/Referenz, „Stornieren“, offener Betrag, Kachel „Offene Rechnungen“, Vormerkungen in der Shop-Vorschau.
- Tests: `Teil4BusinessTests.Shop_flow` bewusst angepasst (Entscheidung C2), neu `Payments_and_stock` (Abnahme 105-05: 100/60/40, Wiederholung idempotent; 105-22: Storno vor/nach Lieferung ausgeglichen), `Legacy_orders`; `Teil4BusinessUiTests.Sell` angepasst.
- Version 0.9.22.

## Nachweise
- Volltest: Api 15/15, Core 858 bestanden/3 übersprungen, App 437 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0248/volltest.log`, SHA-256 `6a901009fd88a8c357892d79d0a3401f53f53547e96e21a676da391445ecb5b4`

## Nicht Teil / offen
- Erstattungen, mehrere Lagerorte, Kontoauszug-Abgleich (Rest von 105-05/105-22).
- Kein CRM-Ausbau (IMP-86 NEIN).
