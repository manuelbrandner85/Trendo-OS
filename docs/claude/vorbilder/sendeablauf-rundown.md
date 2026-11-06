# Vorbild-Analyse Sendeablauf: Rundown-Planer, OBS (Clean-Room)

- Programme: „Live streamen“
- Recherche: Ablaufpläne im Rundfunk (Rundown mit Beginn, Dauer, Hinweisen, Back-Timing), On-Air-Uhren mit Countdown, OBS Studio (Szenensammlungen)
- Werkzeug: Funktionsumfang aus öffentlicher Doku; kein Download, kein Code übernommen. REA nicht eingesetzt.

## Übernommen in Trendo „Sendeablauf“ (0.9.28)
Tabellarischer Ablauf mit mitrechnenden Beginnzeiten, Über-/Unterlänge zum Sendeplatz, Zeitleiste, On-Air-Uhr mit Countdown und „Danach“, Moderationskarten, Kapitelmarken, OBS-Szenen je Programmpunkt.

## Studio wie OBS (Profi 27 und Claude 25, 05.11.2026)
- Modus „🎥 Studio“: Szenen links (Startbesetzung „Gleich geht's los“, Kamera, Bildschirm, Pause; eigene anlegen), Programmbild groß, Quellen je Szene mit Einblendungen (Bauchbinde, Bild/Logo, zweite Kamera als Bild im Bild, ein-/ausblendbar)
- Tonmischer: Mikrofon und Hintergrundmusik (in Schleife) je mit Lautstärke in dB, Stumm und Pegel (EBU R128, momentan) während Probelauf, Aufnahme und Sendung
- Ton-Wache: über 5 s Stille bei offenem Mikrofon oder Übersteuern → sofortige Warnung
- Probelauf vor dem Senden: kurze Probeaufnahme, dann Ampel für Bild (schwarz?), Ton (still, leise, übersteuert) und Datenrate mit nötigem Upload
- Sendeplan: Szenen-Fahrplan „mm:ss Szene – Notiz“, aus dem Ablauf übernehmbar, „Jetzt/Als Nächstes“ während der Sendung, Umschalten per Klick
- Szenenwechsel: Vorschau und Aufnahme schalten sofort um (Aufnahme in neuer Datei); während der Sendung braucht der Wechsel ein „Ja“ und der Stream startet 1–2 s neu – Trendo mischt mit FFmpeg, nicht im eigenen Speicher wie OBS
- Claude als Regie: sieht Szenen, Ton, Ton-Wache und Probelauf, schlägt Szenen und Sendeplan vor; Plan mit Vorschau, erst „Ausführen“ übernimmt; senden macht immer der Nutzer

## Noch nicht
Back-Timing auf festes Sendeende, Teleprompter, Mehrbenutzer-Ablauf, Steuerung von OBS per WebSocket, Szenenwechsel ohne Neustart während der Sendung.
