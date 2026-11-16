# Bericht T-0689 – D5a E-Mail als vollwertiges Programm

**Zweig:** `claude/T-0689-mail` (Basis `claude/T-0685-b1-integration`) · Arbeitskopie: eigener Temp-Klon, nach dem Push gelöscht.

## Entscheidung Bibliothek
Im Projekt war **keine** Mail-Bibliothek; vorhanden war ein eigenes IMAP/SMTP-Protokoll (`MailProtocol.cs`) und ein eigener MIME-Leser. MailKit/MimeKit (MIT, gut gepflegt) wurde geprüft, aber **nicht** eingesetzt:
- QA-019: der Kern darf kein `System.Net.Sockets`/HTTP enthalten – das eigene Protokoll arbeitet bereits auf einem fertigen Stream, die App liefert die TLS-Verbindung (`MailNet`). MailKit bringt seine eigene Netzschicht mit.
- Neue Abhängigkeiten (MailKit + MimeKit + BouncyCastle, zusammen mehrere MB) für den Funktionsumfang nicht nötig; die einzige echte Grenze – OAuth für Gmail/Outlook.com – löst MailKit allein auch nicht (bräuchte eigene, registrierte App-Kennung beim Anbieter).
- Sicherheit/Zuverlässigkeit belegt durch Tests gegen einen echten Protokoll-Server und einen fremden MIME-Leser (siehe unten). Deshalb kein Eintrag in `THIRD_PARTY_NUGET.md`.
Rust: nicht eingesetzt – kein messbarer Vorteil (Netz-I/O-gebunden, bestehender C#-Kern).

## Was war → was ist jetzt → Beweis
| Punkt | Vorher (Audit SYS-09, im Code geprüft) | Jetzt | Beleg |
|---|---|---|---|
| IMAP | nur INBOX, 50 Kopfzeilen, nur lesend (`MailProtocol.cs` HeadersAsync) | Ordnerliste (LIST, Sonderordner, UTF-7-Namen), seitenweise (`HeadersPageAsync`, „Ältere laden“), ganze Nachricht als Bytes, gelesen/ungelesen + Stern (STORE), verschieben (MOVE, sonst COPY+EXPUNGE), Papierkorb / endgültig mit Rückfrage, Suche lokal + auf dem Server (SEARCH, UTF-8-Literal), Entwurf/Gesendet ablegen (APPEND) | `MailRoundtripTests.Send_fetch_roundtrip_tls`, `Paging`, `Fallback_and_wrong_password`; `MailFull_Ui.Read_program` (echt, Testserver) |
| HTML-Ansicht | HTML wurde zu Text | geprüfter Baum aus fester Erlaubtliste (`MailHtml.Sanitize`), gezeichnet mit eigenen Steuerelementen (`MailHtmlView`, kein Browser, keine Skript-Maschine); Bilder aus dem Internet erst nach „Bilder laden“; Links nur http/https/mailto, Ziel wird vor dem Öffnen gezeigt; „Im Browser ansehen“ schreibt eine geprüfte Seite mit Content-Security-Policy | `Html_sanitize` (Skripte, on…-Attribute, `jav&#x09;ascript:`, Rahmen, Formulare, SVG, Zählpixel), `Read_program` |
| Anhänge | Lesen ja; Senden nur `text/plain` (`MailProtocol.cs:184`) | Senden mit Anhängen (multipart/mixed), Text + HTML (multipart/alternative), Dateinamen RFC 2231, lange Betreffzeilen RFC 2047 gefaltet, BCC nie in Kopfzeilen; Lesen auf Bytes (Windows-1252, 8bit, RFC-2231-Fortsetzungen, Pfade im Namen entschärft) | Rundreise **bytegleich** über SMTP→IMAP; `Mime_write`, `Mime_read_foreign`; Fremdleser Python `email`: `reports/T-0689/fremdleser-python-email.log` (bestanden) |
| Zwischenspeicher | `Inbox` nur im Speicher | `MailStore`: Ordner, Kopfzeilen, geöffnete Nachrichten (.eml) über `AtomicFile`; Abgleich (auf dem Server Gelöschtes verschwindet, neue UIDVALIDITY baut neu auf); offline lesbar und durchsuchbar | `Store_offline`; `Read_program` Schritt 9 (Server abgeschaltet, Neustart offline) |
| Senden | Rückfrage vorhanden, ohne Anhänge, keine Ablage auf dem Server | weiterhin nur über `AskSend` → „Ja, jetzt senden“; danach Kopie in „Gesendet“ des Postfachs (außer Anbieter, die selbst ablegen), Entwurfskopie wird entfernt; Antworten mit `In-Reply-To`/`References`; Weiterleiten nimmt Anhänge mit; SMTP mit PLAIN oder LOGIN; TLS-Pflicht (465 oder STARTTLS, sonst Abbruch vor dem Passwort) | `Write_program` (echte Zustellung an Testserver), `Smtp_requires_tls` |
| Entwürfe | nur per Knopf, Text im Speicher | sichert sich 2 s nach jeder Änderung selbst (übersteht Absturz), „Als Entwurf speichern“ legt ihn zusätzlich im Entwürfe-Ordner des Postfachs ab und ersetzt ihn beim nächsten Speichern | `Write_program` (Neustart ohne Schließen, Entwurf vollständig da; Server-Kopie ersetzt) |
| Konto | Formularzeile, Server geraten | Assistent mit Anbieter-Tabelle (`MailProviders`, 10 Anbieter), Hinweis je Anbieter, Server von Hand, Verbindung wird geprüft; Passwort nur `ISecretStore`; Outlook.com/Hotmail ehrlich **BLOCKIERT** (nur OAuth) mit Ausweg; Gmail/iCloud/Yahoo/Telekom: App-Passwort-Anleitung | `Providers`; `Read_program` Schritte 1–2 (Passwort in keiner Datei) |
| Oberfläche | Konto, Ordner, Regeln gleichzeitig; „E-Mail & Kontakte“ = Formular | Leerzustand nur „Konto einrichten“; danach Leiste mit genau einer goldenen Aktion „Neue E-Mail“ (im Schreibfenster „Senden …“, bei Rückfrage „Ja, jetzt senden“); drei Spalten Ordner · Liste · Nachricht, Schreibfenster über Liste+Lesebereich; Regeln hinter „Regeln“; Strg+N, Strg+R, Strg+Umschalt+R, Entf, Strg+Eingabe, Strg+S, J/K; Rechtsklick an jeder Zeile (öffnen, antworten, weiterleiten, gelesen, merken, verschieben, löschen); Datei aufs Schreibfenster ziehen = Anhang; Nachricht auf Ordner ziehen = verschieben. V-21 (Mail-Teil): Reiter „E-Mail & Kontakte“ heißt jetzt „Schreiben“; dort führt die goldene Aktion ins eigene Schreibfenster (ohne Konto zum Assistenten), „Im Mailprogramm öffnen“ bleibt | Bilder geprüft (Leerzustand Posteingang, Seite „Schreiben“); `Write_page_hands_over` |
| KI | `mail.entwurf` schrieb nur eine .eml ins Projekt | Ergebnis von `mail.entwurf` kommt ins offene Schreibfenster; „Zusammenfassen (KI)“ und „Antwort vorschlagen (KI)“ über `Intel.AskAsync`, Mailtext als Inhalt markiert (keine Anweisungen befolgen); „KI-Text zurücknehmen“; die KI sendet nie | `Ki_only_drafts` (KI-Modell ersetzt, Kern echt; Aufruf über die Auftragsleiste) |

## Tests (Rohzahlen)
- `dotnet build Trendo.sln -c Release`: 0 Fehler.
- Core (gezielt `MailRoundtripTests|PimTests|MailPlusTests|W3Tests|PromptActionTests|NoFixedUser|ConnectedPrompt|VorbildAnalysen|LetterOffice`): **Bestanden, 134/134**.
- App, je Klasse: MailFull_Ui 4/4, MailProgram_Ui 1/1, MailPlus_Ui 1/1, O1O6_PimUi 2/2, SYS09_Ui 1/1, Teil4BueroUiTests 4/4, StudiosBueroGeld_Ui 36/36 (nach letzter Änderung); vor den letzten zwei Kleinkorrekturen außerdem grün: AddressBook_Ui 2/2, StudiosAlle_Ui 98/98, EinfachBedienen_Ui 11/11, UebersichtEinfach_Ui 2/2, PromptBarTests 6/6, Modern_Ui 13/13, Assist_Ui 3/3, ClaudeOperate_Ui 3/3, W3UiTests-Klassen je grün.
- Kein Volltest (macht die Hauptsitzung).

**Echt vs. ersetzt:** Gegenstelle aller Rundreisen ist `tests/Trendo.Core.Tests/MailTestServer.cs` – ein eigener IMAP4rev1- und SMTP-Server über echte Loopback-Sockets (IMAP wahlweise TLS, SMTP mit echtem STARTTLS, selbst erzeugtes Testzertifikat, nur dieses wird angenommen). Trendos Protokoll- und MIME-Schicht läuft unverändert. Ersetzt bleiben: das KI-Modell (`KiBackendsOverride`), der Windows-Dateidialog (`PickOverride`), der Abruf fremder Bilder (`MailImageOverride`), in den App-Tests der Verbindungsaufbau (`ImapStreamOverride`/`SmtpStreamOverride` liefern den Socket zum Testserver; `MailNet.Tls` gegen echte Anbieter ungeprüft). Ältere Tests mit `SendOverride` bleiben unverändert.

## Abnahmekriterien
1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **VOLLWERTIG** (Konto einrichten → schreiben → Anhang → Rückfrage → echte Zustellung + Kopie in „Gesendet“, `Write_program`).
2. Fremddatei importieren/bearbeiten/exportieren, öffnet im Fremdprogramm: **TEILWEISE** – fremde MIME-Mails (Windows-1252, RFC 2231, verschachtelt) werden gelesen; „Als Datei sichern“ schreibt die Originalnachricht bytegleich als .eml; die von Trendo gebaute Nachricht ist mit dem Python-Standardparser geprüft (`reports/T-0689/`, Rohlog + SHA-256). Thunderbird/Outlook sind auf diesem PC nicht installiert → Öffnen dort **NICHT GETESTET**. Kein .mbox-Import.
3. Absturz verliert ≤ 30 s: **VOLLWERTIG** für Schreibfenster (Entwurf nach 2 s, `Write_program` mit Neustart ohne Schließen) und gelesene Post (Zwischenspeicher über `AtomicFile`).
4. KI erledigt Fachaufgabe im offenen Dokument, rückgängig: **VOLLWERTIG** (Antwortvorschlag/`mail.entwurf` ins Schreibfenster, „KI-Text zurücknehmen“; mit ersetztem KI-Modell getestet).
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** gegen den lokalen Testserver; gegen echten Anbieter **NICHT GETESTET**.
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen: **TEILWEISE** – Kürzel, Rechtsklick, Datei-Ziehen (Anhang) und Ziehen auf Ordner vorhanden; Datei-Ziehen und Mail-auf-Ordner-Ziehen nur im Code (Headless-Test zieht nicht), Anhängen über denselben Weg (`MailAttach`) getestet. Eigene Einfach/Profi-Unterschiede gibt es auf dieser Seite nicht (Posteingang ist keine Hüllen-Seite; `MainWindow.ProgramShell.cs` nicht angefasst).

## NICHT geprüft
- **Echter Anbieter (GMX, Gmail, Telekom …): NICHT GETESTET** – keine Zugangsdaten (nicht erfragt, nicht erfunden). Damit auch `MailNet.Tls`/STARTTLS gegen echte Zertifikate, Sonderordner-Namen echter Server, Sende-Grenzen.
- Thunderbird/Outlook als Fremdprogramm (nicht installiert).
- Gefüllter Posteingang und Schreibfenster nur über Tests geprüft, nicht als Bild (Renderer kann keinen Server anbinden).
- Gmail und Outlook.com per OAuth: **BLOCKIERT** (bräuchte eigene registrierte App beim Anbieter).
- POP3: nicht gebaut (IMAP deckt alle genannten Anbieter ab).

### Prüfanleitung für den Nutzer (echter Anbieter, z. B. GMX)
1. In GMX/WEB.DE unter Einstellungen → POP3/IMAP den Zugriff einschalten (bei Gmail: App-Passwort erzeugen).
2. Trendo → E-Mails & Termine → Posteingang → Adresse, Name, Passwort → „Konto einrichten“. Erwartet: „Verbindung steht – N Ordner gefunden“, Ordnerliste wie im Webmail.
3. Eine Mail mit Anhang an die eigene Adresse senden („Neue E-Mail“, Anhang, „Senden …“ → „Ja, jetzt senden“). Erwartet: kommt an, Kopie im Ordner „Gesendet“ (im Webmail prüfen), Anhang dort öffnen.
4. „Abrufen“, die Mail öffnen, Anhang speichern, löschen (landet im Papierkorb des Webmails), im Papierkorb endgültig löschen (Rückfrage).
5. Netz trennen, Trendo neu starten: Posteingang und geöffnete Mails sind weiter lesbar.

## Offene Punkte
- OAuth (Gmail/Outlook.com) – nur mit eigener App-Registrierung möglich, Entscheidung des Projektinhabers.
- .mbox-Import, IDLE (Sofort-Benachrichtigung), mehrere Konten, Regeln auf dem Server.
- Mehrfachauswahl in der Liste (Aktionen nehmen derzeit eine Nachricht; Kern kann Listen).

## Berührte Dateien
- Neu: `src/Trendo.Core/W3/MailMime.cs`, `MailHtml.cs`, `MailProviders.cs`, `MailStore.cs`; `src/Trendo.App/MainWindow.MailProgram.cs`, `MainWindow.MailProgramView.cs`, `MailHtmlView.cs`; `tests/Trendo.Core.Tests/MailTestServer.cs`, `MailRoundtripTests.cs`; `tests/Trendo.App.Tests/MailFullUiTests.cs`; `reports/T-0689/*`; `tasks/T-0689-mail*.md`.
- Geändert: `src/Trendo.Core/W3/Mail.cs` (MailDraft/ParsedMail erweitert, `ParseEml` → `MailMime`), `MailProtocol.cs` (IMAP/SMTP ausgebaut, Anbieter aus Tabelle), `MailPlus.cs` (Entwürfe mit Anhängen/BCC, `AtomicFile`); `src/Trendo.App/MailNet.cs`, `MainWindow.Mail.cs` (Reiter „Schreiben“, goldene Aktion), `MainWindow.Pim.cs` (Posteingang-Teil herausgelöst, Kalender/Aufgaben unverändert), `MainWindow.PromptAction.cs` (eine Zeile: KI-Entwurf ins Schreibfenster); `tests/Trendo.App.Tests/Trendo.App.Tests.csproj` (Testserver verlinkt); `.gitattributes` (Belege ohne Zeilenende-Umwandlung).
