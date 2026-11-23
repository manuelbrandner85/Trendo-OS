# Bericht T-0753: Online verkaufen als Arbeitsplatz, POD-Anbieter im Testmodus

**Auftrag:** d7-Restliste Rang 29 (STUDIO-ONLINE-BUSINESS-01) und Rang 30 (STUDIO-POD-01), freigegeben 23.11.2026. Basis `main` (Release 0.9.107).
**Branch:** `claude/T-0753-verkaufen-pod` (eigene Kopie, danach gelöscht).

## 1 Online verkaufen

**Vorher (audit-1):** Artikel und Kunden nur anlegen (`W5Sell.cs` enthielt nur „anlegen“), kein CSV, Shop-Vorschau ohne Bilder/Varianten.

**Jetzt:**
- Artikel mit Beschreibung, Bild (wird in `business/<idee>/bilder/` kopiert, Name aus SHA-256 – bleibt nach Verschieben der Quelle erhalten), Preis, Bestand und **Varianten** (eigener Preis, Bestand, Artikelnummer). Bestellungen auf Varianten („Shirt – M“), Lieferung/Storno ändern den Bestand der Variante.
- **Ändern und Löschen** für Artikel, Kunden und offene Bestellungen – per Knopf und per **Rechtsklick**. Umbenennen überträgt den Namen in Bestellungen, der beim Bestellen festgehaltene Preis bleibt. Löschen eines vorgemerkten Artikels bzw. eines Kunden mit Bestellungen wird abgelehnt.
- **Rückgängig** (Knopf und Strg+Z) für Artikel-, Kunden- und Bestelländerungen, bis zu 20 Schritte. Wurde seitdem verkauft, geliefert oder bezahlt (Prüfsumme der drei Dateien weicht ab), wird es abgelehnt – Bestand, Bons und Buchungen bleiben stimmig.
- **CSV im Shopify-Spaltensatz** (Spaltennamen laut Shopify-Hilfe, gelesen 23.11.2026): Artikel ein/aus (auch ältere Namen wie `Handle`, `Body (HTML)`, `Variant Price`, `Image Src` und deutsche Semikolon-Tabellen), Kunden ein/aus (`First Name`, `Default Address …`), Bestellungen aus. Doppelte Artikelnummern werden gemeldet. **Ziehen und Ablegen** von CSV auf die Austausch-Karte, von Bildern auf eine Artikelkarte.
- Speichern sofort und atomar (`IdeaList` → `AtomicFile`), ein CSV-Import ist ein Schreibvorgang.
- Vorschau-Karten und „Als Webseite speichern“ zeigen den echten Stand: Bild (in der HTML eingebettet), Beschreibung, Varianten mit Preis und Bestand, „auf Bestellung gefertigt“ bei POD-Artikeln. Preisfarbe Gold statt Blau.
- Gefundener Fehler behoben: alte, abgelöste Eingabefelder schrieben ihren Text in die Entwürfe zurück, sodass nach einer Neuzeichnung bereits angelegte Varianten wieder im Formular standen (`W5Sell.cs` `SellField`).

**Belege:** `ShopEditTests` (11, Kern), `SellEdit_Ui.Click_to_file` (Klick bis zur Datei, echte Dateien), `reports/T-0753/csv-fremdprogramme.log` (+ `.sha256`): die exportierten CSV öffnen sich in **Windows PowerShell 5.1 Import-Csv** und im **Python-csv-Modul** (echte Fremdprogramme, Umlaute, Komma im Feld, Varianten-Zeilen, Bild daneben). **LibreOffice: BLOCKIERT** – auf diesem PC nicht installiert (nur Restordner `program\logs`), Excel ebenfalls nicht. Bild der Oberfläche: `reports/T-0753/oberflaeche-online-artikel.png`.

## 2 Print on Demand

**Vorher (audit-1):** Printful, Printify, Stripe, Netlify nur gegen Fake-HTTP, kein Beleg.

**Jetzt:**
- **Testlauf der Anbieter** (`Core/Pod/PodTestRun.cs`, Karte „Anbieter prüfen (Testlauf)“ im POD-Studio): ohne Schlüssel je Dienst **BLOCKIERT** mit drei Schritten, was die Person selbst einrichten muss – und **keine einzige Anfrage**. Mit Schlüssel nur lesende Abfragen (Printful `/oauth/scopes`, Printify `/shops.json`, Stripe `/balance` + `/checkout/sessions`, Netlify `/user` – neu `NetlifyDesignHost.CheckAsync`). Stripe nur mit `sk_test_`/`rk_test_`; ein Live-Schlüssel wird für den Testlauf abgelehnt, ohne Anfrage.
- **Auf diesem PC:** kein Schlüssel der Person hinterlegt (`cmdkey /list` zeigt keine `TrendoOS:`-Einträge) → Printful, Printify, Stripe, Netlify **BLOCKIERT**. Kein echter Dienst wurde angefragt.
- **Abgleich mit den offiziellen Dokumentationen** (nur gelesen, 23.11.2026): Printful v1 (developers.printful.com/docs), Printify v1 (developers.printify.com), Stripe (docs.stripe.com, Payment Links), Shopify-CSV. Ergebnis: Pfade, Methoden und Pflichtfelder stimmen. Hinweise: Printful-Konto-Schlüssel (nicht Shop-Schlüssel) bräuchten `X-PF-Store-Id` – die Anleitung verlangt deshalb einen Schlüssel für den Shop; Printify-Upload (`/uploads/images.json`) lag nicht im gelesenen Teil der Doku und ist nur nach Code und früherer Prüfung belegt.
- **ERSETZT-Beleg:** `tests/Trendo.App.Tests/PodDocServer.cs` bildet die dokumentierten Schnittstellen als lokalen HTTP-Server nach und prüft jede Anfrage; gesendet wird über den echten HTTP-Weg der App (`HttpFetch`/`HttpClient`). `reports/T-0753/pod-docserver.log` (+ `.sha256`): 32 Anfragen, 0 Abweichungen, 0 kostenpflichtige Aufrufe (`/confirm`, `send_to_production` kamen nicht vor); eine Stripe-**Testzahlung** wird nachweislich nicht an Printful gegeben.
- **Übernahme in den Shop:** „In den Shop übernehmen“ im Schritt „Verkaufstext“ legt den Entwurf als Artikel an (Druckdatei als Bild, Preis aus Schritt 3, Beschreibung); im Shop-Bereich übernimmt „Als Artikel übernehmen“ ein Printful/Printify-Produkt samt Varianten.
- **Gelato:** im Auftrag als vorhanden genannt, im Quelltext gibt es **keinen** Gelato-Anschluss (auch audit-1: „Gelato fehlt“). Der Testlauf zeigt Gelato ehrlich als FEHLT. Nicht gebaut.

**Belege:** `ShopEditTests.Testrun_without_keys`, `SellEdit_Ui.Pod_testrun_and_takeover` (Oberfläche, 0 Anfragen), `SellEdit_Ui.Requests_match_docs` (ERSETZT).

## Abnahmekriterien

| Nr | Kriterium | Urteil | Begründung |
|---|---|---|---|
| 1 | Aus leerer Vorlage echtes Ergebnis | VOLLWERTIG (Shop) | Leerer Shop → Kunde, Artikel mit Bild/Varianten, Bestellung, CSV und HTML-Shop über die Oberfläche (`Click_to_file`) |
| 2 | Fremddatei importieren, bearbeiten, exportieren; Fremdprogramm | TEILWEISE | Shopify-CSV (neue und alte Spalten) ein, bearbeiten, aus; Export in PowerShell und Python geöffnet. LibreOffice BLOCKIERT (nicht installiert); kein Import in echtes Shopify (Konto nötig) |
| 3 | Abbruch verliert höchstens 30 s | VOLLWERTIG (Stammdaten) | Jede Änderung sofort atomar geschrieben; kein eigener Abbruchtest in diesem Paket. Kassieren als unteilbare Aktion (audit-1) nicht Teil dieses Auftrags |
| 4 | Claude/KI führt Fachaufgabe aus | FEHLT | Kein neuer KI-Adapter für Artikel/Kunden; nicht Teil des Auftrags |
| 5 | Test vom Klick bis zur echten Datei | VOLLWERTIG | `SellEdit_Ui.Click_to_file` (RealClick bis CSV/HTML auf Platte) |
| 6 | Einfach/Profi, Kürzel, Rechtsklick, Ziehen/Ablegen | TEILWEISE | Strg+Z, Rechtsklick-Menüs, Ablegen von CSV/Bildern vorhanden; Ablegen nur über `SellDropFiles` getestet (Headless kann kein echtes Ziehen); Einfach zeigt „Mehr anzeigen“ wie bisher |
| POD | Anbieter gegen echte Dienste | BLOCKIERT | Kein Konto/Testschlüssel der Person – Voraussetzungen stehen im Testlauf; Doku-Abgleich nur ERSETZT |

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` – Core, App, Tests, Render gebaut, 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…ShopEditTests|Teil4BusinessTests|CashRegisterTests|SalesStatsTests|PodShopTests|PrintfulTests|PrintifyTests|W5BusinessTests|NoFixedUser"` → Bestanden 50/50 (`reports/T-0753/core-tests.txt`). Zusätzlich T0714/T0717/SourceApps/Pod*: 65/65.
- App gezielt (`reports/T-0753/app-tests.txt`): SellEdit_Ui 3/3; Teil4Business, Kasse, PodShop, PodDesign, PodPrintful, PodPrintify, W5_Business, StudiosBueroGeld, DesignPaket3/4: 86/86; Kuenstlerstudio, PodStudio: 7/7; W0b_Look, FreigabenKernUi, DesignHonesty, QA019, QA017: 17/17.
- Rot-Grün: neue Kern-Tests bauen auf altem Code nicht (fehlende APIs).
- Kein Volltest (macht die Hauptsitzung).

## Nicht geprüft
- Echte Dienste (Printful, Printify, Stripe-Testmodus, Netlify) – BLOCKIERT ohne Schlüssel.
- Import der CSV in ein echtes Shopify; LibreOffice/Excel.
- Echtes Ziehen und Ablegen mit der Maus; Bildschirmbild im Einfach-Modus nur mit eingeklappten Karten.
- Die Druckdatei bei „In den Shop übernehmen“ ist im Test ersetzt (`PodPngOverride`), weil die Headless-Umgebung ohne Skia leere PNG schreibt; der echte Weg nutzt `PodExportPng` wie der vorhandene PNG-Export.
- (Im zweiten Durchgang behoben: die Kasse verkauft jetzt Varianten, siehe unten.)

## Offene Punkte
- Gelato anbinden (fehlt im Code).
- KI-Aktion für Artikel/Kunden (Kriterium 4).
- Bestellungen aus fremden Shops lesen (VK-11).
- Echtlauf mit einem Printful-Testshop und Stripe-Testschlüssel der Person, sobald vorhanden (Testlauf-Knopf ist bereit).

**Status:** gepusht auf `claude/T-0753-verkaufen-pod`, kein Merge, kein Release.

## Gegenprüfung behoben (zweiter Durchgang)

| Befund | Lösung | Test (Rot-Grün: baut auf altem Stand nicht, fehlende `SaleItems`/`FindSaleItem`) |
|---|---|---|
| M1 Kasse kennt keine Varianten | Kasse arbeitet mit `Shop.SaleItems()` (jede Variante eine Kachel, eigener Preis), Scanner findet Varianten-Nummern (`FindSaleItem`), Bestand über `Shop.AddStock`; Artikel mit Varianten nur über eine Variante verkaufbar | `ShopEditFixTests.Kasse_variants`, `SellEditFix_Ui.Kasse_variant_tiles` |
| M2 Storno nach Varianten-Änderung verliert Rücknahme | `UpdateProduct` sperrt Umbenennen/Entfernen von Bezeichnungen, auf die vorgemerkte, gelieferte (noch stornierbare) Bestellungen oder gültige Bons zeigen, auch nachträgliche Varianten an verkauften Artikeln; Artikel-Umbenennen zieht Bestellungen und Bons mit; `AddStock`/`RequireKnown` werfen bei unbekannter Bezeichnung, bevor eine Lagerbewegung geschrieben wird | `ShopEditFixTests.Variants_locked_by_history` |
| M3 Formel-Einschleusung im CSV | Jede Zelle aller drei Exporte läuft durch `ClassifierBatch.Safe` (reine Zahlen bleiben); der Import nimmt das Hochkomma wieder ab | `ShopEditFixTests.Csv_formula_injection` |
| M4 POD-Aktualisieren überschreibt eigene Angaben | `TakeOverPod` übernimmt Beschreibung, Einkaufspreis, Art.-Nr., Varianten-Nummern und -Bestand des bestehenden Artikels | `ShopEditFixTests.Pod_update_keeps_own_data` |
| N1 Meldung geht an globalen Fänger | Klick fängt auch `InvalidOperationException`, Meldung in `shop-msg` | `SellEditFix_Ui.Pod_update_message` |
| N2 alte Namen mit „ – “ nicht änderbar | unveränderter Altname ist erlaubt, nur neu vergeben nicht | `ShopEditFixTests.Low_findings` |
| N3 „1,000“ wird still 1 | `^\d{1,3}[.,]\d{3}$` wird als mehrdeutig abgelehnt | `ShopEditFixTests.Low_findings` |
| N4 absolute Pfade / „../“ in der Bildspalte | nur Pfade unterhalb des CSV-Ordners, sonst Hinweis | `ShopEditFixTests.Low_findings` |
| N5 Export in den Ideenordner wirft IOException | keine Kopie auf sich selbst | `ShopEditFixTests.Low_findings` |
| N6 Text „ohne fremde Dienste“ falsch | Hinweis nennt jetzt, dass POD-Bilder von der Adresse des Anbieters geladen werden | Sichtprüfung im Code |
| N7 Schlüsselnamen doppelt | `PfKey/PfyKey/StKey/NfKey` verweisen auf die Konstanten in `PodTestRun` | Build; `Requests_match_docs` nutzt beide Wege |
| optional: zwei goldene Knöpfe | „Rechnung“ nicht mehr golden, nur „Bestellung eintragen“ | – |

**Tests danach:** Kern gezielt 76/76 (`reports/T-0753/core-tests-gegenpruefung.txt`), dazu T0714/T0717/SourceApps 29/29; App 14/14 und 97/97 (`reports/T-0753/app-tests.txt`). `dotnet build Trendo.sln -c Release` 0 Fehler.
**Weiter offen:** Varianten sind an Bons an die Bezeichnung gebunden – wer eine verkaufte Variante umbenennen will, legt eine neue an.

## Zweitprüfung behoben (dritter Durchgang)

| Befund | Lösung | Test (Rot-Grün: baut auf c3d8f3a3a nicht, fehlende `Current`, `LastNotRestocked`, `NotRestocked`, öffentliches `ParseNum`) |
|---|---|---|
| M-A CSV-Import umgeht die Varianten-Sperre | Gemeinsame Prüfung `LockViolation` für `UpdateProduct` und `ImportProductsCsv`; ein Artikel, der dagegen verstößt, wird beim Import mit Hinweis übersprungen | `ShopEditFix2Tests.Import_respects_variant_lock` |
| M-B Bon-Storno eines gelöschten Artikels abgelehnt | Storno (Bon und Bestellung) bucht Geld bzw. Status, ohne Bestandsänderung für Artikel, die es nicht mehr gibt – sichtbar in der Meldung (`NotRestocked`/`LastNotRestocked`), keine Lagerbewegung; Sperre gilt jetzt für alle nicht stornierten Lieferungen (auch bezahlte) und gültige Bons; der Text „nimm Rückgängig“ ist gestrichen | `Cancel_bon_of_deleted_product`, `Paid_delivery_locks_variant`, angepasst `ShopEditFixTests.Variants_locked_by_history` |
| N-a Hochkomma geht verloren | Werte mit führendem „'“ bekommen beim Export ein zweites, der Import nimmt eines ab; CSV-Karte erwähnt das sichtbare Hochkomma in Shopify | `Apostrophe_roundtrip` |
| N-b Zahlen | 1.000,00 · 1,000.50 · 1 000 · 1.234.567,8 werden gelesen; „1.000“/„1,000“ ohne Dezimalteil abgelehnt mit neutraler Meldung „ohne Tausenderpunkt, z. B. 1000 oder 12,50“ | `Numbers_ok`, `Numbers_ambiguous` |
| N-c Umbenennen ändert alte Bons | Bons bleiben wie ausgegeben; Umbenennungen liegen in `umbenennungen.json` (gehört zu Rückgängig), das Storno folgt ihnen, Rück-Umbenennen ohne Schleife | `Rename_keeps_bons` |
| N-d Unpassende Meldung | eigener Text „Varianten lassen sich nicht nachträglich hinzufügen“ | `Add_variants_message` |

**Tests danach:** Kern gezielt 90/90 (`reports/T-0753/core-tests-zweitpruefung.txt`), mit T0714/T0717/SourceApps 114/114; App 80/80 (`reports/T-0753/app-tests.txt`). `dotnet build Trendo.sln -c Release` 0 Fehler.

## Drittprüfung behoben (vierter Durchgang)

Die Umbenennungs-Liste aus N-c ist ersetzt durch eine **feste Artikel-Kennung**: `Product.Id`, `CashLine.ItemId/Variant`, `OrderLine.ItemId/Variant` (alle optional, alte Dateien bleiben lesbar; `umbenennungen.json` wird nicht mehr gelesen). Verkauf, Bestellung, Lieferung, Storno und Sperre arbeiten über die Kennung. Altzeilen ohne Kennung (Bons, nicht stornierte Bestellungen) werden vor Umbenennen, Löschen oder Import an den heute gleichnamigen Artikel gebunden; Text, Menge und Preis des Belegs bleiben unverändert.

| Befund | Lösung | Test (Rot-Grün: baut auf 6b351c379 nicht, fehlende `Product.Id`, `CashLine.ItemId`, `ShopImport.Skipped`) |
|---|---|---|
| M-C Fall 1 (umbenannt, neuer gleichnamiger Artikel) | Bon hängt an der Kennung; neuer „Keks“ hat eine eigene; Sperre läuft über die Kennung | `ShopEditFix3Tests.Rename_then_new_same_name` (Keks 5, Cookie 8; Varianten am neuen Keks gesperrt) |
| M-C Fall 2 (Kette) | keine Namenskette mehr nötig | `Rename_chain` (Storno bucht auf X) |
| M-C Fall 3 (gelöscht, neu angelegt) | Kennung des gelöschten Artikels gibt es nicht mehr → Storno ohne Bestand, `NotRestocked` | `Delete_then_new_same_name` |
| M-C Bestellungen | `Shop.Cancel`/`Deliver` ohne `Current()`, über die Kennung | `Orders_bound_to_item` |
| M-C Altdaten | Bon und Artikel ohne Kennung lesbar, Bindung vor dem Umbenennen | `Legacy_files` |
| N-e nur 5 bzw. 3 Hinweise | Meldung nennt „N nicht übernommen“ und alle Hinweise, je eine Zeile | `SellEditFix3_Ui.Import_shows_all_warnings` (8 von 8) |
| N-f negativer Preis aus CSV | Import prüft jeden Artikel mit `CheckProduct`, Verstöße werden mit Hinweis übersprungen | `Negative_price_and_all_warnings` |

`ShopEditFix2Tests.Rename_keeps_bons` prüfte die entfernte Namenskette (`Current`); er prüft jetzt dasselbe Verhalten über die Kennung (Bon unverändert, Storno trifft den umbenannten Artikel, Rück-Umbenennen geht).

**Tests danach:** Kern 96/96, mit T0714/T0717/SourceApps 120/120 (`reports/T-0753/core-tests-drittpruefung.txt`); App 81/81 (`reports/T-0753/app-tests.txt`). `dotnet build Trendo.sln -c Release` 0 Fehler.
