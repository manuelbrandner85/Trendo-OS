# T-0266 – CNC & Fertigung als echtes Programm „CNC-Werkstatt“

- Freigabe: Manuel im Chat am 31.10.2026 („alles Offene in jedem Studio umsetzen“, Masterprompt GODMODE 131)
- Vorbilder (Funktionsumfang, öffentlich dokumentiert): CAMotics, NC Viewer, bCNC; kein Code übernommen (siehe `docs/claude/vorbilder/cnc-camotics.md`)
- Umgesetzt: G-Code-Editor (F5/Strg+Enter/Klick in die Vorschau prüft), Werkzeugweg-Vorschau von oben oder schräg (drehen, zoomen) mit Arbeitsraum-Rahmen und Tiefenfarbe, Simulation (Abspielen, Regler, Anfang/Ende) mit Fräser und mitlaufender Zeilenmarkierung, Prüfhinweise mit Sprung zur Zeile, Generatoren (Tasche, Kontur, Lochkreis) aus Werkstück-/Fräserwerten, Datei laden, Nullpunkt verschieben, Export .nc/SVG/Prüfbericht, Rechner (Drehzahl & Vorschub mit Werkstoffliste, Zerspanleistung, Teilekosten aus der Programmzeit, Kernloch) sofort.
- Gefunden und behoben: Klick in die Vorschau übernahm getippten G-Code nicht (Vorschau nahm keinen Fokus)
- Tests: Core 2 (Segmente = Prüfung, Bögen, Zoll/relativ, Bericht), App 2 (echte Eingabe) ersetzen SPEC14_Ui/SPEC14_UiPlus; FachBedienungTests auf SPEC-16 umgestellt
