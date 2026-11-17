# T-0734: CORE-02 KI fragen – Datei anhängen und Antwort beim Entstehen

**Ziel:** In „KI fragen“ lassen sich Dateien (TXT/MD/CSV, PDF, DOCX/ODT, Bild) an die Frage hängen – per Knopf und durch Ablegen –, mit klarem Hinweis, wohin die Datei geht; die Antwort wächst sichtbar im Chat und „Stopp“ bricht ab (Rang 4 der Restliste, Lücke laut audit-4).
**Branch:** claude/T-0734-ki-fragen
**Dateien/Bereich:** `src/Trendo.App/MainWindow.ChatWindow.cs`, `src/Trendo.App/KiBackends.cs`, `src/Trendo.Core/W2/IntelligenceCore.cs`, neu `src/Trendo.Core/W2/KiAttachments.cs`, Tests, `reports/T-0734/`.
**Fertig, wenn:**
- [x] Knopf „📎 Datei anhängen“ und Ablegen auf Chat/Eingabe hängen Dateien an; Karte je Datei mit ✕; falsche Art wird mit Grund abgelehnt.
- [x] Hinweis unter den Anhängen sagt, wohin die Datei geht (auf diesem PC / Claude über das Abo; Bilder nur an ein Bildmodell auf dem PC; nie an andere KI-Dienste).
- [x] Text-Anhänge stehen in der Frage, Bilder gehen als Bilddaten nur an ein Bildmodell auf diesem PC; ohne Bildmodell klare Meldung statt erfundener Antwort.
- [x] Antwort erscheint beim Entstehen in einer Sprechblase (über den vorhandenen Stream in `IntelligenceCore`); Stopp beendet sie, das Geschriebene bleibt mit „(abgebrochen)“.
- [x] Echtbeleg mit Ollama (Text-Anhang, Mitschrift, Folgefrage, Stopp, Bild) unter `reports/T-0734/` mit Rohlog und SHA-256.
- [ ] alle drei Testprojekte grün – Volltest macht die Hauptsitzung am Schluss (Vorgabe des Pakets); gezielt gelaufen siehe Bericht.
**Nicht tun:** kein zweiter KI-Kern, kein Claude über API, keine Anhänge an KI-Dienste mit Schlüssel, keine gemeinsamen Dateien (MenuBar, ProgramShell, StudioCatalog, requirements.yaml, installer) anfassen.
**Am Ende:** Branch pushen, Bericht `tasks/T-0734-ki-fragen.bericht.md`.
