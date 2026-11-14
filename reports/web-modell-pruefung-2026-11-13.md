# Z6 – Prüflauf lokaler Modelle für Webdesign

Datum: 13.11.2026 21:26. Rohdaten: `web-modell-pruefung-2026-11-13.json`, SHA-256 `83afa5a74bc2a6be4dc245ec0498e938e0c64d6d8a51aec9aaf5a6a8350082da`.

| Modell | Bestanden | Zeit | Befunde |
|---|---|---|---|
| qwen2.5:3b | 1 von 3 | 4 s | Seite aus einem Satz: ungültig: Aktion 2 (baustein_einfuegen): unbekannter Baustein „kopfbereich“.; Gefundenen Fehler beheben: ungültig: Die KI hat keinen Aktionsplan geliefert. |
| qwen2.5-coder:7b | 2 von 3 | 7 s | Seite aus einem Satz: ungültig: Aktion 2 (baustein_einfuegen): „position“ muss eine ganze Zahl von 0 bis 0 sein. |
| mistral:7b | 0 von 3 | 36 s | Seite aus einem Satz: ungültig: Aktion 4 (baustein_einfuegen): „content“ ist ein Bereich für Bausteine und lässt sich so nicht ändern.; Nur die Auswahl ändern: ungültig: Aktion 1: unbekannte Aktion „text_aendern“.; Gefundenen Fehler beheben: ungültig: Aktion 1 (ansicht_entfernen): unbekannte Angabe „ansicht“. |

Empfehlung: Kein geprüftes Modell hat alle Web-Aufgaben bestanden. Lade ein größeres Modell aus der Liste oder nutze Claude über dein Abo.
