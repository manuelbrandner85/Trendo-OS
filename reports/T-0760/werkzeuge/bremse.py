# T-0760 Nachweis: künstlich verlangsamter Code (NUR vorübergehend, wird nicht committet).
# T0760_BREMSE=<ms> rechnet am Anfang von CncCam.Generate und CadDxf.Parse so lange (CPU), T0760_SCHLAF=<ms> schläft so lange (Warten).
# Aufruf: python bremse.py an|aus
import os, sys
R = os.path.join(os.environ['TEMP'], 'trendo-auftrag-T-0760', 'src', 'Trendo.Core')
B = ('{ if (int.TryParse(Environment.GetEnvironmentVariable("T0760_BREMSE"), out var bms)) { var bsw = System.Diagnostics.Stopwatch.StartNew(); while (bsw.ElapsedMilliseconds < bms) { } } '
     'if (int.TryParse(Environment.GetEnvironmentVariable("T0760_SCHLAF"), out var sms)) Thread.Sleep(sms); } // T0760-BREMSE')
targets = [(os.path.join(R, 'W6', 'CncCam.cs'), ['    public static CamResult Generate(IReadOnlyList<CadContour> source, CamSettings s, CancellationToken ct = default)', '    {']),
           (os.path.join(R, 'Cad', 'CadDrawing.cs'), ['    public static ReadResult Parse(string text, CancellationToken ct = default)', '    {'])]
for p, parts in targets:
    s = open(p, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in s else '\n'
    anchor = nl.join(parts) + nl
    line = '        ' + B + nl
    if sys.argv[1] == 'an':
        assert s.count(anchor) == 1 and 'T0760-BREMSE' not in s, p
        s = s.replace(anchor, anchor + line)
    else:
        assert s.count(line) == 1, p
        s = s.replace(line, '')
    open(p, 'w', encoding='utf-8', newline='').write(s)
print('bremse', sys.argv[1])
