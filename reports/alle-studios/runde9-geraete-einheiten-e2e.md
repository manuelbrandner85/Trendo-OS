# Geräte ohne Namen und mehr Einheiten – 08.11.2026

Alle Studios, Runde 9.

## SYS-07: Geräte ohne Namen benennen (`pci.ids`)
**Vorher:** Geräte, für die Windows keinen Namen liefert (meist fehlt der Treiber), fielen in der Geräteliste komplett weg, obwohl gerade sie interessant sind.

**Jetzt:**
- Trendo liest zusätzlich die Hardware-Kennung (`InstanceId`) und zeigt solche Geräte als „Unbekanntes PCI-Gerät (10DE:2204)“, inklusive Fehlerstatus.
- Sind welche dabei, bietet Trendo **„Namen nachschlagen“** an. Das lädt einmalig `pci.ids` (1,7 MB, BSD-3-Clause, fester Stand `32bce8c6`, SHA-256 geprüft). Dann steht dort „GA102 [GeForce RTX 3090] (NVIDIA Corporation)“ mit dem Hinweis, dass vermutlich der Treiber fehlt.
- Namen von Windows bleiben unverändert; ersetzt werden nur leere und allgemeine („Unbekanntes Gerät“).

**Geprüft:**
- Echte Datei: Prüfsumme stimmt, 2 524 Hersteller, 21 739 Geräte, gelesen in unter 100 ms.
- Echte Ausgabe von `Get-PnpDevice` auf diesem PC: Kennungen haben genau das erwartete Format. Auf diesem PC gibt es kein namenloses Gerät, der Oberflächentest bildet deshalb eines nach.
- Tests: `PciIdsTests` (5), `PciIdsRealTests` (1), `DevicesPci_Ui` (1), alle grün.

## SYS-02: Einheiten
**Änderung gegenüber der Bewertung:** UnitsNet und NCalc waren zunächst ausgewählt. Die Bestandsprüfung hat das gekippt:
- Der eigene Rechner kann Funktionen, Grafiken und Zahlensysteme schon; NCalc brächte nichts.
- UnitsNet kostet 2,6 MB, hat keine deutschen Namen, und im Alltag werden wenige seiner rund 1000 Einheiten gebraucht.

Stattdessen hat die eigene Tabelle jetzt zusätzlich Zeit, Energie (J, kJ, kcal, Wh, kWh), Leistung (W, kW, PS), Druck (Pa, hPa, bar, psi), Datenmengen (KB–TB dezimal, KiB–TiB binär) und Winkel.

**Geprüft** gegen die exakten Definitionen (`UnitsAlltagTests`, 16 Tests, grün). Verschiedene Größen lassen sich weiterhin nicht ineinander umrechnen.

## Nicht geprüft (NOT RUN)
- `pci.ids` auf einem PC mit echtem namenlosen Gerät.
- USB-Geräte ohne Namen (`usb.ids`; die Lizenz der Originaldatei ist noch zu klären).
