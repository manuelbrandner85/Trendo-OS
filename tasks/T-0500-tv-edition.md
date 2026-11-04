# T-0500: TRENDO OS – Premium TV Edition (Android TV, Zielgerät CHiQ U55N9C1)

**Ziel:** Eigenständige, native Android-TV-App (Launcher) mit farbigem T, die alle vorhandenen TV-Apps unverändert lässt und startet; danach die 22 beauftragten Funktionen in Phasen.
**Branch:** claude/T-0500-tv-edition (Audit/Plan); Umsetzung in Unter-Branches `claude/T-0500-<kurz>`
**Dateien/Bereich:** neuer Ordner `tv/android/` (Kotlin/Gradle), dieser Auftrag, Bericht. Windows-/Linux-/Pi-Editionen werden nicht verändert.
**Quelle:** Übergabepaket vom 05.11.2026 (Masterprompt, Gesamtarchitektur, Schutz, WLAN, Design, Testplan). Liegt nicht im Repo.

**Fertig (M1), wenn:**
- [ ] `tv/android/` baut reproduzierbar per Gradle, signierte Debug-APK mit eigener Paket-ID
- [ ] Manifest mit `LEANBACK_LAUNCHER`, Banner, D-Pad-Fokus
- [ ] Startseite offline lauffähig, zeigt installierte Launcher-Apps (TiviMate, Google Play usw.) und startet sie per Original-Intent
- [ ] Unit-Tests grün, Start im Android-TV-Emulator belegt (Log + Screenshot)
- [ ] erste Installation auf dem CHiQ erst nach Freigabe

**Nicht tun:** keine Fremd-App deinstallieren/deaktivieren/leeren (`pm uninstall|clear|disable-user|suspend|hide`), kein Root, kein Flash, kein Werksreset, keine Portweiterleitung, kein Gold in der TV-Edition, Logo C (Windows) nicht überschreiben, keine IPs/Seriennummern/Pairing-Codes ins Repo.

---

## 1. Read-only Audit (05.11.2026)

### Repo
| Bereich | Befund | Folge für TV |
|---|---|---|
| `main` | `bc6251931` (0.9.72, Volltest grün laut Commit), sauber, gleich `origin/main` | Ausgangsbasis |
| Android-Code | **keiner** im Repo (kein `tv/`, kein Gradle, kein Kotlin) | TV-App komplett neu |
| `src/Trendo.Core/Tv/` | `IpTv.cs` (697 Z.), `TvCommands.cs` (221), `TvGuide.cs` (66), `TvPlus.cs` (131): M3U/Xtream/XMLTV, EPG, Favoriten, Befehlsregeln | als Vertrag/Vorlage nach Kotlin portieren, nicht als .NET-Binärdatei |
| `src/Trendo.App/MainWindow.Tv*.cs` | 6 Avalonia-Dateien, Player über mpv/NativeControlHost | nicht übertragbar; Android nutzt Media3 |
| `StudioCatalog.cs` | `STUDIO-TV-01` „TV & Live" vorhanden | IDs als Mapping nutzen, keine zweite Registry |
| `src/Trendo.Api/LocalApi.cs` | Kestrel nur `IPAddress.Loopback`, Loopback-Prüfung + Host-Header-Schutz, Token je Rolle | bleibt so; TV-Bridge wird ein eigener Dienst |
| `src/Trendo.Companion/CompanionServer.cs` | Standard Loopback; mit LAN-Schalter `http://+:<port>/` – **unverschlüsseltes HTTP** | Abweichung: Spezifikation fordert TLS + Pairing für die TV-Bridge → neu entwerfen (M4) |
| `requirements.yaml` / `MODULE_REGISTER.csv` | `CORE-04` „Pi/PC/Android Fusion Engine" `APPROVED_SPEC`; Notiz: Android-adb-Seite gebaut, echte Android-Hardware fehlt; Gegenstelle auf Android nicht gebaut | keine TV-Funktion gilt als vorhanden |
| `.claude/agents/` | 20 Agenten, keiner für Android | Android-Arbeit im Hauptchat, QA über `19-qa-verification`, Sicherheit über `03-security-permissions` |

### Werkzeuge auf dem Entwicklungs-PC
| Werkzeug | Stand |
|---|---|
| JDK | Temurin 21 vorhanden (`JAVA_HOME` gesetzt) |
| Android SDK / cmdline-tools / build-tools / platform | **fehlt** |
| `adb` (platform-tools) | **fehlt** |
| Gradle | fehlt (wird per Gradle-Wrapper geholt) |
| Android-TV-Emulator + Systemabbild | **fehlt** |
| Freier Platz auf C: | ca. 24 GB (98 % belegt) – SDK + Emulator brauchen grob 4–6 GB |

### Zielgerät CHiQ U55N9C1
Noch **nicht** geprüft: Android-TV-Version, API-Level, RAM/CPU, Paketnamen von TiviMate/Google Play, verfügbarer WLAN-Debug-Weg. Ohne diese Werte keine `minSdk`-Festlegung und kein Deploy.

### Abweichungen / Entscheidungsvorschläge (Decision-Delta, nicht eingetragen)
1. **Markenzeile „by Manuel"** widerspricht `NoFixedUserTests`/„keine Personennamen im Produkt", gilt aber laut Übergabe als Marken-Credit der TV-Edition. Vorschlag: nur im Android-Ordner `tv/android/` als Ressourcen-String; Test-Ausnahme ausdrücklich für diesen Pfad, falls der Test ihn erfasst (prüft bisher `src/`, `installer/`, `linux-image/`).
2. **Farbwelt:** TV-Edition Blau/Cyan/Violett/Magenta statt Gold – eigene Design-Tokens nur in `tv/android/`, Windows-Gold bleibt.
3. **Bridge:** neuer TLS-Dienst mit Pairing statt LAN-Schalter der Companion-App.
4. **UI-Technik:** Compose for TV (AndroidX `tv-material`), Leanback nur falls Profiling auf dem CHiQ schlechter ausfällt.

## 2. Backlog – 22 Funktionen

Status aller Funktionen: **PROPOSED** (beauftragt, nichts implementiert).

| Nr | Funktion | Phase | Modul | PC nötig | Fallback |
|---|---|---|---|---|---|
| 01 | TV & Streaming Hub | M1 | launcher, app-discovery | nein | Original-Home |
| 04 | Smart App Organizer | M1 | app-discovery, storage | nein | nicht sichtbar ≠ deinstalliert |
| 22 | Intelligent Focus Navigation | M1 | launcher-UI | nein | – |
| 11 | Ad-Free Launcher | M1 | launcher | nein | – |
| 19 | Dynamic Theme Studio (Basis: 3 Renderstufen) | M1/M2 | settings, theme | nein | Performance-Modus |
| 21 | Profiles & Favorites | M2 | storage | nein | ein Standardprofil |
| 03 | Universal Media Player | M2 | tv/media (Media3, SAF, USB) | nein | – |
| 05 | Continue Watching (nur eigene Medien) | M2 | storage, media | nein | – |
| 06 | Cinema Experience | M2 | media | nein | Systemeinstellungen |
| 20 | Cinema Quick Panel | M2 | media | nein | öffnet TV-Einstellungen |
| 02 | Live-TV Guide (eigene XMLTV/M3U) | M2 | epg (Port von `TvGuide.cs`/`IpTv.cs`) | nein | Cache |
| 16 | Universal Search (lokal) | M2 | search | nein | – |
| 07 | Bloatware Manager (nur Anzeige) | M3 | privacy; ADB-Inventur nur am PC | für Inventur | – |
| 08 | Performance Monitor | M3 | perf | nein | „nicht verfügbar" |
| 09 | Privacy Shield | M3 | privacy | nein | Android-Einstellungen |
| 10 | Recovery Center (nur Trendo-Daten) | M3 | storage, settings | nein | Original-Home |
| 12 | Smart Resource Manager | M3 | perf | nein | Eco-Modus |
| 13 | Trendo AI | M4 | remote-client, lokale Regeln (Port `TvCommands.Rules`) | für KI | lokale Regeln |
| 14 | Device Fusion | M4 | bridge-contracts | ja | „PC offline" |
| 15 | Smart Home Center | M4 | bridge-contracts | ja | nur Anzeige |
| 17 | Gaming Center | M4 | app-discovery | optional | Android-Spiele |
| 18 | Smartphone Remote | M4 | bridge (TLS + Pairing) | nein/ja | Fernbedienung |

## 3. Freigabe-Gates (warten auf Manuel)
- **G-A:** Android-SDK (cmdline-tools, platform-tools, build-tools, platform, TV-Emulator) aus `dl.google.com` laden und **Android-SDK-Lizenz akzeptieren** – Ablage nutzerlokal, keine Systeminstallation.
- **G-B:** am CHiQ Entwickleroptionen + Netzwerk-/Wireless-Debugging einschalten und Pairing am TV bestätigen (macht der Nutzer).
- **G-C:** erste Installation der Trendo-APK auf dem CHiQ.
- **G-D:** Trendo als Standard-Home auswählen (nur falls das Gerät es anbietet, macht der Nutzer).
