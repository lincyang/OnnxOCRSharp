#!/usr/bin/env python3
"""Run v6 det on pic002 and save crops."""

from __future__ import annotations

import math
from pathlib import Path

import cv2
import numpy as np
import onnxruntime as ort


ROOT = Path(r"D:\workplace\workplace\github\ocr\OnnxOCRSharp")
DET_MODEL = ROOT / "models/ppocrv6/PP-OCRv6_tiny_det_onnx/inference.onnx"
REC_MODEL = ROOT / "models/ppocrv6/PP-OCRv6_tiny_rec_onnx/inference.onnx"
DICT_PATH = ROOT / "models/ppocrv6/ppocrv6_tiny_dict.txt"
IMAGE_PATH = ROOT / "test_assets/pic002.png"
OUT_DIR = ROOT / "test_assets/_debug_crops"


def det_preprocess(image: np.ndarray, limit_side_len=960):
    h, w = image.shape[:2]
    ratio = 1.0
    if max(h, w) > limit_side_len:
        ratio = limit_side_len / max(h, w)
    resize_h = max(int(round(h * ratio / 32) * 32), 32)
    resize_w = max(int(round(w * ratio / 32) * 32), 32)
    resized = cv2.resize(image, (resize_w, resize_h))
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    norm = resized.astype(np.float32) / 255.0
    norm = (norm - mean) / std
    chw = norm.transpose(2, 0, 1)
    return chw[np.newaxis, ...], (h, w, resize_h / h, resize_w / w)


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


def crop_quad(image, box):
    w = int(max(np.linalg.norm(box[0] - box[1]), np.linalg.norm(box[2] - box[3])))
    h = int(max(np.linalg.norm(box[0] - box[3]), np.linalg.norm(box[1] - box[2])))
    w = max(w, 1)
    h = max(h, 1)
    dst = np.array([[0, 0], [w, 0], [w, h], [0, h]], dtype=np.float32)
    m = cv2.getPerspectiveTransform(box, dst)
    return cv2.warpPerspective(image, m, (w, h))


def load_dict():
    chars = ["blank"]
    chars.extend(DICT_PATH.read_text(encoding="utf-8").splitlines())
    chars.append(" ")
    return chars


def resize_norm_img_chinese(img, image_shape=(3, 48, 320)):
    img_c, img_h, img_w = image_shape
    max_wh_ratio = img_w / img_h
    h, w = img.shape[:2]
    ratio = w / h
    max_wh_ratio = max(max_wh_ratio, ratio)
    img_w = int(img_h * max_wh_ratio)
    resized_w = img_w if math.ceil(img_h * ratio) > img_w else int(math.ceil(img_h * ratio))
    resized = cv2.resize(img, (resized_w, img_h)).astype("float32")
    resized = resized.transpose((2, 0, 1)) / 255.0
    resized -= 0.5
    resized /= 0.5
    padding = np.zeros((img_c, img_h, img_w), dtype=np.float32)
    padding[:, :, :resized_w] = resized
    return padding[np.newaxis, ...], img_w


def ctc_decode(preds, chars):
    indices = preds.argmax(axis=-1)[0]
    probs = preds.max(axis=-1)[0]
    out, conf = [], []
    for i, token in enumerate(indices):
        if token == 0:
            continue
        if i > 0 and token == indices[i - 1]:
            continue
        out.append(chars[token])
        conf.append(float(probs[i]))
    return "".join(out), (sum(conf) / len(conf) if conf else 0.0)


def main():
    OUT_DIR.mkdir(exist_ok=True)
    image = cv2.imread(str(IMAGE_PATH))
    tensor, shape = det_preprocess(image)
    det = ort.InferenceSession(str(DET_MODEL), providers=["CPUExecutionProvider"])
    out = det.run(None, {det.get_inputs()[0].name: tensor})[0]
    print("det out shape", out.shape)

    # very simplified: threshold map and take bounding rects of contours
    prob = out[0, 0]
    mask = (prob > 0.3).astype(np.uint8) * 255
    contours, _ = cv2.findContours(mask, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    boxes = []
    for cnt in contours:
        rect = cv2.minAreaRect(cnt)
        pts = cv2.boxPoints(rect)
        pts[:, 0] /= shape[3]
        pts[:, 1] /= shape[2]
        boxes.append(order_points(pts))

    boxes = sorted(boxes, key=lambda b: (b[0][1] + b[2][1]) / 2)
    print("boxes", len(boxes))

    rec = ort.InferenceSession(str(REC_MODEL), providers=["CPUExecutionProvider"])
    chars = load_dict()
    for i, box in enumerate(boxes[:5], 1):
        crop = crop_quad(image, box)
        cv2.imwrite(str(OUT_DIR / f"crop_{i}.png"), crop)
        tensor, _ = resize_norm_img_chinese(crop)
        pred = rec.run(None, {rec.get_inputs()[0].name: tensor})[0]
        text, score = ctc_decode(pred, chars)
        print(f"crop {i}: size={crop.shape[1]}x{crop.shape[0]} score={score:.4f} text={text!r}")


if __name__ == "__main__":
    main()
