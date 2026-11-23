# T-0753: Online verkaufen als Arbeitsplatz, POD-Anbieter im Testmodus belegt

**Ziel:** Restliste d7 Rang 29 und 30 (freigegeben 23.11.2026): „Online verkaufen“ wird ein echter Arbeitsplatz für Artikel, Kunden und Bestellungen mit CSV im Shopify-Format; „Print on Demand“ bekommt einen ehrlichen Testlauf der Anbieter und eine Übernahme in den Shop.
**Branch:** claude/T-0753-verkaufen-pod
**Dateien/Bereich:** `src/Trendo.Core/ShopEdit.cs` (neu), `FinanceExtra.cs`, `BusinessOps.cs`, `BusinessMarket.cs`, `Pod/PodTestRun.cs` (neu), `Pod/Printful.cs`; `src/Trendo.App/MainWindow.SellEdit.cs` (neu), `MainWindow.PodTestRun.cs` (neu), `MainWindow.W5Sell.cs`, `MainWindow.PodShop.cs`, `MainWindow.PodStudio.cs`, `MainWindow.PodDesigner.cs`; `tools/Trendo.Render/Program.cs` (Bildmodus `TRENDO_SELL`); Tests und `reports/T-0753/`.
**Fertig, wenn:**
- [x] Artikel anlegen, ändern, löschen – mit Bild, Preis, Bestand, Varianten
- [x] Kunden und Bestellungen als Liste mit Bearbeiten
- [x] CSV ein/aus im Shopify-Spaltensatz, Export in einem Fremdprogramm geöffnet (LibreOffice nicht vorhanden → BLOCKIERT, stattdessen PowerShell Import-Csv und Python csv)
- [x] sofortiges atomares Speichern, Rückgängig
- [x] Vorschau zeigt den echten Datenstand
- [x] POD-Testlauf: ohne Schlüssel BLOCKIERT mit Anleitung, keine Anfrage; Stripe nur Testschlüssel
- [x] Anfragen gegen die offizielle Dokumentation geprüft (lokaler Testserver, ERSETZT)
- [x] POD-Artikel lässt sich in den Shop übernehmen
**Nicht tun:** nichts senden, bestellen, Konten anlegen, Schlüssel eintragen; keine Netzanfrage an echte Dienste ohne Schlüssel; kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0753-verkaufen-pod.bericht.md`.
