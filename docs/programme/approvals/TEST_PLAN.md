# Freigaben – Testplan

Studio: Daten schützen (CORE-11) · Bereich: System & Sicherheit · Seite: `approvals` · Stand: 0.9.112 (Stand d6f9fcd)

12 Tests sprechen dieses Programm an.

- B4 UI: Freigaben-Seite öffnet fremde Wirkungen (z. B. Installieren) nie als Startziel – nur Hinweis und Ablehnen
- B4 UI: Kinderprofil – Rolle Benutzer aus dem Profil, keine Rückfrage, Kern lehnt ab, Freigaben-Seite gesperrt, nichts installiert
- CORE-11 UI: Schutz-Seite über System erreichbar, Sichern, Ordner prüfen
- CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert
- CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll
- CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert
- CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch
- CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück
- SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll
- Schutz: Die Freigaben-Seite bleibt für Claude gesperrt, auch mit Zustimmung
- T-0005 (UI): Not-Stopp-Knopf hält an, Freigabe gesperrt mit Hinweis; Fortsetzen; Pause-Taste stoppt ebenfalls
- T-0754 Gegenprüfung 4: Studienwissen CORE-11 verspricht keine Passwortverwaltung
