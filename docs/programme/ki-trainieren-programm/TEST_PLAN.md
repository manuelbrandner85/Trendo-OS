# Eigene KI trainieren – Testplan

Studio: Eigene KI trainieren (STUDIO-TRAIN-01) · Bereich: KI & Automatik · Seite: `ki-trainieren-programm` · Stand: 0.9.110 (Stand 0eb9d52)

13 Tests sprechen dieses Programm an.

- Eigene KI trainieren: Beispiel trainieren, Genauigkeit und Verwechslungstabelle, Ausprobieren mit Sicherheit, Änderung verlangt neues Training, Modell und CSV laden, Exporte
- T-0740 Eigene KI ohne Modell: Einordnen sagt, was fehlt, statt etwas zu raten
- T-0740 Eigene KI: Beispiel trainieren, CSV aus dem Repo einordnen (Knopf), Vorschau, Speichern als CSV mit BOM, Rückgängig, Ordner einordnen, Ablegen, Menü und Rechtsklick
- T-0740 Eigene KI: „Abbrechen“ hält einen großen Stapel mitten im Lauf an, Fortschritt sichtbar, nichts wird übernommen
- T-0740 Gegenprüfung: CSV aus deutschem Excel (Windows-1252) – Umlaute kommen richtig an, kein Ersatzzeichen
- T-0740 Gegenprüfung: CSV ohne Kopf, erstes Wort „Rechnung“ – bleibt Datenzeile; Schalter „Erste Zeile ist Überschrift“ ordnet neu ein
- T-0740 Gegenprüfung: Hervorhebung und Ausfüllen nutzen dieselbe Lücken-Regel
- T-0740 Gegenprüfung: große CSV einordnen, danach kleine Änderung – Dokument und Rückgängig-Schnappschuss bleiben klein, Rückgängig nimmt den Verweis zurück, Export hat alle Zeilen
- T-0740 Gegenprüfung: in „Einfach“ speichert Datei ▸ Exportieren ▸ „Vorhersagen (.csv)“ die echte Datei (Ersatzweg für den Knopf)
- T-0740 Gegenprüfung: nach erneutem Training sagen die alten Vorhersagen „mit älterem Modell erstellt“
- T-0740 Gegenprüfung: über 50 000 Zeilen – Meldung und Vorschau sagen „nur die ersten 50.000 von …“
- T-0740 KI-Anweisungen: Texteditor – Tippen, Rückgängig im Feld (Knopf und Rechtsklick), „Lücke einfügen“ als ein Schritt, Lücken hervorgehoben, Speichern
- T-0740 KI-Anweisungen: Vorlage als .md speichern, ändern, einlesen → Rückfrage → Ersetzen (neue Version) und Als Kopie; Alle als .json; Ablegen
