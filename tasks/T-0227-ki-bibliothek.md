# T-0227: KI-Bibliothek – kostenlose KI nach Arten, in Trendo OS integriert

**Ziel:** Vorgabe 2026-10-26: Die kostenlos ladbaren KI-Modelle sollen wirklich in Trendo OS laufen, nicht nur verlinkt sein. Sie sollen schön nach Arten sortiert sein, zum Beispiel Chat-KI, Bild-KI, Video-KI, Sprache und Musik.

**Branch:** claude/T-02xx

**Dateien/Bereich:**
- `src/Trendo.Core/W2/KiLibrary.cs`: Katalog mit 12 Arten, Laufzeiten mit festen Veröffentlichungen und SHA-256, Modelle mit offiziellen Quellen
- `src/Trendo.Core/W2/KiLibraryStore.cs`: Ablage unter `TrendoWerkzeuge/ki`, Prüfung von Größe und Prüfsumme
- `src/Trendo.App/MainWindow.KiLibrary.cs`: Seite „KI-Bibliothek“ mit Arten, Laden, Ausprobieren und Entfernen
- Tests

**Stufe 1 (dieser Stand):**
- Chat, Nachdenken, Programmieren, Bilder verstehen, Übersetzen, Wissen: Ollama-Modelle laden. Danach stehen sie in der Trendo KI zur Verfügung.
- Bilder erzeugen (SD Turbo, SDXL Turbo), Bilder verbessern (Real-ESRGAN 4×), Videos erzeugen (Wan 2.1 1,3B), Sprache zu Text (Whisper), Vorlesen (Piper, deutsche Stimmen): Laufzeit und Modell mit einem Klick laden und direkt auf der Seite ausprobieren. Ergebnisse landen im Ordner „KI-Ergebnisse“.
- Musik (ACE-Step): im Katalog sichtbar und als „nächste Stufe“ markiert. Sie braucht eine Python-Laufzeit mit PyTorch (ca. 8 GB) und ist noch nicht eingebaut.

**Fertig, wenn:**
- [x] Jede Art hat Modelle mit Lizenz, Größe, Speicherbedarf und Bedienhinweis.
- [x] Downloads gehen nur über HTTPS von GitHub und Hugging Face, mit festen Laufzeit-Prüfsummen. Bei Modellen gilt die SHA-256 von Hugging Face (X-Linked-Etag), sonst die Größe.
- [x] Lizenzen mit eigenen Bedingungen nennt der Knopf, bevor geladen wird.
- [x] Jeder Ladevorgang landet im Audit-Log (Freigabe und Ergebnis).
- [ ] Am PC: je Art einmal echt laden und ausprobieren. Noch nicht gemacht: Das sind mehrere GB und braucht eine Grafikkarte.
- [ ] Musik-KI (Stufe 2).

**Nicht tun:**
- keine Modelle ohne Klick laden (außer den freien Chat-Modellen, wie in T-0225 festgelegt)
- keine anderen Download-Server
