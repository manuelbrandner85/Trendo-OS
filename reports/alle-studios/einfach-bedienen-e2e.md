# Einfach bedienen – 08.11.2026

Nutzerwunsch: Spiele bauen und alle anderen Studios sollen so einfach wie möglich sein, die vielen Knöpfe irritieren, aber keine Funktion darf verloren gehen. Alle Änderungen gelten nur in der Stufe **Einfach**, die für neue Nutzer voreingestellt ist. Erweitert und Professionell bleiben unverändert.

## Was sich ändert
- **Spiele bauen:** Nur noch ein Eingabefeld „Was soll sich ändern?“ und die Karte „Als Nächstes“, die den schwächsten Bereich in Alltagssprache nennt. Alles andere liegt unter „Alle Bereiche zeigen“.
- **Alle Programme mit Menüband:** Direkt sichtbar sind Speichern, Rückgängig und höchstens vier Programmbefehle. Der Rest liegt unter „Mehr“.
- **3D-Modelle erstellen:** Grundfunktionen bleiben, Export, Fachwerkzeuge, Vorschaubilder und Umwandeln erscheinen nach „Mehr Werkzeuge anzeigen“. Felder wie Position und Drehung behalten ihre Standardwerte.
- **Übrige Seiten:** Hat eine Seite mehr als etwa 14 eigene Knöpfe, erscheint der Rest nach „Mehr anzeigen“. Meldungen bleiben sichtbar.
- **Seitenkopf:** „Freie KI“ und „Team“ liegen in einem kleinen „Mehr“.
- **Alltagssprache:** Zum Beispiel „Spielkonzept“ statt „Spielbibel“, „Eigenschaften“ statt „Inspektor“. Eigene Inhalte werden nie umgeschrieben.

- **3D-Modelle erstellen, in jeder Stufe:** Neue Karte „Drucken & Modelle“ mit „Drucken mit Bambu Studio“ (speichert das Modell als STL und öffnet die Seite 3D-Druck), „Drucker steuern“ und „Freie 3D-Modelle suchen“. Die Funktionen dahinter gab es schon, sie waren von hier aus nicht erreichbar. Mit einem echten Bambu-Drucker und echten Thingiverse-Schlüsseln ist das nicht geprüft.

## Fehler, die der Volltest gefunden hat
Der Einrichtungs-Assistent für neue Nutzer klappte seine Auswahlpunkte ein. Dort wird jetzt nichts eingeklappt, und Auswahlfelder zählen nicht mehr als Knöpfe.

## Befund vorher
Gezählt wurden alle sichtbaren Knöpfe der Seite, auch die feste Navigation. „Spiele bauen“ hatte offen 53 Knöpfe, in Einfach und Erweitert gleich viele. Bei vielen Studios stammt der Großteil aus der festen Navigation: Dort hat die Seite selbst nur wenige Knöpfe und ändert sich deshalb nicht.

## Bewusst unverändert
- **Trendo Musik, TV & Live, Live streamen, Videos & Fotos:** Die Seiten pflegt Sitzung A. Das Menüband von Videoschnitt und Audio folgt trotzdem der neuen Regel.
- **Webseite bauen:** wird separat gepflegt.
- **Briefe schreiben:** ist bereits ein übersichtlicher Assistent. Die hohe Zahl kommt von den Vorlagen-Kacheln.
- **Windows-, Mac- und GNOME-Stil:** Dort bleibt die Menüleiste, weil sie schon aufgeräumt ist.

## Geprüft
- `SimpleMode_Ui`: In jedem Programm bleibt jeder Befehl der Stufe erreichbar, direkt oder unter „Mehr“. Dieser Test hat einen echten Fehler gefunden (das Menüband wurde zweimal umgebaut).
- Spiele bauen, 3D und eine übrige Seite: zugeklappt weniger, aufgeklappt alles, Erweitert unverändert.
- Spiele bauen, 3D und „Kostenlose KI finden“ gerendert und angesehen.

## Nicht geprüft
- Rückmeldung von echten Nutzern ohne Vorwissen.
- Jede einzelne der übrigen Seiten als Bild.
