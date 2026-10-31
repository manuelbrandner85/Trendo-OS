# Bericht T-0241 – Erweiterungen nur mit Freigabe, sicherer Download

- **Status:** IMPLEMENTED, automatisierte Tests grün. NICHT PC_VERIFIED. Kein echter Download von GitHub geprüft (nur nachgebildet bzw. lokaler Katalog).

## Änderung
- `src/Trendo.Core/Extensions.cs`: `ExtensionStore(dataRoot, audit, gate)`; `Install(source, approvalId)` und `Remove(id, approvalId)` laufen mit Gate über `ApprovalGate.Execute` (Aktion `ext.install`/`ext.remove`, Ziel = SHA-256 der Quelle bzw. Kennung; Not-Stopp sperrt); ZIP-Grenzen (höchstens 2000 Einträge, keine doppelten Pfade, Packrate ≤ 100 bei Dateien > 1 MB, 50 MB gesamt); gleiche Kennung mit anderem Herausgeber abgewiesen; atomarer Austausch über `.neu-*`/`.alt-*`.
- `src/Trendo.Core/ExtensionCatalog.cs`: `RedirectAllowed` (nur GitHub, https, Port 443), weitere GitHub-Download-Hosts; `InstallVerified(…, approvalId)`.
- `src/Trendo.App/MainWindow.W5Ext.cs`: Rückfrage mit Name, Version, Herausgeber, Herkunft, Prüfsumme; Freigabe beim „Ja“; Entfernen mit Rückfrage; `ExtRemoveApproved`.
- `src/Trendo.App/MainWindow.W5ExtCat.cs`: ein HttpClient ohne automatische Weiterleitung, jede Weiterleitung geprüft, Größenlimit, offline gesperrt, Audit `ext.download`; Katalog-Installation mit Rückfrage.
- `src/Trendo.App/MainWindow.AddHub.cs`: Entfernen über Freigabe.
- Tests: `W5ExtTests` (+2), `ExtCatalogTests` (+1), `W5ExtUiTests` (+1, angepasst), `ExtCatalogUiTests` (angepasst an Rückfrage).
- Version 0.9.19.

## Nachweise
- Volltest: Api 15/15, Core 852 bestanden/1 übersprungen, App 436 bestanden/3 übersprungen, Paket-Tests 8/8 OK – `reports/T-0241/volltest.log`, SHA-256 `cf0ae3fe448cc51e89b5de99bd5b2a84cebd36145e52185c31e39a87ee7d2161`

## Offen
- Herkunftsnachweis (Signatur, erlaubte Herausgeber) – Entscheidung C10 im Entscheidungs-Delta.
- Deklarierte Rechte (`dateien-lesen`, `internet-links`) werden weiterhin nicht durchgesetzt (eigener Punkt aus V112).
