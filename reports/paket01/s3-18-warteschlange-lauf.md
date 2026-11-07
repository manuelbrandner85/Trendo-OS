# Beleg S3-18 (Teil Warteschlange) – Render-Warteschlange im Videoschnitt (07.11.2026)

- Lauf: `Trendo.App.Tests.exe -class Trendo.App.Tests.VideoQueue_Ui -class Trendo.App.Tests.VideoSchnittPlus_Ui -class Trendo.App.Tests.VideoSchnitt_Ui` → Total 10, Failed 0
- Ablauf im Test: Fassung „YouTube 1080p“ startet sofort; „Hochformat 9:16“, „PNG-Bildsequenz“, „ProRes 422 HQ“ und „AV1“ warten; AV1 wird aus der Schlange entfernt.
- Ergebnis: Reihenfolge yt1080 → hoch → png (Ordner), ProRes meldet „Encoder prores_ks fehlt – Alternative …“ und die Schlange läuft trotzdem weiter; AV1 läuft nicht.
- „⬇ Exportieren“ während eines laufenden Exports reiht jetzt ein, statt „läuft schon“ zu melden.
- Die echten Ausgaben je Vorlage (ProRes, EXR, PNG, H.264) sind belegt in `s3-18-profi-export-lauf.md`; hier ersetzt der Test FFmpeg, um die Reihenfolge zu prüfen.

## Nicht geprüft
- Warteschlange über einen Neustart von Trendo hinweg (sie lebt nur, solange Trendo offen ist)
- Mehrere Projekte gleichzeitig in einer Schlange (je Fassung wird der Projektstand beim Einreihen festgehalten)
