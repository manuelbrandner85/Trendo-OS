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
# Beschriftung im Bild auf Deutsch (gleich wie ObjectDetect.German in C#, per Test geprüft)
GERMAN = {"person": "Person", "bicycle": "Fahrrad", "car": "Auto", "motorcycle": "Motorrad", "airplane": "Flugzeug", "bus": "Bus", "train": "Zug", "truck": "Lastwagen", "boat": "Boot", "traffic light": "Ampel", "fire hydrant": "Hydrant", "stop sign": "Stoppschild", "parking meter": "Parkuhr", "bench": "Bank", "bird": "Vogel", "cat": "Katze", "dog": "Hund", "horse": "Pferd", "sheep": "Schaf", "cow": "Kuh", "elephant": "Elefant", "bear": "Bär", "zebra": "Zebra", "giraffe": "Giraffe", "backpack": "Rucksack", "umbrella": "Schirm", "handbag": "Handtasche", "tie": "Krawatte", "suitcase": "Koffer", "frisbee": "Frisbee", "skis": "Ski", "snowboard": "Snowboard", "sports ball": "Ball", "kite": "Drachen", "baseball bat": "Baseballschläger", "baseball glove": "Baseballhandschuh", "skateboard": "Skateboard", "surfboard": "Surfbrett", "tennis racket": "Tennisschläger", "bottle": "Flasche", "wine glass": "Weinglas", "cup": "Tasse", "fork": "Gabel", "knife": "Messer", "spoon": "Löffel", "bowl": "Schüssel", "banana": "Banane", "apple": "Apfel", "sandwich": "Sandwich", "orange": "Orange", "broccoli": "Brokkoli", "carrot": "Karotte", "hot dog": "Hotdog", "pizza": "Pizza", "donut": "Donut", "cake": "Kuchen", "chair": "Stuhl", "couch": "Sofa", "potted plant": "Topfpflanze", "bed": "Bett", "dining table": "Tisch", "toilet": "Toilette", "tv": "Fernseher", "laptop": "Laptop", "mouse": "Maus", "remote": "Fernbedienung", "keyboard": "Tastatur", "cell phone": "Handy", "microwave": "Mikrowelle", "oven": "Backofen", "toaster": "Toaster", "sink": "Spüle", "refrigerator": "Kühlschrank", "book": "Buch", "clock": "Uhr", "vase": "Vase", "scissors": "Schere", "teddy bear": "Teddybär", "hair drier": "Föhn", "toothbrush": "Zahnbürste"}
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
    # klassenübergreifend wie YOLOX' eigene Vorlage (class_agnostic): ein Gegenstand zählt einmal, auch wenn er z. B. als „Auto“ und „Lastwagen“ erkannt wird
    for i in nms(boxes, score, 0.45) if len(score) else []:
        b = boxes[i]
        found.append({"klasse": COCO[int(cls[i])], "sicherheit": round(float(score[i]), 3),
                      "rahmen": [max(0, round(float(b[0]))), max(0, round(float(b[1]))), min(w, round(float(b[2]))), min(h, round(float(b[3])))]})
    found.sort(key=lambda o: -o["sicherheit"])
    counts = {}
    for o in found:
        counts[o["klasse"]] = counts.get(o["klasse"], 0) + 1

    if out != "-":
        from PIL import ImageFont
        draw = ImageDraw.Draw(rgb)
        lw = max(2, round(min(w, h) / 300))
        font = ImageFont.load_default(size=max(12, round(min(w, h) / 32)))
        for o in found:
            x1, y1, x2, y2 = o["rahmen"]
            draw.rectangle([x1, y1, x2, y2], outline=(245, 165, 36), width=lw)
            label = f"{GERMAN.get(o['klasse'], o['klasse'])} {int(o['sicherheit'] * 100)} %"
            tx1, ty1, tx2, ty2 = draw.textbbox((x1, y1), label, font=font)
            pad = max(2, lw)
            ly = y1 - (ty2 - ty1) - 2 * pad if y1 - (ty2 - ty1) - 2 * pad >= 0 else y1  # Beschriftung über dem Rahmen, am oberen Rand innen
            bw = (tx2 - tx1) + 2 * pad
            lx = max(0, min(x1, w - bw))  # am rechten Bildrand nach links rücken, damit nichts abgeschnitten wird
            draw.rectangle([lx, ly, lx + bw, ly + (ty2 - ty1) + 2 * pad], fill=(245, 165, 36))
            draw.text((lx + pad - (tx1 - x1), ly + pad - (ty1 - y1)), label, fill=(20, 20, 20), font=font)
        Path(out).parent.mkdir(parents=True, exist_ok=True)
        rgb.save(out)
    print(json.dumps({"breite": w, "hoehe": h, "ms": round((time.perf_counter() - t0) * 1000), "objekte": found, "anzahl": counts}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
