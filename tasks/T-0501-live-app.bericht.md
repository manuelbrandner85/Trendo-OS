# Bericht T-0501 – Phase 1 (05.11.2026)

- Module `:shared`/`:app`/`:live` gebaut; Unit-Tests app 17 + shared 7 = alle grün (inkl. Lasttest 14 000 Sender < 1,5 s, Regeltest Zusammenfassen).
- `Trendo Live TV` 1.0.0 neu installiert (Paketvergleich: genau +1 `os.trendo.tv.live`), danach 1.0.1/1.0.2 als Updates; TRENDO OS 0.6.0–0.6.2.
- Übernahme aus Trendo per signaturgeschütztem Provider funktioniert (Quelle „Clubxxc", Xtream, 14 026 Sender, Programmführer geladen).
- **Fehler gefunden und behoben:** In Trendo war dieselbe Quelle dreimal eingetragen (teils gleiche Kennung). Die erste Fassung von `dedupeSources` hat dabei Senderliste und Passwort der behaltenen Quelle gelöscht. Korrektur: nur Kennungen löschen, die nicht behalten werden; Live-App übernimmt bei fehlendem Passwort automatisch neu aus Trendo. Ergebnis am TV: 1 Quelle, 14 026 Sender. Keine Fremd-App betroffen.
- Beobachtet: einmaliger Absturz von `os.trendo.tv` beim Update (Android 11, Start während Paketersetzung, `handleBindApplication`); danach Start ohne Fehler.
- NICHT geprüft: Zappen/Programmführer/Filme in der neuen App, Speicherverbrauch mit 14 000 Sendern über längere Zeit.

## Nachtrag: TV ging in Standby (Bildschirmschoner)
- Meldung Manuel: TV geht ca. 2 min nach Start des Bildschirmschoners ganz aus (Standby), nur mit dem TRENDO-OS-Bildschirmschoner.
- Beleg (logcat 02:53): `DreamActivity` von os.trendo.tv startet 02:53:23 → `am_kill … stop os.trendo.tv due to installPackageLI` 02:53:48 (Claudes Update-Installation) → `proc died` → `power_sleep_requested` → Bildschirm aus. Endet der Prozess des aktiven Dreams, schläft Android sofort ein.
- Abhilfe 0.6.3: `TrendoScreensaver` in eigenem Prozess `:saver` (unabhängig vom Speicher der Trendo-Oberfläche), `FLAG_KEEP_SCREEN_ON`, volle Helligkeit, Höchstdauer wählbar (Standard 4 h, danach regulärer Ruhezustand). Arbeitsregel: nie installieren, während der Bildschirmschoner läuft.
- Nachtest 03:01–03:05 (ohne Installation, ohne Eingaben): `DreamController: Starting dream … TrendoScreensaver`, läuft im eigenen Prozess `os.trendo.tv:saver` (PID getrennt von `os.trendo.tv`); nach 4 min kein `power_sleep_requested`, kein Stopp, TV an. BESTANDEN für 4 min (über die gemeldeten ~2 min hinaus). Langzeit (Stunden) NICHT geprüft.
- Trendo Live TV 1.1.0 installiert (Paketliste unverändert), neues Layout am TV noch nicht per Bildschirmfoto geprüft.
