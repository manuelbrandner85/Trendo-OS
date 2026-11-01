---
name: Webdesign auf Profi-Niveau
description: Gestaltung, Aufbau und Technik professioneller Webseiten und Web-Apps – modern, schnell, barrierefrei.
studios: CORE-07, X-03
lizenz: Trendo OS (eigener Inhalt)
---

# Webdesign auf Profi-Niveau

## Gestaltung
- Klare Hierarchie: eine Hauptaussage je Abschnitt, Überschriften H1 → H2 → H3 ohne Sprünge, genau eine H1 je Seite.
- Raster und Abstände aus einer Skala (4/8-px-Schritte), großzügiger Weißraum; Zeilenlänge für Fließtext 60–75 Zeichen.
- Typografie: höchstens zwei Schriftfamilien, Grundgröße mindestens 16 px, Zeilenhöhe 1,5–1,7 für Text, typografische Skala (z. B. 1,25).
- Farbe: Primärfarbe, Akzent und Neutrale als CSS-Variablen; heller und dunkler Modus über prefers-color-scheme.
- Bilder: echte, passende Motive statt Platzhalter, alt-Texte, moderne Formate (AVIF/WebP), feste Seitenverhältnisse gegen Layoutsprünge.
- Bedienelemente: sichtbarer Fokus, Klickflächen mindestens 44 × 44 px, Zustände für Hover, Fokus, Aktiv, Deaktiviert.

## Technik
- Semantisches HTML (header, nav, main, section, footer, button statt klickbarer div), Sprache lang="de".
- Mobile zuerst, Layout mit CSS Grid/Flexbox, keine festen Breiten; Umbrüche an echten Inhaltsgrenzen.
- Leistung: kritisches CSS klein halten, Bilder mit loading="lazy", Schriften mit font-display: swap, kein unnötiges JavaScript.
- Formulare: label zu jedem Feld, passende type- und autocomplete-Angaben, verständliche Fehlermeldungen am Feld.
- Rechtliches (DE): Impressum und Datenschutzerklärung verlinkt, keine externen Schriften oder Tracker ohne Einwilligung.

## Checkliste
- Kontrast Text/Hintergrund mindestens 4,5 : 1 (große Schrift 3 : 1)
- Auf 360 px Breite ohne waagerechtes Scrollen bedienbar
- Genau eine H1, Überschriften ohne Sprünge
- Alle Bilder mit sinnvollem alt-Text, Formularfelder mit label
- Tastatur: alles erreichbar, Fokus sichtbar
- Impressum und Datenschutz verlinkt
- Keine Platzhaltertexte (Lorem ipsum) und keine kaputten Links
