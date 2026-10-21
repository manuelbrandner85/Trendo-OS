# 2026-10-22 – Fehler: Klick auf Studio-Karte tat nichts

- **Ursache:** Die Karten unter „Alle Studios“ und in den Seitenleisten-Bereichen (Online Business, 3D & Games, Kreativ & Medien, Tools, System, KI) waren seit P1 reine Anzeige-Rahmen (`Border`) ohne Klick-Handler. Zusätzlich sprang `OpenStudio` bei Studios ohne eigene Seite auf genau diese Liste zurück („studios“), sodass der Klick wirkungslos aussah. Die 9 Kacheln auf der Startseite waren nicht betroffen; das zeigte ein echter Mausklick-Test auf dem PC.
- **Fix:** Die Karten sind jetzt Knöpfe → `OpenStudio`. `PageForStudio` sucht den eigenen Bereich, die erste Registerkarte des Moduls oder die Basisseite; ohne Bereich kommt ein ehrlicher Hinweis statt einer Sackgasse. X-09 → „Ressourcen“.
- **Test:** `StudioClickTests` klickt jede Kachel und jede Karte über `MouseDown`/`MouseUp` an der echten Bildschirmstelle (mit Treffer-Prüfung). Mit dem alten Code ist der Test rot („nur 0 Karten“), mit dem neuen grün.
