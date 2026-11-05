# Vorbild-Analyse Netz, Teilen und Leistung: Windows Task-Manager, Free Download Manager, LocalSend, Windows-Freigaben (Clean-Room)

- Programme: „Ordner im Heimnetz teilen“, „Speicher & Leistung“, „Downloads“, „Sicher prüfen“
- Vorbilder: LocalSend, Free Download Manager, GNOME System Monitor (frei), Windows Task-Manager, Windows-Ordnerfreigabe, Windows-Sicherheit (Funktionsumfang aus öffentlicher Doku)
- Werkzeug: Funktionsumfang aus öffentlicher Doku und Bedienkonzept; kein fremder Code, keine fremden Grafiken oder Texte. Trendo-Stand aus Code und Tests, Stand 05.11.2026.

## Funktionen eines Profi-Programms – Stand in Trendo
✓ vorhanden · ◐ teilweise · ✗ fehlt

### Ordner im Heimnetz teilen
- ✓ Ordner nur lesend teilen, mit geheimem Link, der abläuft (sicherer als eine dauerhafte Windows-Freigabe)
- ✓ Vorher deutsche Rückfrage, „Ablehnen“ ist Standard, Esc lehnt ab
- ✓ Freigabe wird beim Schließen sauber beendet
- ◐ Empfänger sehen eine Dateiliste im Browser – kein Hochladen zurück (bewusst nur lesen)

### Speicher & Leistung
- ✓ Wie der Task-Manager: Reiter Leistung mit Kurven der letzten 60 s, Prozesse sortierbar mit Suche
- ✓ Programm beenden nur für eigene Trendo-Programme (nie Trendo selbst), zweistufig bestätigt
- ✗ Autostart-Liste mit Einfluss auf den Start – bewusst nicht, Windows-Einstellungen werden nicht geändert

### Downloads
- ✓ Warteschlange mit mehreren gleichzeitigen Downloads, Anhalten/Fortsetzen je Eintrag (Free Download Manager)
- ✓ Mehrere Links auf einmal einfügen
- ✓ Ziel ist der Download-Ordner; im Testmodus nie ein echter Ordner

### Sicher prüfen
- ✓ Nach dem freigegebenen Katalog SYS-03: Prüfsumme (SHA-256) einer Datei vergleichen, Links und Kurzlinks auflösen und auf Phishing-Merkmale prüfen, verdächtige Dateien in Quarantäne, Prüfung mit dem Windows-Virenschutz (Defender), Formulare vor dem Absenden prüfen
- ✗ Prüfung über fremde Online-Dienste (z. B. VirusTotal) – bewusst nicht, alles bleibt auf dem PC

## Bedienkonzept (Profi-Bauplan A1–A3)
Je Seite eine Liste mit genau einem Hauptknopf je Eintrag; gefährliche Schritte immer mit Rückfrage.

## Nächste Schritte
1. Downloads nach Abschluss automatisch „Sicher prüfen“ anbieten
