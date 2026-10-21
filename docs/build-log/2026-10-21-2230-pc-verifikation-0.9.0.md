# PC-Verifikation 0.9.0
Setup 0.9.0 (SHA 3c14f26f…) im Projektordner, installiert, `--version` = 0.9.0 (Stand 608b393).
32 Seiten per `--page` auf dem PC gerendert (Brief, W2, W3, W4, W5) – alle öffnen.
`dotnet test` auf dem PC: Lauf 1 (608b393) fand Windows-Fehler: Zip-Sperre im Test, fehlendes pdfinfo, LibreOffice-Exit 1 bei parallelen Exporten → behoben (eigenes LO-Profil pro Export).
Lauf 2 (4180fd0): Core 178/178 inkl. echter OCR (deu nachgeladen, Hash geprüft) und LibreOffice PDF/ODT, Api 14/14. App 65/71 – 6 UI-Tests anderer Wellen scheitern nur auf dem PC (Ollama läuft, Unreal installiert, echte Geräte): X-03, X-10, Unreal-Studio-Klick, W0-Assistent, KI-„Nicht verbunden“, SYS-07 → an W2–W5-Builder.
Befunde: Ollama ja (Port 11434), LM Studio nein, winget ja, 164 PnP-Geräte, Outlook nicht installiert (.eml-Test nicht möglich). Firewall-Abfrage Teilen, Tor/Lesemodus: brauchen Manuels Klick.
