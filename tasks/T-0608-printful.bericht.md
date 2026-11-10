# Bericht T-0608: Printful und Shop

**Zweig:** `claude/T-0608-printful`. Nach `main` übernommen am 10.11.2026 nach ausdrücklichem OK, zusammen mit T-0700 in Release 0.9.90.

## Inhalt

- POD-Studio: Printful verbinden (Knopf zur Schlüsselseite, Schlüssel im Tresor), Katalog, Varianten, Druckplätze, echte Mockups, Produkt anlegen.
- Design-Ablage im eigenen Netlify-Konto, weil Printful Designs nur von einer öffentlichen Adresse holt. Nicht erratbare Namen, Ablage wird nach Übernahme geleert.
- Studio „Online verkaufen“: Produkte aus Printful, Bezahl-Links über das eigene Stripe-Konto, Pflichtseiten als Vorlagen, Veröffentlichen auf Netlify, bezahlte Bestellungen als Entwurf bei Printful, Freigabe in zwei Schritten.

## Unabhängige Prüfung

Ein getrennter Prüflauf hat 15 Befunde gemeldet, darunter die fehlende Druckposition, eine zu schwach abgesicherte Freigabe, flüchtiger Entwurfsstand, fehlender Adresszusatz, Versandtext ohne Versandkosten, Testzahlungen und Kundendaten im Seitentext. Behoben in `1a982462f`. Der Preis im Shop enthält jetzt den Versand, die Person rechnet ihn in den Verkaufspreis ein.

## Testergebnis

Volltest auf `1a982462f`, Exitcode 0: Api 16 bestanden; Core 2178 bestanden, 5 übersprungen; App 965 bestanden, 4 übersprungen; Paket-Tests 9 OK. Nach dem Zusammenführen mit T-0700 auf `0109cc7a5`, Exitcode 0: Api 16 bestanden; Core 2212 bestanden, 5 übersprungen; App 1190 bestanden, 4 übersprungen; Paket-Tests 11 OK.

## Nicht geprüft

Kein Lauf mit echten Konten bei Printful, Netlify oder Stripe. Offen sind damit: ob Printful eine doppelte `external_id` ablehnt, ob erstattete Zahlungen als bezahlt erscheinen, ob ältere Netlify-Stände erreichbar bleiben, ob Mockup-Adressen ablaufen, ob Schlüssel auf Kontoebene `X-PF-Store-Id` brauchen. Die Rechtstexte sind Vorlagen und nicht juristisch geprüft. Printify, Gelato, Shopify und Etsy sind nicht gebaut.

## Status

Auf `main`. Keine Abnahme durch eine Person.
