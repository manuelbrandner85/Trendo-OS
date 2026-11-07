"""Trendo – Plugin-Host (Paket 01 S4-06, 07.11.2026) mit Pedalboard (Spotify, GPL-3.0), offline (kein Echtzeit-Ton).
Trendo startet dieses Skript nur in der abgeschotteten Laufumgebung und nur für freigegebene Plugins.
Aufruf:
  python -I -X utf8 -B trendo_plugin.py --site <site-packages> info   <plugin> <ergebnis.json>
  python -I -X utf8 -B trendo_plugin.py --site <site-packages> render <plugin> <eingang.wav> <ausgang.wav> <ergebnis.json>
         [--preset datei.vstpreset] [--param name=wert ...] [--nachlauf sekunden]
Ergebnis (JSON): {"ok", "name", "art", "parameter": [{"name","wert","anzeige"}], "spitze_ein_db", "spitze_aus_db", "sekunden", "veraenderung", "fehler"}
"""
import json
import site
import sys
import time


def main():
    a = sys.argv[1:]
    if '--site' in a:
        i = a.index('--site'); site.addsitedir(a[i + 1]); del a[i:i + 2]
    opts = {'--preset': None, '--nachlauf': '0'}; params = []
    rest = []
    i = 0
    while i < len(a):
        if a[i] in opts and i + 1 < len(a): opts[a[i]] = a[i + 1]; i += 2; continue
        if a[i] == '--param' and i + 1 < len(a): params.append(a[i + 1]); i += 2; continue
        rest.append(a[i]); i += 1
    cmd, plugin = rest[0], rest[1]
    result = rest[-1]
    if cmd == 'absturztest':  # Selbsttest: echte Zugriffsverletzung im Host-Prozess – so wie ein abstürzendes Plugin (Trendo muss das isoliert melden)
        import faulthandler
        faulthandler._read_null()  # liest Adresse 0 in C – ctypes würde den Fehler abfangen, das hier nicht
    res = {'ok': False}
    t0 = time.time()
    try:
        if plugin.lower().endswith('.clap'):
            raise RuntimeError('CLAP lädt dieser Host noch nicht (Pedalboard kennt nur VST3).')
        import numpy as np
        import soundfile as sf
        from pedalboard import load_plugin
        p = load_plugin(plugin)
        res['name'] = p.name
        res['art'] = 'Effekt' if p.is_effect else 'Instrument' if p.is_instrument else 'unbekannt'
        if opts['--preset']:
            p.load_preset(opts['--preset'])
        for kv in params:
            k, v = kv.split('=', 1)
            if k not in p.parameters:
                raise KeyError(f'Parameter „{k}“ gibt es nicht')
            try:
                val = float(v.replace(',', '.'))
            except ValueError:
                val = {'an': True, 'ein': True, 'aus': False, 'true': True, 'false': False}.get(v.lower(), v)
            setattr(p, k, val)
        res['parameter'] = [{'name': k, 'wert': str(getattr(p, k)), 'anzeige': str(getattr(pp, 'string_value', ''))} for k, pp in list(p.parameters.items())[:300]]
        if cmd == 'render':
            inp, out = rest[2], rest[3]
            x, sr = sf.read(inp, dtype='float32', always_2d=True)
            tail = int(float(opts['--nachlauf']) * sr)
            if tail > 0:
                x = np.concatenate([x, np.zeros((tail, x.shape[1]), dtype='float32')])
            y = p.process(x.T.copy(), sr, reset=True).T
            sf.write(out, y, sr, subtype='FLOAT')
            def db(v):
                m = float(np.max(np.abs(v))) if v.size else 0.0
                return round(20 * np.log10(m), 2) if m > 0 else -200.0
            n = min(len(x), len(y)); ch = min(x.shape[1], y.shape[1])
            diff = float(np.sqrt(np.mean((y[:n, :ch] - x[:n, :ch]) ** 2)) / (np.sqrt(np.mean(x[:n, :ch] ** 2)) + 1e-12))
            res.update({'spitze_ein_db': db(x), 'spitze_aus_db': db(y), 'veraenderung': round(diff, 4), 'samples': int(len(y)), 'rate': int(sr)})
        res['ok'] = True
    except Exception as e:  # Fehler des Plugins/Hosts ehrlich zurückgeben (Absturz erkennt Trendo am Prozess-Ende)
        res['fehler'] = f'{type(e).__name__}: {e}'
    res['sekunden'] = round(time.time() - t0, 3)
    with open(result, 'w', encoding='utf-8') as f:
        json.dump(res, f, ensure_ascii=False)
    sys.exit(0 if res['ok'] else 2)


if __name__ == '__main__':
    main()
