# T-0723: KI-Studios echt machen – Code mit KI (Audit D6, Rest)

**Ziel:** Die KI des einen Assistenzwegs (T-0707: Claude nur über das eigene Abo, sonst die lokale KI nach T-0703/T-0712) ändert Code im offenen Projekt nachvollziehbar: Auftrag → Plan → Diff je Datei → Ja/Nein je Änderung → Anwenden als EIN Rückgängig-Schritt → Tests (DevBuild) nach Freigabe → Ergebnis, bei Rot Rückfrage „zurücknehmen?“.
**Branch:** claude/T-0723-code-mit-ki (vom Integrationszweig claude/T-0685-b1-integration)
**Dateien/Bereich:** Studio „Code mit KI“ (`MainWindow.KreativStudios4.cs`, neu `MainWindow.CodeAiKi.cs`), Core `Kreativ/CodeKiChange.cs` (neu), `Programs/ProgramDocument.cs` (Ereignis nach Rückgängig/Wiederholen), Tests, `reports/T-0723/`.
**Fertig, wenn:**
- [x] Vorschau als Diff je Datei, Ja/Nein je Änderung, nichts geändert vor dem Ja
- [x] Anwenden = ein Rückgängig-Schritt für Dokument und Dateien (Strg+Z / „Rückgängig“ / Rückfrage nach roten Tests)
- [x] Tests nur nach Freigabe (ApprovalGate.Execute), Projektgrenzen durchgesetzt
- [x] Echtlauf mit lokaler KI (Ollama) am Python-Beispielprojekt mit Rohlog und SHA256SUMS
- [x] berührte Testklassen grün (Zahlen im Bericht); kein Volltest (macht die Hauptsitzung)
**Nicht tun:** keine zweite KI-Engine, kein API-Weg zu Claude, T-0722 (3D-Bibliothek) nicht anfassen, nichts installieren.
**Am Ende:** Branch pushen, Bericht `tasks/T-0723-code-mit-ki.bericht.md`.
