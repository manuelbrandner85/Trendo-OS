# T-0682: Baut die Fremddateien fuer die Importpruefung von Trendo Text, Tabelle und Praesentation.
#
# Die Dateien stammen NICHT aus Trendos eigenen Schreibern: Sie sind hier als Roh-OOXML bzw. Roh-ODF
# von Hand geschrieben (ZIP mit XML), mit Bausteinen, wie Word, Excel, PowerPoint und LibreOffice sie
# ablegen. Sie wurden nicht mit Word oder LibreOffice erzeugt und dort auch nicht gegengeprueft.
#
# Aufruf (im Ordner tests/Daten/office):  python erzeugen.py
# Die Ausgabe ist bytegleich wiederholbar (feste Zeitstempel).
import os
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
STAMP = (2026, 1, 1, 0, 0, 0)
XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PKG = 'http://schemas.openxmlformats.org/package/2006/relationships'
CT = 'http://schemas.openxmlformats.org/package/2006/content-types'


def pack(name, parts, stored_first=None):
    path = os.path.join(HERE, name)
    with zipfile.ZipFile(path, 'w') as z:
        for part, content in parts:
            info = zipfile.ZipInfo(part, STAMP)
            info.compress_type = zipfile.ZIP_STORED if part == stored_first else zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, content.encode('utf-8') if isinstance(content, str) else content)
    print(name, os.path.getsize(path), 'Bytes')


def rels(*items):
    return XML + f'<Relationships xmlns="{PKG}">' + ''.join(
        f'<Relationship Id="{i}" Type="{REL}/{t}" Target="{target}"/>' for i, t, target in items) + '</Relationships>'


def types(defaults, overrides):
    return XML + f'<Types xmlns="{CT}">' + ''.join(f'<Default Extension="{e}" ContentType="{c}"/>' for e, c in defaults) + ''.join(
        f'<Override PartName="{p}" ContentType="{c}"/>' for p, c in overrides) + '</Types>'


# ---------------------------------------------------------------- DOCX
def docx():
    w = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'
    ns = (w + f' xmlns:r="{REL}" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"'
          ' xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"'
          ' xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"'
          ' xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"'
          ' xmlns:v="urn:schemas-microsoft-com:vml" mc:Ignorable="wps"')

    def p(text, style=None):
        ppr = f'<w:pPr><w:pStyle w:val="{style}"/></w:pPr>' if style else ''
        return f'<w:p>{ppr}<w:r><w:t xml:space="preserve">{text}</w:t></w:r></w:p>'

    def tc(text, width, span=None, vmerge=None):
        pr = f'<w:tcW w:w="{width}" w:type="dxa"/>'
        if span:
            pr += f'<w:gridSpan w:val="{span}"/>'
        if vmerge is not None:
            pr += '<w:vMerge w:val="restart"/>' if vmerge else '<w:vMerge/>'
        return f'<w:tc><w:tcPr>{pr}</w:tcPr>{p(text)}</w:tc>'

    # Tabelle 3 Spalten: Kopfzeile ueber alle drei Spalten verbunden, erste Spalte ueber zwei Zeilen verbunden
    table = ('<w:tbl><w:tblPr><w:tblStyle w:val="TableGrid"/><w:tblW w:w="0" w:type="auto"/></w:tblPr>'
             '<w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="3000"/><w:gridCol w:w="3000"/></w:tblGrid>'
             f'<w:tr>{tc("Umsatz nach Quartal", 9000, span=3)}</w:tr>'
             f'<w:tr>{tc("Nord", 3000, vmerge=True)}{tc("Q1", 3000)}{tc("1200", 3000)}</w:tr>'
             f'<w:tr>{tc("", 3000, vmerge=False)}{tc("Q2", 3000)}{tc("1350", 3000)}</w:tr>'
             '</w:tbl>')

    # Textfeld, wie Word es speichert: neue Form (wps) mit VML-Rueckfall
    box_text = 'Hinweis im Textfeld: bitte bis Freitag pruefen.'
    textbox = ('<w:p><w:r><mc:AlternateContent><mc:Choice Requires="wps"><w:drawing>'
               '<wp:anchor distT="0" distB="0" distL="114300" distR="114300" simplePos="0" relativeHeight="251659264" behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1">'
               '<wp:simplePos x="0" y="0"/><wp:positionH relativeFrom="column"><wp:posOffset>3200400</wp:posOffset></wp:positionH>'
               '<wp:positionV relativeFrom="paragraph"><wp:posOffset>114300</wp:posOffset></wp:positionV>'
               '<wp:extent cx="2286000" cy="685800"/><wp:wrapSquare wrapText="bothSides"/><wp:docPr id="1" name="Textfeld 1"/>'
               '<a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"><wps:wsp>'
               '<wps:cNvSpPr txBox="1"/><wps:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="2286000" cy="685800"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></wps:spPr>'
               f'<wps:txbx><w:txbxContent>{p(box_text)}</w:txbxContent></wps:txbx><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic>'
               '</wp:anchor></w:drawing></mc:Choice><mc:Fallback><w:pict>'
               '<v:shape id="Textfeld1" type="#_x0000_t202" style="position:absolute;width:180pt;height:54pt">'
               f'<v:textbox><w:txbxContent>{p(box_text)}</w:txbxContent></v:textbox></v:shape>'
               '</w:pict></mc:Fallback></mc:AlternateContent></w:r></w:p>')

    body = (p('Quartalsbericht', 'Heading1') + p('Dieser Bericht stammt aus einem anderen Schreibprogramm.') + table + textbox
            + p('Schlussabsatz nach dem Textfeld.')
            + '<w:sectPr><w:headerReference w:type="default" r:id="rId2"/><w:headerReference w:type="first" r:id="rId3"/>'
              '<w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1417" w:right="1417" w:bottom="1134" w:left="1417" w:header="708" w:footer="708" w:gutter="0"/><w:titlePg/></w:sectPr>')
    styles = (XML + f'<w:styles {w}><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr></w:rPrDefault></w:docDefaults>'
              '<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>'
              '<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:pPr><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="32"/></w:rPr></w:style>'
              '<w:style w:type="paragraph" w:styleId="Header"><w:name w:val="header"/><w:basedOn w:val="Normal"/></w:style>'
              '<w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/></w:style></w:styles>')
    # Kopfzeile: zwei Zeilen (Firma, Anschrift); dazu eine eigene Kopfzeile fuer die erste Seite
    header = XML + f'<w:hdr {ns}>' + p('Musterfirma GmbH', 'Header') + p('Beispielweg 1, 12345 Musterstadt', 'Header') + '</w:hdr>'
    header_first = XML + f'<w:hdr {ns}>' + p('Musterfirma GmbH - Deckblatt', 'Header') + '</w:hdr>'
    wml = 'application/vnd.openxmlformats-officedocument.wordprocessingml.'
    pack('fremd-text.docx', [
        ('[Content_Types].xml', types([('rels', 'application/vnd.openxmlformats-package.relationships+xml'), ('xml', 'application/xml')],
                                      [('/word/document.xml', wml + 'document.main+xml'), ('/word/styles.xml', wml + 'styles+xml'),
                                       ('/word/header1.xml', wml + 'header+xml'), ('/word/header2.xml', wml + 'header+xml')])),
        ('_rels/.rels', rels(('rId1', 'officeDocument', 'word/document.xml'))),
        ('word/document.xml', XML + f'<w:document {ns}><w:body>{body}</w:body></w:document>'),
        ('word/_rels/document.xml.rels', rels(('rId1', 'styles', 'styles.xml'), ('rId2', 'header', 'header1.xml'), ('rId3', 'header', 'header2.xml'))),
        ('word/styles.xml', styles), ('word/header1.xml', header), ('word/header2.xml', header_first)])


# ---------------------------------------------------------------- XLSX
def xlsx():
    s = 'xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"'
    c = 'http://schemas.openxmlformats.org/drawingml/2006/chart'
    a = 'http://schemas.openxmlformats.org/drawingml/2006/main'
    strings = ['Messwerte Labor', 'Probe', 'Gewicht', 'Laenge', 'A', 'B', 'C']
    sst = XML + f'<sst {s} count="{len(strings)}" uniqueCount="{len(strings)}">' + ''.join(f'<si><t>{t}</t></si>' for t in strings) + '</sst>'

    def cs(ref, i):
        return f'<c r="{ref}" t="s"><v>{i}</v></c>'

    def cn(ref, v, style=1):
        return f'<c r="{ref}" s="{style}"><v>{v}</v></c>'

    rows = (f'<row r="1">{cs("A1", 0)}</row>'
            f'<row r="2">{cs("A2", 1)}{cs("B2", 2)}{cs("C2", 3)}</row>'
            f'<row r="3">{cs("A3", 4)}{cn("B3", 1.2345)}{cn("C3", 10.5)}</row>'
            f'<row r="4">{cs("A4", 5)}{cn("B4", 2.5)}{cn("C4", 11.25)}</row>'
            f'<row r="5">{cs("A5", 6)}{cn("B5", 3.125)}{cn("C5", 12)}</row>')
    sheet = (XML + f'<worksheet {s} xmlns:r="{REL}"><dimension ref="A1:C5"/><sheetData>{rows}</sheetData>'
             '<mergeCells count="1"><mergeCell ref="A1:C1"/></mergeCells>'
             '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/><drawing r:id="rId1"/></worksheet>')
    # Zahlenformat mit drei Nachkommastellen und Einheit - das kennt Trendos Tabellenmodell nicht
    styles = (XML + f'<styleSheet {s}><numFmts count="1"><numFmt numFmtId="164" formatCode="0.000&quot; kg&quot;"/></numFmts>'
              '<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>'
              '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>'
              '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
              '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
              '<cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>'
              '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>')
    drawing = (XML + f'<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="{a}">'
               '<xdr:twoCellAnchor><xdr:from><xdr:col>4</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>1</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>'
               '<xdr:to><xdr:col>11</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>16</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>'
               '<xdr:graphicFrame macro=""><xdr:nvGraphicFramePr><xdr:cNvPr id="2" name="Diagramm 1"/><xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr>'
               '<xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm>'
               f'<a:graphic><a:graphicData uri="{c}"><c:chart xmlns:c="{c}" xmlns:r="{REL}" r:id="rId1"/></a:graphicData></a:graphic>'
               '</xdr:graphicFrame><xdr:clientData/></xdr:twoCellAnchor></xdr:wsDr>')

    def ser(idx, col, color):
        return (f'<c:ser><c:idx val="{idx}"/><c:order val="{idx}"/><c:tx><c:strRef><c:f>Tabelle1!${col}$2</c:f></c:strRef></c:tx>'
                f'<c:spPr><a:solidFill><a:srgbClr val="{color}"/></a:solidFill></c:spPr>'
                f'<c:cat><c:strRef><c:f>Tabelle1!$A$3:$A$5</c:f></c:strRef></c:cat><c:val><c:numRef><c:f>Tabelle1!${col}$3:${col}$5</c:f></c:numRef></c:val></c:ser>')

    def axis_title(text):
        return f'<c:title><c:tx><c:rich><a:bodyPr/><a:p><a:r><a:t>{text}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val="0"/></c:title>'

    # Diagramm mit zwei Reihen in eigenen Farben, Achsentiteln und Legende
    chart = (XML + f'<c:chartSpace xmlns:c="{c}" xmlns:a="{a}" xmlns:r="{REL}"><c:chart>'
             '<c:title><c:tx><c:rich><a:bodyPr/><a:p><a:r><a:t>Messwerte je Probe</a:t></a:r></a:p></c:rich></c:tx><c:overlay val="0"/></c:title><c:autoTitleDeleted val="0"/>'
             '<c:plotArea><c:layout/><c:barChart><c:barDir val="col"/><c:grouping val="clustered"/><c:varyColors val="0"/>'
             + ser(0, 'B', 'C0504D') + ser(1, 'C', '4F81BD') +
             '<c:axId val="100"/><c:axId val="200"/></c:barChart>'
             f'<c:catAx><c:axId val="100"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/><c:axPos val="b"/>{axis_title("Probe")}<c:crossAx val="200"/></c:catAx>'
             f'<c:valAx><c:axId val="200"/><c:scaling><c:orientation val="minMax"/></c:scaling><c:delete val="0"/><c:axPos val="l"/><c:majorGridlines/>{axis_title("Messwert")}<c:crossAx val="100"/></c:valAx>'
             '</c:plotArea><c:legend><c:legendPos val="r"/><c:overlay val="0"/></c:legend><c:plotVisOnly val="1"/></c:chart></c:chartSpace>')
    sml = 'application/vnd.openxmlformats-officedocument.spreadsheetml.'
    pack('fremd-tabelle.xlsx', [
        ('[Content_Types].xml', types([('rels', 'application/vnd.openxmlformats-package.relationships+xml'), ('xml', 'application/xml')],
                                      [('/xl/workbook.xml', sml + 'sheet.main+xml'), ('/xl/worksheets/sheet1.xml', sml + 'worksheet+xml'), ('/xl/styles.xml', sml + 'styles+xml'),
                                       ('/xl/sharedStrings.xml', sml + 'sharedStrings+xml'), ('/xl/drawings/drawing1.xml', 'application/vnd.openxmlformats-officedocument.drawing+xml'),
                                       ('/xl/charts/chart1.xml', 'application/vnd.openxmlformats-officedocument.drawingml.chart+xml')])),
        ('_rels/.rels', rels(('rId1', 'officeDocument', 'xl/workbook.xml'))),
        ('xl/workbook.xml', XML + f'<workbook {s} xmlns:r="{REL}"><sheets><sheet name="Tabelle1" sheetId="1" r:id="rId1"/></sheets></workbook>'),
        ('xl/_rels/workbook.xml.rels', rels(('rId1', 'worksheet', 'worksheets/sheet1.xml'), ('rId2', 'styles', 'styles.xml'), ('rId3', 'sharedStrings', 'sharedStrings.xml'))),
        ('xl/worksheets/sheet1.xml', sheet), ('xl/worksheets/_rels/sheet1.xml.rels', rels(('rId1', 'drawing', '../drawings/drawing1.xml'))),
        ('xl/styles.xml', styles), ('xl/sharedStrings.xml', sst),
        ('xl/drawings/drawing1.xml', drawing), ('xl/drawings/_rels/drawing1.xml.rels', rels(('rId1', 'chart', '../charts/chart1.xml'))),
        ('xl/charts/chart1.xml', chart)])


# ---------------------------------------------------------------- PPTX
def pptx():
    a = 'http://schemas.openxmlformats.org/drawingml/2006/main'
    ns = f'xmlns:a="{a}" xmlns:r="{REL}" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"'
    grp = ('<p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>'
           '<p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>')
    clr = '<p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>'

    def ph(sid, name, kind, x, y, cx, cy, text=''):
        body = f'<a:p><a:r><a:rPr lang="de-DE"/><a:t>{text}</a:t></a:r></a:p>' if text else '<a:p><a:endParaRPr lang="de-DE"/></a:p>'
        return (f'<p:sp><p:nvSpPr><p:cNvPr id="{sid}" name="{name}"/><p:cNvSpPr><a:spLocks noGrp="1"/></p:cNvSpPr><p:nvPr><p:ph type="{kind}"/></p:nvPr></p:nvSpPr>'
                f'<p:spPr><a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm></p:spPr><p:txBody><a:bodyPr/><a:lstStyle/>{body}</p:txBody></p:sp>')

    # Ein Textfeld mit zwei Textformaten: normal 18 pt und fett, rot, 28 pt
    box = ('<p:sp><p:nvSpPr><p:cNvPr id="3" name="Textfeld 2"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>'
           '<p:spPr><a:xfrm><a:off x="838200" y="1825625"/><a:ext cx="10515600" cy="1325563"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>'
           '<p:txBody><a:bodyPr wrap="square"/><a:lstStyle/><a:p>'
           '<a:r><a:rPr lang="de-DE" sz="1800"/><a:t>Der Umsatz ist um </a:t></a:r>'
           '<a:r><a:rPr lang="de-DE" sz="2800" b="1"><a:solidFill><a:srgbClr val="C00000"/></a:solidFill></a:rPr><a:t>12 Prozent</a:t></a:r>'
           '<a:r><a:rPr lang="de-DE" sz="1800"/><a:t> gestiegen.</a:t></a:r></a:p></p:txBody></p:sp>')
    # Eine Animation: Hervorhebung "Drehen" (presetClass emph) auf dem Textfeld, beim Klick
    tgt = '<p:tgtEl><p:spTgt spid="3"/></p:tgtEl>'
    timing = ('<p:timing><p:tnLst><p:par><p:cTn id="1" dur="indefinite" restart="never" nodeType="tmRoot"><p:childTnLst>'
              '<p:seq concurrent="1" nextAc="seek"><p:cTn id="2" dur="indefinite" nodeType="mainSeq"><p:childTnLst>'
              '<p:par><p:cTn id="3" fill="hold"><p:stCondLst><p:cond delay="indefinite"/></p:stCondLst><p:childTnLst>'
              '<p:par><p:cTn id="4" fill="hold"><p:stCondLst><p:cond delay="0"/></p:stCondLst><p:childTnLst>'
              '<p:par><p:cTn id="5" presetID="8" presetClass="emph" presetSubtype="0" fill="hold" nodeType="clickEffect"><p:stCondLst><p:cond delay="0"/></p:stCondLst><p:childTnLst>'
              f'<p:animRot by="21600000"><p:cBhvr><p:cTn id="6" dur="2000" fill="hold"/>{tgt}<p:attrNameLst><p:attrName>r</p:attrName></p:attrNameLst></p:cBhvr></p:animRot>'
              '</p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn>'
              '<p:prevCondLst><p:cond evt="onPrev" delay="0"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst>'
              '<p:nextCondLst><p:cond evt="onNext" delay="0"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst></p:seq>'
              '</p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>')
    slide = (XML + f'<p:sld {ns}><p:cSld><p:spTree>{grp}{ph(2, "Titel 1", "title", 838200, 365125, 10515600, 1325563, "Jahresrueckblick")}{box}</p:spTree></p:cSld>'
             f'<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>{timing}</p:sld>')
    master = (XML + f'<p:sldMaster {ns}><p:cSld><p:bg><p:bgRef idx="1001"><a:schemeClr val="bg1"/></p:bgRef></p:bg><p:spTree>{grp}'
              f'{ph(2, "Titelplatzhalter 1", "title", 838200, 365125, 10515600, 1325563)}{ph(3, "Textplatzhalter 2", "body", 838200, 1825625, 10515600, 4351338)}</p:spTree></p:cSld>'
              f'{clr}<p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>'
              '<p:txStyles><p:titleStyle><a:lvl1pPr><a:defRPr sz="4400"/></a:lvl1pPr></p:titleStyle><p:bodyStyle><a:lvl1pPr><a:defRPr sz="2800"/></a:lvl1pPr></p:bodyStyle><p:otherStyle><a:lvl1pPr><a:defRPr sz="1800"/></a:lvl1pPr></p:otherStyle></p:txStyles></p:sldMaster>')
    layout = (XML + f'<p:sldLayout {ns} type="titleOnly" preserve="1"><p:cSld name="Nur Titel"><p:spTree>{grp}{ph(2, "Titel 1", "title", 838200, 365125, 10515600, 1325563)}</p:spTree></p:cSld>'
              '<p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>')

    def color(name, val):
        return f'<a:{name}><a:srgbClr val="{val}"/></a:{name}>'

    fill3 = '<a:solidFill><a:schemeClr val="phClr"/></a:solidFill>' * 3
    line3 = '<a:ln w="9525"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln>' * 3
    theme = (XML + f'<a:theme xmlns:a="{a}" name="Office Theme"><a:themeElements><a:clrScheme name="Office">'
             '<a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>'
             + color('dk2', '44546A') + color('lt2', 'E7E6E6') + color('accent1', '4472C4') + color('accent2', 'ED7D31') + color('accent3', 'A5A5A5')
             + color('accent4', 'FFC000') + color('accent5', '5B9BD5') + color('accent6', '70AD47') + color('hlink', '0563C1') + color('folHlink', '954F72')
             + '</a:clrScheme><a:fontScheme name="Office"><a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>'
               '<a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme>'
             f'<a:fmtScheme name="Office"><a:fillStyleLst>{fill3}</a:fillStyleLst><a:lnStyleLst>{line3}</a:lnStyleLst>'
             '<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>'
             f'<a:bgFillStyleLst>{fill3}</a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>')
    pres = (XML + f'<p:presentation {ns}><p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>'
            '<p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst><p:sldSz cx="12192000" cy="6858000"/><p:notesSz cx="6858000" cy="9144000"/></p:presentation>')
    pml = 'application/vnd.openxmlformats-officedocument.presentationml.'
    pack('fremd-praesentation.pptx', [
        ('[Content_Types].xml', types([('rels', 'application/vnd.openxmlformats-package.relationships+xml'), ('xml', 'application/xml')],
                                      [('/ppt/presentation.xml', pml + 'presentation.main+xml'), ('/ppt/slideMasters/slideMaster1.xml', pml + 'slideMaster+xml'),
                                       ('/ppt/slideLayouts/slideLayout1.xml', pml + 'slideLayout+xml'), ('/ppt/slides/slide1.xml', pml + 'slide+xml'),
                                       ('/ppt/theme/theme1.xml', 'application/vnd.openxmlformats-officedocument.theme+xml')])),
        ('_rels/.rels', rels(('rId1', 'officeDocument', 'ppt/presentation.xml'))),
        ('ppt/presentation.xml', pres),
        ('ppt/_rels/presentation.xml.rels', rels(('rId1', 'slideMaster', 'slideMasters/slideMaster1.xml'), ('rId2', 'slide', 'slides/slide1.xml'), ('rId3', 'theme', 'theme/theme1.xml'))),
        ('ppt/slideMasters/slideMaster1.xml', master),
        ('ppt/slideMasters/_rels/slideMaster1.xml.rels', rels(('rId1', 'slideLayout', '../slideLayouts/slideLayout1.xml'), ('rId2', 'theme', '../theme/theme1.xml'))),
        ('ppt/slideLayouts/slideLayout1.xml', layout), ('ppt/slideLayouts/_rels/slideLayout1.xml.rels', rels(('rId1', 'slideMaster', '../slideMasters/slideMaster1.xml'))),
        ('ppt/slides/slide1.xml', slide), ('ppt/slides/_rels/slide1.xml.rels', rels(('rId1', 'slideLayout', '../slideLayouts/slideLayout1.xml'))),
        ('ppt/theme/theme1.xml', theme)])


# ---------------------------------------------------------------- ODF (LibreOffice-Formate)
ODF_NS = ('xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"'
          ' xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"'
          ' xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0"'
          ' xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0" xmlns:presentation="urn:oasis:names:tc:opendocument:xmlns:presentation:1.0"'
          ' xmlns:anim="urn:oasis:names:tc:opendocument:xmlns:animation:1.0" xmlns:smil="urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0"'
          ' xmlns:xlink="http://www.w3.org/1999/xlink" office:version="1.3"')
ODF_XML = '<?xml version="1.0" encoding="UTF-8"?>'


def odf(name, mime, content, styles):
    manifest = (ODF_XML + '<manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.3">'
                f'<manifest:file-entry manifest:full-path="/" manifest:media-type="{mime}"/>'
                '<manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml"/>'
                '<manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml"/></manifest:manifest>')
    pack(name, [('mimetype', mime), ('content.xml', ODF_XML + content), ('styles.xml', ODF_XML + styles), ('META-INF/manifest.xml', manifest)], stored_first='mimetype')


def odt():
    page = ('<style:page-layout style:name="pm1"><style:page-layout-properties fo:page-width="21cm" fo:page-height="29.7cm" fo:margin-top="2cm" fo:margin-bottom="2cm" fo:margin-left="2cm" fo:margin-right="2cm"/></style:page-layout>')
    styles = (f'<office:document-styles {ODF_NS}><office:styles><style:style style:name="Standard" style:family="paragraph"/></office:styles>'
              f'<office:automatic-styles>{page}</office:automatic-styles>'
              '<office:master-styles><style:master-page style:name="Standard" style:page-layout-name="pm1"><style:header><text:p>Musterverein e. V.</text:p></style:header></style:master-page></office:master-styles></office:document-styles>')

    def cell(text, extra=''):
        return f'<table:table-cell office:value-type="string"{extra}><text:p>{text}</text:p></table:table-cell>'

    table = ('<table:table table:name="Tabelle1"><table:table-column table:number-columns-repeated="2"/>'
             f'<table:table-row>{cell("Kassenbericht", " table:number-columns-spanned=\"2\"")}<table:covered-table-cell/></table:table-row>'
             f'<table:table-row>{cell("Einnahmen")}{cell("480")}</table:table-row></table:table>')
    box = ('<text:p><draw:frame draw:name="Rahmen1" text:anchor-type="paragraph" svg:width="6cm" svg:height="2cm">'
           '<draw:text-box><text:p>Notiz im Textrahmen</text:p></draw:text-box></draw:frame>Absatz mit Textrahmen.</text:p>')
    content = (f'<office:document-content {ODF_NS}><office:automatic-styles/><office:body><office:text>'
               f'<text:h text:outline-level="1">Kassenbericht</text:h><text:p>Stand: <text:date>01.01.2026</text:date></text:p>{table}{box}'
               '</office:text></office:body></office:document-content>')
    odf('fremd-text.odt', 'application/vnd.oasis.opendocument.text', content, styles)


def ods():
    styles = f'<office:document-styles {ODF_NS}><office:styles/></office:document-styles>'
    auto = ('<office:automatic-styles><style:style style:name="ce1" style:family="table-cell"><style:text-properties fo:font-weight="bold" fo:color="#C00000"/>'
            '<style:table-cell-properties fo:background-color="#FFFF99"/></style:style></office:automatic-styles>')
    rows = ('<table:table-row><table:table-cell office:value-type="string" table:style-name="ce1" table:number-columns-spanned="2"><text:p>Fahrtenbuch</text:p></table:table-cell><table:covered-table-cell/></table:table-row>'
            '<table:table-row><table:table-cell office:value-type="string"><text:p>Strecke</text:p></table:table-cell>'
            '<table:table-cell office:value-type="float" office:value="42.5"><office:annotation><text:p>geschaetzt</text:p></office:annotation><text:p>42,5</text:p></table:table-cell></table:table-row>')
    content = (f'<office:document-content {ODF_NS}>{auto}<office:body><office:spreadsheet><table:table table:name="Fahrten"><table:table-column table:number-columns-repeated="2"/>{rows}</table:table>'
               '</office:spreadsheet></office:body></office:document-content>')
    odf('fremd-tabelle.ods', 'application/vnd.oasis.opendocument.spreadsheet', content, styles)


def odp():
    styles = (f'<office:document-styles {ODF_NS}><office:styles/><office:master-styles><style:master-page style:name="Default"/></office:master-styles></office:document-styles>')
    auto = ('<office:automatic-styles><style:style style:name="T1" style:family="text"><style:text-properties fo:font-size="28pt" fo:font-weight="bold" fo:color="#C00000"/></style:style></office:automatic-styles>')
    page = ('<draw:page draw:name="Folie1" draw:master-page-name="Default">'
            '<draw:frame presentation:class="title" svg:x="2cm" svg:y="1cm" svg:width="24cm" svg:height="3cm"><draw:text-box><text:p>Vereinsfest</text:p></draw:text-box></draw:frame>'
            '<draw:frame draw:id="id1" svg:x="2cm" svg:y="5cm" svg:width="24cm" svg:height="3cm"><draw:text-box><text:p>Beginn um <text:span text:style-name="T1">15 Uhr</text:span></text:p></draw:text-box></draw:frame>'
            '<anim:par presentation:node-type="timing-root"><anim:seq presentation:node-type="main-sequence"><anim:par smil:begin="next"><anim:par smil:begin="0s">'
            '<anim:par smil:begin="0s" presentation:node-type="on-click" presentation:preset-class="entrance" presentation:preset-id="ooo-entrance-appear">'
            '<anim:set smil:begin="0s" smil:dur="0.001s" smil:fill="hold" smil:targetElement="id1" smil:attributeName="visibility" smil:to="visible"/></anim:par>'
            '</anim:par></anim:par></anim:seq></anim:par></draw:page>')
    content = f'<office:document-content {ODF_NS}>{auto}<office:body><office:presentation>{page}</office:presentation></office:body></office:document-content>'
    odf('fremd-praesentation.odp', 'application/vnd.oasis.opendocument.presentation', content, styles)


if __name__ == '__main__':
    docx(); xlsx(); pptx(); odt(); ods(); odp()
