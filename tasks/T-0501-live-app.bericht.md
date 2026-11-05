# Bericht T-0501 – Phase 1 (05.11.2026)

- Module `:shared`/`:app`/`:live` gebaut; Unit-Tests app 17 + shared 7 = alle grün (inkl. Lasttest 14 000 Sender < 1,5 s, Regeltest Zusammenfassen).
- `Trendo Live TV` 1.0.0 neu installiert (Paketvergleich: genau +1 `os.trendo.tv.live`), danach 1.0.1/1.0.2 als Updates; TRENDO OS 0.6.0–0.6.2.
- Übernahme aus Trendo per signaturgeschütztem Provider funktioniert (Quelle „Clubxxc", Xtream, 14 026 Sender, Programmführer geladen).
- **Fehler gefunden und behoben:** In Trendo war dieselbe Quelle dreimal eingetragen (teils gleiche Kennung). Die erste Fassung von `dedupeSources` hat dabei Senderliste und Passwort der behaltenen Quelle gelöscht. Korrektur: nur Kennungen löschen, die nicht behalten werden; Live-App übernimmt bei fehlendem Passwort automatisch neu aus Trendo. Ergebnis am TV: 1 Quelle, 14 026 Sender. Keine Fremd-App betroffen.
- Beobachtet: einmaliger Absturz von `os.trendo.tv` beim Update (Android 11, Start während Paketersetzung, `handleBindApplication`); danach Start ohne Fehler.
- NICHT geprüft: Zappen/Programmführer/Filme in der neuen App, Speicherverbrauch mit 14 000 Sendern über längere Zeit.
