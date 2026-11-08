# Trendo Umbauplan 4.0

> Entwurf. Umsetzung erst nach Freigabe. Reihenfolge nach dem Auftrag, Inhalte aus dem Audit.

## Abschnitte

| Nr | Abschnitt | Ergebnis | Stand |
|---|---|---|---|
| 1 | Audit | `TRENDO_REA_AUDIT_4.0.md` | Entwurf fertig |
| 2 | Inventar und Funktionssicherung | Inventar erzeugt; Funktionskataloge für 27 Studios | Inventar fertig, Kataloge offen |
| 3 | Recherche | Referenzen je Studio | 45 von 58 Studios mit mindestens 3; Bewertungen offen |
| 4 | Messung reparieren | Ampel, Test-Zuordnung, Formatliste korrigieren | offen |
| 5 | Gemeinsames Designsystem | Andock-Fläche, Layout-Speicher | offen |
| 6 | Pilot | ein Programm vollständig | offen, Auswahl durch den Nutzer |
| 7 | Abnahme des Piloten | Protokoll mit Testartefakten | offen |
| 8 | Übertragen | je Programm, schwächstes zuerst | offen |
| 9 | Zusammenspiel | Ketten mit Ende-zu-Ende-Test | offen |
| 10 | Gesamt-QA | Volltest, Dokumentation, Release nur nach OK | offen |

## Pilot: Empfehlung

**Videoschnitt** (Programm `schnitt` in CORE-08).

- Es ist ein bestehendes, viel genutztes Programm, 117 Tests im Studio. Daran lässt sich die Funktionssicherung wirklich beweisen.
- Mehrspur, Waveform, MLT-Export und melt sind schon da. Die Lücken zu Kdenlive und Resolve sind beschrieben (`docs/claude/vorbilder/video-kdenlive-resolve.md`).
- Es gehört zum Bereich von Sitzung A, stört also den Kleidungs-Ausbau von Sitzung B nicht.
- Ein neues Studio wie „Filme machen“ wäre leichter, beweist aber nicht, dass nichts verloren geht.

## Reihenfolge nach dem Pilot

1. **Rote Programme:** `spiele-programm`, nachdem die Erkennung repariert ist.
2. **Hohes Verlustrisiko:** SYS-10, SYS-09, CORE-08, CORE-02, CORE-01, SYS-11, SYS-12, SYS-13.
3. **Ohne Funktionskatalog:** die 27 Studios aus dem Inventar.
4. **Der Rest** nach Nutzen und offenen Punkten.

Die laufenden Teilaufgaben bleiben bestehen: Kleidung (SPEC-08) bei Sitzung B, „Filme machen“ F5–F11 und „Print on Demand“ P2, P5, P10 bei Sitzung A.

## Grenzen des Auftrags

- 58 Studios in dieser Tiefe sind viele einzelne Umbauten. Erst der Pilot zeigt, wie lange einer wirklich dauert.
- Mindestens 20 eigene Testfälle je Studio entstehen beim Umbau des Studios. Auf Vorrat geschriebene Testlisten wären Scheinnachweise.
- Für Spitzenprogramme wie Resolve, Blender oder Unreal gilt: Trendo baut die Bedienung und die Abläufe; die schwere Arbeit machen freie Fach-Engines oder angebundene Programme.

## Offene Entscheidungen des Nutzers

1. Pilot: Videoschnitt ja oder anderes Programm?
2. Zweige: wirklich je Auftrag ein eigener Zweig mit Merge nach OK, oder der bisherige gemeinsame Zweig?
3. VFX als neues Studio ja oder nein?
4. Darf die Programm-Hülle für Andock-Panels umgebaut werden?
