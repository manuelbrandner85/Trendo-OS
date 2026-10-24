# IMP-13 – keine toten Schaltflächen

Auftrag T-0004. Headless-Test `DeadButtonTests` öffnet die Startseite, Alle Studios und jede Studio-Seite aus `StudioPages` und `PageModules`. Jede sichtbare, aktivierte `Button` ohne Click-Handler (Reflexion auf `Interactive._eventHandlers` / `Button.ClickEvent`) und ohne `Command` gilt als tot.

Erster Lauf: sieben Treffer, alle `CheckBox` (ToggleButton). Der Klick schaltet `IsChecked` um; die Kästchen unter Bedienung speichern zusätzlich über `IsCheckedChanged`. Keine echte tote Schaltfläche. Keine Änderung an `MainWindow*.cs`, kein Tooltip „Noch nicht verfügbar“. SPEC-07 nicht gebaut.

Ausnahmen im Test: ToggleButton (CheckBox, RadioButton); Knöpfe mit `TemplatedParent` (Avalonia-Vorlage). Pro Katalogstudio ein Abnahmefall: Seite, `page-title` = Katalogname, keine tote Taste, mindestens eine Hauptaktion. CORE-01 ist der Startbildschirm ohne `page-title`. SPEC-07 hat keine Seite.

Release, danach grün: Core 427, App 188, Api 14, jeweils 0 Fehler. Darin die neuen 56 Fälle (54 Studios, Sammellauf, Ausnahmenliste).
