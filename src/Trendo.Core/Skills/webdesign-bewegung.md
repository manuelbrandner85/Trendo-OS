---
name: Web: Bewegung
description: Für Animationen, Einblendungen, Übergänge und Effekte beim Scrollen oder Darüberfahren.
studios: CORE-07, X-03
stichworte: bewegung, animation, animieren, einblenden, schweben, effekt, übergang, scroll, hover, dynamisch
lizenz: Trendo OS (eigener Inhalt)
---

# Bewegung

## Grundsatz
- Bewegung erklärt etwas: woher ein Element kommt, was sich geändert hat, wohin der Blick soll. Reine Dekoration lässt man weg.
- Kurz bleiben: kleine Übergänge 150 bis 300 Millisekunden, größere Auftritte höchstens eine Sekunde.
- Sparsam einsetzen: wenige Akzente je Seite, nicht jeder Baustein bewegt sich.

## Technik
- Nur Eigenschaften animieren, die der Browser günstig zeichnet (Verschiebung, Größe, Deckkraft).
- Eigene Umsetzung über CSS und die Web Animations API. Keine Bibliothek, deren Lizenz visuelle Werkzeuge einschränkt.
- Wer „weniger Bewegung“ eingestellt hat, bekommt keine Animation. Inhalt ist immer auch ohne Animation sichtbar.

## Checkliste
- Jede Bewegung hat einen erkennbaren Zweck
- Einstellung „weniger Bewegung“ wird beachtet
- Inhalt bleibt ohne Animation sichtbar
- Kein Dauerblinken und keine Endlosbewegung im Lesebereich
- Dauer passt zur Größe des Elements
