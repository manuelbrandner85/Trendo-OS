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

## Nachtrag: Live 1.2.x (Phasen 3/4 + Mediatheken + Anbieter)
- Catch-up/Timeshift: stabile Sender-Kennungen (`quelle:x<stream_id>`, M3U aus Adresse), `archiveDays`/Catch-up-Vorlage aus Xtream (`tv_archive`), Player: Pause/Weiter (setzt bei Archiv exakt fort), ◀◀/▶▶ ±1 min, Optionen „Sendung von Anfang an", „5 Minuten zurück", „Zurück zu Live"; Programmführer zeigt bei Archiv die letzten 2 h, ⏮ = Nachholen. Zeitzone für Catch-up = Zeitzone des TVs (bei Xtream üblich, mit echtem Anbieter NICHT geprüft).
- Heute-Tipps (`Tips`): Jetzt live Sport, Filme jetzt & gleich, Heute Abend 20:15, Sport heute, Serien, Nachrichten, Kinder; Favoriten zuerst; Kinder-Modus nur Kinder.
- Sprachsteuerung (`LiveVoice`, lokal): „Schalte auf …", „Was läuft heute Abend für …", „Erinnere mich an …", Programmführer/Filme/Serien, Suche. Spracherkennung des TVs per `RecognizerIntent`. Am TV NICHT gesprochen getestet.
- Neuer Reiter „Mediatheken & Streaming": ARD, ZDF, arte, 3sat, phoenix, KiKA, die Dritten über MediathekViewWeb (frei; Videos direkt von den Sendern, teils nur DE), Rubriken Neu/Filme/Serien/Dokus/Krimis; kostenlos mit Werbung (Pluto TV, Rakuten TV, Plex, Joyn) und bezahlte Dienste als Kacheln → offizielle App (Erkennung am Namen, Original-Banner vom Gerät) oder Play-Store-Suche. Keine fremden Logos im Repo.
- Anbieter-Sortierung (`Providers`): Filme/Serien mit Anbieter-Auswahl (Netflix, Disney+, Prime Video, Apple TV+, Sky, …), Live-Gruppen „▸ Anbieter". Kategorienamen werden einmalig (nur Namen) als `TrendoLiveKat` protokolliert, um die Zuordnung an die echte Liste anzupassen – noch NICHT ausgelesen (App seit Installation nicht geöffnet).
- Unit-Tests 32/32 grün. Live 1.2.1 installiert, während der TRENDO-Bildschirmschoner lief: Dream lief weiter (`os.trendo.tv:saver` aktiv), kein Ruhezustand.
