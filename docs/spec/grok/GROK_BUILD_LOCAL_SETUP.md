# Grok Build lokal einrichten – bewusst Schritt für Schritt

**Das Paket installiert nichts und startet keine Befehle selbst.** Die folgenden Schritte muss ein berechtigter Nutzer am eigenen PC durchführen.

1. Trendo-Produktionspaket entpacken und in einen NEUEN, ausdrücklich für Trendo OS autorisierten Projektordner legen. Bestehende Spiel- oder Firmen-Repositories NICHT ersetzen.
2. Offizielle Anleitung öffnen: https://docs.x.ai/build/overview (Windows: die dort angebotene **Windows (PowerShell)**-Installationsvariante verwenden; Linux/WSL: passende Variante). Installationsskripte vor Ausführung prüfen.
3. Grok Build nach offizieller Anleitung installieren, `grok` im Terminal starten, Benutzerkonto anmelden. Abrechnung/Nutzungsplan prüfen; keine kostenlosen KI-API-Zugänge behaupten.
4. Im freigegebenen Trendo-Projektverzeichnis `grok inspect` ausführen. Verfügbare AGENTS.md, lokale Projektkonfiguration, MCP-/Plugin-Quellen und Berechtigungen kontrollieren.
5. `grok mcp doctor` soweit verfügbar prüfen. Anfangs KEINE Blender-/Unreal-MCP-Server aktiviert behaupten.
6. Im lokalen Grok-Build-Chat `GROK_BUILD_MASTERPROMPT.md` als ersten Hauptauftrag übergeben. Zunächst nur P0-Plan-/Inventur, keine destruktiven Befehle, keine Windows-/Firmwareupdates.
7. In Grok Bot `GROK_BOT_PROJEKTLEITER_PROMPT.md` übergeben. Lokale Ausführung zunächst **Ask every time** oder deaktiviert lassen; keine Konto-/Tokenübertragung. Bot und Build haben nicht automatisch eine direkte Session.
8. Für erste sichere Übergabe den Aufgabenauftrag als Markdown lokal von dir oder per ausdrücklich genehmigter Verbindung bereitstellen. Danach Diff/QA von Grok Build prüfen.
9. Erst später nach genehmigtem Projektplan spezielle Blender-/UE5-Adapter installieren, in Grok registrieren und in isolierten Testprojekten verifizieren.

## Sicherheits- und Produktgrenzen
- Grok Bot: Cloud/Dateien/Browsersitzungen kontoübergreifend zwischen eigenen Bots gemeinsam. Keine Geheimnisse/Beweiskopien dorthin senden.
- Grok Build läuft lokal, kann aber für Modellzugriff einen Online-Dienst nutzen. Vertrauliche Projekte dürfen nicht ohne Prüfung der eigenen Konto-/Datenbedingungen verarbeitet werden.
- Auf Windows echte Freigaben für Prozesse/Adminaktionen; auf Linux/USB kein ungefragter Zugriff auf internes Windows-Laufwerk.
- Die Endsoftware Trendo OS wird durch das Entpacken dieses Pakets NICHT installiert.

## Offizielle technische Referenzen (zuletzt geprüft 21.10.2026)
https://docs.x.ai/build/overview
https://docs.x.ai/build/settings
https://docs.x.ai/build/features/mcp-servers
https://docs.x.ai/build/features/permissions
https://docs.x.ai/grok-bot/approvals-security-and-privacy
