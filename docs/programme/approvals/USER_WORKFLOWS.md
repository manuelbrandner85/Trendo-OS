# Freigaben – Arbeitsabläufe

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `approvals` · Stand: 0.9.113 (Stand 1978f1a)

Die Abläufe unten sind die Oberflächentests, die das Programm über den echten Eingabeweg bedienen (Klicks, Tastatur).

- B4 UI: Freigaben-Seite öffnet fremde Wirkungen (z. B. Installieren) nie als Startziel – nur Hinweis und Ablehnen
- B4 UI: Kinderprofil – Rolle Benutzer aus dem Profil, keine Rückfrage, Kern lehnt ab, Freigaben-Seite gesperrt, nichts installiert
- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll
