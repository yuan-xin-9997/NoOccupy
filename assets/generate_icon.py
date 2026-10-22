#!/usr/bin/env python3
"""Generate the NoOccupy application icon (folder + open padlock)."""
from PIL import Image, ImageDraw, ImageFilter
import io
import math
import os
import struct

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
MAIN_ICO = os.path.join(ROOT, "src", "NoOccupy", "app.ico")

S = 1024


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def vertical_gradient(size, top, bottom):
    w, h = size
    img = Image.new("RGBA", size)
    px = img.load()
    for y in range(h):
        c = lerp(top, bottom, y / max(1, h - 1))
        for x in range(w):
            px[x, y] = c + (255,)
    return img


def rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return mask


def paste_gradient(base, box, top, bottom, radius):
    w = box[2] - box[0]
    h = box[3] - box[1]
    grad = vertical_gradient((w, h), top, bottom)
    grad.putalpha(rounded_mask((w, h), radius))
    base.alpha_composite(grad, (box[0], box[1]))


def draw_icon(scale=4, small=False):
    n = S * scale
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def sc(v):
        return int(round(v * n / 1024.0))

    def box(x1, y1, x2, y2):
        return (sc(x1), sc(y1), sc(x2), sc(y2))

    # soft ground shadow
    shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.ellipse(box(150, 820, 900, 960), fill=(20, 50, 110, 70))
    shadow = shadow.filter(ImageFilter.GaussianBlur(sc(30)))
    img.alpha_composite(shadow)

    # folder back tab
    paste_gradient(img, box(100, 225, 590, 420), (58, 120, 235), (30, 82, 190), sc(70))

    # folder body shadow / edge
    body_shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    bsd = ImageDraw.Draw(body_shadow)
    bsd.rounded_rectangle(box(110, 330, 920, 875), radius=sc(90), fill=(16, 45, 110, 90))
    body_shadow = body_shadow.filter(ImageFilter.GaussianBlur(sc(18)))
    img.alpha_composite(body_shadow)

    # folder body
    paste_gradient(img, box(100, 305, 920, 860), (86, 155, 255), (35, 96, 225), sc(92))

    # folder top highlight
    hi = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hi)
    hd.rounded_rectangle(box(125, 325, 895, 410), radius=sc(48), fill=(180, 215, 255, 90))
    img.alpha_composite(hi)

    # subtle folder outline
    d.rounded_rectangle(box(100, 305, 920, 860), radius=sc(92), outline=(28, 76, 180, 230), width=sc(7))

    # ---- open padlock ----
    # lock shadow on folder
    lsh = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    lsd = ImageDraw.Draw(lsh)
    lsd.ellipse(box(455, 820, 875, 930), fill=(10, 30, 80, 110))
    lsh = lsh.filter(ImageFilter.GaussianBlur(sc(24)))
    img.alpha_composite(lsh)

    # shackle (drawn behind the body): attached left leg, open right leg
    green_dark = (16, 165, 100, 255)
    green = (42, 205, 128, 255)
    green_light = (125, 240, 180, 255)
    stroke = sc(86 if not small else 104)

    def line(p1, p2, color, width):
        d.line([(sc(p1[0]), sc(p1[1])), (sc(p2[0]), sc(p2[1]))], fill=color, width=width, joint="curve")
        r = width // 2
        for p in (p1, p2):
            d.ellipse((sc(p[0]) - r, sc(p[1]) - r, sc(p[0]) + r, sc(p[1]) + r), fill=color)

    def arc(bounds, start, end, color, width):
        d.arc(box(*bounds), start=start, end=end, fill=color, width=width)

    # left vertical leg goes into the lock body
    line((548, 690), (548, 505), green_dark, stroke)
    # top arc
    arc((548, 385, 790, 625), 180, 360, green_dark, stroke)
    # open right leg, shorter and angled outward (clear gap above body)
    line((790, 505), (830, 430), green_dark, stroke)
    # highlight along the shackle
    hl = sc(24)
    line((548, 650), (548, 512), green_light, hl)
    arc((565, 402, 773, 608), 190, 350, green_light, hl)
    line((795, 495), (823, 445), green_light, hl)
    # middle green
    line((548, 520), (548, 500), green, sc(34))
    arc((557, 394, 781, 616), 180, 360, green, sc(34))

    # lock body
    body = box(470, 590, 870, 900)
    paste_gradient(img, body, (255, 255, 255), (222, 232, 246), sc(78))
    d.rounded_rectangle(body, radius=sc(78), outline=(166, 185, 212, 255), width=sc(8))
    # inner top highlight
    d.rounded_rectangle(box(492, 610, 848, 672), radius=sc(34), fill=(255, 255, 255, 120))
    # keyhole
    d.ellipse(box(637, 675, 707, 745), fill=(30, 58, 138, 255))
    d.rounded_rectangle(box(657, 725, 687, 815), radius=sc(15), fill=(30, 58, 138, 255))
    d.ellipse(box(650, 688, 682, 720), fill=(90, 125, 205, 160))

    return img


def frame_for(size):
    base = draw_icon(scale=4, small=size <= 32)
    return base.resize((size, size), Image.LANCZOS)


def save_ico(path, sizes):
    frames = []
    for size in sizes:
        img = frame_for(size)
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=True)
        frames.append((size, buf.getvalue()))

    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries = b""
    for size, data in frames:
        b = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", b, b, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    with open(path, "wb") as f:
        f.write(header + entries + b"".join(d for _, d in frames))


def make_preview(path, dark=False):
    sizes = [16, 24, 32, 48, 64, 128, 256]
    pad = 28
    width = pad * 2 + sum(s + pad for s in sizes)
    height = 360
    bg = (24, 34, 52, 255) if dark else (247, 249, 252, 255)
    canvas = Image.new("RGBA", (width, height), bg)
    x = pad
    for s in sizes:
        img = frame_for(s)
        y = 40 + (256 - s) // 2
        canvas.alpha_composite(img, (x, y))
        # 4x nearest zoom for tiny sizes on a second row
        if s <= 48:
            zoom = img.resize((s * 4, s * 4), Image.NEAREST)
            canvas.alpha_composite(zoom, (x, 300 - s * 2))
        x += s + pad
    canvas.save(path)


def main():
    os.makedirs(ASSETS, exist_ok=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    save_ico(os.path.join(ASSETS, "app.ico"), sizes)
    save_ico(MAIN_ICO, sizes)
    frame_for(256).save(os.path.join(ASSETS, "icon-256.png"))
    make_preview(os.path.join(ASSETS, "preview-light.png"), dark=False)
    make_preview(os.path.join(ASSETS, "preview-dark.png"), dark=True)
    print("generated", os.path.join(ASSETS, "app.ico"))


if __name__ == "__main__":
    main()
