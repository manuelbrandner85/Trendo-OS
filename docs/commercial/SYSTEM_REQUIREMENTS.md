# Systemanforderungen

Stand 13.11.2026. **Entwurf, nicht zur Veröffentlichung.** Die offiziellen Mindest- und Empfehlungswerte stehen erst fest, wenn die Messreihe in `PERFORMANCE_PROFILES.md` auf echter Hardware abgeschlossen ist. Bis dahin darf keine Zahl unten auf der Verkaufswebseite stehen.

## Was feststeht (aus dem Produkt selbst)

| Punkt | Wert | Beleg |
|---|---|---|
| Betriebssystem | Windows 11, 64 Bit (x64) | Produktstrategie vom 13.11.2026; Setup wird für `win-x64` gebaut (`installer/build-setup.sh`) |
| Rechte | keine Administratorrechte nötig | Velopack-Installation pro Benutzer |
| Laufzeit | im Setup enthalten | self-contained .NET 10 |
| Internet | nur für Kauf, Updates und Onlinefunktionen | lokale Studios laufen offline |
| Claude | nicht enthalten, eigenes Konto der Person | `AI_PROVIDER_COMPLIANCE.md` |

## Vorläufige Werte (nicht gemessen)

| | Mindestens (Basic) | Empfohlen (Standard) | Für 3D, Video, Spiele, lokale KI (Professional) |
|---|---|---|---|
| Prozessor | 4 logische Kerne | 6 logische Kerne | 8 logische Kerne |
| Arbeitsspeicher | 8 GB | 16 GB | 32 GB |
| Grafikspeicher | keine Vorgabe | 4 GB | 8 GB |
| Freier Platz | 20 GB | 50 GB | 200 GB, SSD |
| Bildschirm | 1366 × 768 | 1920 × 1080 | 1920 × 1080 oder mehr |

## Zusätzlich je nach Studio

| Studio | braucht zusätzlich (kostenlos, Installation nach Zustimmung) |
|---|---|
| 3D-Modelle, Filme machen | Blender |
| Spiele bauen (Unreal) | Unreal Engine über den Epic Launcher, starke Grafikkarte |
| Videos, Podcast | FFmpeg |
| Live streamen | OBS Studio |
| Gegenstände einscannen | Meshroom, Grafikkarte mit CUDA empfohlen |
| KI fragen, KI-Team | Ollama mit lokalem Modell |
| VR & AR | VR-Brille für die Brillenansicht |

Welche Studios auf einem bestimmten PC laufen, zeigt Trendo unter Einstellungen › Hardware & Leistung, auch in der Free Edition und vor dem Kauf.
