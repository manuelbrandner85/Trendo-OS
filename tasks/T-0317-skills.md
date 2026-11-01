# T-0317 – Skills für professionelle Ergebnisse
- Freigabe: Manuel im Chat am 01.11.2026 – alle vier: Skill-Bibliothek je Fachgebiet (mitgeliefert, lesbare Dateien: Webdesign, Spiele, Brief/DIN 5008, Präsentation, Tabellen, Fotos …; automatisch passend geladen), eigene Skills anlegen/bearbeiten/versionieren und Programmen zuordnen (auf der vorhandenen `PromptLibrary`), Qualitätsprüfung nach Skill-Checkliste vor „fertig“, Skills aus vertrauten Quellen holen (nur nach Zustimmung, mit Herkunft und Lizenz).
- Kein zweites Prompt-System: Skills sind eine Kategorie bzw. Erweiterung der `PromptLibrary` (Versionen, Prüfung vorhanden).

## Umsetzung 01.11.2026
- Kern `src/Trendo.Core/W2/Skills.cs`: Skill-Format (Markdown mit Kopf name/description/studios/lizenz/quelle und „## Checkliste“), `SkillLibrary` mit drei Quellen – mitgeliefert (eingebettet aus `src/Trendo.Core/Skills/*.md`: Webdesign, Spiele, Brief DIN 5008, Präsentation, Tabellen, Foto/Bild, Texte/Lektorat, Technik/CAD, Sauberer Code), eigene (PromptLibrary, Kategorie „Skill“, Versionen/Wiederherstellen), geladen (Datenordner `skills/`, mit Lizenz und Herkunft, höchstens 100 KB Text).
- Passende Skills je Seite/Studio (eigene zuerst), Fachregeln für die KI (gekürzt auf 7000 Zeichen), Checkliste ohne Doppelte, Ablage als Claude-Code-Skill `.claude/skills/trendo-<id>/SKILL.md`.
- App `MainWindow.Skills.cs`: Seite „Skills“ (KI-Bereich) – ansehen, eigene anlegen/bearbeiten/löschen, offizielle Sammlung `anthropics/skills` nach Klick (nur SKILL.md, Lizenz aus LICENSE.txt); Fachregeln gehen an die KI des Programm-Assistenten; „In Claude Code öffnen“ legt die Skills in den Projektordner und verlangt die Prüfung gegen die Checklisten; der Assistent zeigt „Prüfen nach Skill“.
- Tests: `SkillsTests` (6), `Skills_Ui` (3).
