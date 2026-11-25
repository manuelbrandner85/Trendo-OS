# T-0767: Handy verbinden – Datei senden in beide Richtungen, Entfernen sperrt, Lauf aufzeichnen

**Ziel:** CORE-04/X-08 „Handy verbinden“ (Restliste Rang 40) bekommt „Datei senden“ PC ↔ Android-Handy über das Heimnetz ohne App, sofortige Sperre beim Entfernen und ein anonymisiertes Lauf-Protokoll für den echten Handy-Lauf des Nutzers.
**Branch:** claude/T-0767-handy
**Dateien/Bereich:** `src/Trendo.Core/W7/PhoneTransfer.cs` (neu), `src/Trendo.App/PhoneLinkServer.cs`, `src/Trendo.App/MainWindow.PhoneEasy.cs`, `src/Trendo.App/MainWindow.PhoneSend.cs` (neu), `src/Trendo.App/MainWindow.Phone.cs`, `tools/Trendo.Render/Program.cs`, Tests, `reports/T-0767/`.
**Fertig, wenn:**
- [x] Kopplung per QR-Code über den vorhandenen Handy-Zugang (`PhoneAccess` + `PhoneLinkServer`, keine zweite Engine); Entfernen sperrt sofort
- [x] Handy → PC: Datei aus dem Handy-Browser, Name gesäubert, Größenlimit, erst vollständig ablegen, SHA-256 am Handy und am PC
- [x] PC → Handy: „Datei senden …“ (Knopf, Rechtsklick, Ziehen und Ablegen) nur für das gewählte Handy, 24 Stunden
- [x] Kabel-Weg nur, wenn adb schon auf dem PC liegt; sonst ehrlich „nicht verfügbar“
- [x] „Lauf aufzeichnen“ schreibt Bericht, Rohlog und Prüfsummen ohne Gerätenamen und Adressen
- [x] Ende-zu-Ende mit echtem Chrome (headless) als „Handy“ über die Heimnetz-Adresse, Beleg unter `reports/T-0767/`
- [ ] Echter Lauf mit Android-Handy – macht der Nutzer
**Nicht tun:** Firewall-Regeln anlegen, etwas installieren, Handy steuern, Dienste außerhalb des Heimnetzes, Raspberry Pi (nicht im Angebot), Volltest (macht die Hauptsitzung).
**Am Ende:** Branch pushen, Bericht `tasks/T-0767-handy.bericht.md`.
