# Trendo UX/UI-Redesign 4.0

> Entwurf. Das bestätigte Logo „Gold-Orbit“ und der Dunkel-Gold-Look bleiben unverändert.

## Was es heute gibt

| Baustein | Datei | Stand |
|---|---|---|
| Programm-Hülle mit Menüband, Dateien, Bedienstufen | `MainWindow.ProgramShell.cs` | in allen angemeldeten Programmen |
| Rückgängig bis 100 Schritte, Sicherung, Versionen | `ProgramDocument.cs` | für `.tdoc`-Programme |
| Stufe „Einfach“ | `MainWindow.SimpleMode.cs` | vorhanden |
| Ziehbare und einklappbare Spalten | `MainWindow.Panes.cs` | nur in Trendo Musik genutzt |
| Rechtsklick-Menüs | `MainWindow.ContextMenus.cs` | nur in Trendo Musik genutzt |
| Eigene Linien-Symbole | `TrendoIcons.cs` | nur teilweise genutzt |
| Hinweise, Leerzustände, Befehlsleiste | `MainWindow.ProgramFrame.cs` | überall |
| Typografie und Übergänge | `MainWindow.Look.cs`, `MainWindow.axaml.cs` | überall |

## Was fehlt

| Forderung | Stand | Vorschlag |
|---|---|---|
| Frei andockbare Panels | fehlt | eigene Andock-Fläche in der Programm-Hülle, Layout je Programm gespeichert |
| Arbeitsbereiche speichern und wiederherstellen | nur Spaltenbreiten | Layout-Datei je Programm, Standard zurückholen |
| Vollbild | je Programm unterschiedlich | ein Befehl in der Hülle |
| Mehrere Monitore | nur im TV-Teil | Panel als eigenes Fenster, soweit das System es erlaubt |
| Kontextabhängige Werkzeugleisten | Menüband je Programm | Bereich je Auswahl |
| Rechtsklick überall | nur Musik | Listen und Arbeitsflächen je Programm |
| Tastaturbedienung | uneinheitlich | Tastenliste je Programm, Prüfung im Test |
| Barrierefreiheit | Stufen und Schriften, sonst uneinheitlich | Fokusreihenfolge, Namen für Screenreader, Kontrastprüfung |

## Reihenfolge

1. Andock-Fläche und Layout-Speicher als gemeinsamer Baustein in der Programm-Hülle.
2. Am Pilot-Programm vollständig nutzen.
3. Erst danach auf weitere Programme übertragen. Programme, die nicht umgestellt sind, behalten ihr heutiges Layout.

## Grenzen

- Keine fremden Designs, Symbole oder Grafiken übernehmen. Vorbilder liefern nur Bedienkonzepte.
- Screenshots werden aus dem echten Programm erzeugt (`tools/Trendo.Render`). Entwürfe werden als Entwurf gekennzeichnet.
