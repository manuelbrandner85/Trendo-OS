# Recherche im Lesemodus – Testplan

Studio: Sicher recherchieren (SYS-08) · Bereich: Wissen & Lernen · Seite: `recherche` · Stand: 0.9.114 (Stand 99cbb5c)

14 Tests sprechen dieses Programm an.

- Recherche: zeigt zuerst nur den Artikel, Knopf wechselt ohne neuen Abruf zur ganzen Seite und zurück; ohne Artikel ein Hinweis
- SYS-08 (UI): Suchen zeigt Treffer mit Herkunft, Klick liest im Lesemodus; Sammlung behalten übersteht den Neustart (Netz ersetzt)
- SYS-08 Suche: Adressen der Suchdienste, Wikipedia-Antwort wird Trefferliste mit Herkunft
- SYS-08 Suche: DuckDuckGo-HTML – Ziel entpackt, Werbung, .onion und Heimnetz fallen weg
- SYS-08 UI: Seite im Lesemodus lesen, .onion wird nicht abgerufen, Sitzung beenden
- SYS-08 echt: Wikipedia-Suche und Lesen des ersten Treffers über den echten Lese-Weg (übersprungen nur ohne Internet)
- SYS-08: .onion nur Tor Browser, andere Schemata und Heimnetz abgelehnt, Webseiten im Lesemodus
- SYS-08: Lesetext ohne Skripte und Formulare, Links absolut; Sitzung flüchtig
- SYS-08: Sammlung behalten ausschalten fragt vorher; Fehler beim Verschieben zeigt eine Meldung ohne halben Zustand
- SYS-08: Sammlung behalten übersteht den Neustart, ausgeschaltet beginnt jede Sitzung leer; Suchen stehen im Bericht
- SYS-08: Sammlung behalten – Fehler beim Verschieben meldet sich klar und lässt alles, wie es war
- SYS-08: ohne Sammlung behalten beginnt jede Sitzung leer (wie bisher)
- T-0309: Recherche – Auswahl markieren (farbig im Text), Notiz, Farbe, Zitat mit Quelle kopieren, Literaturliste einfach/APA, Bericht mit Zitaten, Sitzungsende löscht
- T-0711 Recherche: Notizfeld – bleibt beim Neuzeichnen, Rückgängig, Notsicherung, Karte, Zurückholen
