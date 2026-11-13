# KI-Anbieter und Zahlungsanbieter: Bedingungen

Phase 1. Recherche am 13.11.2026 in öffentlichen Quellen. Die Quellen stehen am Ende. Aussagen mit „(Suchtreffer)“ stammen nur aus Suchzusammenfassungen; die Seite selbst wurde nicht geöffnet. Das ist keine Rechtsberatung.

## 1. Claude Code (Anthropic)

| Punkt | Befund | Quelle |
|---|---|---|
| Kunde meldet sich im unveränderten Claude Code mit eigenem Abo an | erlaubt | [A1] |
| Claude Code in einem Produkt vorinstalliert oder darin laufend | Es gelten die Commercial Terms. Das Programm darf nicht verändert und die Anmeldewege dürfen nicht entfernt werden. Jeder Nutzer meldet sich selbst an, mit eigenem API-Schlüssel, Abo oder Cloud-Zugang | [A1] |
| Anbieter bezahlt Claude für Kunden, verkauft es weiter oder vermittelt es | verboten | [A1] |
| Claude.ai-Anmeldung in eigener App anbieten, Anfragen über fremde Abos leiten, Zugangsdaten oder Sitzungstoken sammeln oder speichern | verboten | [A1] |
| „Claude Code“ im Produktnamen oder Logo | verboten | [A1] |
| Verbraucherbedingungen: keine geteilten Zugangsdaten, keine kommerzielle Nutzung des Verbraucher-Abos | gilt für jedes Kundenabo | [A2] |
| Fällt „Trendo ruft das lokal installierte Claude Code des Kunden auf“ unter „running Claude Code in your products“? | **nicht eindeutig geregelt** | [A1], [A3] |

### Abgleich mit Trendo

- Trendo speichert keine Claude-Tokens und hat keine eigene Claude-Anmeldung (Memory-Regel, Code `W2/ClaudeCode.cs`). Das passt.
- Trendo verkauft kein Claude-Kontingent. Der Preis der Complete Edition darf keine Claude-Leistung enthalten. In Shop-Texten muss stehen: „Claude ist nicht enthalten. Für Claude-Funktionen braucht jede Person ein eigenes Claude-Konto.“
- Der Name „Claude“ darf in Trendo nur beschreibend vorkommen („arbeitet mit Claude“), nicht als Teil eines Produkt- oder Studionamens. Zu prüfen: Bezeichnungen wie „Claude-Studio“ oder „Claude-Chat“ in der Oberfläche.
- Offen: schriftliche Rückfrage bei Anthropic, ob der Aufruf des lokalen Claude Code aus einer verkauften Desktop-Software so zulässig ist. Bis dahin Status **offen**.

## 2. Lokale Modelle

| Modell | Lizenz | Verkauf |
|---|---|---|
| Qwen2.5 0.5B, 1.5B, 7B, 14B, 32B | Apache-2.0 (7B direkt geprüft, übrige Suchtreffer) | erlaubt |
| **Qwen2.5 3B** | Qwen Research License, nur Forschung und Bewertung. Für kommerzielle Nutzung muss eine Lizenz angefragt werden | **nicht erlaubt** ohne eigene Lizenz. Vorschlag: durch Qwen2.5 1.5B oder 7B ersetzen |
| Qwen2.5 72B | Qwen License, kommerziell bis 100 Mio. monatliche Nutzer | erlaubt mit Hinweis |
| Qwen2.5-VL 7B | nicht geprüft | offen |
| Bild-, Musik- und Stimmmodelle | je Modell verschieden | eigene Liste nötig |

## 3. Paddle Billing

| Punkt | Befund | Quelle |
|---|---|---|
| Merchant of Record | ja. Paddle verkauft an den Kunden, stellt die Rechnung mit eigener Nummer aus und schickt sie | [P1], [P2] |
| Rechnung abrufen | `GET /transactions/{id}/invoice`. Der Link gilt eine Stunde. Erst im Status `completed`, nicht bei Betrag 0 | [P4] |
| Checkout vorbereiten | `POST /transactions` auf dem Server, Antwort enthält `checkout.url` | [P5] |
| Webhook-Prüfung | Header `Paddle-Signature` (`ts=…;h1=…`), HMAC-SHA256 über `ts:Rohtext` mit dem Endpunkt-Geheimnis. Zeitabweichung begrenzen, Serveruhr per NTP | [P6] |
| Sandbox | `sandbox-api.paddle.com`, Schlüssel mit `pdl_sdbx_`, nur Testkarten, keine Auszahlungen, keine Monatsauszüge | [P7], [P8] |
| Unterlagen für den Verkäufer | je Monat und Währung: Statement, Reverse Invoice (Rechnung des Verkäufers an Paddle, von Paddle erstellt), Remittance Advice. Per E-Mail und im Dashboard. **Per API nicht belegt.** Per API gibt es CSV-Berichte (`transactions`, `payout_reconciliation`) | [P9], [P10] |
| Freischaltung | Domain, Unternehmen (entfällt für Einzelunternehmer), Identität. Öffentlich nötig: AGB mit Paddle als MoR, Erstattungsregel, Datenschutzerklärung, Preise ohne Anmeldung sichtbar | [P11], [P12] |
| Gebühr | 5 % + 0,50 USD je Transaktion, keine Monatsgebühr | [P14] |

### Paddle-Liste verbotener und eingeschränkter Produkte: Abgleich mit Trendo

Laut [P13] verboten sind unter anderem Deepfakes, Stimmimitation, Reinigungs- und Antivirus-Programme sowie Spyware. Eingeschränkt sind Software zur Reparatur oder Leistungssteigerung von Geräten und VPN. Trendo enthält Funktionen, die Paddle so einordnen könnte:

- Musik-Studio „Stimme lernen“ (Stimme der eigenen Person, mit Einwilligung)
- Forensik-, Sicherheits- und Systemwerkzeuge
- Wartungs- und Aufräumfunktionen

Software mit eigener Lizenzprüfung erwähnt Paddle nicht. **Vor der Kontoeröffnung schriftlich bei Paddle klären**, ob Trendo mit diesen Studios verkauft werden darf oder ob sie aus der verkauften Fassung herausmüssen.

## 4. Italien

- Paddle erstellt die Reverse Invoice selbst, „no action is needed“. Italien und die elektronische Rechnung über SdI erwähnt Paddle nicht [P15].
- Grenzüberschreitende Vorgänge laufen seit 1.7.2022 über SdI statt Esterometro (Suchtreffer) [I1].
- **Offen:** Muss ein italienischer Anbieter die von Paddle erstellte Gutschrift zusätzlich als XML über SdI senden oder selbst eine Rechnung an Paddle ausstellen? Die Fachquellen widersprechen sich (Suchtreffer) [I2]. **Klärung durch einen Commercialista ist Pflicht.** Trendo erzeugt dafür keine Steuerdaten und keine Rechnungsnummern selbst.

## 5. Code-Signatur unter Windows

| Weg | Befund | Quelle |
|---|---|---|
| Azure Artifact Signing | Public Trust für Organisationen in der EU, Einzelentwickler nur USA und Kanada. Bezahltes Azure-Abo nötig, Prüfung 1–20 Werktage. Preis laut Fachpresse 9,99 USD pro Monat (Suchtreffer) | [S1], [S2], [S3] |
| Certum Standard Code Signing in der Cloud | ab 209 €, auch für natürliche Personen | [S4] |
| Certum Open Source | nur für Open-Source-Software, nicht für ein Bezahlprodukt | [S5] |
| OV-Zertifikat anderer Anbieter | Schlüssel auf zertifizierter Hardware, Händlerpreise ab etwa 219 USD pro Jahr (Suchtreffer) | [S6] |

Laut Projektregel gilt die Code-Signatur nie als bestanden, bis echte signierte Dateien geprüft sind.

## Quellen (abgerufen 13.11.2026)

- A1 https://code.claude.com/docs/en/legal-and-compliance
- A2 https://www.anthropic.com/legal/consumer-terms
- A3 https://support.claude.com/en/articles/11145838-using-claude-code-with-your-pro-or-max-plan
- P1 https://www.paddle.com/seller-guides/seller-handbook (Suchtreffer)
- P2 https://developer.paddle.com/build/invoices/create-issue-invoices (Suchtreffer)
- P4 https://developer.paddle.com/api-reference/transactions/get-invoice-pdf
- P5 https://developer.paddle.com/api-reference/transactions/create-transaction
- P6 https://developer.paddle.com/webhooks/signature-verification
- P7 https://developer.paddle.com/build/tools/sandbox
- P8 https://developer.paddle.com/getting-started/sandbox (Suchtreffer)
- P9 https://www.paddle.com/help/manage/get-paid/what-statements-will-i-receive
- P10 https://developer.paddle.com/api-reference/reports/create-report
- P11 https://www.paddle.com/help/start/account-verification/what-is-account-verification
- P12 https://developer.paddle.com/build/onboarding/set-up-checklist (Suchtreffer)
- P13 https://www.paddle.com/help/start/intro-to-paddle/what-am-i-not-allowed-to-sell-on-paddle
- P14 https://www.paddle.com/pricing
- P15 https://www.paddle.com/help/manage/get-paid/do-i-need-to-invoice-paddle-for-my-payout
- Q-Lizenzen: https://huggingface.co/Qwen/Qwen2.5-3B-Instruct/blob/main/LICENSE, https://huggingface.co/Qwen/Qwen2.5-7B-Instruct, https://huggingface.co/Qwen/Qwen2.5-72B-Instruct/blob/main/LICENSE
- I1 https://www.fiscoetasse.com/normativa-prassi/13169-esterometro-2022-il-punto-dellagenzia-sui-nuovi-adempimenti.html (Suchtreffer)
- I2 https://autofatturiamo.it/articoli/autofattura-invio-sdi-obbligo/ (Suchtreffer)
- S1 https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- S2 https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- S3 https://www.devclass.com/security/2026/01/14/code-signing-windows-apps-may-be-easier-and-more-secure-with-new-azure-artifact-service/4079554 (Suchtreffer)
- S4 https://shop.certum.eu/standard-code-signing-in-the-cloud.html
- S5 https://shop.certum.eu/open-source-code-signing-on-simplysign.html (Suchtreffer)
- S6 https://www.thesslstore.com/blog/code-signing-price-changes-as-cas-align-with-new-industry-standards/ (Suchtreffer)
