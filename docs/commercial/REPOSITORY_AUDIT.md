# Trendo OS – Repository-Audit für den kommerziellen Vertrieb

Phase 1, nur lesend. Stand der Prüfung: 13.11.2026, Commit `7f0f9e3fa1421c8e44be5424dbed84c1c55c62ca` auf `main` (Release 0.9.92). Arbeitszweig für die Berichte: `claude/T-0710-commercial`.

## 1. Ist-Stand

| Merkmal | Befund | Beleg |
|---|---|---|
| Repository | `manuelbrandner85/Trendo-OS`, **öffentlich**, Standardzweig `main` | GitHub-API, `private: false` |
| Lizenzdatei | **keine** (`license: null`, keine `LICENSE` im Wurzelordner) | GitHub-API, Dateiliste |
| Letztes Release | 0.9.92 (Tag `v0.9.92`) | `git tag` |
| Versionsangabe im Projekt | `Trendo.App.csproj` nennt `0.9.36` | Zeile 10 |
| Beitragende laut Git | `manuelbrandner85` 1168 Commits, `Cursor Agent` 61, zweiter Name des Inhabers 1 | `git shortlog -sne` |
| Projekte | Trendo.App (Avalonia), Trendo.Core, Trendo.Api, Trendo.Companion | `Trendo.sln` |
| Tests (letzter Volltest auf `main`) | Api 16, Core 2191, App rund 1180, Paket-Tests 11, alle grün | `reports/t-0700/`, `reports/volltest-0.9.9x` |
| Studios | 59 Kennungen, 37 in der Übersicht nach T-0607 | `StudioCatalog.cs`, Bericht T-0607 |
| Agenten | 20 unter `.claude/agents/` | Ordner |
| CI | nur `trendo-os-abbild.yml` (Linux-Abbild). Kein CI-Lauf für Build, Tests oder Signatur unter Windows | `.github/workflows/` |
| Installer und Updates | Velopack, Updates aus den öffentlichen GitHub-Releases (`TrendoUpdater.cs`, `GithubSource`) | Code |
| Code-Signatur | **keine**. Laut Projektregeln darf sie nie als bestanden gelten | `CLAUDE.md`, `requirements.yaml` |

Auffällig: `installer/release.sh` bricht ab, wenn die Version nicht in `Trendo.App.csproj` steht. Dort steht aber 0.9.36, die Releases tragen 0.9.92. Die Releases entstehen also auf einem anderen Weg. Vor dem ersten Verkauf muss die Version eindeutig aus einer Quelle kommen, weil Lizenzen und Update-Berechtigungen sich darauf beziehen.

## 2. Vorhandene Bausteine, auf die der Verkauf aufbauen kann

| Baustein | Was er heute kann | Eignung für den Verkauf |
|---|---|---|
| `Rbac` (`Trendo.Core/Rbac.cs`) | Rollen Gast, Nutzer, Besitzer auf diesem PC | Grundlage für lokale Rechte. Kein Ersatz für Konten auf einem Server |
| `ApprovalGate` | einmalige, ablaufende Freigaben mit Protokoll, Not-Stopp | gut für kritische Owner-Aktionen im Client |
| `AuditLog` | Protokoll auf diesem PC | für Client-Ereignisse. Der Server braucht ein eigenes, manipulationsgeschütztes Protokoll |
| `Profile` und Personen | mehrere Personen auf einem PC | kann ein Kundenkonto anzeigen, verwaltet aber keines |
| `ProjectStore` | Projektordner und Isolation | bleibt lokal (Local First) |
| `TrendoUpdater` (Velopack) | Updates aus GitHub-Releases | Grundlage für Updates. Update-Berechtigung je Lizenz fehlt |
| Studio „Geld & Rechnungen“ (`Finance`, XRechnung über ZUGFeRD-csharp) | Rechnungen der Nutzer an ihre Kunden | Kundenfunktion, nicht der Verkauf von Trendo selbst. Teile (Dezimalrechnung, XRechnung) sind wiederverwendbar |
| POD-Shop mit `StripeClient` (T-0608) | Shop der Nutzer mit eigenem Stripe-Konto | zeigt, wie Zahlungsdienste im Code angebunden werden. Ist nicht der Trendo-Verkauf |
| `Trendo.Api` | lokale API auf 127.0.0.1 | nicht als öffentlicher Server gedacht |
| Claude-Anbindung (`W2/ClaudeCode.cs`) | jeder Nutzer meldet sich mit seinem eigenen Claude an, Trendo speichert keine Tokens | passt grundsätzlich zum Verkauf, siehe `AI_PROVIDER_COMPLIANCE.md` |

## 3. Matrix der kommerziellen Komponenten

Stufen: geplant, begonnen, implementiert, automatisiert getestet, auf echter Hardware getestet, fachlich abgenommen, verkaufsreif.

| Komponente | Existiert | Stand | Getestet | Verkaufsreif | Maßnahme |
|---|---|---|---|---|---|
| Kostenloser Download | ja, GitHub-Releases | implementiert | Release-Belege | nein (keine Signatur, keine Produktseite) | Produktseite, stabile Downloadadresse, Signatur |
| Installer | ja, Velopack | implementiert | Setup-Läufe laut früheren Berichten | nein (unsigniert) | signieren, Reparatur und Deinstallation prüfen |
| Updates | ja | implementiert | automatisiert | nein (keine Bindung an Lizenz, keine signierten Pakete) | Update-Berechtigung, Signaturprüfung |
| Free Edition | nein, alles ist frei | geplant | – | nein | Umfang festlegen (Entscheidung) |
| Complete Edition | nein | geplant | – | nein | Berechtigungen je Studio |
| Lizenzverwaltung | nein | geplant | – | nein | neu, signierte Berechtigungen |
| In-App-Shop | nein | geplant | – | nein | neue Seite „Konto, Lizenz & Vollversion“ |
| Zahlungsanbieter | nein (Stripe nur für Nutzer-Shops) | geplant | – | nein | Paddle-Sandbox über den Server |
| Kundenkonto und E-Mail-Bestätigung | nein | geplant | – | nein | Server, Einmal-Links |
| Owner Console | nein | geplant | – | nein | Webportal und Seite in der App |
| Kaufbestätigung | nein | geplant | – | nein | E-Mail-Vorlagen in drei Sprachen |
| Offizielle Rechnung | nein | geplant | – | nein | vom Merchant of Record abrufen, nicht selbst erzeugen |
| Finance & Tax Center | nein | geplant | – | nein | Abgleich, Archiv, Export |
| Lizenzserver | nein | geplant | – | nein | eigenes, privates Repository, eigener Server |
| E-Mail-Versand | nein (E-Mail-Studio sendet nicht) | geplant | – | nein | Versanddienst mit SPF, DKIM, DMARC |
| Sprachen Deutsch, Italienisch, Englisch | nur Deutsch | – | – | nein | Projektentscheidung „Trendo nur Deutsch“ vom 02.11.2026 steht dem entgegen, siehe Masterplan |
| Code-Signatur | nein | – | – | nein | Zertifikat beschaffen |
| SBOM | jetzt erstellt für NuGet | begonnen | – | nein | um Assets und externe Programme ergänzen |
| Studio-Abnahmen | 0 von 58 abgenommen | – | automatisiert | nein | fachliche Abnahme je Studio |

## 4. Bewertung

Trendo OS ist als Programm weit, als verkaufbares Produkt steht es am Anfang. Es fehlt alles, was einen Verkauf ausmacht: Lizenz, Konto, Zahlung, Rechnung, Server, Signatur. Dazu kommen drei Grundsatzfragen, die vor jeder Zeile Code für den Verkauf entschieden werden müssen (siehe `COMMERCIAL_MASTERPLAN.md`):

1. Das Repository ist öffentlich und ohne Lizenz. Jeder kann den Quelltext lesen und eine eigene Fassung ohne Lizenzprüfung bauen.
2. Es gibt keinen rechtlichen Verkäufer: kein Unternehmen, keine Steuernummer, kein Konto beim Zahlungsanbieter.
3. Die Oberfläche ist absichtlich nur deutsch. Der Auftrag verlangt drei Sprachen.

Nicht geprüft in diesem Audit: Lauf des Installers auf einem frischen Windows-11-Rechner, Linux- und Pi-Edition, Inhalte aller Dokumente unter `docs/` und `handoff/`.
