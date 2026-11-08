"""capture.py が書き出した post.json を読んで、切り抜きと番号付けを行う。

    py -3 tools/screenshots/postprocess.py %TEMP%/fara-avatar-modding-guide-shots/post.json

post.json の各項目:
    {"file": "C:/.../images/x.png", "crop": [x0, y0, x1, y1] | null,
     "labels": [{"n": 1, "x": 100, "y": 200}, ...]}   # 座標は切り抜き前の画像のピクセル
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

FONT_CANDIDATES = [r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\segoeuib.ttf"]


def font(size):
    for f in FONT_CANDIDATES:
        if Path(f).exists():
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def process(item):
    path = Path(item["file"])
    if not path.exists():
        print(f"skip (missing): {path}")
        return
    img = Image.open(path).convert("RGBA")
    labels = item.get("labels") or []
    if labels:
        draw = ImageDraw.Draw(img)
        r = 22
        f = font(26)
        for lb in labels:
            x, y = lb["x"], lb["y"]
            draw.ellipse([x - r, y - r, x + r, y + r], fill=(255, 140, 0, 255), outline=(255, 255, 255, 255), width=3)
            text = str(lb["n"])
            bbox = draw.textbbox((0, 0), text, font=f)
            tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
            draw.text((x - tw / 2 - bbox[0], y - th / 2 - bbox[1]), text, font=f, fill=(255, 255, 255, 255))
    if item.get("crop"):
        img = img.crop(tuple(int(v) for v in item["crop"]))
    img.convert("RGB").save(path, optimize=True)
    print(f"ok: {path.name} {img.size[0]}x{img.size[1]}")


def main():
    post = Path(sys.argv[1])
    for item in json.loads(post.read_text(encoding="utf-8")):
        process(item)


if __name__ == "__main__":
    main()
