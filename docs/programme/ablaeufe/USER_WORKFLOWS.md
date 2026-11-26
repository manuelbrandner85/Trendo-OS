# Abläufe (Automatisch erledigen) – Arbeitsabläufe

Studio: Aufgaben automatisieren (SYS-05) · Bereich: KI & Automatik · Seite: `ablaeufe` · Stand: 0.9.110 (Stand 0eb9d52)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- B4 UI: Ablauf „Freigeben …“ – der Klick zeigt Probelauf und Rückfrage; ohne Ja bleibt der Ablauf gesperrt
- SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf
- T-0681 Abläufe UI: fehlgeschlagener Lauf steht mit Fehlerstand am Ablauf; beschädigte Ablaufdatei wird auf der Seite gemeldet
- T-0698 UI +4: Abläufe – Baustein „Agent ausführen“ anhängen und Agent wählen (Zeitplan/Auslöser ohne zweiten Scheduler)
- T-0742 UI: Satz → KI → echter Ablauf im Editor (aus), ändern, Rückgängig/Wiederholen, Einschalten nur nach Ja, Lauf schreibt Datei
- T-0742 UI: Unbrauchbare KI-Antwort legt nichts an und sagt warum
- T-0742 UI: Windows-Aufgabe nur nach Ja, sichtbar, ein Klick entfernt sie; Löschen des Ablaufs entfernt sie mit; „neue Datei“ zeigt deutlich „nur bei offenem Trendo“
- Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille
