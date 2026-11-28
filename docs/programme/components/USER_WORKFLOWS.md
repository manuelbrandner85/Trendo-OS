# Zusatzprogramme – Arbeitsabläufe

Studio: Programme starten (SYS-11) · Bereich: System & Sicherheit · Seite: `components` · Stand: 0.9.114 (Stand 99cbb5c)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- SYS-11 T-0764 UI: Kinderprofil – „Ja, entfernen“ auf der Programm-Seite verschiebt nichts (Kern lehnt ab)
- SYS-11 T-0764 UI: Pflegen → Öffnen → Starten/Entfernen auf einer Seite; Entfernen mit Rückfrage in die Sicherung, Wiederherstellen bringt es zurück
- T-0713 UI: Kinderprofil – „Fehlende Programme installieren“ zeigt keine Rückfrage, legt keine Freigabe an, installiert nichts; Not-Stopp kann es nicht aufheben
- T-0713 UI: „Fehlende Programme installieren“ – echter Sammel-Dialog mit Lizenz/Größe/Herkunft/Wirkung; abgewählt = keine Freigabe, gewählt = genau eine Freigabe und genau eine Ausführung im Tor (Testmodus)
