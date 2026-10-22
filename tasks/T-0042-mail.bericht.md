# T-0042 E-Mail

**Ziel:** Entwurf als echte EML, Versand ohne Postfach bleibt bei null Nachrichten.
**Branch:** cursor/v42-mail-fa7f
**Dateien:** `MailPromptAdapter.cs`, Registrierung, Matrix, Render-Aufruf.

## Testergebnis

`Mail_entwurf_wird_gelesen`, `Mail_senden_schickt_nichts`, `Mail_adresse_ungueltig` und die Matrix sind grün. Der Gesamtlauf folgt.

## Offene Punkte

- Kein SMTP und kein Mailkonto. Eine Freigabe kann deshalb nichts abschicken.

## Nicht geprüft

- Kein Windows-PC, keine Codesignatur, kein QA-017, kein QA-019.
