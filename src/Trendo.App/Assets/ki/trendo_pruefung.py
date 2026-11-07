"""Trendo – Artefakt-Prüfung für KI-Videos (KI-Musikvideo G, freigegeben 04.11.2026). Lokal mit MediaPipe (Apache-2.0) und OpenCV.
Aufruf: python trendo_pruefung.py [ordner] <video> <ausgabe.json> --modelle <ordner> --personen N [--singt] [--gesang teil.wav] [--referenz bild.png]
Prüft jedes 4. Bild (höchstens 72; beim Singen jedes Bild, höchstens 160):
  G1 Körper: mehr Personen/Gesichter als im Drehbuch, mehr Hände als zwei je Person, Gesicht verdoppelt oder verschwindet
  G3 Gleiche Person: Gesichtsform (Abstände der Gesichtspunkte) im Vergleich zum Figurenbild
  G4 Ruhiges Bild: Bildsprünge und Flackern zwischen benachbarten Bildern
  G5 Lippen: Mund offen beim Singen, zu in Pausen; bei durchgehendem Gesang deutliche Mundbewegung
Ehrlich: Das sind automatische Schätzungen. Grobe Fehler (drei Arme, zwei Köpfe, andere Person) fallen auf; feine Fehler in einem
einzelnen Bild können durchrutschen. Ergebnis: {"ok": bool, "issues": [{"code", "text", "at"}], "stats": {...}}
"""
import json
import os
import sys

import numpy as np
import cv2
import mediapipe as mp
from mediapipe.tasks.python import vision
from mediapipe.tasks.python.core.base_options import BaseOptions


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        return sys.argv[i + 1] if i + 1 < len(sys.argv) else default
    return default


def frames_of(path, step=4, maxn=72):
    if path.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')):  # Startbild: als drei gleiche Bilder prüfen (Körper, Gesichter; kein Bildsprung)
        im = cv2.imread(path)
        return ([(0.0, im), (0.04, im), (0.08, im)] if im is not None else []), 24.0
    cap = cv2.VideoCapture(path)
    fps = cap.get(cv2.CAP_PROP_FPS) or 24.0
    out, i = [], 0
    while len(out) < maxn:
        ok, f = cap.read()
        if not ok:
            break
        if i % step == 0:
            out.append((i / fps, f))
        i += 1
    cap.release()
    return out, fps


def face_shape(lm):
    """Formvektor aus Gesichtspunkten (unabhängig von Größe und Lage): Augenabstand, Nase, Mund, Kinn im Verhältnis zur Gesichtsbreite."""
    p = np.array([[l.x, l.y] for l in lm])
    w = np.linalg.norm(p[234] - p[454]) + 1e-6
    pairs = [(33, 263), (1, 152), (61, 291), (10, 152), (168, 1), (13, 14), (33, 133), (362, 263), (70, 300), (4, 152)]
    return np.array([np.linalg.norm(p[a] - p[b]) / w for a, b in pairs])


def rms_series(wav, times):
    import wave
    with wave.open(wav, 'rb') as w:
        sr = w.getframerate(); n = w.getnframes(); ch = w.getnchannels()
        y = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float32)
    if ch > 1:
        y = y.reshape(-1, ch).mean(1)
    win = int(0.08 * sr)
    out = []
    for t in times:
        a = int(max(0, t * sr - win / 2)); b = int(min(len(y), a + win))
        out.append(float(np.sqrt(np.mean(y[a:b] ** 2))) if b > a else 0.0)
    return np.array(out)


def main(video, out):
    models = arg('--modelle', os.path.dirname(os.path.abspath(__file__)))
    want = int(arg('--personen', '1'))
    singing = '--singt' in sys.argv
    vocal = arg('--gesang')
    ref = arg('--referenz')
    frames, fps = frames_of(video, step=1 if singing else 4, maxn=160 if singing else 72)  # Lippen brauchen jedes Bild
    issues = []
    if len(frames) < 3:
        json.dump({'ok': False, 'issues': [{'code': 'leer', 'text': 'Das Video ist leer oder nicht lesbar.', 'at': 0}], 'stats': {}}, open(out, 'w', encoding='utf-8'), ensure_ascii=False)
        return
    mode = vision.RunningMode.IMAGE
    pose = vision.PoseLandmarker.create_from_options(vision.PoseLandmarkerOptions(base_options=BaseOptions(model_asset_path=os.path.join(models, 'pose_landmarker_full.task')), running_mode=mode, num_poses=5, min_pose_detection_confidence=0.5))
    hands = vision.HandLandmarker.create_from_options(vision.HandLandmarkerOptions(base_options=BaseOptions(model_asset_path=os.path.join(models, 'hand_landmarker.task')), running_mode=mode, num_hands=8, min_hand_detection_confidence=0.55))
    faces = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=BaseOptions(model_asset_path=os.path.join(models, 'face_landmarker.task')), running_mode=mode, num_faces=5, output_face_blendshapes=True, min_face_detection_confidence=0.5))
    n_pose, n_hand, n_face, jaw, shapes, diffs, prev = [], [], [], [], [], [], None
    for t, f in frames:
        rgb = cv2.cvtColor(f, cv2.COLOR_BGR2RGB)
        im = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        p = pose.detect(im); h = hands.detect(im); fc = faces.detect(im)
        n_pose.append(len(p.pose_landmarks)); n_hand.append(len(h.hand_landmarks)); n_face.append(len(fc.face_landmarks))
        if fc.face_blendshapes:
            jaw.append(next((b.score for b in fc.face_blendshapes[0] if b.category_name == 'jawOpen'), 0.0))
        else:
            jaw.append(np.nan)
        if fc.face_landmarks:
            shapes.append(face_shape(fc.face_landmarks[0]))
        g = cv2.cvtColor(cv2.resize(f, (160, 90)), cv2.COLOR_BGR2GRAY).astype(np.float32)
        if prev is not None:
            diffs.append(float(np.mean(np.abs(g - prev))))
        prev = g
    times = [t for t, _ in frames]
    n_pose, n_hand, n_face = np.array(n_pose), np.array(n_hand), np.array(n_face)

    def first(mask):
        idx = np.nonzero(mask)[0]
        return round(times[idx[0]], 2) if len(idx) else 0

    # G1 Körper
    if want >= 0 and (n_pose > max(want, 0)).sum() >= 2:
        issues.append({'code': 'personen', 'text': f'Mehr Personen im Bild als im Drehbuch ({int(n_pose.max())} statt {want}).', 'at': first(n_pose > want)})
    if want >= 0 and (n_face > max(want, 0)).sum() >= 2:
        issues.append({'code': 'gesichter', 'text': f'Zusätzliches oder doppeltes Gesicht erkannt ({int(n_face.max())} statt {want}).', 'at': first(n_face > want)})
    limit = 2 * max(1, max(want, int(np.median(n_pose)) if len(n_pose) else 1))
    if (n_hand > limit).sum() >= 2:
        issues.append({'code': 'haende', 'text': f'Zu viele Hände oder Arme ({int(n_hand.max())} bei {max(1, want)} Person(en)) – typische KI-Fehlbildung.', 'at': first(n_hand > limit)})
    if want > 0 and len(n_face) > 6:
        changes = int(np.sum(np.abs(np.diff(np.minimum(n_face, want))) > 0))
        if changes > len(n_face) * 0.3:
            issues.append({'code': 'gesicht-flackert', 'text': 'Das Gesicht verschwindet und erscheint ständig neu.', 'at': 0})
    # G3 gleiche Person
    if ref and os.path.exists(ref) and shapes:
        r = faces.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(cv2.imread(ref), cv2.COLOR_BGR2RGB)))
        if r.face_landmarks:
            rs = face_shape(r.face_landmarks[0])
            dist = float(np.median([np.linalg.norm(s - rs) / (np.linalg.norm(rs) + 1e-6) for s in shapes]))
            if dist > 0.16:
                issues.append({'code': 'person', 'text': f'Die Person sieht anders aus als auf dem Figurenbild (Abweichung {dist:.2f}).', 'at': 0})
    # G4 ruhiges Bild
    if len(diffs) > 4:
        d = np.array(diffs); med = float(np.median(d)) + 1e-6
        jumps = np.nonzero((d > 3.5 * med) & (d > 22))[0]
        if len(jumps) > 0:
            issues.append({'code': 'bildsprung', 'text': f'Bildsprung oder Flackern ({len(jumps)}×).', 'at': round(times[int(jumps[0]) + 1], 2)})
    # G5 Lippen – gemessen 04.11.2026: bei durchgehendem Gesang (keine Pausen) sagt der Vergleich mit der Lautstärke nichts aus.
    # Darum: mit Pausen muss der Mund beim Singen weiter offen sein als in den Pausen; ohne Pausen muss er sich deutlich bewegen.
    lip = None
    if singing and vocal and os.path.exists(vocal):
        j = np.array(jaw, dtype=float); ok = np.isfinite(j)
        if ok.sum() >= 8:
            r = rms_series(vocal, [t for t, k in zip(times, ok) if k]); jj = j[ok]
            rn = r / (r.max() + 1e-9); pause = rn < 0.25
            if pause.mean() >= 0.15 and (~pause).sum() >= 4:
                lip = float(jj[~pause].mean() - jj[pause].mean())
                if lip < 0.05:
                    issues.append({'code': 'lippen', 'text': f'Der Mund folgt dem Gesang nicht (offen beim Singen kaum anders als in den Pausen: {lip:+.2f}).', 'at': 0})
            else:
                lip = float(np.std(jj))
                if lip < 0.05 or jj.mean() < 0.08:
                    issues.append({'code': 'lippen', 'text': 'Beim Singen bewegt sich der Mund kaum.', 'at': 0})
        else:
            issues.append({'code': 'lippen', 'text': 'Beim Singen ist das Gesicht zu selten zu sehen.', 'at': 0})
    stats = {'frames': len(frames), 'fps': fps, 'personen_max': int(n_pose.max()), 'haende_max': int(n_hand.max()), 'gesichter_max': int(n_face.max()),
             'bildsprung_median': round(float(np.median(diffs)) if diffs else 0, 2), 'lippen': None if lip is None else round(lip, 3)}
    json.dump({'ok': len(issues) == 0, 'issues': issues, 'stats': stats}, open(out, 'w', encoding='utf-8'), ensure_ascii=False)


def mouth(video, out):
    """Lippensynchron (Paket 01 S3-09): Mundöffnung (Blendshape jawOpen, 0 … 1) für jedes Bild – fehlt das Gesicht, steht null."""
    models = arg('--modelle', os.path.dirname(os.path.abspath(__file__)))
    frames, fps = frames_of(video, step=1, maxn=25 * 120)
    faces = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=BaseOptions(model_asset_path=os.path.join(models, 'face_landmarker.task')), running_mode=vision.RunningMode.IMAGE, num_faces=1, output_face_blendshapes=True, min_face_detection_confidence=0.5))
    vals = []
    for _, f in frames:
        r = faces.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=cv2.cvtColor(f, cv2.COLOR_BGR2RGB)))
        vals.append(round(float(next((b.score for b in r.face_blendshapes[0] if b.category_name == 'jawOpen'), 0.0)), 4) if r.face_blendshapes else None)
    json.dump({'fps': fps, 'mund': vals, 'gesicht': sum(v is not None for v in vals)}, open(out, 'w', encoding='utf-8'))


if __name__ == '__main__':
    pos = [a for a in sys.argv[1:] if not a.startswith('--')]
    if '--mund' in sys.argv:
        skip = {sys.argv[sys.argv.index('--modelle') + 1]} if '--modelle' in sys.argv else set()
        p = [a for a in pos if a not in skip]
        mouth(p[-2], p[-1])
        sys.exit(0)
    # Werte hinter Schaltern sind keine Dateien
    skip = set()
    for k in ('--modelle', '--personen', '--gesang', '--referenz'):
        if k in sys.argv:
            i = sys.argv.index(k)
            if i + 1 < len(sys.argv):
                skip.add(sys.argv[i + 1])
    pos = [a for a in pos if a not in skip]
    main(pos[-2], pos[-1])
