"""T-0689: Von Trendo gebaute Nachricht mit einem fremden Leser prüfen (Python-Standardbibliothek `email`, policy=default).

Aufruf: python -I pruefe_eml.py <ordner>   (Ordner aus TRENDO_MAIL_BELEG, enthält trendo-nachricht.eml und die Originalanhänge)
Gibt je Prüfung eine Zeile aus und endet mit Code 0 nur, wenn alles stimmt.
"""
import email
import email.policy
import hashlib
import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")
ordner = pathlib.Path(sys.argv[1])
roh = (ordner / "trendo-nachricht.eml").read_bytes()
msg = email.message_from_bytes(roh, policy=email.policy.default)
fehler = 0


def pruefe(name, ok, info=""):
    global fehler
    print(("OK    " if ok else "FEHLER") + " " + name + (" | " + info if info else ""))
    if not ok:
        fehler += 1


pruefe("Betreff (RFC 2047, gefaltet)", str(msg["Subject"]) == "Größere Übersicht für März: Zähler, Maße und Öffnungszeiten – bitte bis Freitag prüfen 😀", str(msg["Subject"]))
pruefe("Absender mit Umlaut", msg["From"].addresses[0].display_name == "Erika Müßig" and msg["From"].addresses[0].addr_spec == "erika@example.org", str(msg["From"]))
pruefe("Empfänger mit Umlaut", msg["To"].addresses[0].display_name == "Jörg Größe", str(msg["To"]))
pruefe("Kopie mit Komma im Namen", msg["Cc"].addresses[0].display_name == "Muster, Erika", str(msg["Cc"]))
pruefe("keine Blindkopie in den Kopfzeilen", b"geheim@example.net" not in roh and msg["Bcc"] is None)
pruefe("Zeilen höchstens 78 Zeichen", all(len(z) <= 78 for z in roh.split(b"\r\n")), str(max(len(z) for z in roh.split(b"\r\n"))))
pruefe("nur ASCII auf der Leitung", all(b < 128 for b in roh))
pruefe("Aufbau multipart/mixed", msg.get_content_type() == "multipart/mixed", msg.get_content_type())
text = msg.get_body(preferencelist=("plain",))
html = msg.get_body(preferencelist=("html",))
pruefe("Textteil", text is not None and "12,50 €" in text.get_content() and "Äpfel" in text.get_content())
pruefe("HTML-Teil", html is not None and "<b>Übersicht</b>" in html.get_content() and "<li>Äpfel</li>" in html.get_content())
anh = list(msg.iter_attachments())
pruefe("zwei Anhänge", len(anh) == 2, ", ".join(a.get_filename() or "?" for a in anh))
for a in anh:
    name = a.get_filename()
    orig = ordner / name
    daten = a.get_content()
    gleich = orig.exists() and orig.read_bytes() == daten
    pruefe(f"Anhang „{name}“ bytegleich", gleich, "sha256=" + hashlib.sha256(daten).hexdigest())
print("sha256(trendo-nachricht.eml)=" + hashlib.sha256(roh).hexdigest())
print("ERGEBNIS: " + ("bestanden" if fehler == 0 else f"{fehler} Fehler"))
sys.exit(1 if fehler else 0)
