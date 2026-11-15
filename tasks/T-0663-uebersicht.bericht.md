# T-0663 Bericht: Programme übersichtlicher (V1–V10)

**Auftrag:** „Für den Nutzer sind die Programme immer noch zu unübersichtlich in der Bedienung und im Verständnis – Vorschläge machen, dass alles viel besser wird, aber nichts an Funktionen verlieren.“ Alle zehn Vorschläge freigegeben am 15.11.2026 („Alle Ja“). Dazu der Hinweis des Nutzers: Die linke Leiste zeigte „Programmieren“, das Studio heißt aber „Webseite bauen“.
**Branch:** `claude/T-0663-uebersicht` von `origin/main` 1fc842acc.

## Messung vorher

Alle 197 Ansichten in der Stufe „Einfach“ gemessen (sichtbare Knöpfe, Knöpfe ohne Wort, Wörter):

| | Vorher | Nachher |
|---|---|---|
| Webseite bauen | 75 Knöpfe, 60 Felder, 1144 Wörter | 30 Knöpfe, 252 Wörter |
| Studio-Übersicht | 86 Knöpfe, 993 Wörter | Bereichs-Kacheln, innerhalb der Grenze |
| Knöpfe nur mit Zeichen | 735 (24 Ansichten mit 8 oder mehr) | 0 außer allgemein bekannten (Hilfe, Reiter schließen, Rechnertasten) |
| Ansichten über der Grenze (40 Knöpfe, 8 ohne Wort, 400 Wörter) | 24 | 1 begründete Ausnahme (Taschenrechner) |

## Umsetzung

- **V1 Webseite bauen:** In „Einfach“ der geführte Weg (beschreiben → „Seite bauen“ → Vorschau → Editor/KI → Veröffentlichen). Seiten und Adressen, Gestaltung, Bausteine, Medien, Code, Inhalte, Sprachen, Erweiterungen, Daten sowie 3D/Video, Code-Ansicht, Qualitätsprüfung und Software-Projekte in Klappbereichen „Mehr Werkzeuge“ (Zustand bleibt beim Neuaufbau).
- **V2 Lange Listen:** zentral nach jedem Seitenaufbau (`SimplifyView`): eine Folge von mindestens 12 gleichartigen Einträgen zeigt die ersten 8 und „Alle … anzeigen“. Zusatzprogramme und Desktop-Symbole in Gruppen zum Aufklappen, „Hinzufügen“ vier je Gruppe mit „Alle … zeigen“, Brief ohne doppelte Schrittleiste, Kleidung acht Grundfarben und ein Knopf für die Produktions-Ausgaben.
- **V3 Wort am Knopf:** zentraler Klassen-Handler (`SimpleWords`): Symbol-Knöpfe im Seiteninhalt bekommen in „Einfach“ das Wort aus ihrem Hinweis. Nicht in Fensterleiste, Hülle oder Claude-Bereich. Beim Stufenwechsel zurückgenommen, das Symbolzeichen bleibt für Claude und Strg+K abrufbar.
- **V4 Ein KI-Weg:** Studio-Übersicht, Suche und 3D zeigen in „Einfach“ nur „KI ▾“ (Claude und „Programme hinzufügen“ darin). Fokusmodus, Erweitert und Profi wie bisher.
- **V5 Texte & Tabellen:** drei Reiter (Start, Einfügen, Ansicht) und „Mehr ▾“. Die Formatvorlagen-Kacheln entfallen in „Einfach“, die Liste „Absatzart“ wählt dieselben Vorlagen.
- **V6 Musik-Studio:** eine Reiterleiste. Die Art (Lied, Sound, Sprache) wählt in „Einfach“ eine Liste. Die eigene Umschaltung heißt in allen Stufen „Nur beschreiben / Mit eigenem Songtext“ statt „Einfach/Erweitert“. Die Ideen-Woche gibt es in „Einfach“ nur für heute.
- **V7 Alltagswörter:** feste Knopf-Beschriftungen (CSV, ZIP, DXF, SVG, XRechnung, ICS, UV, Render, Pixel Streaming, GitHub-Suche …) in „Einfach“ als Alltagswort, das Fachwort steht im Hinweis. Nicht bei Inhalten der Person (Claude, PDF-Felder, Abläufe). „GitHub“ wird nur am Reiter von „Hinzufügen“ ersetzt.
- **V8 Weniger Text:** Auf Übersichtsseiten stehen Beschreibungen mit mehr als 25 Wörtern in einer Zeile, ein Klick zeigt alles. Statuslisten (Hardware, Lizenz) werden gekürzt, Fortschritt zeigt die Wellen zum Aufklappen. Nie bei Chat oder Protokoll.
- **V9 Studio-Übersicht:** „Zuletzt benutzt“ und eine Kachel je Bereich, ein Klick zeigt die Studios des Bereichs.
- **V10 Grenze als Test:** `UebersichtEinfach_Ui` misst jede Ansicht. Ein zweiter Test vergleicht „Einfach“ mit „Erweitert“: Jedes Bedienelement ist vorhanden oder hat einen genannten Ersatzweg. Vergleichsstand vor T-0663: `tests/Trendo.App.Tests/Daten/einfach-vorher-ausgeblendet.txt`.
- **Einheitliche Namen:** Jeder Eintrag der linken Leiste heißt wie sein Fenster (Webseite bauen, KI-Zentrale, KI-Team einsetzen, Kostenlose KI finden, Geräte erkennen, Handy verbinden). Ein Test klickt alle Einträge an.

## QA

Unabhängige Prüfung (Unteragent, nur lesend), 11 Befunde, alle behoben:

1. Die Kürzung hätte den Claude-Chat erfasst.
2. Wiederholtes Anwenden führte zu doppelten Knöpfen und Klick-Handlern.
3. V4 änderte auch Erweitert/Profi; im Fokusmodus fehlte auf 3D „Programme hinzufügen“.
4. „GitHub“ → „Freie Programme“ auch am Hochlade-Knopf.
5. Wörter an Fensterknöpfen, kein Zurücknehmen beim Stufenwechsel.
6. Das Symbolzeichen ging nach dem Einwickeln verloren.
7. Ein Klick auf gekürzten Text wurde von Karten verschluckt.
8. Kleidungs-Ausgaben waren für Claude nicht auffindbar (ContextMenu → Klappmenü).
9. Alltagswörter hätten Inhalte der Person ersetzen können.
10. Der Zustand von „Mehr ▾“ blieb nach dem Stufenwechsel hängen.
11. Testlücken.

Zusätzlich behoben: Wettlauf im Test „Bilder erkennen“ (wartete auf „geprüft“, das schon in „werden geprüft …“ steht).

## Tests

- Neu: `UebersichtEinfachUiTests.cs` (2), `UebersichtVorschlaegeUiTests.cs` (13).
- Gezielt: 754 von 759 grün (4 übersprungen, 1 Wettlauf im alten Test → behoben), danach Nachläufe 24/24 und 74/74.
- Echte App-Bildschirmfotos in „Einfach“ angesehen (Studios, Texte, Webseite bauen).
- Volltest 1: Api 16/16, Core 2567/2572, App 1326/1333 – rot nur „Pilot Text Leistung“ (Zeitgrenze 3 s, einzeln 2 s). Darauf schneller Ausstieg im Wort-Handler, wenn „Einfach“ nirgends gilt.
- Volltest 2 (`dotnet test Trendo.sln -c Release`): Api 16/16, Core 2567/2572 (5 übersprungen), App 1327/1333 (6 übersprungen) – grün. Python 11/11.
- Release 0.9.101: Version und Neuerungen stehen bereit; das Release baut die Hauptsitzung nach OK des Nutzers.

## Festgestellt, nicht Teil dieses Auftrags

Von den 79 am 09.11.2026 freigegebenen neuen Studios sind 63 nicht gebaut. Entscheidung des Nutzers: nach dem Release zuerst das Künstlerstudio (K1–K8 alle Ja), danach die fehlenden Studios etappenweise.
