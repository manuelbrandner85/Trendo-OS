# Welle 0 – PC-Nachweise (Trendo OS 0.7.3-preview1, unsigniert)
Setup.exe SHA-256: 0063f423693a7c7c42a254f71009adcc0273ba12c813cc6e5548e3eaf5c265ed (liegt im Projektordner auf dem PC, installiert, Exit 0)

Geprüft auf dem PC:
- Auto-Update 0.7.1 → 0.7.2 über GitHub Releases (update-test.txt)
- SumatraPDF per Benutzer installiert (sumatra-install.txt)
- KI-Empfehlung nach Hardware (31 GB RAM, RTX 5070 11 GB → qwen2.5:3b) (w0-04-ki.png)
- Automatische Sicherung erstellt (w0-05-sicherung.png)
- Systemcheck 0.7.3: 13 von 15 bereit, keine hängenden Prozesse (w0-06-systemcheck-nachher.png; Pfade geschwärzt)
- Designs dunkel / hell / kontrast (w0b-start-*.png)

Nicht oder nur teilweise geprüft (ehrlich):
- Klickpfad des Assistenten: nur im Headless-UI-Test; auf dem PC wurde settings.json direkt geschrieben
- Projektordner fehlt auf dem PC, weil der Assistent dort nicht durchgeklickt wurde
- Update-Prüfung 0.7.3 im Screenshot „noch nicht geprüft“
- Schriften und Browser-Einbindung nur teilweise
- Tastatur-Test im ISO offen
- Code-Signierung: keine (QA-017/QA-019 nicht bestanden)
