# T-0705: Fremddateien für die Büro-Echtläufe – erzeugt von LibreOffice selbst (UNO), nicht von Trendo.
#   formular       PDF mit echten AcroForm-Feldern (Writer: Textfelder „Nachname“, „Wohnort“, Ankreuzfeld „Zustimmung“)
#   praesentation  PowerPoint-Datei (Impress: drei Folien mit Titel, Punkten und Sprechernotiz)
# Aufruf mit dem Python, das LibreOffice mitbringt:
#   <LibreOffice>\program\python.exe lo_fremd.py <formular|praesentation> <soffice.exe> <ziel> <profilordner>
import os
import subprocess
import sys
import time

import uno
from com.sun.star.awt import Point, Size
from com.sun.star.beans import PropertyValue


def prop(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


def start(soffice, profile):
    pipe = "trendo_buero_%d" % os.getpid()
    proc = subprocess.Popen([soffice, "-env:UserInstallation=" + uno.systemPathToFileUrl(os.path.abspath(profile)), "--headless", "--norestore",
                             "--nologo", "--accept=pipe,name=%s;urp;" % pipe])
    ctx = uno.getComponentContext()
    resolver = ctx.ServiceManager.createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", ctx)
    for _ in range(240):
        try:
            remote = resolver.resolve("uno:pipe,name=%s;urp;StarOffice.ComponentContext" % pipe)
            return proc, remote.ServiceManager.createInstanceWithContext("com.sun.star.frame.Desktop", remote)
        except Exception:
            time.sleep(0.5)
    proc.kill()
    raise SystemExit("LibreOffice antwortet nicht")


def formular(desktop, target):
    doc = desktop.loadComponentFromURL("private:factory/swriter", "_blank", 0, (prop("Hidden", True),))
    text = doc.Text
    cur = text.createTextCursor()
    text.insertString(cur, "Antrag auf Musterleistung", False)
    text.insertControlCharacter(cur, 0, False)
    y = 3000
    for kind, name, label in [("TextField", "Nachname", "Nachname"), ("TextField", "Wohnort", "Wohnort"), ("CheckBox", "Zustimmung", "Ich stimme zu")]:
        text.insertString(cur, label, False)
        text.insertControlCharacter(cur, 0, False)
        model = doc.createInstance("com.sun.star.form.component." + kind)
        model.Name = name
        if kind == "CheckBox":
            model.Label = label
        shape = doc.createInstance("com.sun.star.drawing.ControlShape")
        shape.Size = Size(7000 if kind == "TextField" else 5000, 800)
        shape.Position = Point(4500, y)
        shape.AnchorType = uno.Enum("com.sun.star.text.TextContentAnchorType", "AT_PAGE")
        shape.AnchorPageNo = 1
        shape.setControl(model)
        doc.DrawPage.add(shape)
        y += 1500
    data = uno.Any("[]com.sun.star.beans.PropertyValue", (prop("ExportFormFields", True), prop("FormsType", 0)))
    uno.invoke(doc, "storeToURL", (uno.systemPathToFileUrl(os.path.abspath(target)), (prop("FilterName", "writer_pdf_Export"), prop("FilterData", data))))
    doc.close(True)


SLIDES = [
    ("Vereinsjahr 2026", ["Rückblick und Ausblick"], "Begrüßen und kurz den Ablauf nennen."),
    ("Was wir geschafft haben", ["Neues Vereinsheim eröffnet", "120 neue Mitglieder", "Sommerfest mit 800 Gästen"], "Den Helfern danken."),
    ("Was wir vorhaben", ["Jugendgruppe gründen", "Spielplatz erneuern"], ""),
]


def praesentation(desktop, target):
    doc = desktop.loadComponentFromURL("private:factory/simpress", "_blank", 0, (prop("Hidden", True),))
    pages = doc.DrawPages
    while pages.Count < len(SLIDES):
        pages.insertNewByIndex(pages.Count - 1)
    for i, (title, points, note) in enumerate(SLIDES):
        page = pages.getByIndex(i)
        page.Layout = 0 if i == 0 else 1  # Titelfolie, dann Titel und Inhalt
        for k in range(page.Count):
            shape = page.getByIndex(k)
            if shape.ShapeType.endswith("TitleTextShape"):
                shape.setString(title)
            elif shape.ShapeType.endswith("OutlinerShape") or shape.ShapeType.endswith("SubTitleShape"):
                shape.setString("\n".join(points))
        if note:
            notes = page.getNotesPage()
            for k in range(notes.Count):
                s = notes.getByIndex(k)
                if s.ShapeType.endswith("NotesShape"):
                    s.setString(note)
    doc.storeToURL(uno.systemPathToFileUrl(os.path.abspath(target)), (prop("FilterName", "Impress MS PowerPoint 2007 XML"),))
    doc.close(True)


def main(mode, soffice, target, profile):
    proc, desktop = start(soffice, profile)
    try:
        {"formular": formular, "praesentation": praesentation}[mode](desktop, target)
    finally:
        try:
            desktop.terminate()
        except Exception:
            pass
        try:
            proc.wait(60)
        except Exception:
            proc.kill()
    print("geschrieben:", os.path.basename(target), os.path.getsize(target), "Bytes")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4])
