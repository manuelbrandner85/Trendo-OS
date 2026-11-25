# T-0765: X-03 „Apps zusammenklicken“ wird ein echter App-Baukasten

**Ziel:** Aus dem Einzelseiten-Baukasten wird ein App-Baukasten: mehrere Seiten mit Navigation, Daten und Logik ohne Server (Datensammlungen, Formular mit Pflichtfeldern, Liste, Anzahl/Summe, Speichern im Browser), Text direkt auf der Seite bearbeiten, Vorlage „Einkaufsliste“, Rundreise über die exportierte ZIP (freigegeben vom Nutzer, Restliste Rang 36, Audit `docs/audit/2026-11-16-profi-audit/audit-5-entwickeln-system.md` Abschnitt X-03).
**Branch:** claude/T-0765-echte-apps (Basis main = Release 0.9.109)
**Dateien/Bereich:** `src/Trendo.Core/PageBuilder.cs`, `src/Trendo.App/MainWindow.W4Builder.cs`; dazu der KI-Weg `seite.schreiben` (`src/Trendo.Core/PromptAction/SystemPromptAdapter.cs`), eine Zeile „Einfach“ (`MainWindow.SimpleMode.cs`), Bild-Hilfe (`tools/Trendo.Render/Program.cs`), Tests, Beleg `reports/T-0765/`.
**Fertig, wenn:**
- [x] Seiten anlegen/umbenennen/sortieren/löschen, Knopf-Ziel „zu Seite X“, Navigation in der fertigen App
- [x] Datensammlungen mit Feldern (Text/Zahl, Pflicht), Bausteine Eingabe-Formular, Datenliste, Auswertung (Anzahl/Summe); fertige App speichert im Browser (localStorage), offline, nur eigenes eingebettetes Skript (CSP-Hash), kein eval/innerHTML
- [x] Doppelklick/F2 = Text direkt auf der Seite, Wiederholen (Strg+Y / Strg+Umschalt+Z), Autosave über die Hülle (sofort speichern + Notsicherung 20 s)
- [x] Vorlage „Einkaufsliste“, Leerzustand zeigt den ersten Schritt, Abgrenzung zum Web-Design-Studio in einem Satz
- [x] KI „seite.schreiben“ kennt `Daten:` und `Seite:`, lässt vorhandene Seiten/Daten stehen, Ergebnis im Baukasten rückgängig
- [x] Abnahme im echten Chrome (headless, eigenes Profil, ohne Netz): Einträge bleiben nach Browser-Neustart, zwei Seiten verlinkt, ZIP läuft offline; Rundreise ZIP → wieder öffnen; Rohlog + SHA-256 unter `reports/T-0765/`
- [x] betroffene Testklassen grün (kein Volltest – macht die Hauptsitzung)
**Nicht tun:** keine zweite Engine (kein Umbau auf das Web-Design-Studio), alte `seite.json` bleibt lesbar, kein Push nach main, kein Release.
**Am Ende:** Branch pushen, Bericht `tasks/T-0765-echte-apps.bericht.md`.
