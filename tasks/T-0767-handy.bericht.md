# Bericht T-0767: Handy verbinden – Datei senden, Entfernen sperrt, Lauf aufzeichnen

**Auftrag:** CORE-04/X-08 (Restliste Rang 40), Audit `audit-5-entwickeln-system.md` Abschnitt CORE-04/X-08; Vorbild-Notiz `geraete-phonelink-kdeconnect.md` („Datei senden“ fehlte). Nur Android, kein Raspberry Pi (Desktop Edition).
**Branch:** `claude/T-0767-handy` (Basis main = Release 0.9.109), Kopie je Auftrag in `%TEMP%\trendo-auftrag-T-0767`.

## Was war → was ist jetzt
1. **Kopplung per QR:** war vorhanden (`PhoneAccess` Einmal-Code 10 min, Schlüssel nur als SHA-256, `PhoneLinkServer` nur Heimnetz/Tailscale, Host-Prüfung). Unverändert genutzt, keine zweite Engine. Entfernen sperrte schon sofort (Schlüssel wird je Anfrage geprüft); neu: Entfernen löscht zusätzlich alles, was nur für dieses Handy bereitlag, und wird protokolliert. Kanal im WLAN bleibt unverschlüsseltes HTTP im Heimnetz (vorhandenes Schema), verschlüsselt nur unterwegs über Tailscale.
2. **Datei Handy → PC:** war `ReadFormAsync` + `File.Create` direkt unter dem Endnamen (halbe Datei nach Abbruch, Windows-Gerätenamen wie `CON` nicht abgefangen, ASP.NET-Formularlimit 128 MB statt der genannten 2 GB). Jetzt `PhoneTransfer.ReceiveAsync`: Datenstrom direkt (MultipartReader), versteckte Teil-Datei, Größenlimit je Datei (Standard 2 GB, 413), Name gesäubert (Pfad, Sonderzeichen, CON/NUL/COM1…, 120 Zeichen), SHA-256. Die Handy-Seite bildet vor dem Senden selbst SHA-256 (reines JavaScript, weil `crypto.subtle` über http im Heimnetz fehlt; bis 64 MB je Datei) – der PC vergleicht und meldet „Prüfsumme gleich“.
3. **Datei PC → Handy:** war nur „Datei in den Ordner ‚Zum Handy‘ legen“ (alle Handys sehen alles, unbegrenzt). Jetzt je Handy „Datei senden …“ (Knopf, Rechtsklick-Menü, Datei auf die Handy-Karte ziehen): Kopie liegt 24 Stunden nur für dieses Handy bereit (versteckter Unterordner), andere Handys sehen und laden sie nicht; Auslieferung selbst gestreamt mit SHA-256 der tatsächlich gesendeten Bytes. Gemeinsamer Ordner „Zum Handy“ bleibt (keine Funktion entfällt). Handy-Seite: „Aktualisieren“, alle 15 s neu, zeigt nach dem Entfernen „nicht mehr verbunden“.
4. **Kabel-Weg:** nur, wenn adb schon auf dem PC liegt. Neue Statuszeile „Kabel-Weg möglich / nicht verfügbar“ (gleiche adb-Suche wie „Handy suchen“). Kabel-Senden und „Neuestes Foto holen“ werden im Lauf-Protokoll mit SHA-256 mitgeschrieben. Nichts installiert.
5. **Lauf aufzeichnen:** neu (`PhoneRunLog`, Karte „Lauf aufzeichnen (Nachweis)“). Schreibt in den Ordner „Handy-Protokolle“ Bericht (Markdown), Rohlog (JSON Lines) und `.sha256`; anonym („Gerät 1“, Netzart statt Adresse, keine Datei- und Gerätenamen); bewertet Kopplung, beide Richtungen mit Prüfsumme, Entfernen, Abweisung danach.
6. Handy-Seiten (Kopplung/Startseite) auf Gold statt Blau (Leitsatz Dunkel-Gold). Die bildschirmfüllende Handy-App (`/app`) blieb unverändert.

## Tests (gezielt, kein Volltest)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core: `PhoneTransferTests|PhoneAccessTests|W7PhoneLinkTests|W7Tests|NoFixedUserTests` → Bestanden, 45 erfolgreich, 5 übersprungen (Linux-only), 0 Fehler. Davon neu `PhoneTransferTests` 13.
- App: `PhoneSendT0767Tests` + `PhoneEasyTests` → Total 10, Failed 0, Skipped 0. `W7_Ui` → Total 6, Failed 0, Skipped 1 (Linux-only). Vorher-Lauf mit `AppsAndSetupResumeTests, DesignPaket1_Ui, GameRelease_Ui, ProgramFrameTests, T0201_Dateiauswahl_Ui, W7_Ui, PhoneEasyTests, PhoneSendT0767Tests`: 65 Tests, 1 Fehler (`PhoneEasyTests.Remote_drives_ui` – verursacht durch meine erste Fassung der Kabel-Statuszeile, die die Seite neu aufbaute; behoben, danach grün).
- Neu `PhoneSendT0767Tests`:
  - `Server_flow_with_run_log` – echter Kestrel, HttpClient als Handy: Prüfsumme gleich/abweichend, 413 ohne Rest, PC→Handy nur für Handy A, Entfernen → 401, Protokoll vollständig.
  - `Ui_send_remove_record` – Klick „Lauf aufzeichnen“ → „Datei senden …“ (Dateiauswahl ersetzt über `PickOverride`) → „Entfernen“ → „Aufzeichnung beenden“ → Bericht ohne Gerätenamen. adb hier bewusst als fehlend gesetzt (Anzeige „nicht verfügbar“).
  - `Ui_cable_real_adb` – **echtes adb** dieses PCs: Anzeige „Kabel-Weg möglich“, echtes `adb devices -l` (ohne Handy leere Liste), eigener adb-Server danach beendet.
  - `Real_browser_end_to_end` – **echtes Chrome headless** als „Handy“ über die Heimnetz-Adresse dieses PCs: QR-Link → Verbinden → Datei hoch (1,5 MB, SHA-256 im Browser = PC) → Datei runter (0,9 MB, Download im Browser, SHA-256 gleich) → Entfernen → Liste und Upload abgewiesen. PC-Seite hier über dieselben Kernfunktionen (`PhoneTransfer.OfferAsync`, `PhoneAccess.Remove`), nicht über die Knöpfe (die prüft `Ui_send_remove_record`).
- Belege: `reports/T-0767/` (README, `browser-lauf.log`, `handy-lauf-*.md/.jsonl/.sha256`, `adb-pruefung.log`, `SHA256SUMS.txt`).
- Oberfläche im Bild geprüft (`Trendo.Render`, neu `TRENDO_PHONE_DEMO=1|lauf`): Handy-Karte mit „Datei senden …“, Lauf-Karte in beiden Zuständen, eine goldene Hauptaktion je Ansicht bleibt („Zuhause (im selben WLAN)“).

## Abnahmekriterien
1. Echtes Ergebnis über die Oberfläche: **TEILWEISE** – Kopplung, Dateien in beide Richtungen und Protokoll real mit Browser-Gegenstelle; echtes Android-Handy steht aus.
2. Fremddatei/Rundreise: **TEILWEISE** – zufällige Binärdateien bitgenau hin und zurück (SHA-256); Öffnen in einer Handy-App nicht geprüft.
3. Abbruch verliert ≤ 30 s: **TEILWEISE** – abgebrochene Übertragungen hinterlassen nichts Halbes (Teil-Datei); laufende Aufzeichnung liegt nur im Speicher bis „beenden“.
4. KI-Fachaufgabe: **FEHLT** (nicht Teil dieses Auftrags; „handy.nennen“ unverändert).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** für PC-Seite (Klick → bereitgestellte Datei → Bericht) und Browser-Seite (Auswahl → Datei am PC).
6. Bedienung: **TEILWEISE** – Rechtsklick-Menü und Ziehen-und-Ablegen auf der Handy-Karte neu; Tastenkürzel keine; Einfach/Profi unverändert.

## Nicht geprüft
- Echtes Android-Handy (WLAN und Kabel) – macht der Nutzer.
- Windows-Firewall-Abfrage bei Zugriff von einem anderen Gerät; Trendo ändert keine Regel selbst (der vorhandene Knopf „Windows-Freigabe“ bleibt Sache des Nutzers).
- Ziehen und Ablegen nur gebaut, nicht im Test ausgelöst (Avalonia headless).
- Dateien > 64 MB: Prüfsumme nur am PC.

## Offene Punkte
- Echter Handy-Lauf mit „Lauf aufzeichnen“, Protokoll nach `reports/T-0767/` kopieren.
- Im Render-Werkzeug meldet der Kabel-Weg „nicht verfügbar“, obwohl adb in `TrendoWerkzeuge\scrcpy` liegt – die adb-Suche (`ResolveAdbAsync`, unverändert) findet es dort im isolierten Render-Lauf nicht; in der echten App mit `AdbLocator` belegt, ohne Override nicht geprüft.

**Status:** umgesetzt, gezielt getestet, echter Handy-Lauf ausstehend.
