# T-0686: Design-Paket 2 – Programm-Hülle verständlich

**Ziel:** Die gemeinsame Programm-Hülle bekommt in allen Hüllen-Programmen auf einmal einen schmalen Kopf, genau einen goldenen Knopf je Ansicht, lesbare Werkzeugleisten, einen ersten Schritt im leeren Dokument und einen einzigen, verständlichen Speicherstand.
**Freigabe:** Der Nutzer hat am 16.11.2026 alle 22 Vorschläge V-01 bis V-22 des Bild-Audits („Audit 7: Anordnung und Design aus Nutzersicht“) mit Ja freigegeben. Dieses Paket setzt V-01, V-02, V-03, V-04 und V-08 um (Befunde S-01 bis S-04, S-11).
**Branch:** `claude/T-0686-design-2` (vom Integrationszweig `claude/T-0685-b1-integration` – dort ist die absturzsichere Hülle aus T-0680 schon enthalten)
**Dateien/Bereich:** `src/Trendo.App/MainWindow.ProgramShell.cs`, `MainWindow.ProgramFrame.cs` und zugehörige gemeinsame Hilfen; in einzelnen Programmen nur die Angabe „erster Schritt“ und Hinweise wie „Rechts hinzufügen“.

**Fertig, wenn:**
- [ ] **V-01 Schmaler Kopf:** eine Zeile mit Programmname, Dokumenttitel und Speicherstand, darunter sofort die Werkzeuge – in allen Hüllen-Programmen dieselbe kompakte Form. Die Arbeitsfläche beginnt in „Einfach“ (1600 × 1000) bei höchstens rund 30 % der Fensterhöhe; im Bild nachgemessen, je Programm vorher/nachher im Bericht.
- [ ] **V-03 Genau ein goldener Knopf je Ansicht:** „Speichern“ ist nicht mehr golden; golden ist die Hauptaktion des Programms (`Primary`) – bei mehreren nur die erste der sichtbaren Stufe (Regel in der Hülle); in der Arbeitsfläche keine zweite Hervorhebung; kein Blau als Akzent.
- [ ] **V-04 Werkzeugleisten:** nichts abgeschnitten, Abstand zwischen Knöpfen, Werkzeuge in „Einfach“ links mit Symbol und Wort (breitere Leiste), in den höheren Stufen nur Symbol mit Hinweis.
- [ ] **V-02 Erster Schritt im Leerzustand:** `ProgramSpec` bekommt die optionale Angabe „erster Schritt“ (Text + Aktion); die Hülle zeigt bei leerem Dokument einen großen, mittigen Knopf. Eingetragen für die im Audit genannten Programme; die Aktion löst die echte Funktion aus, auch wenn die rechte Spalte in „Einfach“ eingeklappt ist.
- [ ] **V-08 Speicherstand:** nur an einer Stelle; „Wird automatisch gespeichert“ / „Gespeichert um HH:MM“ / „Nicht gespeichert – Speichern“. Die Karten aus T-0680 (Wiederherstellen, anderes Fenster, von außen geändert) bleiben.
- [ ] UI-Tests für jede Regel; alle App-Testklassen, die die Hülle nutzen (`prog-`), laufen – Zahlen ehrlich im Bericht.
- [ ] Vorher/nachher-Bilder (mindestens 12 Programme in „Einfach“, 4 in „Professionell“) angesehen.

**Nicht tun:** `MainWindow.Text*.cs`, `MainWindow.Tabelle*.cs`, `MainWindow.Praesentation*.cs`, `SimpleWords.cs` und Seiten außerhalb der Hülle (Design-Paket 1); Core-Dateien von T-0680 bis T-0683 nur lesen. Keine Funktion entfällt (`UebersichtEinfach_Ui.Simple_keeps_every_function`), Knopfnamen `prog-cmd-*`, `prog-tpl-*` bleiben. Dunkel-Gold, Deutsch, Bedienstufen bleiben. Kein Volltest (Hauptsitzung), kein Push, kein Merge, kein Release.
**Am Ende:** nur committen, Bericht `tasks/T-0686-design-2.bericht.md`.
