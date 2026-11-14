# Bericht T-0619: Sammelstand „alles Offene durchziehen“ (14.11.2026)

**Zweig:** `claude/T-0619-sammel` von `main` (cce5c6261), danach nach `main`.

## Enthalten

| Auftrag | Inhalt | Bericht |
|---|---|---|
| T-0614 | Kreativ & KI Teil 3: Videos mit KI, KI-Agenten bauen, KI-Modelle vergleichen, Eigene KI trainieren | `tasks/T-0614-kreativ-ki-3.bericht.md` |
| T-0615 | Kreativ & KI Teil 4: Code mit KI, Bilder erkennen, Recherche mit Quellen, Tagesplaner | `tasks/T-0615-kreativ-ki-4.bericht.md` |
| T-0616 | Einfacher bedienen E1–E12 (Freigabe 14.11.2026) | `tasks/T-0616-einfach-bedienen.bericht.md` |
| T-0617 | Reste aus dem ruhigen Design (Dichte Kompakt, letzte Schmuckzeichen) | `tasks/T-0617-design-reste.bericht.md` |
| T-0618 | kie.ai: Veo 3.1, Suno-Geräusche, eigene Stimme | `tasks/T-0618-kie-ausbau.bericht.md` |

Zusätzlich hier: „Bilder mit KI“ speichert den Bildordner im Dokument (Bilder fehlten nach dem Wiederöffnen; Befund aus der Durchsicht von T-0614), „Modelle vergleichen“ nutzt verständliche Fehlersätze.

## Zusammenführung

- Konflikte nur an Registrierstellen (W6-Init, Zähl-Tests, kie.ai-Zuordnung je Studio, Programmkatalog). Zähler jetzt: 75 Studios, 102 Adapter, 72 teilweise fähige Studios, 53 in der Übersicht. Programmkatalog neu erzeugt (117 Programme), Fähigkeitsmatrix neu geschrieben.

## Volltest

- Lauf 1: Api 16; Core 1 Fehler (Temp-Zugriff, beim Wiederholen grün); App 4 Fehler; Paket-Tests 1 Fehler. Ursachen und Behebung:
  - Eigene deutsche Meldungen ohne typische Füllwörter („… mindestens zwei Kategorien“) und mit kurzem Kopf („Routing-Kreis: …“) wurden vom neuen Fehlerbaustein durch den Allgemeinsatz ersetzt → Erkennung erweitert, neuer Test.
  - Funktions-Baseline meldete 5 „fehlende“ Funktionen, die mit E1/E4/E11 an einen neuen Ort gewandert sind (KI-Menü, Dateiauswahl, eine Befehlssuche, ein Suchfeld) → Baseline auf die neuen Elemente umgeschrieben, Funktions-IDs unverändert.
  - Widget-Test kannte die E12-Vorgabe (Kachel in „Einfach“ anfangs aus) noch nicht; Grundriss-Test muss die Werkzeugleiste in einem niedrigen Fenster erst sichtbar scrollen.
- Lauf 2 (Stand eff26d660): Exitcode 0 – Api 16; Core 2488 bestanden, 5 übersprungen; App 1283 bestanden, 5 übersprungen (43 min); Paket-Tests OK. Auszug `reports/volltest-T-0619/`.

## Nicht geprüft / offen

- Echte Aufträge mit kie.ai (Schlüssel der Person), echtes Mixamo, echte lokale Modelle und echte Grafikkarte über die Oberfläche; Dateiauswahl und KI-Menü nur ohne Fenster geprüft.
- Aus T-0616: automatisch angelegte „Unbenannt“-Datei wird bei späterer Titeländerung nicht umbenannt; im Fokusmodus fehlt der Knopf „Markierung an Claude“ (Tastenkürzel und Rechtsklick bleiben); wer früher „Einfacher Modus“ und „Profi“ zugleich hatte, landet auf „Einfach“.
- Aus T-0615: sehr große, böswillige BibTeX-Datei kann den Import spürbar aufhalten (höchstens 5 MB).
