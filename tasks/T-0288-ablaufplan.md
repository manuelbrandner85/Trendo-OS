# T-0288 – Abläufe als sichtbarer Ablaufplan; Katalog erkennt mehr Arbeitsflächen

- Freigabe: Manuel im Chat am 31.10.2026 („Ja, weiter umbauen“).
- Vorher: Liste der Abläufe, neue Abläufe nur über ein Formular; Schritte nicht änderbar.
- Jetzt: links die Abläufe (mit Probelauf, Ausführen, Freigeben, Pausieren, Löschen wie bisher), Mitte der **Ablaufplan** (Start-Baustein → Schritt-Bausteine → Ende, Symbole, „braucht Freigabe“ farbig), rechts das Einstellen des gewählten Bausteins.
  - Bausteine aus der Palette anhängen, per Maus umsortieren, ↑/↓, löschen; Start (von Hand, täglich, Intervall, neue Datei) einstellen mit Prüfung der Eingaben.
  - Rückgängig (Knopf und Strg+Z). Jede Änderung an einem freigabepflichtigen Ablauf verlangt eine neue Freigabe (Hinweis in der Meldung).
  - Pfade unter dem Benutzerordner kurz als `~\…`.
- Katalog-Erkennung: Zeichenflächen (`ShapeView`), Lernkarte, Textseite, PDF-Seite, Ablaufplan; „Beispiel laden“ wird im Generator genutzt, damit Seiten mit Inhalt erscheinen; Shell-Programme mit Beispielvorlage. Befund: Lernen, Auto & Fahrten, Geräte überwachen, Trendo Text, VR & AR waren schon Programme – jetzt richtig erkannt (59 statt 63 Seiten ohne erkannte Arbeitsfläche, überwiegend System- und Einstellungsseiten).
- Tests: neu `WorkflowFlow_Ui` (echte Maus: anhängen, ziehen, einstellen, falsche Uhrzeit, Probelauf, Rückgängig, neue Freigabe); `SYS05_Ui` unverändert grün; 75 Querschnitt-Tests grün.
