"""生成竖排双列中文测试图，供 PP-OCR 检测出 2 框并触发 ImageCropper 旋转分支。"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "test_assets" / "crop_bug_vertical_2cols.png"


def load_font(size: int) -> ImageFont.FreeTypeFont:
    for name in ("msyh.ttc", "msyhbd.ttc", "simhei.ttf", "simsun.ttc"):
        path = Path("C:/Windows/Fonts") / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    raise FileNotFoundError("未找到中文字体，请安装微软雅黑或黑体")


def render_vertical_strip(text: str, font: ImageFont.FreeTypeFont, padding: int = 20) -> Image.Image:
    probe = Image.new("RGB", (1, 1))
    draw = ImageDraw.Draw(probe)
    sizes = [draw.textbbox((0, 0), ch, font=font) for ch in text]
    char_w = max(b[2] - b[0] for b in sizes)
    char_h = max(b[3] - b[1] for b in sizes)
    line_gap = int(char_h * 0.35)

    w = char_w + padding * 2
    h = char_h * len(text) + line_gap * (len(text) - 1) + padding * 2

    img = Image.new("RGB", (w, h), (255, 255, 255))
    draw = ImageDraw.Draw(img)
    draw.rectangle((2, 2, w - 3, h - 3), fill=(235, 235, 235), outline=(160, 160, 160), width=2)

    y = padding
    for ch in text:
        bbox = draw.textbbox((0, 0), ch, font=font)
        cw, ch_h = bbox[2] - bbox[0], bbox[3] - bbox[1]
        x = (w - cw) // 2
        draw.text((x, y), ch, font=font, fill=(20, 20, 20))
        y += ch_h + line_gap

    return img


def rotate_image(img: Image.Image, angle_deg: float, bg=(248, 248, 248)) -> Image.Image:
    return img.rotate(angle_deg, expand=True, resample=Image.BICUBIC, fillcolor=bg)


def paste_center(canvas: Image.Image, patch: Image.Image, center: tuple[int, int]) -> None:
    cx, cy = center
    x = cx - patch.width // 2
    y = cy - patch.height // 2
    canvas.paste(patch, (x, y), mask=None)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)

    canvas = Image.new("RGB", (800, 1100), (248, 248, 248))
    font = load_font(52)

    left = rotate_image(render_vertical_strip("竖排测试甲", font), -14)
    right = rotate_image(render_vertical_strip("竖排测试乙", font), 12)

    paste_center(canvas, left, (220, 520))
    paste_center(canvas, right, (580, 540))

    canvas.save(OUT, format="PNG", optimize=True)
    print(f"已生成: {OUT}")
    print(f"尺寸: {canvas.width} x {canvas.height}")
    print()
    print("用未修复版桌面端打开此图识别，应检出 2 框；两框裁剪后高宽比>=1.5 会走旋转分支。")


if __name__ == "__main__":
    main()
