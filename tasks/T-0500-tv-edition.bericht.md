# Bericht T-0500 – TV-Edition M1 (Stand 05.11.2026)

**Auftrag:** `tasks/T-0500-tv-edition.md` · **Branch:** `claude/T-0500-tv-edition` (lokal, nicht gepusht)

## Geänderte Dateien
`tasks/T-0500-tv-edition.md`, dieser Bericht, `tv/android/**` (neu), `reports/tv/**` (Belege). Windows-/Linux-/Pi-Code unverändert.

## Werkzeuge (nutzerlokal, keine Systeminstallation)
Android SDK unter `C:\Users\<user>\TrendoEntwicklung\android-sdk`: cmdline-tools 23.0.0 (SHA-1 geprüft), platform-tools 37.0.1, platforms;android-35, build-tools 35.0.0; Android-SDK-Lizenz nach Freigabe angenommen. Gradle 8.11.1 (SHA-256 geprüft) nur zum Erzeugen des Wrappers. Signaturschlüssel lokal außerhalb des Repos.

## Kommandos und Ergebnisse
| Schritt | Ergebnis |
|---|---|
| `gradlew testDebugUnitTest assembleDebug` | BUILD SUCCESSFUL; AppCatalogTest 4/4, AppGuardTest 3/3; Log `reports/tv/T-0500-m1-build-2026-11-05.log` (SHA-256 `1a7b997a…dcc597e`) |
| `gradlew testReleaseUnitTest assembleRelease` 0.1.0 | BUILD SUCCESSFUL, APK 1 549 850 Byte, SHA-256 `8082ac7b…d959fd20`, Zertifikat SHA-256 `a9039051…6a32f83` |
| Installation 0.1.0 auf CHiQ (nach Freigabe) | `Success`; Paketvergleich vorher/nachher: **genau +1** (`os.trendo.tv`), deaktivierte Pakete unverändert, Standard-Home unverändert `com.google.android.tvlauncher` |
| Start am CHiQ | `am start -W`: Status ok, TotalTime 1370 ms; kein Eintrag im Crash-Puffer; Screenshot `reports/tv/T-0500-chiq-start-2026-11-05.png` |
| Speicher | Prozess ca. 74 MB, nach Wechsel in den Hintergrund vom Hersteller-LMK beendet (normal bei 1,35 GB RAM) |
| 0.1.1 (Startfokus) | Release gebaut, SHA-256 `b9454989…f07a7a`; **noch nicht installiert** |

## Funktionsstatus
| Nr | Funktion | Status |
|---|---|---|
| 01 | TV & Streaming Hub (Basis) | REAL IMPLEMENTIERT, AUF CHiQ GESTARTET (Kacheln sichtbar; App-Start per Fernbedienung noch nicht geprüft) |
| 04 | Smart App Organizer (Basis) | REAL IMPLEMENTIERT, AUF CHiQ SICHTBAR (TiviMate + Play Store als Favoriten) |
| 10 | Recovery (Teil: „Alles rückgängig", Original-Startseite) | REAL IMPLEMENTIERT, nicht auf CHiQ ausgelöst |
| 11 | Ad-Free Launcher | REAL IMPLEMENTIERT (keine Werbe-/Analytics-SDKs) |
| 22 | Focus Navigation | 0.1.0 ohne Startfokus (Fehler gefunden), Korrektur in 0.1.1 gebaut |
| alle übrigen | GEPLANT |

## Offen / Risiken / nicht geprüft
- Emulator-Test BLOCKIERT (keine Hardware-Beschleunigung auf dem PC).
- Fernbedienungs-Durchlauf, Start von TiviMate/Play Store aus Trendo, Offline-Test, 60-min-Leerlauf, 5 Neustarts: NICHT geprüft.
- Paketliste nur lokal gesichert (Scratchpad), nicht im Repo.
- `dumpsys` am CHiQ eingeschränkt („Service dumps disabled").
- Kein Push, kein PR, kein Merge, kein Release.
- Löschwunsch Disney+/Kodi/AI Pont/Google Play Spiele: nicht ausgeführt (Datenverlust, Übergabe verbietet Fremd-App-Deinstallation); Anleitung zum Selbstmachen an Manuel gegeben.

## Nachtrag 05.11.2026 – Design-Umbau 0.2.x (nach Vorlage von Manuel)
- Oberfläche neu: Seitenleiste (11 Bereiche), Kopfzeile mit prismatischem T, „by Manuel" in Schreibschrift (Great Vibes, SIL OFL 1.1, Lizenz in `tv/android/THIRD_PARTY_FONTS_OFL_GreatVibes.txt`), Suchfeld, Uhr + Datum, Online-Anzeige, Titelbild, „Meine Apps", 6 Themenkarten, 11 Werkzeuge, farbiger Randschein (statisch).
- Bilder: 7 eigene Bilder lokal mit ComfyUI + FLUX.2 klein erzeugt (keine Fremdfotos, keine Filmcover), als WebP zusammen 133 KB.
- Ehrlich weggelassen: Wetter, Profilbild (keine Datenquelle). Smart Home, Trendo AI, Timer: Seite „Geplant".
- Echte Funktionen: App-Start, Kategorien (Video/Audio/Spiel/Bild laut App), Suche, HDMI-Eingänge über TvInputManager (CHiQ meldet Composite, HDMI 1–3; Umschalten NICHT getestet), Performance-Werte (RAM/Speicher), Android-Einstellungsseiten, Original-Startseite, „Alles rückgängig".
- 0.2.0 und 0.2.1 jeweils als Update von `os.trendo.tv` aufgespielt; Paketliste vorher/nachher unverändert, Standard-Home unverändert. Start: Status ok, TotalTime 3728 ms (0.2.0) / 3424 ms (0.2.1); kein Crash-Eintrag.
- 0.2.1 APK: 2291645 Byte, SHA-256 `cfa43bd313a11a8b82a4f0b1116d2ceedfeaf1260db11f50d3d2e0681512f526`. Screenshots `reports/tv/T-0500-chiq-v020-start.png`, `reports/tv/T-0500-chiq-v021-start.png`.
- NICHT geprüft: Fernbedienungs-Durchlauf (dumpsys am CHiQ gesperrt → Vordergrund nicht feststellbar), Unterseiten am TV, HDMI-Umschaltung, Suche mit Bildschirmtastatur.
- Kaltstart 3,4 s ist langsamer als 0.1.0 (1,4 s) – Bilder und Symbole; Optimierung offen.

## Nachtrag 05.11.2026 – Startseiten-Modus 0.3.0 (Wunsch: Trendo als Startseite, nicht als App)
- Offizieller Weg: eigener Eintrag `HomeAlias` wird erst bei „Trendo als Startseite festlegen" eingeschaltet (nur eigenes Paket), dann Android-Dialog `RoleManager.ROLE_HOME`; Ausweg `Settings.ACTION_HOME_SETTINGS`.
- Original-Startseite bleibt erreichbar: Kachel startet die fremde Home-App ausdrücklich (nicht über HOME-Intent, der sonst auf Trendo zeigen würde).
- Home-Taste springt auf „Start"; Zurück-Taste schließt Trendo als Startseite nicht.
- Rückweg: Einstellungen → „Standard-Startseite ändern (zurück zu Google)" oder „Alles rückgängig" (Trendo entfernen → Android nimmt automatisch wieder die Google-Startseite).
- 0.3.0 aufgespielt, Paketliste unverändert. Dialog „TRENDO OS als standardmäßige Start-App festlegen?" erscheint am CHiQ (Screenshot geprüft). Bestätigung durch den Nutzer am TV: AUSSTEHEND (nach 3 min weiterhin `com.google.android.tvlauncher`).

## Nachtrag 05.11.2026 – Startseite auf dem CHiQ (0.3.1–0.3.3)
- Befund: Android-Dialog „Standard-Start-App" erscheint und die Wahl wird gespeichert (Trendo löst intern als Standard auf), die Home-Taste startet trotzdem `com.google.android.tvlauncher` (HOME-Filter Priorität 2, Trendo 0). Die Google-Startseite wird NICHT abgeschaltet (Übergabe: Default-Launcher geschützt).
- Lösung: `HomeGuardService` (Bedienungshilfe, vom Nutzer einzuschalten). Empfängt nur `TYPE_WINDOW_STATE_CHANGED` der Original-Startseite(n) (`packageNames` zur Laufzeit gesetzt), `canRetrieveWindowContent=false`, holt dann Trendo nach vorne. Schleifenschutz max. 3 Sprünge/10 s. „Original-Startseite" in Trendo pausiert den Wächter bis zum nächsten Öffnen von Trendo.
- CHiQ: `ACTION_ACCESSIBILITY_SETTINGS` zeigt auf einen leeren Platzhalter (`frameworkpackagestubs`); eigener Eintrag `android.settings.ACCESSIBILITY_TV_OEM_LINK` wird zuerst genutzt.
- Unit-Tests 10/10 grün (AppCatalog 4, AppGuard 3, HomeGuardRules 3). 0.3.3 aufgespielt, Paketliste unverändert. Dienst am TV angeboten (`query-services` zeigt `os.trendo.tv/.HomeGuardService`), eingeschaltet: NEIN (wartet auf Nutzer).
- NICHT geprüft: Wächter-Verhalten nach Home-Taste und nach Neustart.
- 0.3.4: Fehler behoben – ohne `<queries>`-Eintrag für HOME sah Trendo unter Android 11 die Original-Startseite nicht, der Wächter beobachtete nichts. Nach Korrektur Log am CHiQ: „Wächter aktiv, beobachtet: [com.google.android.tvlauncher]" → „Original-Startseite erkannt – zeige Trendo" → `START … os.trendo.tv/.MainActivity`, `Displayed +3s916ms`. Wächter vom Nutzer eingeschaltet (`enabled_accessibility_services` enthält `os.trendo.tv/os.trendo.tv.HomeGuardService`). Paketliste unverändert. Screenshot `reports/tv/T-0500-chiq-v034-startseite.png`.
- Bekannt: Die Google-Startseite blitzt kurz auf, bevor Trendo erscheint (Wächter reagiert erst auf ihr Erscheinen). Kaltstart von Trendo ca. 3,9 s.
- NICHT geprüft: Verhalten nach TV-Neustart, Rückkehr aus TiviMate.

## Nachtrag 05.11.2026 – 0.4.x: alle Blöcke + „Geplant" ausgebaut
- **Neustart-Test:** TV neu gestartet (Laufzeit 128 s beim Prüfen), Wächter sofort aktiv, Trendo 6 s nach der Google-Startseite vorne (Log + Screenshot). BESTANDEN.
- **Medienplayer:** eigene Filme/Musik/Fotos über den Android-Medienindex (inkl. USB-Volumes), Player AndroidX Media3 1.4.1 (`PlayerActivity`), Fotoanzeige mit Links/Rechts, „Weitersehen" mit gemerkter Position (`ResumeStore`, Regeln in `ResumeRules`). Medienzugriff erst nach Android-Erlaubnis. Am CHiQ: Erlaubnis-Seite sichtbar; Abspielen NICHT getestet (keine eigenen Medien/USB am TV geprüft).
- **Wetter:** Open-Meteo (frei, ohne Konto), Ort wählt der Nutzer, Cache 30 min, offline letzter Stand. API vom PC geprüft (Geocoding + Forecast antworten). Am TV: Ort noch nicht gesetzt → Anzeige NICHT geprüft.
- **Einschlaf-Timer:** AlarmManager → Medien-Pause an jede App + abgedunkelter Bildschirm; TV-Ausschalten kann eine App nicht (ehrlich angezeigt). Ablauf am TV NICHT getestet.
- **Trendo AI:** lokale Befehlsregeln (`CommandParser`: Apps öffnen, HDMI n, Timer, Lautstärke, Stumm, Pause/Weiter, Seiten, Suche, Wetter), Sprache über die Spracherkennung des TVs (`RecognizerIntent`). Seite am TV sichtbar; Sprache NICHT getestet.
- **Smart Home:** mDNS-Suche nur bei offener Seite; am CHiQ echte Geräte gefunden (Google-Lautsprecher, LG-TVs, Drucker, Spotify Connect). Keine Steuerung (braucht das jeweilige System + Freigabe).
- **Bildschirmschoner:** `TrendoScreensaver` (DreamService, 12 eigene Bilder lokal mit ComfyUI/FLUX.2 klein + Titelbild, 905 KB), Uhr, Datum, Wetter. Am TV als Dienst registriert; aktuell gewählt: Google Backdrop. CHiQ hat kein `ACTION_DREAM_SETTINGS` → Trendo öffnet `com.android.tv.settings/.device.display.daydream.DaydreamActivity`. Auswahl durch Nutzer AUSSTEHEND.
- **Apps verwalten:** öffnet nur die Android-Infoseite (Nutzer deinstalliert/deaktiviert selbst); `ProtectedApps` markiert TV-/HDMI-/Store-/TiviMate-Teile als „wichtig". Trendo deinstalliert weiterhin nichts. Paketvergleich zeigt: Nutzer hat selbst Disney+, Downloader, Google TV entfernt und Play Spiele/com.cltv.fast deaktiviert.
- Unit-Tests 18/18 grün (CommandParser 4, Media/Wetter 2, AppCatalog 4, AppGuard 3, HomeGuardRules 3, ProtectedApps 2). 0.4.2 APK 4807899 Byte, SHA-256 `7ad2d447b65445d2ad8ca5e732a6b07b91f7c0afa9d9aadf78158d5fdc81df3a`. Jede Installation: Paketliste vorher/nachher unverändert.

## Nachtrag 05.11.2026 – 0.5.x Trendo Live TV, Schnellstart, Volltest
- Freigaben Manuel (Ja-Liste): Live TV (Sender+Player, Programmführer, Filme & Serien, Jugendschutz & Filter), Schneller Start, Profile, Handy als Fernbedienung, Aufnahmen auf USB, Bild-in-Bild, Theme-Studio, Trendo-PC-Verbindung. NEIN: Smart Home steuern.
- **Live TV (Port von `Trendo.Core.Tv`):** M3U, Xtream (Live/VOD/Serien/Folgen), XMLTV gestreamt mit Zeitfenster −6 h…+48 h, Filter wie Windows (Länder, nur Deutsch, Erwachsene, Kinder-Modus, doppelte zusammenführen), ausblendbare/PIN-gesperrte Gruppen (PIN nur als SHA-256 mit Salt), Favoriten, Erinnerungen (AlarmManager, 2 min vorher), Sendungssuche, Zeitraster-Programmführer, Player `LiveActivity` (Hoch/Runter, OK-Liste, Ziffern, Bild-in-Bild mit Links-Taste). Passwörter AES-GCM mit Android-Keystore (`SecretBox`), Fehlermeldungen ohne Adressen. `usesCleartextTraffic` für http-Quellen.
- **Quelle vom Handy einfügen:** QR-Code (ZXing core 3.5.3, Apache-2.0) → kleine Eingabeseite im Heimnetz, Einmal-Code (20 Zeichen), nur private Adressen, Ende nach erster Eingabe oder 5 min. Claude hat KEINE Zugangsdaten eingegeben (Nutzer gibt sie selbst ein).
- **Am TV NICHT getestet:** Live-TV mit echter Quelle (noch keine eingetragen), Player-Zappen, Programmführer, Filme/Serien, PIN, Bild-in-Bild auf 1,35 GB RAM, Handy-Seite.
- **Schneller Start:** Startbild als Fensterhintergrund, Laden in Stufen (Apps sofort, Medien/HDMI danach, Wetter zuletzt). Kaltstart am CHiQ 2644 / 2043 / 2308 ms (vorher bis ~4–5 s).
- **Volltest (05.11.2026):** Core 1189 bestanden / 5 übersprungen, App 681 / 4, Api 15 – 0 Fehler. Paket-Tests zuerst 1 Fehler (`test_no_former_agent_left` fand die Zeichenfolge zufällig in Binärdaten von `tv_saver_06.webp` und Android-Build-Ordnern) → Test nimmt jetzt `.webp/.ttf/.jar/.apk` und `tv/android/app/build/`, `tv/android/.gradle/` aus; danach 9/9 OK. Android-Unit-Tests 23/23. Beleg `reports/tv/T-0500-volltest-2026-11-05.txt` (SHA-256 `b1f3ac82f1f77670ad68c2e6cbe467e3c228d4bd465024c838770691b52e6a20`).
