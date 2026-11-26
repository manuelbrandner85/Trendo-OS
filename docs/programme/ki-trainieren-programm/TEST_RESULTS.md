# Eigene KI trainieren – Testergebnisse

Studio: Eigene KI trainieren (STUDIO-TRAIN-01) · Bereich: KI & Automatik · Seite: `ki-trainieren-programm` · Stand: 0.9.112 (Stand d6f9fcd)

Lauf: `api.trx, app.trx, core.trx`

Bestanden 13 · Fehlgeschlagen 0 · Übersprungen 0 · nicht im Lauf 0

| Test | Ergebnis |
|---|---|
| Eigene KI trainieren: Beispiel trainieren, Genauigkeit und Verwechslungstabelle, Ausprobieren mit Sicherheit, Änderung verlangt neues Training, Modell und CSV laden, Exporte | ✓ bestanden |
| T-0740 Eigene KI ohne Modell: Einordnen sagt, was fehlt, statt etwas zu raten | ✓ bestanden |
| T-0740 Eigene KI: Beispiel trainieren, CSV aus dem Repo einordnen (Knopf), Vorschau, Speichern als CSV mit BOM, Rückgängig, Ordner einordnen, Ablegen, Menü und Rechtsklick | ✓ bestanden |
| T-0740 Eigene KI: „Abbrechen“ hält einen großen Stapel mitten im Lauf an, Fortschritt sichtbar, nichts wird übernommen | ✓ bestanden |
| T-0740 Gegenprüfung: CSV aus deutschem Excel (Windows-1252) – Umlaute kommen richtig an, kein Ersatzzeichen | ✓ bestanden |
| T-0740 Gegenprüfung: CSV ohne Kopf, erstes Wort „Rechnung“ – bleibt Datenzeile; Schalter „Erste Zeile ist Überschrift“ ordnet neu ein | ✓ bestanden |
| T-0740 Gegenprüfung: Hervorhebung und Ausfüllen nutzen dieselbe Lücken-Regel | ✓ bestanden |
| T-0740 Gegenprüfung: große CSV einordnen, danach kleine Änderung – Dokument und Rückgängig-Schnappschuss bleiben klein, Rückgängig nimmt den Verweis zurück, Export hat alle Zeilen | ✓ bestanden |
| T-0740 Gegenprüfung: in „Einfach“ speichert Datei ▸ Exportieren ▸ „Vorhersagen (.csv)“ die echte Datei (Ersatzweg für den Knopf) | ✓ bestanden |
| T-0740 Gegenprüfung: nach erneutem Training sagen die alten Vorhersagen „mit älterem Modell erstellt“ | ✓ bestanden |
| T-0740 Gegenprüfung: über 50 000 Zeilen – Meldung und Vorschau sagen „nur die ersten 50.000 von …“ | ✓ bestanden |
| T-0740 KI-Anweisungen: Texteditor – Tippen, Rückgängig im Feld (Knopf und Rechtsklick), „Lücke einfügen“ als ein Schritt, Lücken hervorgehoben, Speichern | ✓ bestanden |
| T-0740 KI-Anweisungen: Vorlage als .md speichern, ändern, einlesen → Rückfrage → Ersetzen (neue Version) und Als Kopie; Alle als .json; Ablegen | ✓ bestanden |
