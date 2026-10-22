# Kurzfassung der Trendo-Spezifikation

Für Agenten. Ersetzt keinen Volltext. Diese Seite zuerst; den Volltext nur, wenn die Zeile unten nicht reicht. `APPROVED_SPEC` ist nicht gebaut (`requirements.yaml`).

## Produkt
Trendo OS, Assistent Trendo. Ein Kern, drei Editionen: Pi 4 (8 GB) steuert, Windows-PC rechnet; Linux-Full-Studio bootet persistent von externer SSD/USB; Windows-App ersetzt weder Shell, Taskleiste, Treiber noch Registry. Kein eigener Kernel. Basis offline. Keine Pflicht auf kostenpflichtige KI-APIs.

## Grenzen
Grok Bot plant, Grok Build programmiert, Manuel gibt frei. Kein zweiter Coding-Agent als Pflicht. Konkrete Freigabe vor Nachricht, Veröffentlichung, Zahlung, Kauf, Live-Gang, Upload sensibler Daten, Treiber- oder Firmwareänderung. Forensik-Kopien samt Ableitungen höchstens 120 Stunden. Deep/Dark Web nur lesend und isoliert. Keine gefährlichen autonomen Aktoren. Secrets nie ins Repo. Business 30/30 (beide Modi) und Brief 25/25 sind nur spezifiziert: kein Live-Geld, keine Kundenpost, kein Versand, kein Deploy ohne Gate, keine erfundene Rechtslage.

## Nein, Logo, Design
Abgelehnt: SPEC-07 Language & Localization 2.0, Verbesserungen 23, 25, 34. Nicht per Umbenennung zurückholen. Verbesserung 35 ist offen. Logo A „Obsidian-Kachel“, `src/Trendo.App/Assets/logo/trendo-logo-a.svg`. Design 41 stoppt bei UI41-6; Rollout auf alle Studios nur nach gesondertem JA. UI41-SM-01: das Startmenü öffnet jedes Katalog-Studio im eigenen Trendo-Fenster; sonst „Noch nicht verfügbar“. Referenzbilder: keine Kundendaten. Obsidian-Startwerte nicht hardcoden: Fläche `#112138`, Text `#F0F6FF`, Akzent `#51C5FF`.

## Welche Datei
- Entscheidung: `docs/spec/decisions.yaml`, dann `requirements.yaml`.
- Edition, Schicht, Sicherheit: `docs/spec/SYSTEMBIBEL.md`.
- Modulwortlaut, nur der Abschnitt: `docs/spec/MASTERPROMPT.md`.
- Designphasen: `docs/spec/design-41/GROK_BUILD_DESIGN_41_MASTERPROMPT.md`.
- Business oder Brief: eine Datei unter `docs/spec/studios/` plus die passende CSV.
- Reihenfolge, Schnitte, offene Punkte, ein Katalog: `docs/spec/ROADMAP.md`, `docs/spec/ARCHITEKTUR.md`, `docs/spec/OFFENE_ENTSCHEIDUNGEN.md`, `docs/spec/kataloge/`.
