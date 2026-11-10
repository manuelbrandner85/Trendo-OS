# Bericht T-0609: Printify und Schlüsselfeld

**Zweig:** `claude/T-0609-pod-anbieter`. Nach `main` übernommen am 10.11.2026 nach ausdrücklichem OK, Release 0.9.91.

## Inhalt

- Printify als zweiter Anbieter im POD-Studio: Schlüssel, Shop, Katalog, Druckerei, Varianten, Produkt mit Mockups. Designs bis 5 MB direkt als Base64, größere über die Netlify-Ablage.
- Shop: Printify-Produkte mit Bezahl-Links, Bestellungen nur bei bestätigter manueller Freigabe des Printify-Shops, Fertigung über die zweistufige Freigabe mit Kostenanzeige.
- Schlüsselfelder (Printful, Netlify, Printify, Stripe) untereinander mit fester Breite. Vorher schob ein langer Schlüssel den Bereich aus dem Bild (Meldung des Nutzers).
- Gelato zurückgestellt: Mockup-Generator und Katalog-Schnittstelle in der Dokumentation nicht belegt.

## Unabhängige Prüfung

Befunde: Bestätigung der manuellen Freigabe war an keinen Shop gebunden und überlebte das Trennen (hoch), verlorene Antwort beim Anlegen ohne Wiederverknüpfung, Knopftext „Entwurf bei Printful“ für Printify, fehlende Kostenanzeige, Ablage-Hinweis überschrieben, alte Shop-Kennungen. Behoben in `2afc9d168`: Bestätigung gilt nur für die Shop-ID und fällt beim Trennen und Neuverbinden weg, 409 mit klarer Handlungsanweisung, Kosten aus der Bestellung, Shop-ID mit der Bestellung gespeichert, keine Fehlertexte mit Adressdaten.

## Testergebnis

Volltest auf `2afc9d168`, Exitcode 0: Api 16 bestanden; Core 2217 bestanden, 5 übersprungen; App 1194 bestanden, 4 übersprungen; Paket-Tests 11 OK.

## Nicht geprüft

Kein Lauf mit echtem Printify-Konto. Offen: ob Printify die Datei beim Hochladen per Adresse sofort abholt, ob Produkte in der Shop-Währung abgewiesen werden, ob Druckereien Telefonnummern verlangen. Die Bestellung bei Printify lässt sich nach einer verlorenen Antwort nicht automatisch wieder verknüpfen, Trendo verweist dann auf Printify.

## Status

Auf `main`. Keine Abnahme durch eine Person.
