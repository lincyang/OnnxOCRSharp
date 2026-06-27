#!/usr/bin/env python3
"""Replicate C# TextRecognizer batch logic in Python."""

from __future__ import annotations

import math
from pathlib import Path

import cv2
import numpy as np
import onnxruntime as ort


ROOT = Path(r"D:\workplace\workplace\github\ocr\OnnxOCRSharp")
REC_MODEL = ROOT / "models/ppocrv6/PP-OCRv6_tiny_rec_onnx/inference.onnx"
DICT_PATH = ROOT / "models/ppocrv6/ppocrv6_tiny_dict.txt"
IMAGE_PATH = ROOT / "test_assets/pic002.png"


def load_dict(use_space_char=True):
    chars = ["blank"]
    chars.extend(DICT_PATH.read_text(encoding="utf-8").splitlines())
    if use_space_char:
        chars.append(" ")
    return chars


def csharp_resize_norm(image, target_width, img_h=48):
    ratio = image.shape[1] / image.shape[0]
    resized_w = int(math.ceil(img_h * ratio))
    if resized_w > target_width:
        resized_w = target_width
    resized = cv2.resize(image, (resized_w, img_h))
    img_c = 3
    normalized = np.zeros((img_c, img_h, target_width), dtype=np.float32)
    for y in range(img_h):
        for x in range(resized_w):
            b, g, r = resized[y, x]
            normalized[0, y, x] = (b / 255.0 - 0.5) / 0.5
            normalized[1, y, x] = (g / 255.0 - 0.5) / 0.5
            normalized[2, y, x] = (r / 255.0 - 0.5) / 0.5
    return normalized


def csharp_recognize_batch(images, rec_batch_num=6, base_img_w=320, img_h=48):
    width_ratios = [img.shape[1] / img.shape[0] for img in images]
    sorted_indices = sorted(range(len(images)), key=lambda i: width_ratios[i])
    results = [("", 0.0)] * len(images)
    chars = load_dict(True)
    session = ort.InferenceSession(str(REC_MODEL), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name

    batch_start = 0
    while batch_start < len(images):
        batch_end = min(len(images), batch_start + rec_batch_num)
        max_wh_ratio = base_img_w / img_h
        for i in range(batch_start, batch_end):
            image = images[sorted_indices[i]]
            ratio = image.shape[1] / image.shape[0]
            max_wh_ratio = max(max_wh_ratio, ratio)
        batch_width = max(base_img_w, int(math.ceil(img_h * max_wh_ratio)))
        batch_size = batch_end - batch_start
        batch_tensor = np.zeros((batch_size, 3, img_h, batch_width), dtype=np.float32)
        for i in range(batch_size):
            norm = csharp_resize_norm(images[sorted_indices[batch_start + i]], batch_width, img_h)
            batch_tensor[i] = norm
        out = session.run(None, {input_name: batch_tensor})[0]
        for i in range(batch_size):
            indices = out[i].argmax(axis=-1)
            probs = out[i].max(axis=-1)
            text_chars, conf = [], []
            for t, token in enumerate(indices):
                if token == 0:
                    continue
                if t > 0 and token == indices[t - 1]:
                    continue
                text_chars.append(chars[token])
                conf.append(float(probs[t]))
            text = "".join(text_chars)
            score = sum(conf) / len(conf) if conf else 0.0
            results[sorted_indices[batch_start + i]] = (text, score)
        batch_start = batch_end
    return results


def main():
    image = cv2.imread(str(IMAGE_PATH))
    crops = [
        image[8:35, 8:190],
        image[38:65, 8:190],
        image[68:98, 8:190],
    ]
    expected = ["五、中文识别", "六、高级应用场景", "5.1 结构化数据提取"]
    results = csharp_recognize_batch(crops)
    for i, ((text, score), exp) in enumerate(zip(results, expected), 1):
        print(f"line {i}: score={score:.4f} pred={text!r} expected={exp!r}")


if __name__ == "__main__":
    main()
