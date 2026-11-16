# T-0689: D5a – E-Mail als vollwertiges Programm

**Ziel:** Posteingang und „Schreiben“ (SYS-09, Seiten `posteingang`, `post`) zu einem echten Mailprogramm im Bedienkonzept von Thunderbird/Outlook ausbauen – ohne neue kostenpflichtige Abhängigkeit, Senden nur nach Rückfrage, KI nur als Entwurf.
**Branch:** claude/T-0689-mail (Basis: claude/T-0685-b1-integration)
**Dateien/Bereich:** `src/Trendo.Core/W3/Mail*.cs`, Seiten `post`/`posteingang` (`src/Trendo.App/MainWindow.Mail*.cs`, `MainWindow.Pim.cs` nur Posteingang-Teil, `MailNet.cs`, neue `MailHtmlView.cs`), eine Zeile in `MainWindow.PromptAction.cs` (KI-Entwurf ins Schreibfenster), zugehörige Tests.
**Grundlage:** Audit `docs/audit/2026-11-16-profi-audit/audit-1-buero-business.md` Abschnitt SYS-09; Design-Vorschlag V-21 (nur Mail-Teil: Reiter neu benennen).
**Fertig, wenn:**
- [x] IMAP: Ordnerliste, seitenweise, ganze Nachricht mit sicherer HTML-Ansicht (keine Skripte, externe Bilder erst nach Klick), Anhänge speichern, gelesen/ungelesen, Stern, verschieben, löschen (Papierkorb, endgültig nur mit Rückfrage), Suche, Entwürfe und Gesendet, lokaler Zwischenspeicher (offline lesbar)
- [x] Senden: Text und HTML, Anhänge, Antworten/Allen/Weiterleiten mit Zitat, CC/BCC, Entwurf speichern; korrekte MIME-Kodierung; Senden nur nach Rückfrage, KI nie
- [x] Konto-Assistent mit Anbieter-Tabelle, TLS-Pflicht, Passwort nur in `ISecretStore`; OAuth-Anbieter ehrlich als blockiert
- [x] Oberfläche: drei Spalten, Schreibfenster, eine goldene Hauptaktion, Leerzustand „Konto einrichten“, Strg+N/Strg+R/Entf, Rechtsklick, Datei ziehen = Anhang
- [x] Tests gegen einen echten lokalen Mailserver (IMAP + SMTP über Loopback, TLS)
- [x] KI: `mail.entwurf` ins offene Schreibfenster, „Zusammenfassen“, „Antwort vorschlagen“ – nie senden
**Nicht tun:** `MainWindow.ProgramShell.cs`, Kalender, Kontakte/Adressbuch, Aufgaben, `SimpleWords.cs`; kein Volltest; kein Push außer dem eigenen Zweig.
**Am Ende:** Branch pushen, Bericht `tasks/T-0689-mail.bericht.md`.
