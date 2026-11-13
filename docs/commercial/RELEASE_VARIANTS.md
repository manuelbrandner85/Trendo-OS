# Release-Varianten

Stand 13.11.2026.

| Variante | Bau | Veröffentlichung bisher | Ab jetzt | Verkaufsangebot |
|---|---|---|---|---|
| **Windows-Installation** (Setup.exe, Velopack) | `installer/build-setup.sh` | GitHub-Release `vX.Y.Z` über `installer/release.sh` | unverändert, Kundenkanal | **ja, Hauptprodukt** |
| Windows-Portable (ZIP) | `installer/build-package.ps1` | nicht automatisch | unverändert, nur auf Anfrage | nein, vorerst |
| Bootfähiges Linux (USB/SSD) | `linux-image/build.sh`, Workflow `trendo-os-abbild.yml` | automatisch an **jedes** veröffentlichte Release gehängt, ab 0.9.85 zusätzlich als Brücke aus dem Vor-Release mitkopiert | **getrennt**: eigener Vor-Release-Kanal `trendo-os-abbild`, nur per Hand gestartet | nein, vorerst |
| Raspberry-Pi-Begleiter (`trendo-companion-linux-*.tar.gz`) | `installer/companion/build.sh` | in jedem Release über `release.sh` | unverändert, siehe offene Frage | nein, vorerst |

## Trennung des Linux-Abbilds (umgesetzt auf `claude/T-0710-commercial`)

- Der Workflow `trendo-os-abbild.yml` startet nicht mehr bei jedem veröffentlichten Release. Er läuft nur noch von Hand (`workflow_dispatch`) mit dem Tag, aus dem gebaut wird.
- Das Abbild landet im eigenen Vor-Release `trendo-os-abbild`. Der Workflow legt es an, falls es fehlt. Ein Vor-Release ist nie „latest“, deshalb sehen Windows-Kunden es nicht als neueste Version und der Windows-Updater bleibt unberührt.
- Der Stick-Assistent in Trendo (`TrendoImage.LatestBase`) lädt das Abbild jetzt aus `releases/download/trendo-os-abbild/` statt aus dem neuesten Release.
- Das Mitkopieren des Abbilds aus dem Vor-Release beim Windows-Release ist damit nicht mehr nötig. Die Memory-Regel dazu gilt erst nach dem Merge als überholt.
- Nichts aus `linux-image/`, `installer/companion/` oder der Linux-Edition wurde gelöscht.

**Voraussetzung nach dem Merge:** Der Inhaber startet den Workflow einmal von Hand, damit der Kanal `trendo-os-abbild` existiert. Bis dahin findet der Stick-Assistent kein Abbild. Das ist eine öffentliche Veröffentlichung und braucht seine Freigabe.

## Offene Frage

Die Raspberry-Pi-Begleiter hängen weiter an jedem Windows-Release. Laut Code (`src/Trendo.Companion`) holt der Begleiter keine Updates selbst von GitHub, man lädt das Paket von Hand. Ein eigener Kanal wie beim Abbild wäre also ohne Programmänderung möglich. Ob die Begleiter umziehen, entscheidet der Inhaber. Bis dahin bleibt `release.sh` unverändert.
