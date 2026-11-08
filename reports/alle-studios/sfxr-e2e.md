# Spiel-Effekte nach sfxr in der Geräusch-Werkstatt – 08.11.2026

Alle Studios, Runde 7, CORE-06 „Spiele bauen“.

**Grundlage:** das sfxr-Verfahren von DrPetter (2007, gemeinfrei). Vorbild ist jsfxr (Unlicense). Umgesetzt ist es als **eigener C#-Code** in `src/Trendo.Core/Media/Sfxr.cs`, ohne Fremdcode, ohne Samples und ohne Abhängigkeit.

**Sprachwahl:** C#, nicht Rust. Die Synthese ist klein (eine Schleife pro Sample) und läuft im vorhandenen C#-Audiopfad, Gemessen: 50 s Ton in 275 ms, also 5,5 ms je Ton-Sekunde. Das ist etwa 180-mal schneller als Echtzeit; ein typischer Effekt (0,1–0,5 s) ist in unter 3 ms fertig. Rust brächte keinen messbaren Vorteil und nur eine zusätzliche Sprachgrenze.

## Was die Nutzerin oder der Nutzer sieht
Audioeditor → Geräusch-Werkstatt → **„Spiel-Effekte (8-Bit)“**.
- **Vorlagen:** Münze, Laser, Explosion, Power-up, Treffer, Sprung, Menü-Klick.
- **„Neue Variante“** würfelt die passenden Regler und öffnet den Effekt zum Bearbeiten.
- **Eintrag:** Lizenz „selbst erzeugt (sfxr-Verfahren gemeinfrei)“ und ein Ereignis-Tag, zum Beispiel `Spiel.Schuss`.
- **Weiterverwendung:** Mit „In die Bibliothek“ und „Paket ausgeben“ landet der Effekt im Spiel- oder Film-Paket, mit Lizenzen.csv und Ereignisse.json.

## Prüfung am erzeugten Ton
| Prüfung | Ergebnis |
|---|---|
| Sinus, Grundton 0,3 → Soll 8 · 44 100 / 1098,9 ≈ 321 Hz | gemessen über Nulldurchgänge: im Bereich 311–331 Hz |
| Hüllkurve: Halten 0,3 und Abklingen 0,4 → 0,09 · 10⁵ + 0,16 · 10⁵ Samples | 25 000 ± 10 Samples |
| Sprung (4 Varianten) | Tonhöhe am Ende > 1,2 × Anfang |
| Laser (fallend) | Tonhöhe am Ende < 0,8 × Anfang |
| Arpeggio (Wechsel nach 3232 Samples) | danach > 1,1 × höher |
| Alle 7 Vorlagen × 5 Varianten | 0,02–5 s lang, Spitze −3 dBFS, keine ungültigen Werte |
| Gleicher Startwert | Byte für Byte gleich; anderer Startwert ergibt einen anderen Effekt |
| Frequenzgrenze | beendet einen fallenden Ton vorzeitig |
| WAV schreiben und lesen | gleiche Länge, 44,1 kHz |

Wellenformen aller Vorlagen (Variante 2): `sfxr-wellen.png`, SHA-256 3af56a70de6de214…
- **Laser:** fallender Sinus.
- **Power-up:** steigender Sägezahn mit Wiederholung.
- **Sprung:** steigendes Rechteck.
- **Explosion und Treffer:** Rauschen.
- **Münze:** Rechteck mit Arpeggio-Sprung.

## Tests
- **`SfxrTests`** (9 Tests, grün): Messungen wie in der Tabelle.
- **`AudioSfx_Ui`** (1 Oberflächentest, grün): Laser würfeln; die neue Variante ist eine andere Datei; Lizenz und Tag stimmen; Bibliothek und Paket enthalten den Effekt.
- Die bisherige Geräusch-Werkstatt (`AudioFoley_Ui`) bleibt grün.

## Nicht geprüft (NOT RUN)
- Ein Hörtest durch einen Menschen.
- Die Einbindung in ein Godot- oder Unreal-Projekt über das Paket. Der Paket-Weg selbst ist seit Paket 01 vorhanden.
