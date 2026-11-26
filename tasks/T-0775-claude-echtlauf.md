# T-0775: Querschnitt Q2 – ein echter Claude-Abo-Lauf je Programmart

**Ziel:** Testschicht „echt“ mit `TRENDO_ECHT_CLAUDE`: Claude (installiertes Claude Code, Abo der Person) ändert in je einem Programm aus Kreativ, Büro und Technik das offene Dokument, Strg+Z nimmt es zurück, Rohlog und SHA-256 liegen unter `reports/T-0775/`. Zusätzlich je ein Lauf in „Code mit KI“ und „KI fragen“ mit Anhang.
**Branch:** claude/T-0775-claude-echtlauf (Basis main 0.9.111 = 2b1ffa923)
**Grundlage:** `docs/audit/2026-11-26-neubewertung/restliste.md` Zeile Q2, Rang 2 (Code mit KI), Rang 5 (Logos & Marken), Rang 7 (KI fragen); `BERICHT.md`.
**Dateien/Bereich:** Claude-Anbindung (`MainWindow.Claude.cs`, `MainWindow.ClaudeLos.cs`, `MainWindow.W2.cs`), Briefe (`MainWindow.W1Office.cs`), Logos (`MainWindow.KreativStudios.cs`), neue Testklasse `ClaudeEchtlauf_Ui`, Belege `reports/T-0775/`.
**Fertig, wenn:**
- [x] Kreativ: „Ideen mit Claude“ (Logos & Marken) färbt das offene Logo um, Vorschau → Ja → Datei → Strg+Z
- [x] Büro: Claude kürzt den offenen Brief, Strg+Z stellt ihn her
- [x] Technik: Claude verschiebt im Grundriss eine Wand, Strg+S-Datei, Strg+Z
- [x] Zusatz: Code mit KI („Mit Claude ändern“ → Zurücknehmen), KI fragen mit Textanhang
- [x] ohne Variable erscheinen die Echt-Tests als „übersprungen“; fehlt Claude oder die Anmeldung: „übersprungen“ mit BLOCKIERT und dem Schritt für die Person
- [x] gezielte Testklassen der berührten Dateien grün (kein Volltest, macht die Hauptsitzung)
**Nicht tun:** keine API, keine Tokens speichern oder ausgeben, kein `ClaudeRunOverride` im Echtlauf, nichts an `%LOCALAPPDATA%\TrendoOS`, kein Merge, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0775-claude-echtlauf.bericht.md`.
