# E-Mail über den Auftrag

`mail.entwurf` schreibt eine .eml und liest Empfänger, Betreff und Text zurück. `mail.senden` bleibt blockiert: auch „Senden: ja“ und „Freigabe: erteilt“ schicken nichts. Der Entwurf trägt `X-Unsent: 1`.

Bild: `TRENDO_MAIL_PROMPT=1 dotnet run --project tools/Trendo.Render -c Release -- mail-vorschau.png 1280 800 post`
