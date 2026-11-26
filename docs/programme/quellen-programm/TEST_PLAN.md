# Recherche mit Quellen – Testplan

Studio: Recherche mit Quellen (STUDIO-CITE-01) · Bereich: KI & Automatik · Seite: `quellen-programm` · Stand: 0.9.112 (Stand d6f9fcd)

8 Tests sprechen dieses Programm an.

- Recherche mit Quellen: Beispiel, Stil wechseln, Quelle anlegen und ausfüllen, Doppelte, Zitat mit Seite, BibTeX hin und zurück, aus der Recherche übernehmen
- STUDIO-CITE-01 (UI): DOI eintippen füllt die Quelle mit Herkunft, Rückgängig nimmt sie weg; RIS und CSL-JSON aus, RIS wieder ein (Netz ersetzt)
- STUDIO-CITE-01 echt: DOI 10.1038/nature14539 bei Crossref über den echten Lese-Weg (übersprungen nur ohne Internet)
- SYS-08 (UI): Suchen zeigt Treffer mit Herkunft, Klick liest im Lesemodus; Sammlung behalten übersteht den Neustart (Netz ersetzt)
- SYS-08 echt: Wikipedia-Suche und Lesen des ersten Treffers über den echten Lese-Weg (übersprungen nur ohne Internet)
- SYS-08: Sammlung behalten ausschalten fragt vorher; Fehler beim Verschieben zeigt eine Meldung ohne halben Zustand
- SYS-08: ohne Sammlung behalten beginnt jede Sitzung leer (wie bisher)
- X-07 echt: KI-Auftrag „Quelle holen“ lädt nach Freigabe ein HDRI von Poly Haven mit Beleg je Datei (übersprungen nur ohne Internet)
