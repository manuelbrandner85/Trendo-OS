# Recherche im Lesemodus – Testergebnisse

Studio: Sicher recherchieren (SYS-08) · Bereich: Wissen & Lernen · Seite: `recherche` · Stand: 0.9.114 (Stand 99cbb5c)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 14 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Recherche: zeigt zuerst nur den Artikel, Knopf wechselt ohne neuen Abruf zur ganzen Seite und zurück; ohne Artikel ein Hinweis | ✓ bestanden |
| SYS-08 (UI): Suchen zeigt Treffer mit Herkunft, Klick liest im Lesemodus; Sammlung behalten übersteht den Neustart (Netz ersetzt) | ✓ bestanden |
| SYS-08 Suche: Adressen der Suchdienste, Wikipedia-Antwort wird Trefferliste mit Herkunft | ✓ bestanden |
| SYS-08 Suche: DuckDuckGo-HTML – Ziel entpackt, Werbung, .onion und Heimnetz fallen weg | ✓ bestanden |
| SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden | ✓ bestanden |
| SYS-08 echt: Wikipedia-Suche und Lesen des ersten Treffers über den echten Lese-Weg (übersprungen nur ohne Internet) | ✓ bestanden |
| SYS-08: .onion nur Tor Browser, andere Schemata und Heimnetz abgelehnt, Webseiten im Lesemodus | ✓ bestanden |
| SYS-08: Lesetext ohne Skripte und Formulare, Links absolut; Sitzung flüchtig | ✓ bestanden |
| SYS-08: Sammlung behalten ausschalten fragt vorher; Fehler beim Verschieben zeigt eine Meldung ohne halben Zustand | ✓ bestanden |
| SYS-08: Sammlung behalten übersteht den Neustart, ausgeschaltet beginnt jede Sitzung leer; Suchen stehen im Bericht | ✓ bestanden |
| SYS-08: Sammlung behalten – Fehler beim Verschieben meldet sich klar und lässt alles, wie es war | ✓ bestanden |
| SYS-08: ohne Sammlung behalten beginnt jede Sitzung leer (wie bisher) | ✓ bestanden |
| T-0309: Recherche – Auswahl markieren (farbig im Text), Notiz, Farbe, Zitat mit Quelle kopieren, Literaturliste einfach/APA, Bericht mit Zitaten, Sitzungsende löscht | ✓ bestanden |
| T-0711 Recherche: Notizfeld – bleibt beim Neuzeichnen, Rückgängig, Notsicherung, Karte, Zurückholen | ✓ bestanden |
