# V3 – Messung der Suno-Regler mit ACE-Step 1.5 (lokal), 07.11.2026

Ablauf: Je Regler zwei Stellungen, feste Zufallswerte 11/22/33, 30 s Lieder, ACE-Step 1.5 Turbo mit LM 0.6B (`thinking=true`).
Merkmale: librosa-MFCC (Mittel und Streuung) plus Chroma. Schlagzeug-Anteil = perkussive Energie (HPSS).
„Vielfalt“ = mittlerer Merkmalsabstand zwischen den Zufallswerten. „Abstand gleicher Seed“ = Abstand derselben Zufallszahl zwischen den beiden Stellungen.

| Regler (ACE-Feld) | Messung | Ergebnis | Entscheidung |
|---|---|---|---|
| Verrücktheit (`lm_temperature` 0,55 → 1,35) | Vielfalt 36,32 → 37,31 (+2,7 %), Abstand gleicher Seed 35,8 | Das Lied ändert sich zwar, wird aber nicht nachweisbar „wilder“ als ein anderer Zufallswert. | **ausgeblendet** |
| Vielfalt (`lm_top_p` 0,6 → 1,0) | Vielfalt 32,81 → 38,57 (**+17,6 %**) | Mehr Abwechslung ist klar messbar. | **sichtbar** |
| Stile ausschließen (`lm_negative_prompt` „drums“) | Schlagzeug-Anteil 0,207 → 0,173 (−16 %) | Wirkt in die erwartete Richtung, aber schwach (Eingabefeld, kein Regler). | Feld bleibt mit ehrlichem Hinweis |
| Stil-Treue (`lm_cfg_scale` 1 → 5) | Abstand gleicher Seed 59,25; Schlagzeug-Anteil 0,035 → 0,033 | Ändert das Lied stark, ohne messbar treuer zu werden. | **ausgeblendet** |
| Audio-Treue (`audio_cover_strength` bei Klangvorbild) | nicht gemessen | Ohne Messung kein Regler. | **ausgeblendet** |

Grenzen: drei Zufallswerte je Stellung, nur 30-s-Stücke, Merkmale messen Klang und nicht „Überraschung“. Eine erste Runde lief wegen voller Grafikkarte (Unreal parallel) in den CPU-Decoder und wurde nach Serverneustart fortgesetzt. Bereits fertige Stücke wurden wiederverwendet.
Werte stammen aus `messung.json` des Laufs. Die Audiodateien liegen nur lokal (Temp) und sind nicht im Repo.
