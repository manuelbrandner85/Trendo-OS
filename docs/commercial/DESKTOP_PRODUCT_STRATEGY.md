# Produktstrategie: Trendo OS als Desktop-Software für Windows 11

Stand 13.11.2026. Grundlage: Auftrag „Commercial Desktop Edition & Hardware Compatibility“ des Inhabers vom 13.11.2026, weitergeleitet aus der Sitzung „Spiele-Studio Stufe 1“.

## Festlegungen

1. Trendo OS wird zuerst als professionelle Desktop-Software für **Windows 11** verkauft. Der Name „Trendo OS“ bleibt.
2. Trendo OS wird **nicht als Ersatz für Windows** beworben. Die Software läuft unter Windows und nutzt Windows.
3. Kommerzielles Hauptprodukt ist die normale Installation auf der internen SSD oder Festplatte:
   - Installer kostenlos von der Verkaufswebseite
   - Free Edition, Complete Edition kaufbar (siehe `DECISIONS_2026-11-13.md`)
   - automatische Updates (Velopack)
   - kein Raspberry Pi und kein bootfähiger USB-Stick nötig
4. Die Zusatzvarianten bleiben im Repository und werden weiterentwickelt, gehören aber vorerst nicht zum Verkaufsangebot: bootfähiges Linux auf USB/SSD, Raspberry-Pi-Verbund, Windows-Portable. Siehe `RELEASE_VARIANTS.md`.
5. Vor dem Kauf zeigt Trendo in der Free Edition, welche Studios auf diesem PC laufen, welche eingeschränkt laufen und welche Zusatzprogramme oder stärkere Hardware brauchen. Siehe `HARDWARE_COMPATIBILITY.md`.
6. Es wird nicht behauptet, dass alle professionellen Funktionen auf jedem PC gleich schnell laufen.

## Werbeaussagen: erlaubt und nicht erlaubt

| Erlaubt | Nicht erlaubt |
|---|---|
| „Desktop-Software für Windows 11“ | „Ersetzt Windows“, „neues Betriebssystem statt Windows“ |
| „Systemcheck vor dem Kauf“ | „Läuft auf jedem PC“ |
| „Anforderungen je Studio“ | Systemanforderungen, die nicht gemessen wurden |
| „Arbeitet mit deinem eigenen Claude-Konto“ | „Mit Claude inklusive“ |

## Folgen für den Masterplan

- Phase 3 (Free/Complete) zeigt die Hardware-Eignung je Studio neben dem Kaufknopf.
- Phase 8 (Qualität) misst die Leistungsprofile auf echter Hardware. Erst danach stehen offizielle Systemanforderungen fest.
- Phase 10 (Release) veröffentlicht nur noch Windows-Pakete im Kundenkanal. Linux-Abbilder laufen getrennt.
