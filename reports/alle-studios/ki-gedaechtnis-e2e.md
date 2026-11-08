# KI-Gedächtnis mit Kontrolle – 08.11.2026

Studio „KI fragen“ (CORE-02). Freigegeben am 08.11.2026. Eigener Code nach der Idee von mem0, kein neues Paket.

## Was die Person sieht
Links im Chat steht der Bereich **Gedächtnis**. Er ist von Haus aus aus, denn Trendo hat keinen festen Nutzer und jede Person entscheidet selbst.

Eingeschaltet merkt sich die KI Aussagen wie „Ich wohne in Köln“ oder alles nach „Merk dir …“. Nach jedem gemerkten Punkt erscheint ein Hinweis mit dem Knopf „Vergessen“. Alle Einträge lassen sich ansehen, einzeln löschen, von Hand ergänzen oder auf einmal vergessen (mit Nachfrage).

## Regeln
- Die Einträge bleiben auf dem PC. KIs im Internet bekommen sie nie, auch nicht der Claude-Zugang.
- Sie gelten nur im Chat „KI fragen“, nicht in anderen Programmen.
- Erkannt wird mit festen Satzmustern, ohne KI-Aufruf und nur aus den eigenen Sätzen der Person. Fragen und Sätze über andere zählen nicht.
- Passwörter, PIN, TAN, IBAN, Kartennummern und Schlüssel werden nie gespeichert, auch nicht auf Wunsch.
- Gesundheit, Glaube, Politik und Sexualität nur auf ausdrückliches „Merk dir“.
- Höchstens 100 Einträge. Im Protokoll steht nur die Länge, nie der Inhalt.
- Ausschalten stoppt Lernen und Nutzen; die Einträge bleiben sichtbar, bis man sie löscht.

## Geprüft
- `KiMemoryTests`: Erkennen, Ignorieren, Geheimnisse, besondere Themen, aus von Haus aus, Löschen, Neustart, Grenzen, kaputte Datei, nichts ins Internet.
- `KiMemory_Ui`: der ganze Ablauf in der Oberfläche.
- **Echter Lauf mit lokaler KI** (Ollama, qwen2.5-coder:7b): Aus „Hallo, ich heiße Erika Muster. Ich wohne in Köln.“ wurden „Heißt Erika Muster“ und „Wohnt in Köln“. Frage „In welcher Stadt wohne ich?“: mit Gedächtnis „Köln“, ohne Gedächtnis eine falsche Antwort.
- Bereich gerendert und angesehen: `ki-gedaechtnis.png` (erfundene Beispieldaten).

## Grenzen
Die Satzmuster erkennen nur typische Formulierungen. „Ich lebe seit Jahren in der Nähe von Köln“ wird als „Wohnt“ nicht erkannt („Merk dir …“ geht immer). Falsch Erkanntes lässt sich über „Vergessen“ sofort löschen.
