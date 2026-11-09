---
name: Web: Leistung
description: Für kurze Ladezeiten, schlanke Bilder und Schriften, wenig JavaScript und ruhigen Seitenaufbau.
studios: CORE-07, X-03
stichworte: leistung, ladezeit, schnell, performance, langsam, bilder optimieren, dateigröße, lazy, cache
lizenz: Trendo OS (eigener Inhalt)
---

# Leistung

## Laden
- Was oben sichtbar ist, kommt zuerst. Alles darunter darf später laden; Bilder außerhalb des Sichtbereichs mit `loading="lazy"`.
- Moderne Bildformate (AVIF, WebP) in der Größe, in der sie angezeigt werden.
- Schriften lokal einbinden, mit `font-display: swap` und nur den Schnitten, die wirklich gebraucht werden.

## Ausführen
- So wenig JavaScript wie möglich, nichts Blockierendes im Kopf der Seite.
- Bilder und eingebettete Inhalte haben feste Maße, damit beim Laden nichts verrutscht.
- Eine schwere 3D-Szene lohnt sich nur, wenn sie der Seite etwas bringt; sonst ein Standbild.

## Checkliste
- Gesamtgewicht der Seite unter 2,5 MB
- Bilder in passender Größe und mit festem Seitenverhältnis
- Keine Skripte oder Schriften von fremden Servern
- Bilder außerhalb des Sichtbereichs laden später
- Kein unnötiges JavaScript
