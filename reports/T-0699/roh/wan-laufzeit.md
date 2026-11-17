# Wan 2.2 TI2V-5B – gemessene Rechenzeit (16.11.2026, RTX 5070 12 GB, sd-cli vid_gen, 1280 × 704, 12 Schritte, Vorschau-Qualität)

| Lauf | Clip | Bilder | Ergebnis | Zeit |
|---|---|---|---|---|
| 1 | 4 s | 97 | parallel lag ein fremdes Ollama-Modell (qwen3:14b, 10 GB) im Grafikspeicher – sd-cli brach ab (Code 1) | – |
| 2 | 4 s | 97 | 270–293 s je Schritt (ETA über 50 min); nach Schritt 10 von 12 von mir abgebrochen | 2994 s bis Abbruch |
| 3 | 1,5 s (verlangsamt auf 4 s Szene) | 37 | Code 0, Clip `ki-clip-1-20261116-091820-s42.mp4`, Herkunftsnachweis `wan-clip-herkunft.json` | 2141 s |

Folge im Code: `VideoAiKit.MaxKiClip = 1.5` – längere Szenen spielen den Clip bis 2,5-fach verlangsamt (`ClipFitArgs`). Die Oberfläche nennt „etwa 5 bis 15 Minuten“; gemessen waren hier 36 min, weil der Grafikspeicher mit anderen Programmen geteilt war. Das fertige Video (Lauf `videos-mit-ki-echt.log`) übernimmt den Clip aus Lauf 3 nach Prüfung der Prüfsumme gegen den Herkunftsnachweis.
