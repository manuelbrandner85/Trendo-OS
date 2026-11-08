# Trendo – Objekte im Bild erkennen und zählen mit YOLOX (Megvii, Apache-2.0) als ONNX-Modell über onnxruntime (MIT), eigener Prozess.
# Aufruf: python trendo_objekte.py <repo (unbenutzt)> <modell.onnx> <bild> <ausgabe.png oder -> <schwelle 0.10-0.90>
# Ausgabe (stdout, eine Zeile JSON, nur ASCII): Bildgröße, Dauer, Objekte (Klasse englisch, Sicherheit, Rahmen in Bildpixeln), Zählung je Klasse.
import json
import sys
import time
from pathlib import Path

COCO = ["person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light", "fire hydrant", "stop sign",
        "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
        "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard",
        "surfboard", "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
        "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv",
        "laptop", "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase",
        "scissors", "teddy bear", "hair drier", "toothbrush"]
SIZE = 640


def nms(boxes, scores, thr):
    import numpy as np
    x1, y1, x2, y2 = boxes[:, 0], boxes[:, 1], boxes[:, 2], boxes[:, 3]
    areas = (x2 - x1) * (y2 - y1)
    order = scores.argsort()[::-1]
    keep = []
    while order.size > 0:
        i = order[0]
        keep.append(int(i))
        xx1 = np.maximum(x1[i], x1[order[1:]]); yy1 = np.maximum(y1[i], y1[order[1:]])
        xx2 = np.minimum(x2[i], x2[order[1:]]); yy2 = np.minimum(y2[i], y2[order[1:]])
        inter = np.maximum(0.0, xx2 - xx1) * np.maximum(0.0, yy2 - yy1)
        iou = inter / (areas[i] + areas[order[1:]] - inter + 1e-9)
        order = order[1:][iou <= thr]
    return keep


def main() -> int:
    if len(sys.argv) != 6:
        print("Aufruf: trendo_objekte.py <repo> <modell.onnx> <bild> <ausgabe.png|-> <schwelle>", file=sys.stderr)
        return 2
    model, src, out = Path(sys.argv[2]), Path(sys.argv[3]), sys.argv[4]
    try:
        thr = float(sys.argv[5])
    except ValueError:
        print("Schwelle als Zahl erwartet.", file=sys.stderr)
        return 2
    if not 0.1 <= thr <= 0.9:
        print("Schwelle außerhalb 0,1-0,9.", file=sys.stderr)
        return 2
    if not model.is_file() or not src.is_file():
        print("Modell oder Bild fehlt.", file=sys.stderr)
        return 2
    import numpy as np
    import onnxruntime as ort
    from PIL import Image, ImageDraw

    t0 = time.perf_counter()
    try:
        img = Image.open(src)
        img.load()
    except Exception as e:  # beschädigte oder fremde Datei
        print(f"Bild nicht lesbar: {e}", file=sys.stderr)
        return 3
    rgb = img.convert("RGB")
    w, h = rgb.size
    if w * h > 40_000_000:
        print("Bild zu groß (über 40 Megapixel).", file=sys.stderr)
        return 3
    # Letterbox wie YOLOX: Seitenverhältnis halten, oben links einpassen, Rest grau (114); BGR, 0-255, ohne Normierung
    r = min(SIZE / w, SIZE / h)
    nw, nh = int(w * r), int(h * r)
    pad = np.full((SIZE, SIZE, 3), 114, dtype=np.uint8)
    pad[:nh, :nw] = np.asarray(rgb.resize((nw, nh), Image.BILINEAR), dtype=np.uint8)[:, :, ::-1]
    x = np.ascontiguousarray(pad.transpose(2, 0, 1), dtype=np.float32)[None]

    sess = ort.InferenceSession(str(model), providers=["CPUExecutionProvider"])
    pred = sess.run(None, {sess.get_inputs()[0].name: x})[0][0]  # (8400, 85)
    grids, strides = [], []
    for s in (8, 16, 32):
        g = SIZE // s
        xv, yv = np.meshgrid(np.arange(g), np.arange(g))
        grids.append(np.stack((xv, yv), 2).reshape(-1, 2))
        strides.append(np.full((g * g, 1), s))
    grids = np.concatenate(grids, 0); strides = np.concatenate(strides, 0)
    pred[:, :2] = (pred[:, :2] + grids) * strides
    pred[:, 2:4] = np.exp(pred[:, 2:4]) * strides

    cls = pred[:, 5:].argmax(1)
    score = pred[:, 4] * pred[np.arange(len(pred)), 5 + cls]
    m = score >= thr
    boxes = np.stack([pred[m, 0] - pred[m, 2] / 2, pred[m, 1] - pred[m, 3] / 2, pred[m, 0] + pred[m, 2] / 2, pred[m, 1] + pred[m, 3] / 2], 1) / r
    score, cls = score[m], cls[m]
    found = []
    for c in np.unique(cls):  # NMS je Klasse
        idx = np.where(cls == c)[0]
        for k in nms(boxes[idx], score[idx], 0.45):
            i = idx[k]
            b = boxes[i]
            found.append({"klasse": COCO[int(c)], "sicherheit": round(float(score[i]), 3),
                          "rahmen": [max(0, round(float(b[0]))), max(0, round(float(b[1]))), min(w, round(float(b[2]))), min(h, round(float(b[3])))]})
    found.sort(key=lambda o: -o["sicherheit"])
    counts = {}
    for o in found:
        counts[o["klasse"]] = counts.get(o["klasse"], 0) + 1

    if out != "-":
        draw = ImageDraw.Draw(rgb)
        lw = max(2, round(min(w, h) / 300))
        for o in found:
            x1, y1, x2, y2 = o["rahmen"]
            draw.rectangle([x1, y1, x2, y2], outline=(245, 165, 36), width=lw)
            draw.text((x1 + lw + 2, y1 + lw + 1), f"{o['klasse']} {int(o['sicherheit'] * 100)}%", fill=(245, 165, 36))
        Path(out).parent.mkdir(parents=True, exist_ok=True)
        rgb.save(out)
    print(json.dumps({"breite": w, "hoehe": h, "ms": round((time.perf_counter() - t0) * 1000), "objekte": found, "anzahl": counts}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
