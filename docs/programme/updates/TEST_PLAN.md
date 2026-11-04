# Trendo-Updates – Testplan

Studio: Schrift & Farben anpassen (SYS-12) · Bereich: System & Sicherheit · Seite: `updates` · Stand: 0.9.36 (Stand c58bb7c)

18 Tests sprechen dieses Programm an.

- Neuerungen im Vollbild: nicht wegklickbar (Esc, Klick daneben), läuft von selbst weiter, Pfeile blättern, „Los geht's“ erst auf der letzten Folie
- SYS-12 (UI): Bericht über alle Seiten; keine Seite tiefer als 2 Ebenen; Prüfseite hat keine unbenannten Knöpfe
- SYS-12 (UI): Leise Benachrichtigung; Einstellung bleibt gespeichert; einfacher Modus zeigt weniger Kacheln
- SYS-12 (UI): Mindestschrift greift; Knöpfe haben Namen und sind per Tab erreichbar; Bewegung aus = keine Übergänge
- SYS-12-P03/P09/P18: Mindestschrift, Status mit Zeichen, leise Meldungen
- SYS-12-P07: WCAG-Kontrast (Schwarz/Weiß 21:1, AA-Grenzen)
- SYS-12-P10: Leichte Sprache – höchstens 2 kurze Sätze, einfache Wörter
- SYS-12-P14: Jede Akzentfarbe wird für Text auf jedem Design AA-tauglich
- SYS-12-P15/P16: Navigation >2 Ebenen und englische Beschriftungen werden gefunden
- SYS-12-P20: Vorlesen – Windows-SAPI-Befehl, Linux espeak oder nichts
- SYS-12-P22: Einstellungen exportieren/importieren verlustfrei, Unsinn abgelehnt
- SYS-12-P23: Fensterposition nur auf vorhandenem Bildschirm wiederherstellen
- SYS-12-P25: Bericht listet Befunde je Seite
- SYS-12: Anzeigegröße wird auf erlaubte Stufen begrenzt
- SYS-12: Anzeigegröße „Sehr groß“ wird gespeichert; F1 zeigt die Hilfe
- Update sofort: geladenes Update startet 30-Sekunden-Countdown (alles gesichert), danach Installation; „Jetzt neu starten“ geht sofort
- Updates: nach dem Neustart kommen die Fenster zurück und „Was ist neu?“ erscheint einmal
- Updates: suchen und im Hintergrund laden; „beim nächsten Start“ installiert beim Beenden, „sofort“ mit Einrichtungsbildschirm, Sicherung und Fenstern
