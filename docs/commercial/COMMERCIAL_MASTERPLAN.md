# Commercial-Masterplan: Lückenanalyse und Phasen

Phase 1, Stand 13.11.2026. Grundlage: `REPOSITORY_AUDIT.md`, `RIGHTS_AND_OWNERSHIP.md`, `THIRD_PARTY_LICENSES.md`, `AI_PROVIDER_COMPLIANCE.md`.

Die geplanten Preise (Complete 299 € netto einmalig, Team 999 € netto für 5 Nutzer, Update-Verlängerung 99 € netto pro Jahr, Enterprise auf Anfrage) sind **Planungswerte**. Sie werden nirgends veröffentlicht, bevor der Inhaber zustimmt.

## 1. Die größten Lücken

| Nr. | Lücke | Folge |
|---|---|---|
| L1 | Öffentliches Repository ohne Lizenz | Jeder kann eine Fassung ohne Lizenzprüfung bauen. Eine Lizenzprüfung im Client ist dann nur eine Hürde, kein Schutz |
| L2 | Kein Server | Ohne Server keine Konten, keine Zahlungsbestätigung, keine signierten Berechtigungen, keine Owner Console im Web |
| L3 | Kein rechtlicher Verkäufer | Paddle prüft Identität und Unternehmen. Ohne Steuerstatus kein Konto |
| L4 | Kein Lizenz- und Editionssystem | Free und Complete lassen sich nicht unterscheiden |
| L5 | Keine Code-Signatur | Windows warnt beim Start, Kunden brechen ab |
| L6 | Paddle-Produktfreigabe unklar | Stimme lernen, Forensik und Systemwerkzeuge stehen auf Paddles Verbots- oder Einschränkungsliste oder nahe daran |
| L7 | Qwen2.5 3B nur für Forschung | in der verkauften Fassung nicht erlaubt |
| L8 | Nur Deutsch | Auftrag verlangt Deutsch, Italienisch, Englisch. Steht gegen die Entscheidung vom 02.11.2026 |
| L9 | Version kommt aus zwei Quellen | Lizenzen mit Update-Zeitraum brauchen eine eindeutige Version |
| L10 | Keine Rechtstexte | AGB, Widerruf, Erstattung, Datenschutz, Impressum fehlen |
| L11 | Italienische Steuerfragen offen | SdI-Pflicht für die Paddle-Gutschrift ungeklärt |

## 2. Entscheidungen des Inhabers (vor jedem Bau)

| Nr. | Frage | Optionen | Vorschlag |
|---|---|---|---|
| E1 | Quelltext und Repository | a) Repository privat stellen, b) öffentlich lassen und Lizenz „alle Rechte vorbehalten“ hinzufügen, Verkaufsteile in privatem Repository, c) Open Source mit Bezahl-Zusatz | b: Desktop-Code bleibt sichtbar, Lizenzserver, Owner Console und Schlüssel liegen in einem neuen privaten Repository. Die Sichtbarkeit ändert nur der Inhaber selbst |
| E2 | Umfang der Free Edition | a) alle Studios mit Grenzen (Export, Projektzahl), b) ausgewählte Studios frei, Rest Complete, c) zeitlich begrenzte Testversion | b, mit 14 Tagen Test für Complete |
| E3 | Verkäufer | Einzelunternehmer mit Partita IVA, Gesellschaft, anderes Land | mit Commercialista festlegen, vor dem Paddle-Konto |
| E4 | Zahlungsanbieter | Paddle (Merchant of Record) oder Alternative | Paddle, zuerst nur Sandbox |
| E5 | Server | eigener kleiner Server (z. B. bei einem EU-Hoster) oder verwalteter Dienst | EU-Hoster, Datenhaltung in der EU |
| E6 | Sprachen | nur Deutsch (bisherige Entscheidung) oder Deutsch, Italienisch, Englisch | Shop, Konto, Rechnungsmails und Rechtstexte dreisprachig; die Studios zunächst deutsch |
| E7 | Kritische Studios beim Verkauf | mitverkaufen nach Paddle-Freigabe oder aus Complete herausnehmen | erst Paddle schriftlich fragen |
| E8 | Qwen2.5 3B | ersetzen oder kommerzielle Lizenz anfragen | durch 1.5B oder 7B ersetzen |
| E9 | Code-Signatur | Certum Cloud, OV-Zertifikat, Azure (nur mit Organisation) | Certum Cloud |

## 2a. Erweiterung „Commercial Desktop Edition & Hardware Compatibility“ (13.11.2026)

Verbindliche Vorgaben des Inhabers, umgesetzt bzw. dokumentiert in:
- `DESKTOP_PRODUCT_STRATEGY.md`: Windows-11-Desktop-Software, nicht als Windows-Ersatz beworben, Name bleibt
- `RELEASE_VARIANTS.md`: Hauptprodukt ist die Windows-Installation. Linux-USB, Pi-Verbund und Portable bleiben erhalten, sind aber vorerst nicht im Angebot. Das Linux-Abbild ist von den Windows-Releases getrennt
- `HARDWARE_COMPATIBILITY.md`: Systemcheck vor der Installation (`installer/systemcheck.ps1`), in Trendo und vor dem Kauf
- `PERFORMANCE_PROFILES.md`: Basic, Standard, Professional (vorläufig), Eignung je Studio, Leistungsanpassung nach Zustimmung
- `SYSTEM_REQUIREMENTS.md`: Entwurf. Es werden keine ungemessenen Werte veröffentlicht

Neue Lücke **L12**: Die Profilgrenzen sind nicht auf echter Hardware gemessen. Die Messreihe gehört zu Phase 8.

## 3. Zielarchitektur (Vorschlag)

- **Desktop-Client (dieses Repository):** Seite „Konto, Lizenz & Vollversion“, Prüfung signierter Berechtigungen mit dem **öffentlichen** Schlüssel, Freischaltung je Studio, Offline-Nachfrist. Kein privater Schlüssel, kein Paddle-Geheimnis, keine Kartendaten.
- **Lizenzserver (neues, privates Repository):** Konten, E-Mail-Bestätigung, Paddle-Webhooks mit Signaturprüfung, signierte Berechtigungen (Ed25519), Geräteverwaltung, Protokoll.
- **Owner Console:** im Webportal des Servers, mit Zwei-Faktor-Anmeldung. In der App nur eine Ansicht für den Inhaber. Owner-Rechte kommen vom Server, nie aus einer festen E-Mail-Adresse im Code.
- **Rechnungen:** von Paddle abrufen und anzeigen, nicht selbst erzeugen.
- **Finance & Tax Center:** Paddle-CSV-Berichte einlesen, mit Bestellungen abgleichen, Export für den Steuerberater. Keine eigenen Steuersätze oder Rechnungsnummern.

Ob Rust für einen Teil des Servers Vorteile bringt (Signatur, Webhook-Annahme), wird beim Serverentwurf geprüft. Im Desktop-Client gibt es dafür keinen Grund, die Prüfung einer Signatur ist in .NET vorhanden.

## 4. Phasen

| Phase | Inhalt | Voraussetzung |
|---|---|---|
| 1 Audit | diese Dokumente | erledigt mit diesem Stand |
| 2 Grundlagen | Editionsmodell und Berechtigungsformat im Client, eindeutige Version, Lizenzseite im Produkt, Qwen 3B ersetzen, Hardware-Check und Leistungsprofile, Linux-Abbild getrennt | E1, E2, E8 (umgesetzt auf `claude/T-0710-commercial`) |
| 3 Free/Complete | Freischaltung je Studio, Testzeitraum, Offline-Nachfrist, Tests | Phase 2 |
| 4 Owner Console | Server mit Konten, Grants per E-Mail, Protokoll | E3, E5 |
| 5 Zahlungen | Paddle-Sandbox, Checkout, Webhooks, Berechtigung nach Zahlung | E4, Paddle-Sandbox-Konto vom Inhaber |
| 6 Rechnungen | Paddle-Rechnungen abrufen, Mails in drei Sprachen | Phase 5, E6 |
| 7 Finance & Tax | Abgleich, Archiv, Export | Phase 5, Commercialista |
| 8 Qualität | Testmatrix, Sicherheitsprüfung, frischer Windows-Rechner, Messreihe der Leistungsprofile auf mindestens sechs Geräten | Phasen 2–7 |
| 9 Commercial Beta | kleine Gruppe, nur Sandbox oder nach Freigabe live | Rechtstexte, Signatur, alle Punkte in `RIGHTS_AND_OWNERSHIP.md` geklärt |
| 10 Release Readiness | Release Gate | ausdrückliche Freigabe des Inhabers |

## 5. Was ohne ausdrückliche Zustimmung nicht passiert

Keine echten Zahlungen, kein Live-Checkout, keine Registrierung von Firmen, keine Bankverbindung, keine Steuererklärungen oder Meldungen, kein öffentliches Release, keine Änderung der Repository-Sichtbarkeit, keine Übertragung von Rechten, kein Löschen produktiver Daten, keine Vertragsabschlüsse.
