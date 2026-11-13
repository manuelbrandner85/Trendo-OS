# Bericht T-0710 Commercial (Phase 1, Phase 2, Desktop Edition)

**Branch:** `claude/T-0710-commercial` im Worktree `T-0710-commercial`. Nicht gepusht, kein PR, kein Merge.

## Commits
- `993c12487`: Phase 1, Audit und Lückenanalyse (nur Dokumente, NuGet-SBOM)
- `94252fce0`: Phase 2, Lizenzkern, Seite Konto/Lizenz, Qwen 3B → 7B, Version 0.9.92
- dieser Commit: Hardware-Kompatibilität, Leistungsprofile, getrenntes Linux-Abbild, Desktop-Dokumente

## Geänderte und neue Dateien
- **Lizenzkern:** `src/Trendo.Core/Licensing/Licensing.cs`. Freischaltcodes sind mit ECDSA P-256 signiert. Lizenzarten Free, Complete, Trial, OwnerGrant, Partner, Team, Enterprise. Free-Studio-Liste als Vorschlag. Geräte-Kennung ist zufällig, kein Fingerabdruck. Schutz gegen zurückgestellte Uhr.
- **Hardware:** `src/Trendo.Core/Compat/HardwareCompat.cs` mit Profilen, Systemcheck, Eignung je Studio, Vorschlägen und Messung. Die Registrierung wird nur gelesen.
- **Seiten:** `src/Trendo.App/MainWindow.Lizenz.cs` und `src/Trendo.App/MainWindow.Hardware.cs`. Angemeldet in `MainWindow.W3.cs`, `MainWindow.Einstellungen.cs`.
- **Vorab-Skript:** `installer/systemcheck.ps1`, nur lesend.
- **Linux-Abbild getrennt:**
  - `.github/workflows/trendo-os-abbild.yml` läuft nur noch von Hand und legt das Abbild im Vor-Release `trendo-os-abbild` ab.
  - `src/Trendo.Core/TrendoImage.cs` lädt das Abbild jetzt aus diesem Kanal.
- **Qwen:** `Dependencies.cs`, `KiSources.cs`, `KiModel.cs`, `ToolCheck.cs`, `MainWindow.axaml.cs`, `MainWindow.Setup.cs`, `THIRD_PARTY.md`, `docs/DEPENDENCY_FEASIBILITY.md`.
- **Version:** `src/Trendo.App/Trendo.App.csproj` von 0.9.36 auf 0.9.92.
- **Programmkatalog:** `docs/programme/katalog.json` neu erzeugt mit `TRENDO_KATALOG=.`, dazu die neuen Ordner `docs/programme/hardware/` und `docs/programme/lizenz/`. Die übrigen 1134 neu erzeugten Programmdokumente wurden zurückgesetzt. Ohne TRX-Datei hätten sie gespeicherte Testergebnisse verloren.
- **Tests:** `LicensingTests.cs` (16), `HardwareCompatTests.cs` (16), `LizenzUiTests.cs`, `HardwareUiTests.cs` und angepasste bestehende Tests (Qwen-Kennung `model-7b`).
- **Dokumente in `docs/commercial/`:**
  - DECISIONS_2026-11-13, PADDLE_ANFRAGE
  - DESKTOP_PRODUCT_STRATEGY, RELEASE_VARIANTS, HARDWARE_COMPATIBILITY, PERFORMANCE_PROFILES, SYSTEM_REQUIREMENTS
  - Ergänzung im COMMERCIAL_MASTERPLAN

## Testergebnisse (Zahlen aus der Ausgabe)

**Volltest 1** (Stand vor der Hardware-Arbeit): Api 16/16, Core 2240 bestanden, 5 übersprungen. App 1207 bestanden, 1 Fehler (SYS-13-P13, Seite „lizenz“ ohne Modul), danach behoben. Paket-Tests 11 OK. Das Rohprotokoll ging durch einen Fehler beim Bereinigen verloren, die Zahlen stammen aus der Laufausgabe.

**Volltest 2** (`reports/t-0710/volltest-2.txt`, SHA-256 `d66f9608b5b24c9b58d9c8c25e8a0a688ef71a8a317642ba624fce45b709a9f1`):
- Api 16/16
- Core 2253 bestanden, 1 Fehler: QA-019, `System.Net.Http` im Core durch die Internetprüfung
- App 1208 bestanden, 1 Fehler: Programmkatalog ohne `hardware` und `lizenz`
- Paket-Tests 11 OK

**Nach den beiden Korrekturen:**
- Internetprüfung in die App verschoben
- Katalog neu erzeugt

Ergebnisse:
- Core vollständig: 2254 bestanden, 0 Fehler, 5 übersprungen
- App gezielt (Programmkatalog, Hardware, Lizenz, SYS-13, Setup/Abbild, W2): 15/15 bestanden

**App-Volltest 3** auf Stand `188714390` (`reports/t-0710/app-volltest-3.txt`, SHA-256 `2377214a40ffaf04ed95907b034cda7705a9c9b4f7b75d0fa77b749ff856cea4`): 1209 bestanden, 0 Fehler, 4 übersprungen, Exit 0, Dauer 40 m 33 s.

Danach kam noch eine Änderung. Für den Oberflächenumbau von Sitzung A (keine Emoji oder Sonderzeichen) zeigt die Hardware-Seite den Status als farbige Punkte statt ✓ • ! ? +. Diese Seite wurde gezielt getestet (Hardware, Lizenz, Programmkatalog): 3/3 bestanden.

**Vorab-Skript:** auf dem Entwicklungs-PC ausgeführt, Ergebnis Profil „Professional“ (vorläufige Grenzen).

## Offene Punkte und Risiken
- Die Profilgrenzen sind **vorläufig** und nicht auf verschiedener Hardware gemessen. Sie dürfen nicht als offizielle Systemanforderungen erscheinen. Die Messreihe ist beschrieben in `PERFORMANCE_PROFILES.md`.
- Von den Leistungsvorschlägen ist nur „kleines KI-Modell“ wirklich angebunden. Vorschau, Parallelität und CPU-Modus sind als Hinweis gekennzeichnet.
- Ohne Serverschlüssel bleibt die Editionsprüfung aus, alle Studios sind frei. Server, Paddle, Konto und Owner Console fehlen. Sie kommen in ein privates Repository, das der Inhaber anlegt.
- **Nach dem Merge** muss der Inhaber den Abbild-Workflow einmal von Hand starten. Bis dahin findet der Stick-Assistent kein Abbild. Das ist eine öffentliche Veröffentlichung und braucht sein OK.
- Das Mitkopieren des Abbilds bei Windows-Releases entfällt nach dem Merge.
- Die Pi-Begleiter hängen weiter an jedem Windows-Release. Darüber entscheidet der Inhaber.
- In `requirements.yaml` nennt GRUNDSATZ noch „Qwen2.5 3B“. Ohne Freigabe bleibt das unverändert.
- `TrendoCore.Version` meldet weiter „0.2.0-p1“ (Statusleiste), unabhängig von der App-Version.
- SSD oder HDD erkennt Trendo selbst nicht, nur das Vorab-Skript.

## Nicht geprüft
- Installation auf frischem Windows 11, Geräte von AMD und Intel, schwache Hardware
- Abbild-Workflow auf GitHub (nicht ausgelöst)
- Linux- und Pi-Editionen

**Status:** umgesetzt und getestet wie oben. Nicht gemergt.
