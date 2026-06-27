#!/usr/bin/env python3
"""Debug PP-OCRv6 tiny recognition on pic002 crops."""

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


def load_dict(use_space_char: bool) -> list[str]:
    chars = ["blank"]
    chars.extend(DICT_PATH.read_text(encoding="utf-8").splitlines())
    if use_space_char:
        chars.append(" ")
    return chars


def resize_norm_img_chinese(img: np.ndarray, image_shape=(3, 48, 320)) -> tuple[np.ndarray, int]:
    img_c, img_h, img_w = image_shape
    max_wh_ratio = img_w * 1.0 / img_h
    h, w = img.shape[:2]
    ratio = w * 1.0 / h
    max_wh_ratio = max(max_wh_ratio, ratio)
    img_w = int(img_h * max_wh_ratio)
    if math.ceil(img_h * ratio) > img_w:
        resized_w = img_w
    else:
        resized_w = int(math.ceil(img_h * ratio))
    resized = cv2.resize(img, (resized_w, img_h)).astype("float32")
    resized = resized.transpose((2, 0, 1)) / 255.0
    resized -= 0.5
    resized /= 0.5
    padding = np.zeros((img_c, img_h, img_w), dtype=np.float32)
    padding[:, :, :resized_w] = resized
    return padding[np.newaxis, ...], img_w


def ctc_decode(preds: np.ndarray, chars: list[str]) -> tuple[str, float]:
    indices = preds.argmax(axis=-1)[0]
    probs = preds.max(axis=-1)[0]
    out = []
    conf = []
    for i, token in enumerate(indices):
        if token == 0:
            continue
        if i > 0 and token == indices[i - 1]:
            continue
        if token < 0 or token >= len(chars):
            continue
        out.append(chars[token])
        conf.append(float(probs[i]))
    text = "".join(out)
    score = sum(conf) / len(conf) if conf else 0.0
    return text, score


def main() -> None:
    image = cv2.imread(str(IMAGE_PATH))
    assert image is not None

    # manual crops approximating full lines
    crops = [
        image[8:35, 8:190],    # line 1
        image[38:65, 8:190],   # line 2
        image[68:98, 8:190],   # line 3
    ]
    expected = [
        "五、中文识别",
        "六、高级应用场景",
        "5.1 结构化数据提取",
    ]

    session = ort.InferenceSession(str(REC_MODEL), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name
    print("output shape meta", session.get_outputs()[0].shape)

    for use_space in (True, False):
        chars = load_dict(use_space)
        print(f"\n=== use_space_char={use_space}, dict_size={len(chars)} ===")
        for i, (crop, exp) in enumerate(zip(crops, expected), 1):
            tensor, width = resize_norm_img_chinese(crop)
            out = session.run([output_name], {input_name: tensor})[0]
            text, score = ctc_decode(out, chars)
            print(f"line {i}: score={score:.4f} pred={text!r} expected={exp!r}")


if __name__ == "__main__":
    main()
