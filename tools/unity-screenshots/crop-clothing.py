"""衣装の記事（avatar-clothing/06）用: capture-unity.ps1 で撮ったウィンドウ全体の画像から、Inspector の部分を切り出す。

使い方: python tools/unity-screenshots/crop-clothing.py <ShotsOut のフォルダー>
Inspector は、ウィンドウ（1920 x 1032）の右端（x 1431〜1912）にある。高さは、各画面で見える範囲に合わせた値。
"""
import sys
from pathlib import Path

from PIL import Image

src = Path(sys.argv[1])
out = Path(__file__).resolve().parents[2] / "Content" / "learn" / "avatar-clothing" / "images"
JOBS = [
    ("model-rig-window.png", "unity-import-rig.png", (1431, 81, 1912, 314)),
    ("merge-armature-window.png", "unity-merge-armature.png", (1431, 81, 1912, 760)),
    ("blendshape-sync-window.png", "unity-blendshape-sync.png", (1431, 81, 1912, 425)),
]
for name, dest, box in JOBS:
    full = Image.open(src / name).convert("RGB")
    if full.size != (1920, 1032):
        raise SystemExit(f"{name}: ウィンドウの大きさが {full.size} で、切り出しの座標（1920 x 1032 の前提）と合わない")
    img = full.crop(box)
    img.save(out / dest, optimize=True)
    print(dest, img.size)
