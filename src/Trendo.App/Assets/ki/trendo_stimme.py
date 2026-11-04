"""Trendo Musik M6 (freigegeben 04.11.2026): eigene Stimme lernen und Gesang darin singen lassen – über Applio (RVC, MIT), fester Stand.
Aufruf (Trendo übergibt vorn den Applio-Ordner):
  python trendo_stimme.py <applio> lernen <name> <ordner-mit-wav> <epochen>
  python trendo_stimme.py <applio> singen <name> <gesang.wav> <ausgabe.wav> <halbtöne>
Trendo gibt hier nur Mikrofon-Aufnahmen der eigenen Stimme hinein (Zustimmung in der Oberfläche); fremde Stimmen lernt Trendo nicht.
Ausgabe: letzte Zeile JSON {"ok": bool, "pth": ..., "index": ...} bzw. {"ok": bool, "out": ...}
"""
import glob
import json
import os
import subprocess
import sys


def core(repo, *args):
    env = dict(os.environ, PYTHONUTF8='1', HF_HUB_DISABLE_TELEMETRY='1')
    r = subprocess.run([sys.executable, os.path.join(repo, 'core.py'), *map(str, args)], cwd=repo, env=env)
    if r.returncode != 0:
        raise SystemExit(f'Applio-Schritt fehlgeschlagen: {args[0]} (Code {r.returncode})')


def model_files(repo, name):
    d = os.path.join(repo, 'logs', name)
    pth = sorted(glob.glob(os.path.join(d, f'{name}*.pth')) + glob.glob(os.path.join(repo, 'logs', f'{name}*.pth')), key=os.path.getmtime)
    idx = sorted(glob.glob(os.path.join(d, '*.index')), key=os.path.getmtime)
    pth = [p for p in pth if not os.path.basename(p).startswith(('G_', 'D_'))]
    return (pth[-1] if pth else None), (idx[-1] if idx else None)


def learn(repo, name, wavs, epochs):
    core(repo, 'prerequisites', '--no-exe')  # Grundmodelle (einmalig) laden
    core(repo, 'preprocess', '--model-name', name, '--dataset-path', wavs, '--sample-rate', 40000, '--cpu-cores', max(2, (os.cpu_count() or 4) - 2), '--cut-preprocess', 'Automatic')
    core(repo, 'extract', '--model-name', name, '--f0-method', 'rmvpe', '--gpu', 0, '--sample-rate', 40000, '--cpu-cores', max(2, (os.cpu_count() or 4) - 2), '--embedder-model', 'contentvec')
    core(repo, 'train', '--model-name', name, '--vocoder', 'HiFi-GAN', '--total-epoch', epochs, '--save-every-epoch', max(10, int(epochs) // 2), '--save-only-latest',
         '--sample-rate', 40000, '--batch-size', 6, '--gpu', 0, '--pretrained', '--index-algorithm', 'Auto')
    pth, idx = model_files(repo, name)
    print(json.dumps({'ok': pth is not None, 'pth': pth, 'index': idx}))


def sing(repo, name, src, out, pitch):
    pth, idx = model_files(repo, name)
    if pth is None:
        print(json.dumps({'ok': False, 'error': 'Noch keine gelernte Stimme.'})); return
    args = ['infer', '--input-path', src, '--output-path', out, '--pth-path', pth, '--pitch', pitch, '--f0-method', 'rmvpe', '--protect', 0.33, '--index-rate', 0.6, '--export-format', 'WAV']
    if idx: args += ['--index-path', idx]
    core(repo, *args)
    print(json.dumps({'ok': os.path.exists(out), 'out': out}))


if __name__ == '__main__':
    repo, cmd = sys.argv[1], sys.argv[2]
    if cmd == 'lernen':
        learn(repo, sys.argv[3], sys.argv[4], sys.argv[5])
    elif cmd == 'singen':
        sing(repo, sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6])
    else:
        raise SystemExit('Unbekannter Befehl: ' + cmd)
