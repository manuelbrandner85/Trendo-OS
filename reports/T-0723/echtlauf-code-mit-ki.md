# T-0723 ECHT: Code mit KI – lokale KI ändert Code im offenen Projekt

Datum: 2026-11-16 21:36 · KI: Ollama 127.0.0.1:11434 · Modell: qwen3:14b (Das größte vorhandene allgemeine Sprachmodell (8,6 GB), das in deinen Grafikspeicher (12 GB) passt.)
Bedienung über die Oberfläche: Vorlage „Beispiel: kleines Python-Projekt“, Wunsch in „Mit KI ändern“, „Plan und Vorschau holen“, „Ja – … übernehmen und testen“, Freigabe „Tests starten“ (Rückfrage mit Ja beantwortet), danach „Rückgängig“ der Programm-Hülle. Fenster headless, Claude nicht beteiligt.

Auftrag: Ergänze in rechner.py eine Funktion multipliziere(a, b), die das Produkt von a und b zurückgibt, und schreib in test_rechner.py einen Test test_multipliziere dafür.

- vorher rechner.py: SHA-256 f738c719c1dd6a05e297faa609ad35cec75b8aec5125893a6e68099eda220126
- vorher test_rechner.py: SHA-256 513dc14174abfb92c9af4b9afb2b0c8892733a36e03a5d6d863d7a027c7fd94f
- vorher README.md: SHA-256 4785460db8fc1336cf07ab4e0f9e28ce2049430b97abffb5f086a3a9f2c8673b

Antwortzeit KI: 22 s · Phase: vorschau · Vorschlag: 2 Datei(en), 4 Änderung(en). Noch ist nichts geändert – prüfe jede Änderung und entscheide mit Ja oder Nein.
Plan der KI:
- In rechner.py die Funktion `multipliziere(a, b)` hinzufügen, die das Produkt von a und b zurückgibt.
- In test_rechner.py einen Test `test_multipliziere` hinzufügen, der die korrekte Funktionalität der neuen Funktion prüft.
Vorschau: rechner.py (geändert, 2 Änderung(en), +4 −1), test_rechner.py (geändert, 2 Änderung(en), +4 −1); abgelehnt: 0

Nach „Ja“: Phase gruen · ✓ Änderung übernommen und geprüft: 3 bestanden, 0 fehlgeschlagen.
- nach Ja rechner.py: SHA-256 899c4072d2a255803ca04c0d5e5142d2d94ffbb862c31bbe99089e597b20f097
- nach Ja test_rechner.py: SHA-256 d57b03cb5084112fd26cd3218ee77cd4b91f4f7d7b27f06c89db5c6564d9dc70

Testlauf (echt, nach Freigabe):
```
✓ Tests bestanden: 3 bestanden, 0 fehlgeschlagen (Exit 0)
$ python -m unittest -v
test_addiere (test_rechner.RechnerTest.test_addiere) ... ok
test_multipliziere (test_rechner.RechnerTest.test_multipliziere) ... ok
test_teile (test_rechner.RechnerTest.test_teile) ... ok

----------------------------------------------------------------------
Ran 3 tests in 0.000s

OK
```

Rückgängig: alle Dateien wieder wie vorher = True
- nach Rückgängig rechner.py: SHA-256 f738c719c1dd6a05e297faa609ad35cec75b8aec5125893a6e68099eda220126
- nach Rückgängig test_rechner.py: SHA-256 513dc14174abfb92c9af4b9afb2b0c8892733a36e03a5d6d863d7a027c7fd94f
- nach Rückgängig README.md: SHA-256 4785460db8fc1336cf07ab4e0f9e28ce2049430b97abffb5f086a3a9f2c8673b

Ergebnis: Kette vollständig durchlaufen.
