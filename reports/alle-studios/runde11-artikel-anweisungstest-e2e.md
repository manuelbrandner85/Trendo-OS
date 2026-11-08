# Runde 11: Artikel-Ansicht und Anweisungs-Test – 08.11.2026

## SYS-08 „Sicher recherchieren“: nur der Artikel
Der Lesemodus zeigte bisher alles, was auf einer Seite steht, auch Menüs, Cookie-Hinweise und Fußzeilen. Jetzt erscheint zuerst nur der Artikel. Der Knopf „Ganze Seite zeigen“ wechselt, ohne die Seite neu zu laden. Findet Trendo keinen eindeutigen Artikel, bleibt es bei der ganzen Seite und sagt das.

Eigener Code nach dem Verfahren von mozilla/readability (Apache-2.0), kein neues Paket. Die Nur-lesen-Grenzen bleiben unverändert.

### Echter Lauf mit 11 Seiten vom 08.11.2026
Geprüft wurde, welche Textzeilen gegenüber der ganzen Seite wegfallen.

| Seite | ganze Seite (Zeichen) | Artikel (Zeichen) | Zeit | Ergebnis |
|---|---:|---:|---:|---|
| Wikipedia de „Wildbienen“ | 30 107 | 25 535 | 27 ms | Artikel; weggefallen sind v. a. Weblinks und Menüs |
| Wikipedia en „Honey bee“ | 96 831 | 83 329 | 82 ms | Artikel; weg sind Einzelnachweise und einige Bildunterschriften |
| Python-Doku `re` | 69 326 | 66 448 | 47 ms | alle Abschnitte da; ein Eintrag fehlt (siehe unten) |
| GitHub-Projektseite | 10 875 | 6 756 | 73 ms | nur die README |
| heise-Artikel | 6 414 | 1 886 | 8 ms | Artikel ohne Rahmen |
| tagesschau Wetterseite | 5 408 | 1 668 | 13 ms | alle Vorhersagen da, ohne Rahmen |
| Verbraucherzentrale Phishing-Radar | 18 122 | 8 569 | 21 ms | die Warnungen, ohne Rahmen |
| MDN `<article>` | 10 859 | 10 859 | 9 ms | **nicht erkannt:** ganze Seite, Hinweis erscheint |
| tagesschau Regionalmeldung | 3 720 | 3 720 | 14 ms | richtig: die Seite enthält keinen Artikeltext |
| heise Startseite | 33 093 | 33 093 | 43 ms | richtig: kein Artikel, ganze Seite |
| tagesschau Startseite | 16 592 | 16 592 | 31 ms | richtig: kein Artikel, ganze Seite |

Die Zeiten stammen aus einem einzelnen Lauf auf diesem PC.

Der echte Lauf hat Fehler gezeigt, die Beispielseiten nicht gezeigt hatten:
- Beim Phishing-Radar blieb nur eine von vielen Warnungen, bei der Wetterseite fehlten die Tage ab Montag. Jetzt zählt bei gleichwertigen Blöcken der gemeinsame Rahmen.
- Bei GitHub hat ein Rahmen mit „sidebar“ im Namen den ganzen Artikel verworfen. `main` und `article` sind davor jetzt geschützt.
- Bei der Python-Doku fehlte der halbe Text. Abschnitte desselben Dokuments bleiben jetzt zusammen.
- Bei Wikipedia en haben Quellenlisten zeitweise den Fließtext überstimmt. Listeneinträge zählen jetzt wenig.
- Die heise-Startseite galt zeitweise als Artikel. Ein Treffer unter 10 % der Seite zählt jetzt nicht.

### Bekannte Grenzen
- MDN wird nicht als Artikel erkannt; dort bleibt es bei der ganzen Seite mit Menüs.
- Elemente mit Wörtern wie „comment“ im Namen fallen weg. In der Python-Doku traf das den Eintrag zu `(?#...)`.
- Die Erkennung ist ein Näherungsverfahren. Dass nichts fehlt, ist nicht garantiert. Deshalb bleibt die ganze Seite einen Klick entfernt.

## CORE-12 „KI-Anweisungen schreiben“: Anweisungs-Test
Bei einer Vorlage trägt man Regeln ein, eine pro Zeile: `enthält: Preis`, `nicht: leider`, `höchstens 80 Wörter`, `mindestens 3 Wörter`, `ist JSON`, `Muster: \d+ Euro`. Trendo fragt die KI je Fassung dreimal und zählt, wie oft alle Regeln stimmen. Gibt es eine ältere Fassung, wird sie mitgetestet. Die Regeln bleiben je Vorlage gespeichert.

Eigener Code nach der Idee von promptfoo (MIT), ohne Node und ohne neues Paket.

### Echter Lauf mit lokaler KI (Ollama, qwen2.5-coder:7b)
Regeln: `enthält: Biene`, `höchstens 25 Wörter`, `ist JSON`.

| Fassung | bestanden |
|---|---|
| genaue Anweisung (JSON, höchstens zehn Wörter) | 3 von 3 |
| „Erzähl mir etwas über die Biene.“ | 0 von 3 (zu lang, kein JSON) |

Der Lauf nutzte die Auswertung `PromptEval` mit direktem Aufruf der lokalen KI. Der Weg über die Oberfläche ist mit einer Test-KI geprüft.

## Kein Einbau bei CORE-04 und X-02
Die Kopplung läuft schon über WLAN und Tailscale; die Selbstheilung repariert Trendo selbst. Die Kandidaten brächten keinen Mehrwert oder sind verboten (Treiber, Dienste, Windows-Einstellungen, AGPL). Begründung je Projekt: `docs/ALL_STUDIOS_GITHUB_GODMODE`.

## Nicht geprüft (NOT RUN)
- Sichtprüfung der neuen Knöpfe und der Testkarte als gerendertes Bild.
- Vergleich mit dem Original mozilla/readability auf denselben Seiten (fremdes Programm, nicht ausgeführt).
- Anweisungs-Test über die Oberfläche mit einer echten KI.
