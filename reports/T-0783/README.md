# T-0783 – Studio-Bilder ohne SDXL Turbo (Beleg)

Stand 26.11.2026. Grund: Die Stability AI Community License (SDXL Turbo) verlangt für einen Verkauf Registrierung, Umsatzgrenze und den Hinweis „Powered by Stability AI“.

## 1. Lizenz des Ersatzmodells (an der Originalquelle geprüft, 26.11.2026)

| Modell | Quelle | Lizenz laut Quelle | Zugang | Urteil |
|---|---|---|---|---|
| FLUX.2 [klein] 4B | https://huggingface.co/black-forest-labs/FLUX.2-klein-4B (Stand `e7b7dc27`) | `license: apache-2.0`; Modellkarte: „Open weights available for commercial use under the Apache 2.0 license“; `LICENSE.md` = Apache License 2.0 | nicht zugangsbeschränkt (`gated: false`) | **erlaubt** |
| FLUX.2 [klein] 9B | https://huggingface.co/black-forest-labs/FLUX.2-klein-9B | `license: other` (FLUX Non-Commercial License) | zugangsbeschränkt (`gated: auto`) | nicht verwendet |
| Textmodell + VAE (`qwen_3_4b`, `flux2-vae`) | https://huggingface.co/Comfy-Org/flux2-klein-4B (Stand `5b3abca5`) | `license: apache-2.0` (Qwen3-4B selbst Apache-2.0) | frei | erlaubt |

Apache-2.0 erlaubt die kommerzielle Nutzung; für die erzeugten Bilder gibt es weder Registrierung noch Umsatzgrenze noch Hinweispflicht. Die Modellkarte nennt zusätzlich eine Nutzungsrichtlinie (verbotene Inhalte) und schützt die Marken „FLUX“/„Black Forest Labs“ – beides betrifft Studio-Kacheln nicht; Trendo nennt das Modell nur im Lizenznachweis.

Die Dateien auf diesem PC sind genau diese Dateien (SHA-256 gemessen = SHA-256 der Hugging-Face-LFS-Datei):

| Datei | SHA-256 (PC = Hugging Face) | Quelle |
|---|---|---|
| `flux-2-klein-4b.safetensors` (7 751 105 712 B) | `ec3d4e733a771f61c052fb4856c48b336c55eaf2c65487c2a1faeb9bbda7a343` | black-forest-labs/FLUX.2-klein-4B und Comfy-Org (gleiche Datei; nicht die „base“-Variante) |
| `qwen_3_4b.safetensors` (8 044 982 048 B) | `6c671498573ac2f7a5501502ccce8d2b08ea6ca2f661c458e708f36b36edfc5a` | Comfy-Org/flux2-klein-4B |
| `flux2-vae.safetensors` (336 211 292 B) | `868fe7b343cc8f3a19dbcfcafbc3d5f888802be3f89bd81b65b3621a066ce8f3` | Comfy-Org/flux2-klein-4B |

Rohdaten: `hf-api-flux2-klein-4b.json`, `hf-api-flux2-klein-9b.json`, `hf-api-comfy-org-flux2-klein-4b.json`, `hf-api-comfy-org-flux2-klein-4b-dateien.json`, `hf-modellkarte-flux2-klein-4b.md`, `hf-lizenz-flux2-klein-4b.md` (am 26.11.2026 abgerufen). Der Generator prüft die Prüfsummen selbst (`--modelle`, Ausgabe „MODELL … sha256 ok“ in `erzeugung.log`).

## 2. Welche Bilder waren SDXL Turbo? (Beleg je Bild)

Drei unabhängige Quellen:
1. **Git/Berichte:** Commit `cce5c6261` „Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“ (8 Bilder); `tasks/T-0611-kreativ-ki-2.bericht.md`: „Studio-Bilder lokal mit der vorhandenen SDXL-Turbo-KI erzeugt (ComfyUI ist nicht mehr installiert)“ (4 Bilder aus `676ea9466`); Zweig `claude/T-0782-nachhilfe-programmieren`, Bericht: Bilder „lokal mit stable-diffusion.cpp …, ComfyUI ist auf dem PC nicht vorhanden“ (2 Bilder).
2. **ComfyUI-Ausgabeordner (Forensik):** Jedes Studio-Bild, das mit ComfyUI entstand, liegt dort noch als PNG; alle 119 `trendo-studio_*.png` tragen im eingebetteten Ablauf `unet_name = flux-2-klein-4b.safetensors`. Bildvergleich (96 × 54, Graustufen): 71 der 85 Bilder gleichen einer ComfyUI-Ausgabe (Abweichung ≤ 0,4 – dasselbe Bild), genau die 14 unten nicht (Abweichung ≥ 26). Zwischen 11.11. 00:14 und 15.11. 20:36 gibt es keine ComfyUI-Ausgabe – das ist der Zeitraum von T-0611 und `cce5c6261`.
3. **Bericht T-0670/T-0674:** Künstlerstudio und Büro-Bilder ausdrücklich „lokal mit FLUX.2 klein“ (passt zu 2.).

Damit sind **alle übrigen 71 Bilder bereits FLUX.2 [klein] 4B** (Zuordnung je Bild am Ende von `erzeugung.log`).

## 3. Ersetzte Bilder (vorher SDXL Turbo → nachher FLUX.2 [klein] 4B, 768 × 432 JPG)

Prompt und Stil unverändert aus `tools/studio_bilder.py` (TUTOR/CODELEARN aus dem T-0782-Zweig übernommen). Startwert in `docs/design/STUDIOBILDER.json`.

| Bild | vorher erzeugt in | SHA-256 vorher (SDXL Turbo) | SHA-256 nachher (FLUX.2 [klein] 4B) | Startwert |
|---|---|---|---|---|
| STUDIO-BOOK-01 | 676ea9466 (T-0611, Bericht: „mit der vorhandenen SDXL-Turbo-KI erzeugt“) | `51bfad172460e15d29eb2280795a3b307af93d1077738153952b909106dcb0f9` | `60c1fae50d981d99ebbc57bbd53b5212675271ea59ed3a65a3a73a40e495741c` | 20261102 |
| STUDIO-ANIM-01 | 676ea9466 (T-0611) | `c264f741a9d98f7e0103bfb891e18bb1475bf7883e384fc2fea3a4203636a1ee` | `d84d145ad657172f27d8ca6068fc27fb4dbf6766b82c8485454d66743677a6d3` | 20261103 |
| STUDIO-PHOTOS-01 | 676ea9466 (T-0611) | `b5bbd7d95a9a837fffa7c9b0ae61d4f7b5759fe321b73e66b89647bdddb220d6` | `8fb5bd76e00995066bdf20892cf34bd2eb9c2712aec4c49e1d9b3e253b23f4b8` | 20261726 |
| STUDIO-IMAGEAI-01 | 676ea9466 (T-0611) | `bac79802a6ee9a05015b52e67faa4884ba4a3cc86faff73707bb915676a22cde` | `262a5fc67a0ffaa3a9e1bf8790c8d74dbebeccbc1f45594248ba160983a049ac` | 20261105 |
| STUDIO-VIDEOAI-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `30bb3f61a7379fc449c421d10c92ae86bf3ef347d18ab0cbcf8d9ace906c0d96` | `12e4e0cc91b6b54cf527d92a8dbe8f204332ccc187cf67e7903c569e5438a446` | 20261326 |
| STUDIO-AGENTS-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `73a150d2ca48de30d01125fd03f8114b983d4fb55c5900efac058eab4fd3c18e` | `717a09b8ffe76a7e9a2dc30338922b4b39d4b27f2d9a3e9926e6420a47b4d3b3` | 20261107 |
| STUDIO-MODELS-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `03fa69e9f119b3e5d2a4784fe4be05c006844837137c235e78aa2660570e547c` | `378433f6fb816435fcecb9d1416111f333c4c1d36ea261286af6a232191c5276` | 20261108 |
| STUDIO-TRAIN-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `bbc3a32df6fe7208ac7d7d291fdff31ded5696a770c8c54a48ab05071ced69c8` | `c31caae7895e0fa1c3e36c564e5947e00ed93f4d6b139e5076da4a1189db6ca8` | 20261429 |
| STUDIO-CODEAI-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `623de52f9bdc4e475ee57c592d5a7da3b68316276bdf6254904cad5a6c239b67` | `f7aa66fec4397917de7b06198c9d8d79df374f3fc15cc70f15cb8a4931665952` | 20261227 |
| STUDIO-VISION-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `f81ac0bf46f669ae74544f16f80a8d01a9383e9b5ac1a4cf9be502a363a8a482` | `3ebe15578762c77a82892a18f9b93483155f04459c1f12c5d85d9628cc79d4d2` | 20261111 |
| STUDIO-CITE-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `06000a2fad5c82542c40a287307bbf84912bc739e23f1b4b77823cf225332c65` | `74596be0b82fc3c3c230c5fe06733cc8edeea6e193a1675094db0effa5300bbc` | 20261112 |
| STUDIO-DAYPLAN-01 | cce5c6261 („Studio-Bilder für Kreativ & KI Teil 3 und 4 (lokal mit SDXL Turbo)“) | `2782d88e5135fae0a614eb50c6f955bb1ba1f6ce005022d483f861fc89f738b1` | `991806a8a51247a1dc7e0263086cfb10dcb051a7addcec67482b279283a30592` | 20261231 |
| STUDIO-TUTOR-01 | 8a8358a4b auf claude/T-0782-nachhilfe-programmieren (Bericht: „lokal mit stable-diffusion.cpp …, ComfyUI ist auf dem PC nicht vorhanden“) | `0551a49346e8356dceb8ce263132ed89572c43b258198759307c5aed523d068e` | `6ab0415ad4c95dc13e890e732c0d6fa027d690a052e138ea4858b8d0b921ceed` | 20261114 |
| STUDIO-CODELEARN-01 | 8a8358a4b auf claude/T-0782-nachhilfe-programmieren (Bericht: „lokal mit stable-diffusion.cpp …, ComfyUI ist auf dem PC nicht vorhanden“) | `cd7090c58eb3430d0a5c46012a20b949c0831574a25ef6e7da4bda10a0b39bfb` | `9d3c9c3ad422447960f5863916eae0dd66071c5ca757f42673b6f7e57a246fda` | 20261428 |

Sichtprüfung: Jedes Bild einzeln angesehen (Kontaktbogen vorher/nachher und Ausschnitte). Sechs Bilder aus Lauf 1 zeigten Marken-Anmutung (Schriftzug am Monitorrand, Plakette am Laptop, Typenschild an der Kamera, Startleiste mit Symbolen) und wurden mit anderem Startwert neu gewürfelt (3 bzw. 7 Kandidaten); gewählt wurde je Bild der Kandidat ohne lesbares Logo. Ergebnis: dunkle Flächen, warmes Gold-Licht, keine lesbaren Marken. Bekannte Restabweichung: FLUX hält „no faces“ in Fotos-im-Bild nicht ein – STUDIO-PHOTOS-01 zeigt kleine Porträts erfundener Personen in den Bilderrahmen (keine echten Personen, keine Marke).

## 4. Dateien dieses Belegs (SHA-256)

```
ad6a68e9c7e40798d81bf7e88402f92903a62100f51cd276d565703dfa66c7fa *hf-api-comfy-org-flux2-klein-4b-dateien.json
2ef2f14b9cae4133dd06a0894cc2a48e50dc043625f400608486e25737856200 *hf-api-comfy-org-flux2-klein-4b.json
3c6c78a8907797a6bd25a30d5d00613232e7b41daad9add1e7093523bf598470 *hf-api-flux2-klein-4b.json
b754716476022948ebe8cd6baddef762e0f51874f25505129e2167b6d0395c4f *hf-api-flux2-klein-9b.json
ca02bc51900ab07789d1b70283329e7137f5af98f5161c23a1c81fc38a4af1fe *hf-lizenz-flux2-klein-4b.md
bd447051cf2db4bf67b992e563d89c35ebf1123f009c063361a222ab14e1c4f7 *hf-modellkarte-flux2-klein-4b.md
9f088c0e94acf04bb2899d0bd6923d66045a2afa6153234078fddde9155bd7bf *erzeugung.log
```
