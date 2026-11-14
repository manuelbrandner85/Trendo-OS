# Bericht T-0616: Trendo OS einfacher bedienen (E1–E12)

**Zweig:** `claude/T-0616-einfach-bedienen` (Worktree, Basis `origin/main` cce5c6261). Nicht auf `main`.

## Inhalt

- **E1 Ein KI-Knopf:** Neben „Los“ steht nur noch „KI ▾“ (`ki-menu`, Klappmenü als Popup). Darin: Claude, Markierung an Claude (jetzt in jedem Fachprogramm), Weiter mit dem letzten Claude-Projekt, kie.ai (öffnet sein Klappfeld am KI-Knopf), Programme hinzufügen. Die Einträge behalten ihre Namen (`auftrag-claude`, `auftrag-kie` …). Ziehen auf „KI ▾“ gibt Dateien und Text an Claude. Das zweite „Claude“/„Markierung“ im Programmband ist entfallen.
- **E2 Einfach räumt auf:** In „Einfach“ sind Stufenwahl, Eigenschaften-Spalte und Rand-Schalter eingeklappt. „Mehr Werkzeuge“ (`prog-tools-more`) holt sie. Die Stufe bleibt wählbar: im Menü „Mehr“ (`prog-stufe-erweitert/-profi`) und in den Einstellungen.
- **E3 Automatisch speichern:** Ein geändertes Dokument wird 1,5 s nach der letzten Änderung gespeichert. Ein neues Dokument wird dabei im Studio-Ordner angelegt (vorhandene Dateien werden nie überschrieben). Danach wird bei jeder Änderung gespeichert, ebenso beim Schließen, im 30-s-Takt und beim Beenden.
  - Leere, unveränderte Dokumente legen keine Datei an.
  - Höchstens alle 10 Minuten bleibt ein früherer Stand als Version (`ProgramDocument.Save(keepVersion)`, `VersionDue`), damit die 20 Versionen nicht nach 20 Klicks voll sind.
  - Die Anzeige zeigt nur noch „✓ Gespeichert“ bzw. „Wird gespeichert …“.
  - Solange eine Änderung von Claude noch nicht behalten oder verworfen ist, wird nicht gespeichert.
  - Schlägt das Speichern fehl, erscheint eine Meldung mit „Anderen Ort wählen“.
  - Abschaltbar unter Einstellungen → Sehen & Bedienhilfen → „Automatisch speichern“. Ausgeschaltet gelten Speichern von Hand und die Sicherung wie bisher; „Wiederherstellen“ bleibt.
- **E4 Dateiauswahl:** „Anderes Dokument öffnen …“ (Startansicht) und „Öffnen …“ (Menüband) nutzen die Dateiauswahl des Systems, ebenso „Speichern unter …“ → „Ort wählen …“. Das Eintippen von Ordner und Namen bleibt im Klappfeld, auch für Claude. Tests ersetzen die Auswahl über `PickOverride`.
- **E5 Weitermachen:** Die Startansicht hat oben die Karte „Weitermachen: <Dokument>“. Die Desktop-Karten „Weitermachen“ öffnen bei Programmen das zuletzt bearbeitete Dokument. Gemerkt wird je Programm (`LastProgramDoc`), weil ein Studio mehrere Programme haben kann.
- **E6 Aufgaben starten direkt:** Ein Klick auf „Was möchtest du machen?“ füllt die Eingabe.
  - Fehlt noch eine Angabe (Thema, offenes Dokument, unvollständiger Satzanfang), fragt Trendo darunter kurz nach. „Los“ startet dann mit der Antwort.
  - Ist der Auftrag vollständig, startet er sofort über den normalen Ablauf (Plan, Rückfrage, Freigabe bei sensiblen Schritten, am Ende „Behalten?“).
- **E7 Fehler mit „Was tun?“:** Der Kern `Trendo.Core.FriendlyError` macht aus einer Ausnahme einen Alltagssatz mit genau einem Knopf.
  - Fehlerarten: Datei fehlt, kein Zugriff, Datei belegt, Laufwerk voll, kein Internet, Dienst lehnt ab, Zeit abgelaufen, Programm fehlt, Datei beschädigt.
  - Knöpfe: Nochmal versuchen, Einrichten, Anderen Ort wählen, Claude fragen. „Claude fragen“ füllt nur die Eingabe, abgeschickt wird von der Person.
  - Eigene deutsche Meldungen bleiben unverändert. Englische Systemmeldungen werden übersetzt; hängen sie an einem eigenen Satz, bleibt nur der eigene Teil.
  - Der technische Text ist aufklappbar („Technische Einzelheiten“).
  - Eingehängt in `ProgramRun` (Fehlerkarte), in den Seiten-Helfer `Try` und in die Auftragsleiste.
  - Neu ist `ToastError`: Der Ton der Benachrichtigung wird ausdrücklich über ⚠/✕ gesetzt statt aus Wörtern erraten.
  - 172 Stellen `"…: " + ex.Message` zeigen jetzt `FriendlyError.Say(ex)`. Ausnahmen sind Claude-Brücke, Claude-Ersatzweg, USB-Schreiben und Protokolle: Dort bleibt der rohe Text, weil Claude oder ein Protokoll ihn braucht.
- **E8 Kurze Einführung:** Beim ersten Öffnen eines Programms erscheinen drei kurze Schritte (Hier anfangen, Hier hilft die KI, Speichern passiert von selbst). Sie sind überspringbar und in `SeenHints` gemerkt; „Hilfe → Einführung“ zeigt sie wieder. Es ist eine Karte, kein Dialog. In abgeschotteten Tests ist sie aus (`ProgramIntroOn`).
- **E9 Eine Wahl:** In den Einstellungen gibt es nur noch „Bedienung: Einfach, Erweitert oder Profi“; der Einrichtungs-Assistent bietet dieselbe Wahl. Der frühere Schalter „Einfacher Modus“ heißt jetzt ehrlich „Größere Knöpfe“. Die Umrechnung steht in `Sys.Quality.MigrateSettings` (Format 4): Wer den einfachen Modus an hatte, bekommt die Stufe „Einfach“.
- **E10 Alltagswörter:** `PlainWords.Everyday` gilt in allen Stufen, das Fachwort steht im Hinweis.
  - Linke Leiste: „Command Center“ → KI-Zentrale, „Agenten“ → KI-Helfer, „Assets“ → KI-Quellen, „GitHub & Code“ → Programmieren, „Mobile“ → Handy.
  - Widgets: Welche KI hilft, KI-Helfer bei der Arbeit, Studios auf dem Desktop, Freie Programme, Dein Trendo.
  - Einstellungen und KI-Zentrale: KI-Fähigkeiten (Skills), KI-Helfer und Teams.
  - kie.ai-Guthaben in Punkten (Credits).
  - Profi-Knopf „Gespeicherte Daten“ statt „{ } Projektdaten“.
- **E11 Ein Suchfeld:** Die Studio-Übersicht hat ein Feld: Tippen filtert, Enter sucht überall. Die Profi-„Befehle suchen“ ist in Strg+K aufgegangen; die Befehlsleiste findet jetzt alle Befehle des offenen Programms, auch die, die die Stufe ausblendet. Die Hilfetexte zu Strg+K sind korrigiert.
- **E12 Schlanker:** In „Einfach“ sind rechte Widget-Spalte, Erdkugel und Schnellaktionen ausgeblendet; die Mitte bekommt den Platz. Unter „Startbildschirm anpassen“ lassen sie sich einblenden (gemerkt in `ShownTiles`). Die Einstellungen zeigen in „Einfach“ 6 Hauptseiten, der Rest steht unter „Mehr Einstellungen“; die Suche findet immer alles.

## Unabhängige Prüfung (Subagent, nur lesend) – behoben

- **hoch:** Verworfene Claude-Änderung hätte schon auf der Platte gestanden. Jetzt wird nicht gespeichert, solange die Entscheidung offen ist oder eine Live-Animation läuft.
- **hoch:** Eine Titeländerung zählte nicht als Änderung. Jetzt gilt sie als Änderung.
- **hoch:** „Claude fragen“ schloss ein offenes Claude. Jetzt nur noch öffnen bzw. vorbefüllen.
- **hoch:** Aufgaben mit unvollständigem Satzanfang wären sofort abgeschickt worden. Jetzt fragt Trendo „Was genau?“.
- **mittel:** HTTP-Ablehnung (401/429 …) erschien als „kein Internet“. Jetzt gibt es eine eigene Meldung mit „Einrichten“.
- **mittel:** Englisch-/Deutsch-Erkennung war fehleranfällig. Jetzt zählt nur ein deutsches Merkmal und der Teil vor „: “, „(Parameter …)“ wird entfernt, und NullReference und Verwandte gelten nie als eigene Meldung.
- **mittel:** Eigenes Abbrechen erschien als Zeitablauf. Behoben.
- **mittel:** „Weitermachen“ konnte das Dokument eines anderen Programms desselben Studios öffnen. Jetzt wird je Programm gemerkt.
- **mittel:** Fehlgeschlagenes automatisches Speichern blieb stumm. Jetzt erscheint eine Meldung, und die Anzeige gilt nur für das eigene Dokument.
- **mittel:** „Anderen Ort wählen“ passt jetzt zum jeweiligen Fall.
- **mittel:** Technischer Text für Claude und Protokolle ist wieder roh.
- **niedrig:** Kein Absturz mehr im Takt; Fehler der Dateiauswahl werden angezeigt; ein alter Seitenfehler gilt nur für seine Seite.

## Testergebnis (gezielt, nicht der Volltest – den macht die Hauptsitzung)

- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- `dotnet test tests/Trendo.Core.Tests -c Release`: Bestanden – 2455 erfolgreich, 5 übersprungen, 0 Fehler (darin neu `EinfachBedienenTests` 3 Tests, `Sys13Tests` Migration Format 4).
- App, 34 Klassen in einem Lauf (`Trendo.App.Tests.exe -class …`): Total 308, Failed 0, Errors 0 (1291 s). Klassen: EinfachBedienen_Ui (10 neu), ProgramShellUiTests, SimpleMode_Ui, ProgramLevel_Ui, ClaudeOperate_Ui, ClaudeEverywhere_Ui, Kie_Ui, ClaudeDock_Ui, ClaudePrograms_Ui, ClaudeChat_Ui, ClaudeLive_Ui, Claude_Ui, Connections_Ui, TrendoAgent_Ui, ProposalsUiTests, ProfiRahmen_Ui, ClaudePolish_Ui, PathPickAuditTests, DeadButtonTests, EveryButton_Alltag, Sys12_Bedienung_Ui, StudioFolds_Ui, Design41RolloutTests, ReferenzStudioSpiele_Ui, StudiosKreativ3D_Ui, StudiosBueroGeld_Ui, StudiosTechnikWissen_Ui, DesktopIconsUiTests, OsStyles_Ui, ClaudeBridge_Ui, AppsAndSetupResumeTests, Leistungsprofil_Ui, Einfachheit_Ui, StartMenu_Ui.
- Angepasste Tests (gewolltes neues Verhalten): ClaudeOperate (Claude/Markierung im Menü „KI ▾“, kein `prog-claude`), ClaudeEverywhere (sichtbar muss der Weg zu Claude sein), Kie (Eintrag im Menü), ProgramLevel (Stufenwahl in Einfach hinter „Mehr Werkzeuge“, Befehlssuche über Strg+K), ButtonProbe.Risiko (`ki-menu` statt `prog-claude*`), Sys13 (Format 4).
- Vorher rot und behoben: Namenskollision `prog-more-tools` mit `prog-more` (Präfixsuche der Tests) → `prog-tools-more`; 3D-Seite (KI-Menü der Seitenspalte statt schlankem Claude-Knopf gemessen).

## Offene Punkte / nicht geprüft

- Ein automatisch angelegtes „Unbenannt.tdoc“ wird bei späterer Titeländerung oder „Speichern unter“ nicht umbenannt bzw. entfernt.
- Im Fokusmodus gibt es keinen Knopf „Markierung an Claude“. Strg+Umschalt+A und der Rechtsklick „Claude fragen“ bleiben.
- Die Umrechnung setzt bei früher eingeschaltetem „Einfacher Modus“ die Stufe auf „Einfach“, auch wenn vorher „Profi“ gewählt war (so beauftragt).
- Echte Systemdateiauswahl und Popup-Menü nur im Headless-Test (Übersteuerung), nicht am echten Bildschirm.
- Kein App-Volltest, kein Klick-Audit über alle Seiten (TRENDO_ALL_BUTTONS).

## Status

Auf dem Zweig, nicht auf `main`. Keine Abnahme durch eine Person.
