#!/usr/bin/env python3
"""Debug crop_bug_rotate_2boxes: det -> crop -> orientation -> rec."""

from __future__ import annotations

import math
from pathlib import Path

import cv2
import numpy as np
import onnxruntime as ort

ROOT = Path(r"D:\workplace\workplace\github\ocr\OnnxOCRSharp")
IMAGE = ROOT / "test_assets/crop_bug_rotate_2boxes.png"
DET = ROOT / "models/ppocrv6/PP-OCRv6_tiny_det_onnx/inference.onnx"
REC = ROOT / "models/ppocrv6/PP-OCRv6_tiny_rec_onnx/inference.onnx"
YML = ROOT / "models/ppocrv6/PP-OCRv6_tiny_rec_onnx/inference.yml"
ORI = ROOT / "models/orientation/rapid_orientation.onnx"
OUT = ROOT / "test_assets/_debug_rotate"


def parse_dict(yml_path: Path) -> list[str]:
    chars = ["blank"]
    in_dict = False
    for line in yml_path.read_text(encoding="utf-8").splitlines():
        if line.strip() == "character_dict:":
            in_dict = True
            continue
        if not in_dict:
            continue
        s = line.strip()
        if not s.startswith("- "):
            break
        v = s[2:].strip()
        if len(v) >= 2 and v[0] == v[-1] and v[0] in "\"'":
            v = v[1:-1]
        chars.append(v)
    chars.append(" ")
    return chars


def det_preprocess(image, limit=960):
    h, w = image.shape[:2]
    ratio = limit / max(h, w) if max(h, w) > limit else 1.0
    rh = max(int(round(h * ratio / 32) * 32), 32)
    rw = max(int(round(w * ratio / 32) * 32), 32)
    resized = cv2.resize(image, (rw, rh))
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    norm = resized.astype(np.float32) / 255.0
    norm = (norm - mean) / std
    return norm.transpose(2, 0, 1)[None, ...], (h, w, rh / h, rw / w)


def order_points(pts):
    pts = np.array(pts, dtype=np.float32)
    sums = pts.sum(axis=1)
    diffs = np.diff(pts, axis=1).reshape(-1)
    ordered = np.zeros((4, 2), dtype=np.float32)
    ordered[0] = pts[np.argmin(sums)]
    ordered[2] = pts[np.argmax(sums)]
    remaining = [i for i in range(4) if i not in (np.argmin(sums), np.argmax(sums))]
    ordered[1] = pts[remaining[np.argmin([diffs[i] for i in remaining])]]
    ordered[3] = pts[remaining[np.argmax([diffs[i] for i in remaining])]]
    return ordered


def crop_quad(image, box, vertical_rotate=True):
    w = int(max(np.linalg.norm(box[0] - box[1]), np.linalg.norm(box[2] - box[3])))
    h = int(max(np.linalg.norm(box[0] - box[3]), np.linalg.norm(box[1] - box[2])))
    w, h = max(w, 1), max(h, 1)
    dst = np.array([[0, 0], [w, 0], [w, h], [0, h]], dtype=np.float32)
    m = cv2.getPerspectiveTransform(box, dst)
    c = cv2.warpPerspective(image, m, (w, h))
    if vertical_rotate and c.shape[0] / max(c.shape[1], 1) >= 1.5:
        c = cv2.rotate(c, cv2.ROTATE_90_CLOCKWISE)
    return c


def ori_prep(im):
    h, w = im.shape[:2]
    sc = 256 / min(w, h)
    im = cv2.resize(im, (int(w * sc), int(h * sc)), interpolation=cv2.INTER_LANCZOS4)
    h, w = im.shape[:2]
    if h < 224 or w < 224:
        im = cv2.copyMakeBorder(
            im, 0, max(0, 224 - h), 0, max(0, 224 - w), cv2.BORDER_CONSTANT, value=(255, 255, 255)
        )
        h, w = im.shape[:2]
    x0, y0 = (w - 224) // 2, (h - 224) // 2
    crop = im[y0 : y0 + 224, x0 : x0 + 224]
    x = crop.astype(np.float32) / 255.0
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    x = (x - mean) / std
    return x.transpose(2, 0, 1)[None, ...].astype(np.float32)


def classify_orientation(im, ori_sess, labels):
    out = ori_sess.run(None, {"x": ori_prep(im)})[0]
    return int(labels[int(out.argmax(-1))])


def rotate_upright(im, angle: int):
    if angle == 0:
        return im
    if angle == 90:
        return cv2.rotate(im, cv2.ROTATE_90_COUNTERCLOCKWISE)
    if angle == 180:
        return cv2.rotate(im, cv2.ROTATE_180)
    if angle == 270:
        return cv2.rotate(im, cv2.ROTATE_90_CLOCKWISE)
    return im


def rec_infer(im, rec_sess, chars):
    img_h = 48
    h, w = im.shape[:2]
    ratio = w / h
    img_w = max(320, int(math.ceil(img_h * ratio)))
    resized_w = min(img_w, int(math.ceil(img_h * ratio)))
    resized = cv2.resize(im, (resized_w, img_h)).astype("float32")
    x = resized.transpose(2, 0, 1) / 255.0
    x = (x - 0.5) / 0.5
    pad = np.zeros((3, img_h, img_w), dtype=np.float32)
    pad[:, :, :resized_w] = x
    pred = rec_sess.run(None, {rec_sess.get_inputs()[0].name: pad[None, ...]})[0]
    idx = pred.argmax(axis=-1)[0]
    prob = pred.max(axis=-1)[0]
    out, conf = [], []
    for i, token in enumerate(idx):
        if token == 0:
            continue
        if i > 0 and token == idx[i - 1]:
            continue
        out.append(chars[token])
        conf.append(float(prob[i]))
    text = "".join(out)
    score = sum(conf) / len(conf) if conf else 0.0
    return text, score


def main():
    OUT.mkdir(exist_ok=True)
    image = cv2.imread(str(IMAGE))
    det = ort.InferenceSession(str(DET), providers=["CPUExecutionProvider"])
    rec = ort.InferenceSession(str(REC), providers=["CPUExecutionProvider"])
    ori = ort.InferenceSession(str(ORI), providers=["CPUExecutionProvider"])
    labels = [int(x) for x in ori.get_modelmeta().custom_metadata_map["character"].splitlines()]
    chars = parse_dict(YML)

    tensor, shape = det_preprocess(image)
    out = det.run(None, {det.get_inputs()[0].name: tensor})[0]
    prob = out[0, 0]
    mask = (prob > 0.2).astype(np.uint8) * 255
    contours, _ = cv2.findContours(mask, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    boxes = []
    for cnt in contours:
        if cv2.contourArea(cnt) < 50:
            continue
        rect = cv2.minAreaRect(cnt)
        pts = cv2.boxPoints(rect)
        pts[:, 0] = np.clip(pts[:, 0] / shape[3], 0, shape[1] - 1)
        pts[:, 1] = np.clip(pts[:, 1] / shape[2], 0, shape[0] - 1)
        boxes.append(order_points(pts))
    boxes = sorted(boxes, key=lambda b: (b[0][1] + b[2][1]) / 2)
    print("boxes", len(boxes))

    for i, box in enumerate(boxes[:5], 1):
        for name, vr in [("heuristic", True), ("no_heuristic", False), ("no_heuristic+ori", False)]:
            crop = crop_quad(image, box, vertical_rotate=vr)
            if name.endswith("+ori"):
                ang = classify_orientation(crop, ori, labels)
                crop2 = rotate_upright(crop, ang)
            else:
                ang = None
                crop2 = crop
            text, score = rec_infer(crop2, rec, chars)
            tag = f"ori={ang}" if ang is not None else ""
            print(f"  box{i} {name:18} {crop2.shape[1]}x{crop2.shape[0]} {tag:8} score={score:.4f} text={text!r}")
            cv2.imwrite(str(OUT / f"box{i}_{name}.png"), crop2)


if __name__ == "__main__":
    main()
