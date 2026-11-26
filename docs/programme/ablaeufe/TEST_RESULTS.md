# Abläufe (Automatisch erledigen) – Testergebnisse

Studio: Aufgaben automatisieren (SYS-05) · Bereich: KI & Automatik · Seite: `ablaeufe` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 22 · Fehlgeschlagen 0 · Übersprungen 1 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Ablauf-Editor: Probelauf mit Anzeige – jeder Baustein leuchtet vorher auf, danach ✓ mit Anzahl bzw. ✕ bei Fehler, Ergebnis am Ende; nichts wird geändert | ✓ bestanden |
| Ablaufplan: Bausteine anhängen, Schritt per Maus nach oben ziehen, einstellen, Probelauf, Rückgängig; Änderung an freigegebenem Ablauf verlangt neue Freigabe | ✓ bestanden |
| Abläufe Profi: n8n einrichten nur nach Ja, starten, Vorlage als Fluss mit Nach-außen-Hinweis, Nein verwirft, Ja speichert .n8n.json; Claude-Datei erscheint; Probe-Ereignis geht an den lokalen Webhook | ✓ bestanden |
| Abläufe: Fehler im Zeitplan wird angezeigt und protokolliert, der nächste fehlerfreie Lauf nimmt den Hinweis wieder weg | ✓ bestanden |
| B4 UI: Ablauf „Freigeben …“ – der Klick zeigt Probelauf und Rückfrage; ohne Ja bleibt der Ablauf gesperrt | ✓ bestanden |
| I2/I4: KI-Rollen als Karten (Auftrag, Schreibrecht), Wahl markiert; Ablauf-Vorlagen als Karten, ohne Ablauf Leerzustand, Vorlage legt Ablauf an | ✓ bestanden |
| SYS-05 UI: Vorlage anlegen, Probelauf, Freigeben, Ausführen, eigener Ablauf | ✓ bestanden |
| SYS-05: Auslöser täglich, Intervall und neue Datei; KI-Zusammenfassung | ✓ bestanden |
| SYS-05: Nur erlaubte Ordner, Textdatei mit {datum}, ZIP, Datum voranstellen | ✓ bestanden |
| SYS-05: Probelauf ändert nichts, Verschieben braucht Freigabe, Änderung verlangt neue Freigabe | ✓ bestanden |
| T-0201: jedes Pfad- oder Ordnerfeld hat „Datei wählen …“ bzw. „Ordner wählen …“ | ✓ bestanden |
| T-0681 Abläufe UI: fehlgeschlagener Lauf steht mit Fehlerstand am Ablauf; beschädigte Ablaufdatei wird auf der Seite gemeldet | ✓ bestanden |
| T-0698 UI +4: Abläufe – Baustein „Agent ausführen“ anhängen und Agent wählen (Zeitplan/Auslöser ohne zweiten Scheduler) | ✓ bestanden |
| T-0717 Abläufe: Bausteine ändern – Rückgängig/Wiederholen der Hülle (vorher 50 ohne Wiederholen), gespeichert nach dem Schließen, keine Karte | ✓ bestanden |
| T-0717 Abläufe: neuer Ablauf (Name, Schritte) – Rückgängig/Wiederholen, Notsicherung, Karte, Zurückholen; Speichern legt den Ablauf an und räumt weg | ✓ bestanden |
| T-0742 ECHT: Satz → Claude → Ablauf → Windows-Aufgabe startet TrendoOS.exe ohne Fenster → Datei entsteht (nur mit TRENDO_T0742_ECHT) | übersprungen |
| T-0742 Gegenprüfung: Gibt es den Ablauf nicht mehr, entfernt die Aufgabe sich selbst; im offenen Trendo zeigt ein Ablauf mit KI-Schritt „nur bei offenem Trendo“ und bietet das Entfernen an | ✓ bestanden |
| T-0742 Gegenprüfung: Lässt sich die Windows-Aufgabe beim Löschen nicht entfernen, bleibt der Ablauf mit Hinweis bestehen | ✓ bestanden |
| T-0742 UI: Satz → KI → echter Ablauf im Editor (aus), ändern, Rückgängig/Wiederholen, Einschalten nur nach Ja, Lauf schreibt Datei | ✓ bestanden |
| T-0742 UI: Unbrauchbare KI-Antwort legt nichts an und sagt warum | ✓ bestanden |
| T-0742 UI: Windows-Aufgabe nur nach Ja, sichtbar, ein Klick entfernt sie; Löschen des Ablaufs entfernt sie mit; „neue Datei“ zeigt deutlich „nur bei offenem Trendo“ | ✓ bestanden |
| Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille | ✓ bestanden |
| X-01 (UI): Ohne Angaben gesperrt; Plan freigeben → Ja → alle Schritte ✓ | ✓ bestanden |
