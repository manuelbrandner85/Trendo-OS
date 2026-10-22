# 2026-10-22 Business-Auftrag

`rechnung.schreiben` schreibt eine HTML-Rechnung oder ein Angebot und liest die Summe mit `Finance.Totals` zurück. Nichts wird gebucht. `geld.zahlen` bleibt blockiert: auch „Zahlen: ja“ und „Freigabe: erteilt“ überweisen nichts. `geschaeft.bewerten` schreibt den Bauplan aus `BusinessStudio` und kennzeichnet ihn als Schätzung.

Bild: `TRENDO_MONEY_PROMPT=1 dotnet run --project tools/Trendo.Render -c Release -- rechnung-vorschau.png 1536 1024 finanzen` und `TRENDO_BUSINESS_PROMPT=1` auf der Seite `business`.
