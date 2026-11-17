# Bericht T-0684 Design-Paket 1 – kleine, sofort sichtbare Korrekturen

**Auftrag:** V-05, V-06, V-10, V-13, V-16, V-18, V-19 aus dem Bild-Audit „Anordnung und Design“ (16.11.2026). Freigabe: alle 22 Vorschläge am 16.11.2026 mit Ja.
**Branch/Worktree:** `claude/T-0684-design-1` von `origin/main` (f68974c6d), eigener Worktree. Nicht gepusht, kein PR, kein Merge, kein Release.
**Commits:** `dd91ecd76` (Umsetzung und Tests), dieser Commit (Bericht).

## Was war falsch, was ist jetzt anders, welcher Test hält es fest

Alle Tests stehen in `tests/Trendo.App.Tests/DesignPaket1UiTests.cs` (Klasse `DesignPaket1_Ui`), wenn nichts anderes genannt ist. Es sind echte Oberflächen-Tests im Avalonia-Headless-Fenster (kein Ersatz): Seite öffnen, Knopf drücken, Baum und Layout prüfen.

### V-05 Falsche Wörter
- **Befund:** Im Kalender und bei Geld & Rechnungen hieß der Pfeil „▶“ in „Einfach“ „Abspielen“ (`kalender_start.png`, `finanzen_start.png`).
- **Ursache:** `GlyphIcons.Map` ordnet jedem Zeichen genau ein Wort zu („▶“ = „Abspielen“) und setzt es als Hinweis; `SimpleWords.WordFor` nimmt den Hinweis und hängt ihn in „Einfach“ an den Knopf. Das Zeichen allein sagt aber nicht, ob abgespielt oder geblättert wird.
- **Jetzt:** `GlyphIcons.NameFor(zeichen, knopfname)`. Für die mehrdeutigen Dreiecke (▶ ⏵ ◀ ⏴) kommt das Wort aus dem Zusammenhang (Name des Knopfs: `…play…` → „Abspielen“, `…next…` → „Weiter“, `…prev/back…` → „Zurück“), sonst gibt es **kein** Wort. Die Blätter-Knöpfe tragen ihr genaues Wort selbst (`SimpleWords.Named`): Kalender „Voriger/Nächster Monat“, „Vorige/Nächste Woche“, „Voriger/Nächster Tag“ je nach Ansicht; Haushaltsbuch „Voriger/Nächster Monat“; Jahresübersicht „Voriges/Nächstes Jahr“; Fotoalbum „Voriges/Nächstes Foto“; Serienbrief „Voriger/Nächster Brief“; Spiel-Aufgaben „Eine Spalte zurück/weiter“; Reihenfolge in den Einstellungen „Nach vorn/Nach hinten“.
- **„Kopieren“ unter Hinweisen:** `ResultCard` zeigte unter jeder Meldung („Vorlage … geöffnet.“) einen Knopf „Kopieren“. Jetzt nur unter einem echten Ergebnis (mehrzeilig oder länger als 140 Zeichen); jede Meldung lässt sich weiter per Rechtsklick → „Kopieren“ kopieren.
- **Tests:** `Glyph_word_depends_on_context`, `Calendar_and_money_arrows_say_where_they_go`, `No_page_calls_an_arrow_play` (geht alle Seiten durch), `No_copy_button_under_short_hints`.
- **Formulare ausfüllen:** gibt es auf `origin/main` nicht – übersprungen wie vorgegeben.

### V-06 „Mehr anzeigen“
- **Befund:** Texte & Tabellen zeigte die Überschrift „Vorlagen“ und darunter nichts; die KI-Bibliothek nach der Wahl einer Art kein Modell; der Knopf hieß „Mehr anzeigen (19 weitere Funktionen)“; die Begrüßung der Ersteinrichtung war mit „…“ gekürzt.
- **Jetzt (`MainWindow.SimpleMode.cs`, `MainWindow.Uebersicht.cs`):**
  - Fällt der Schnitt direkt hinter eine Überschrift, bleibt deren Abschnitt offen (Schnitt rückt bis vor die nächste Überschrift).
  - Der Knopf nennt die Namen der eingeklappten Abschnitte: „Mehr anzeigen: Zuletzt benutzt“, „Mehr anzeigen: Mitgeliefert mit Trendo OS, Automatisch pro Benutzer und 2 weitere“. Nur ohne Abschnittsnamen bleibt die Zahl.
  - Lange Listen: „Studios auf diesem PC: alle 76 zeigen“ statt „Alle 76 anzeigen“; ohne Überschrift „Alle 12 Einträge zeigen“.
  - Die Einleitung einer Seite (erster langer Satz unter den ersten vier Texten nach dem Titel) und Leerzustände werden nicht mehr einzeilig gekürzt.
  - KI-Bibliothek: Der für alle Modelle gleiche Satz „So geht's: …“ steht einmal über der Liste statt auf jeder Karte.
- **Abweichung vom Wortlaut des Audits:** Das Beispiel „Alle 19 Vorlagen zeigen“ setzt voraus, dass die Überschrift eine Mehrzahl ist. Damit kein falscher Satz entsteht („Alle 7 Chat & Schreiben zeigen“), heißt es „<Überschrift>: alle N zeigen“.
- **Tests:** `More_label_names_what_follows`, `Main_content_stays_visible`, `List_button_names_its_list_and_intro_is_whole`.

### V-10 Fenstertitel
- **Befund:** Lizenz hieß „Schrift & Farben anpassen“, Hardware „Qualität prüfen“, Verbindungen und „Mit Claude verbinden“ hießen „KI fragen“, die KI-Bibliothek „Kostenlose KI finden“, Audio/Bild/Videoschnitt „Videos & Fotos bearbeiten“ – jeweils der Name des Studios, in das die Seite einsortiert ist.
- **Jetzt:** `MainWindow.PageOwnName(key)`: Eine Seite mit eigenem Namen (Eintrag der linken Leiste in Einstellungen/KI-Zentrale, Programmname) heißt im Fenstertitel **und** im Seitenkopf so wie die Seite. Die Titelkarte des Studios (Symbol, Name, Satz) steht nur noch auf Seiten, die so heißen wie das Studio. Programme zeigen ihren Namen einmal (in ihrer eigenen Kopfzeile), nicht doppelt.
- **Bewusste Grenze:** Die **Startseite** eines Studios heißt weiter wie das Studio, weil man sie unter diesem Namen öffnet (Startmenü, Studio-Übersicht, linke Leiste des Desktops – Tests `StartMenu_StudioFenster.Jedes_Studio` und `Side_names_match_windows` aus T-0663 bleiben unverändert grün). Siehe „Offene Punkte“.
- **Tests:** `Window_title_is_the_page` (10 Seiten), `Shell_pages_are_named_like_the_left_bar`; angepasst: `StudioHeadingTests.Heading_matches_catalog` (erwartet jetzt den eigenen Namen der Seite, sonst den Katalognamen, und zusätzlich Fenstertitel = Seitenkopf).

### V-13 Eine KI-Eingabe, „Verwandte Programme“
- **Befund:** „KI fragen“, „Trendo KI (lokal)“ und „Webseite bauen“ hatten oben die Auftragszeile und darunter eigene KI-Eingaben; über Seiten stand die Zeile „Gehört hierher:“ mit bis zu sieben Knöpfen.
- **Jetzt:** Auf `ki-fragen`, `ki`, `dev` steht oben nur „KI ▾“. Die Auftragszeile bleibt einen Klick entfernt („KI ▾“ → „Auftragszeile zeigen“); wählt man dort eine Aufgabenkarte, blendet Trendo sie selbst ein. „Gehört hierher:“ ist weg: Die Einträge stehen als „Verwandte Programme“ im Menü „Mehr“ des Seitenkopfs – in jeder Stufe. Seiten ohne Seitenkopf (Fokusmodus, Programme, die das Fenster füllen) bekommen einen einzelnen Knopf „Verwandte Programme ▾“.
- **Tests:** `Ki_pages_have_one_input` (3 Seiten), `Related_programs_live_in_the_more_menu` (Einfach und Erweitert), `Related_programs_without_page_head`; angepasst: `StudioFolds_Ui.Host_strip_opens_page` (klickt jetzt „Mehr“ und den Eintrag statt den Knopf in der Zeile; hält weiter fest, dass jeder eingegliederte Bereich erreichbar ist).
- **Nicht erledigt:** „Webseite bauen“ hat weiter zwei eigene Eingaben („Beschreibe die Seite“ und „Seite mit KI bauen und ändern“). Das Zusammenlegen gehört zum Umbau der Seite (V-22).

### V-16 Leere Flächen
- **Adressbuch:** Die drei Spalten endeten nach wenigen Zeilen; ohne Kontakte standen zwei Leerzustände nebeneinander. Jetzt reichen die Spalten bis zum unteren Fensterrand (neuer Baustein `FillDown` in `MainWindow.ProgramFrame.cs`), ohne Kontakte steht ein Leerzustand mit Knopf „+ Neuer Kontakt“.
- **Aufgaben:** Ohne Aufgabe stand der Leerzustand über drei leeren Spalten. Jetzt nur der Leerzustand; die Spalten erscheinen mit der ersten Aufgabe.
- **Projekte, Großprojekte:** Der leere Kasten rechts wird nicht mehr gezeichnet.
- **Tests:** `Address_book_fills_the_window` (misst den Abstand zum unteren Rand), `Tasks_one_empty_state`, `No_empty_boxes`.

### V-18 Entwicklerseiten und Rohprotokolle
- **Jetzt:** „Bau-Fortschritt“ steht in der Leiste der Einstellungen und auf „Protokoll & Abstürze“ nur in „Professionell“. Die rohen Protokollzeilen ebenfalls; sonst steht dort der Satz „Die einzelnen Protokollzeilen zeigt die Bedienstufe ‚Professionell‘.“ Pfade sind außerhalb von „Professionell“ gekürzt (ganzer Pfad im Hinweis).
- **Systemcheck:** ein Satz („6 Zusatzprogramme fehlen.“ / „Alles in Ordnung: …“) mit einem Knopf „Jetzt einrichten“ (öffnet „Zusatzprogramme“, wo nach Zustimmung installiert wird). Die Liste mit „Reparieren“ je Zeile steht in „Einfach“ zugeklappt unter „Einzelheiten“.
- **Handy verbinden:** Der Teil für den Entwicklermodus („Bildschirm und Apps steuern“) steht in „Einfach“ zugeklappt unter dem geführten Weg (Audit: „Profi-Teil unter dem einfachen Weg sichtbar“). Er öffnet sich von selbst, wenn dort ein Handy gefunden wurde oder eine Meldung steht.
- **Tests:** `Developer_pages_only_in_pro`, `System_check_is_one_sentence`, `Phone_developer_part_is_folded`.

### V-19 Leiste am unteren Rand
- **Jetzt:** Unter jedem Symbol steht ein kurzes Wort (Start, Dateien, Browser, Trendo KI, Medien, Einstellungen, Hinzufügen, Webseite, Texte, Schutz, Papierkorb; hinzugefügte Apps mit ihrem Namen). Der ausführliche Name bleibt im Hinweis.
- **Test:** `Dock_icons_have_words`.

## Keine Funktion entfällt
- `UebersichtEinfach_Ui.Simple_keeps_every_function` ist grün. In `tests/Trendo.App.Tests/Daten/einfach-vorher-ausgeblendet.txt` stehen keine neuen Ausnahme-Zeilen (dieses Paket blendet in „Einfach“ nichts aus, was „Erweitert“ zeigt); am Ende der Datei ist als Kommentar festgehalten, wo die verschobenen Dinge jetzt liegen.
- `ButtonProbe.Risiko` hat zwei neue Einträge (`page-head-more`, `fold-menu`): Beide Knöpfe klappen nur ein Menü auf, wie der schon eingetragene Knopf „KI ▾“. Ihre Einträge prüfen `StudioFolds_Ui` und `DesignPaket1_Ui`.

## Bilder (vorher/nachher, 1600 × 1000, je mit eigenem `TRENDO_RENDER_ROOT`)
Ordner im Scratch-Bereich der Sitzung: `ux-t0684\vorher\` (Stand origin/main) und `ux-t0684\nachher\`. Die acht aussagekräftigsten Paare (gleicher Dateiname in beiden Ordnern):

| Paar | Zeigt |
|---|---|
| `kalender.png` | „Voriger Monat / Nächster Monat“ statt „Zurück / Abspielen“ (V-05) |
| `doku-start.png` | Vorlagen sichtbar, „Mehr anzeigen: Zuletzt benutzt“ (V-06) |
| `ki-bibliothek.png` | Modelle sichtbar, Titel „KI-Bibliothek“ statt „Kostenlose KI finden“ (V-06, V-10) |
| `lizenz.png` | Titel „Konto, Lizenz & Vollversion“, „Studios auf diesem PC: alle 76 zeigen“ (V-10, V-06) |
| `ki-fragen.png` | eine Eingabe, keine Zeile „Gehört hierher:“ (V-13) |
| `kontakte.png` | Adressbuch füllt das Fenster, ein Leerzustand (V-16) |
| `systemcheck.png` | ein Satz mit einem Knopf statt sechs roter Zeilen (V-18) |
| `desktop.png` | Wörter unter den Symbolen der Leiste (V-19) |

Weitere Paare: `settings`, `aufgaben`, `projects`, `produktion`, `system`, `components`, `einrichtung`, `finanzen`, `ton`, `dev`, `hardware`, `verbindungen`.
Nicht weitergeben: `nachher\handy.png` zeigt die Tailscale-Adresse dieses PCs (kein Vorher-Bild aus diesem Lauf; Vergleich mit `ux\handy_start.png` des Audits).

## Kommandos und Testergebnisse (Zahlen aus der Ausgabe)
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- App, gezielt (`Trendo.App.Tests.exe -class …`, 68 Klassen in einem Lauf, Stand des Commits `dd91ecd76`): **Total 526, Errors 0, Failed 0, Skipped 1, Not Run 0**, Dauer 793 s. Übersprungen ist `EveryButton_AllPages` (läuft nur mit `TRENDO_ALL_BUTTONS=1`, wie auf `origin/main`). `DesignPaket1_Ui` allein: 31 Tests, alle grün (41 s).
  Enthalten: `DesignPaket1_Ui`, `UebersichtEinfach_Ui`, `UebersichtVorschlaege_Ui`, `Modern_Ui`, `StudioFolds_Ui`, `StudioHeadingTests`, `DeadButtonTests`, `SimpleMode_Ui`, `EveryButton_Alltag`, `EveryButton_AllPages`, `StartMenu_Ui`, `StartMenu_StudioFenster`, `ProgramCatalog_Ui`, `PromptBarTests`, `Assist_Ui`, `EinfachBedienen_Ui`, `AddressBook_Ui`, `TodoBoard_Ui`, `CalendarGrid_Ui`, `Haushaltsbuch_Ui`, `Projektmappe_Ui`, `Lizenz_Ui`, `Hardware_Ui`, `Sys13_Qualitaet_Ui`, `ProgramFrameTests`, `DockLookUiTests`, `KiLibraryUiTests`, `PhoneEasyTests`, `W7_Ui`, `W0b_Look` (Sprach-Test W0b-01) und weitere Klassen der berührten Seiten.
- Core: `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter "…NoFixedUser|Wording|PlainWords|Sprache"` → 1 bestanden, 0 Fehler (nur `NoFixedUserTests` passt auf den Filter; eine Core-Klasse `WordingTests` gibt es nicht, der Sprach-Test W0b-01 liegt im App-Projekt und lief dort mit).
- Datenschutz-Grep über alle geänderten Dateien: leer.
- Kein Volltest (macht die Hauptsitzung).

Zwischenstände, ehrlich: Der erste gezielte Lauf hatte 4 rote Tests (`EveryButton_Alltag`: neuer Menüknopf ohne Eintrag; `StartMenu_StudioFenster` und `Side_names_match_windows`: mein erster Entwurf benannte auch Studio-Startseiten um; `Every_view_stays_clear`: drei Seiten über 400 Wörter, weil Einleitung und Modelle jetzt sichtbar sind). Behoben durch die Regel „Startseite heißt wie das Studio“, das Zuklappen des Entwicklerteils bei Handy und den einmaligen „So geht's“-Satz – nicht durch Lockern der Grenzen.

## Angepasste bestehende Tests (kein Test gelöscht oder abgeschwächt)
- `StudioHeadingTests.Heading_matches_catalog`: erwartet für Seiten mit eigenem Namen diesen Namen (V-10), prüft zusätzlich Fenstertitel = Seitenkopf.
- `StudioFolds_Ui.Host_strip_opens_page`: Weg über „Mehr“ → „Verwandte Programme“ (V-13).
- `ButtonProbe.Risiko`: zwei Einträge für die neuen Menüknöpfe.

## Was NICHT geprüft wurde
- Kein Volltest, keine Api-Tests, keine Paket-Tests.
- Nicht in der echten App mit Maus und Tastatur bedient; die Klappmenüs („Mehr“, „Verwandte Programme ▾“) sind nur über den Baum und per ausgelöstem Klick geprüft, nicht als geöffnetes Fenster im Bild.
- Stufe „Erweitert“ und „Professionell“ nur in Tests, nicht im Bild. Heller Stil, Stile Windows/macOS/GNOME, andere Fenstergrößen, große Schrift: nicht gerendert. Die Leiste mit Wörtern ist nur im Trendo-Stil angesehen.
- Leiste mit vielen hinzugefügten Web-Apps (Breite) nicht ausprobiert.
- Adressbuch, Aufgaben, Projekte, Großprojekte mit vielen Einträgen nur über die bestehenden Tests, nicht im Bild.
- Programme mit geöffnetem Dokument (ton, bild, schnitt) nicht neu gerendert; dort gibt es keinen Seitenkopf, die verwandten Programme stehen unter „Verwandte Programme ▾“.

## Offene Punkte
1. **V-10, zwei Namen für dieselbe Seite:** 18 Seiten in Einstellungen und KI-Zentrale sind zugleich Startseite eines Studios und heißen in der linken Leiste der App anders als das Studio (z. B. Leiste „Leistung messen“ – Studio „Qualität prüfen“; „KI-Quellen“ – „Kostenlose KI finden“; „KI-Helfer und Teams“ – „KI-Team einsetzen“; „Sehen & Bedienhilfen“ – „Schrift & Farben anpassen“). Dort steht weiter der Studio-Name im Fenstertitel. Ganz einheitlich wird es erst, wenn je Seite ein Name gilt – das ist eine Namensentscheidung (Katalog oder Leiste), kein Design-Detail.
2. **Seite `ki`:** Fenster „KI-Zentrale“ (T-0663), Kopf „KI“, Leiste „Trendo KI (lokal)“ – unverändert gelassen, gehört zur selben Entscheidung.
3. **Programm-Hülle** (`MainWindow.ProgramShell.cs`, gesperrt): Der Hinweis „Vorlage … geöffnet.“ steht weiter als Karte im Kopf (nur der Knopf „Kopieren“ ist weg); der schmale Kopf ist V-01. Für Programme mit eigenem Namen bleibt über der Programm-Kopfzeile eine schmale Zeile mit „Mehr“ und „?“.
4. **V-06:** Auf `doku-start` bleibt das Suchfeld „Dokumente suchen …“ sichtbar, während „Zuletzt benutzt“ eingeklappt ist.
5. **V-18:** Interne Kennungen im Geräteverbund und Seitenschlüssel in der Skill-Liste (Befund S-15) sind nicht Teil dieses Pakets.
6. Zeilenenden: Git meldet bei einigen Dateien „LF will be replaced by CRLF“ – keine inhaltliche Änderung.

## Status
Umgesetzt und mit gezielten Tests belegt; wartet auf Volltest, Review und Freigabe der Hauptsitzung.

```
ENTWICKLUNGSBERICHT
- Projekt: Trendo OS, T-0684 Design-Paket 1
- Verwendete Sprachen: C# (.NET 10, Avalonia 12)
- Rust eingesetzt: NEIN
- Rust-Komponenten: keine
- Grund der Sprachwahl: reine Oberflächen-Korrekturen in der bestehenden C#-App; kein Engpass, den Rust lösen würde
- Durchgeführte Tests: siehe oben (gezielte App-Klassen, Core NoFixedUser)
- Gemessene Verbesserungen: keine Leistungsmessung (nicht Ziel des Auftrags)
- Offene Probleme: siehe „Offene Punkte“
```
