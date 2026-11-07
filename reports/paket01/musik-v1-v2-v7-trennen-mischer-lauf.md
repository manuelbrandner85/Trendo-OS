# Beleg Trendo Musik V1, V2, V7 – Hochladen → Trennen, Spuren-Mischer, Modellwahl (07.11.2026)

- Freigabe: „Alle ja“ zu V1–V8 (07.11.2026)
- Lauf: `Trendo.App.Tests.exe` mit den Klassen MusikStems_Ui, MusikSpurenUiTests, MusikImport_Ui, MusikPlusUiTests, MusikCoverWerkstatt_Ui, MusikProWerkzeuge_Ui, MusikExtendUiTests, MusikVideoKiUiTests → Total 22, Failed 0

- V1: Nach „＋ Eigene Aufnahme“ trennt Trendo sofort in Gesang und Begleitung (abschaltbar); ist die Trennung nicht eingerichtet, steht dort, wie man sie einrichtet.
- V2: Spuren-Mischer im Lied – Gesang, Schlagzeug, Bass, Rest mit Stumm, Solo und Pegel; „Mischung anhören“ rendert die Mischung und spielt sie in Trendo selbst ab (kein fremdes Fenster); „Als Lied speichern“ legt sie als eigenes Lied ab. Gemessen: Gesang stumm → Mischung deutlich leiser.
- V7: Trennung „Schnell“ (htdemucs) oder „Genau“ (htdemucs_ft, etwa 4× langsamer, lädt einmal ca. 320 MB); nach jeder Trennung misst Trendo, ob die Spuren wieder das Original ergeben (dB) und wie stark der Gesang in die Begleitung überspricht. Im Test: ≥ 40 dB, Übersprechen < 0,05.

## Nicht geprüft
- Echte Trennung mit Demucs auf diesem PC: Die Laufumgebung „Spuren trennen“ ist hier nicht eingerichtet (einmalig ca. 3 GB mit PyTorch, Knopf in Trendo). Im Test liefert ein Ersatz die Spurdateien; die Modellwahl wird als Argument „--modell htdemucs_ft“ übergeben (Skript prüft den Namen).
- Der frühere echte Lauf (03.11.2026, htdemucs, RTX 5070): 3:26 Minuten in 24 s, Rekonstruktion 30 dB, Korrelation 0,02 – siehe Kopf von MainWindow.MusikSpuren.cs.

## V8 Karaoke (Nachtrag)
- „🎤 Karaoke (LRC)“ im Spuren-Mischer: Die Liedtext-Zeilen werden auf die gesungenen Phrasen der Gesangsspur gelegt (Zeilen überlappen nie), als `begleitung.lrc` neben die Begleitung geschrieben; die Begleitung läuft in Trendo.
- Test (MusikStems_Ui): vier Zeilen auf vier Phrasen → Zeiten 0, 5, 10, 15 s; ohne Liedtext freundlicher Hinweis, keine Datei.
- Liedtexte kommen nur vom Nutzer (feste Regel); Trendo schreibt keinen Text dazu.
