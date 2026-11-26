# Mathe-Helfer – Anforderungen (Clean-Room)

Studio `STUDIO-MATH-01`, Seite `mathe-helfer-programm`, Kategorie Wissen & Lernen. Freigabe: 79 neue Studios am 09.11.2026, Etappe Wissen & Lernen am 26.11.2026 (T-0781).

## Vorbilder (nur Bedienung und Funktionsumfang, kein Code, keine Texte, keine Symbole)

| Vorbild | Was wir uns ansehen | Lizenz / Zugang | Quelle (abgerufen 26.11.2026) |
|---|---|---|---|
| GeoGebra CAS | Zellen mit Eingabe oben, Ausgabe darunter; Enter rechnet; Objekte im Graph ein-/ausblenden; Rechtsklick je Zeile (kopieren, als LaTeX) | Apps: eigene Lizenz (nicht-kommerziell frei), Quellcode EUPL 1.2 (Stand „November 2025“) – nur Bedienvorbild | geogebra.github.io/docs/manual/en/CAS_View/, geogebra.org/license |
| Photomath (Google) | Lösungsweg Schritt für Schritt, Schritte aufklappbar, Eingabe über Rechner-Tastatur, wenn die Kamera scheitert | proprietär | support.google.com/photomath/answer/14328660; Common Sense Media Testbericht |
| Symbolab | Mathe-Tastatur mit Vorschau, „Schritte zeigen/verbergen“, Graph neben der Lösung, „Prüfen“ der eigenen Lösung | proprietär (kostenlos mit Tageslimit, 2026) | symbolab.com/solver; Test myengineeringbuddy.com (2026) |
| Wolfram\|Alpha Step-by-step | Weg zur Antwort aufklappen, feinere Zwischenschritte, Lösungsmethoden | proprietär (Pro ab 5,25 €/Monat) | wolframalpha.com/pro/step-by-step-math-solver |
| Microsoft Math Solver | eingestellt am 07.07.2025 – kein Vorbild mehr | – | de.wikipedia „Microsoft Math Solver“, Microsoft Q&A |

## Offene Alternativen und Entscheidung „eigener Kern statt Bibliothek“

| Name | Lizenz | Stand | Entscheidung |
|---|---|---|---|
| AngouriMath (.NET) | MIT (NuGet und GitHub bestätigt) | 2.9.0 vom 24.11.2026 | **nicht genommen**: liefert laut README nur Endergebnisse, **keine Lösungsschritte**. Den Lösungsweg hätten wir ohnehin selbst bauen müssen; dazu kämen Abhängigkeiten (PeterO.Numbers, HonkSharp) und eine sehr schnelle Versionsfolge (2.7 → 2.9 in zehn Tagen). |
| SymPy | BSD-3 | 1.14.0 (27.04.2025) | nicht genommen: Python-Laufzeit wäre nötig, keine Schritte für Schüler. |
| Maxima | GPL-2 | 5.50.0 (08/2026) | Clean-Room, kein Code; Copyleft. |
| Giac/Xcas | GPL-3 (Doppellizenz auf Anfrage) | 2.0.0-21 (02/2026) | Clean-Room, kein Code. |
| math.js | Apache-2.0 | 15.3.0 (23.11.2026) | nicht genommen: JavaScript, symbolisches Lösen nicht vorhanden. |

**Ergebnis:** eigener, geprüfter Rechenkern in C# (`src/Trendo.Core/Wissen/`): exakte Brüche (`Bruch`, BigInteger), Termbaum mit eigenem Leser (`Term`, `TermLeser`), Polynome (`Polynom`), Zahlen mit Wurzel für exakte Proben (`WurzelZahl`), Lösungswege (`MatheKern`), Export (`Rechenblatt`). Keine neue Abhängigkeit.

**Rust-Prüfung (CLAUDE.md):** kein Rust. Der Kern läuft im Programm (Avalonia/.NET), rechnet Schul-Aufgaben in Millisekunden (Core-Test „200 zufällige quadratische Gleichungen“ zusammen unter einer Sekunde); eine Rust-Bibliothek brächte FFI, zweite Toolchain und Build-Aufwand ohne messbaren Vorteil.

## Vorhandenes genutzt
- Funktionsgraph: `GraphView` (bisher nur im Rechner, `MainWindow.Helfer.cs`), neu mit einstellbaren Kurvenfarben (Gold statt Blau).
- Punkte für den Graph und die unabhängige Gegenrechnung beim Bruchrechnen: `Calc.Eval`/`Calc.Plot` (eigener Gleitkomma-Leser des Rechners).
- PDF-Satz: `BizPdf` (PDFsharp, wie Rechnung und Brief); absturzsicheres Schreiben: `AtomicFile`.
- Programm-Hülle (Vorlagen, Rückgängig, automatisches Sichern, Wiederherstellen, Bedienstufen), KI-Kette mit `ProgramSpec.KiActions` (T-0774).

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Vorbild | Stand |
|---|---|---|---|---|---|
| MH-01 | Eingabe in deutscher Schreibweise | Komma/Punkt, ·, ×, :, ÷, ², ³, √, π, \|x\|, Malzeichen darf fehlen (2x, 3(x+1)), „xy²“ = x·y² | Core `MatheKernTests.Leser`, `Leser_fehler` | alle | erfüllt |
| MH-02 | Exakte Arithmetik | Brüche statt Gleitkomma (0,1 + 0,2 = 0,3 exakt), Dezimal und gemischte Zahl als Zusatz | Core `Bruch_exakt` | Symbolab | erfüllt |
| MH-03 | Terme vereinfachen / ausmultiplizieren | binomische Formeln benannt, Distributivgesetz, gleichartige Glieder zusammenfassen; Probe durch exaktes Einsetzen an 3 Stellen | Core `Ausmultiplizieren`, `Vereinfachen` | Photomath | erfüllt |
| MH-04 | Faktorisieren | Ausklammern, binomische Formeln rückwärts, Nullstellen mit Mitternachtsformel, rationale Nullstelle + Polynomdivision (Horner); Probe: Faktoren ausmultiplizieren = Ausgangsterm | Core `Faktorisieren` (8 Fälle) | GeoGebra „Faktorisiere“ | erfüllt |
| MH-05 | Lineare Gleichungen | Äquivalenzumformungen bis x = …; Sonderfälle „jede Zahl“ und „keine Lösung“ | Core `Linear`, `Sonderfaelle` | Photomath | erfüllt |
| MH-06 | Quadratische Gleichungen | Normalform, p-q-Formel, Ausklammern (Satz vom Nullprodukt), Wurzelziehen; Lösungen rational oder als a + b√m; Probe exakt (auch mit Wurzeln, Rechnen in ℚ(√m)) | Core `Quadratisch` (9 Fälle), `Zufall` (200 Gleichungen) | Symbolab | erfüllt |
| MH-07 | Höhere Grade | rationale Nullstellen + Polynomdivision, Rest quadratisch; sonst Näherung (Vorzeichenwechsel + Halbierung), als ≈ gekennzeichnet | Core `Quadratisch` (x³) | Wolfram\|Alpha | teilweise (Näherung statt exakter Cardano-Formel) |
| MH-08 | Gleichungen mit Wurzel, Exponent, ln, sin | Variable kommt einmal vor → Umkehrfunktionen Schritt für Schritt; Scheinlösung durch Probe verworfen; Hauptwert-Hinweis bei sin/cos/tan | Core `Umformen` | Photomath | teilweise (nur einmal vorkommende Variable) |
| MH-09 | Gleichungssysteme | linear, bis 10 Unbekannte, Gauß mit Brüchen, Additionsverfahren je Schritt (I − 2·II), Widerspruch, unendlich viele Lösungen mit Parameter; Probe in jeder ursprünglichen Gleichung | Core `Systeme` | Symbolab | erfüllt |
| MH-10 | Bruchrechnen | Kürzen, Hauptnenner (kgV), Erweitern, Zähler addieren, Kehrwert beim Teilen, Potenzen; Gegenrechnung mit dem Taschenrechner von Trendo | Core `Brueche`, `Bruch_weg` | Photomath | erfüllt |
| MH-11 | Ableiten | Summen-, Faktor-, Potenz-, Produkt-, Quotienten-, Kettenregel; e, aˣ, ln, lg, sin, cos, tan, arcsin, arccos, arctan, Betrag, xˣ; Probe mit dem Differenzenquotienten | Core `Ableiten` (9 Fälle), `Ableiten_kopf` | Symbolab | erfüllt |
| MH-12 | Integrieren | Potenzregel, lineare Substitution, e, aˣ, sin, cos, 1/x, ln, Polynom · e/sin/cos (partielle Integration); bestimmte Integrale exakt + Simpson-Gegenrechnung; Unlösbares ehrlich abgelehnt | Core `Integrieren` (10 Fälle), `Bestimmt`, `Integral_ehrlich` | Wolfram\|Alpha | teilweise (keine allgemeine Substitution, keine Partialbrüche) |
| MH-13 | Arbeitsfläche wie ein CAS-Blatt | Aufgabenart wählen, Eingabezeile + Mathe-Tasten, Liste der Rechnungen mit ✓/✗, Ergebnis groß, Lösungsweg aufklappbar, Probe grün/rot | UI `T0781Wissen_Ui.Mathe_klick_bis_datei`; Bild `reports/T-0781/bilder/` | GeoGebra, Symbolab | erfüllt |
| MH-14 | Funktionsgraph | für Funktionen in x: f und f′, f und F, linke und rechte Seite; Bereich einstellbar | UI `Mathe_klick_bis_datei` (GraphView, 2 Kurven, Gold) | Symbolab, GeoGebra | erfüllt |
| MH-15 | Bedienung | Enter rechnet, Entf löscht, ↑/↓ wählen, Rechtsklick (In die Eingabe, Kopieren, Nach oben, Löschen), Ziehen und Ablegen eines Aufgabenblatts, Einfach/Erweitert/Profi (LaTeX nur Profi) | UI `Mathe_fehler_und_bedienung`, `Mathe_import` | GeoGebra | erfüllt (Ziehen und Ablegen nur im Code, Test über „Aufgabenblatt öffnen …“) |
| MH-16 | Aufgabenblatt einlesen | Text/Markdown/CSV/TeX, eine Aufgabe je Zeile, „art: aufgabe“, Nummern und Aufzählungen übergangen, Datei bleibt unverändert | UI `Mathe_import`; Beleg Fremddatei aus LibreOffice | – | erfüllt |
| MH-17 | Export | PDF (Lösungswege), LaTeX (LuaLaTeX/XeLaTeX), Markdown | UI `Mathe_klick_bis_datei`; Beleg PDF in pdftotext und LibreOffice | GeoGebra (als LaTeX kopieren) | erfüllt (LaTeX nicht übersetzt – kein TeX auf dem PC) |
| MH-18 | KI: Textaufgabe → Rechnung | „mathe.rechnung“: KI nennt nur Art und Aufgabe; Vorschau, „Ja, behalten“, ein Rückgängig-Schritt; Ergebnis rechnet der Kern; „x = 5“ (fertiges Ergebnis) abgelehnt; „dokument.setzen“ auf die Rechnungen gesperrt | UI `Mathe_ki_textaufgabe`, `Mathe_ki_rechnet_nie`; Echtlauf `Real_local_ki` (`reports/T-0781/`) | – | erfüllt |
| MH-19 | Absturzschutz | Rechnungen überleben einen Absturz (20-s-Takt der Hülle, Wiederherstellen) | UI `Mathe_absturz` | – | erfüllt |
| MH-20 | KI-Auftrag ohne offenes Dokument | Adapter „mathe.loesen“: Lösungsweg als Markdown vom Kern | Core `WissenAdapterTests` | – | erfüllt |

## Bewusst nicht gebaut
- Keine Kamera-/Handschrifterkennung (Photomath), keine Lösungen aus dem Netz, kein Konto.
- Keine Ungleichungen, Grenzwerte, Matrizen, komplexen Zahlen (nicht im Auftrag; ehrlich abgelehnt statt geraten).
