"""生成更易触发「2 框 + 竖长裁剪 + 90° 旋转」的测试图。

与 crop_bug_2boxes.png（黑条）不同：使用真实中文字体、竖排长条、明显倾斜，
PP-OCRv6 检测器更容易召回为 2 个四边形框。
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "test_assets" / "crop_bug_rotate_2boxes.png"


def load_font(size: int) -> ImageFont.FreeTypeFont:
    for name in ("msyhbd.ttc", "msyh.ttc", "simhei.ttf", "simsun.ttc"):
        path = Path("C:/Windows/Fonts") / name
        if path.exists():
            return ImageFont.truetype(str(path), size)
    raise FileNotFoundError("未找到中文字体")


def render_vertical_strip(text: str, font: ImageFont.FreeTypeFont) -> Image.Image:
    probe = Image.new("RGB", (1, 1))
    draw = ImageDraw.Draw(probe)
    sizes = [draw.textbbox((0, 0), ch, font=font) for ch in text]
    char_w = max(b[2] - b[0] for b in sizes)
    char_h = max(b[3] - b[1] for b in sizes)
    gap = int(char_h * 0.28)
    pad_x, pad_y = 28, 36

    w = char_w + pad_x * 2
    h = char_h * len(text) + gap * (len(text) - 1) + pad_y * 2

    img = Image.new("RGB", (w, h), (255, 255, 255))
    draw = ImageDraw.Draw(img)
    draw.rectangle((0, 0, w - 1, h - 1), fill=(228, 228, 228), outline=(90, 90, 90), width=3)

    y = pad_y
    for ch in text:
        bbox = draw.textbbox((0, 0), ch, font=font)
        cw = bbox[2] - bbox[0]
        ch_h = bbox[3] - bbox[1]
        x = (w - cw) // 2
        draw.text((x, y), ch, font=font, fill=(0, 0, 0))
        y += ch_h + gap

    return img


def rotate_patch(img: Image.Image, angle_deg: float) -> Image.Image:
    return img.rotate(angle_deg, expand=True, resample=Image.BICUBIC, fillcolor=(245, 245, 245))


def paste_center(canvas: Image.Image, patch: Image.Image, center: tuple[int, int]) -> None:
    cx, cy = center
    canvas.paste(patch, (cx - patch.width // 2, cy - patch.height // 2))


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)

    # 更高、更窄、倾斜更明显 → 检测 2 框 + 裁剪后 Rows/Cols >= 1.5
    canvas = Image.new("RGB", (960, 1280), (245, 245, 245))
    font = load_font(64)

    left = rotate_patch(render_vertical_strip("竖排文字测试甲", font), -22)
    right = rotate_patch(render_vertical_strip("竖排文字测试乙", font), 18)

    paste_center(canvas, left, (260, 640))
    paste_center(canvas, right, (700, 660))

    canvas.save(OUT, format="PNG")
    print(f"已生成: {OUT}")
    print(f"尺寸: {canvas.width} x {canvas.height}")
    print()
    print("请用【未修复版】桌面端 / Console 打开此图识别。")
    print("预期: 检出约 2 个框；竖长裁剪触发 90° 旋转；旧版 ImageCropper 崩溃。")


if __name__ == "__main__":
    main()
