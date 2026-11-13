#!/usr/bin/env python3
"""Draws the enemy pixel art and writes the sprite sheets Godot uses.

Run from the project root after changing anything here:

    python3 EnemyAssets/Source/make_enemy_sprites.py

For each enemy it writes one horizontal PNG strip per animation and a
SpriteFrames .tres file that the enemy scene loads. Art faces right;
EnemyController flips it when the enemy walks left. Uses only the
Python standard library.
"""

import math
import os
import struct
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "EnemyAssets")


def color(hex_code, alpha=255):
    h = hex_code.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), alpha)


CLEAR = (0, 0, 0, 0)
OUTLINE = color("#1f1426")


class Canvas:
    """A small RGBA pixel grid with the few drawing tools the art needs."""

    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.pixels = [[CLEAR] * width for _ in range(height)]

    def set(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.width and 0 <= y < self.height:
            self.pixels[y][x] = c

    def get(self, x, y):
        if 0 <= x < self.width and 0 <= y < self.height:
            return self.pixels[y][x]
        return CLEAR

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, c)

    def ellipse(self, cx, cy, rx, ry, fill, max_y=None):
        """Fills an ellipse. fill is a color or fn(nx, ny) -> color,
        where nx/ny run from -1 to 1 across the ellipse."""
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            if max_y is not None and y > max_y:
                continue
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                nx, ny = (x - cx) / rx, (y - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    self.set(x, y, fill(nx, ny) if callable(fill) else fill)

    def line(self, x0, y0, x1, y1, c):
        steps = int(max(abs(x1 - x0), abs(y1 - y0), 1))
        for i in range(steps + 1):
            t = i / steps
            self.set(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c)

    def outline(self, c=OUTLINE):
        """Adds a 1px outline around everything drawn so far."""
        edge = []
        for y in range(self.height):
            for x in range(self.width):
                if self.pixels[y][x][3] != 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    n = self.get(x + dx, y + dy)
                    if n[3] != 0 and n != c:
                        edge.append((x, y))
                        break
        for x, y in edge:
            self.pixels[y][x] = c


def write_strip(path, frames):
    """Writes frames side by side as one RGBA PNG."""
    w, h = frames[0].width, frames[0].height
    rows = []
    for y in range(h):
        row = bytearray([0])  # PNG filter type: none
        for f in frames:
            for x in range(w):
                row.extend(f.pixels[y][x])
        rows.append(bytes(row))

    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", w * len(frames), h, 8, 6, 0, 0, 0)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
           + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


def write_sprite_frames(path, enemy, animations):
    """Writes a Godot SpriteFrames resource that slices each strip."""
    lines = ['[gd_resource type="SpriteFrames" format=3]', ""]
    for i, anim in enumerate(animations):
        lines.append(f'[ext_resource type="Texture2D" path="res://EnemyAssets/{enemy}/{anim["file"]}" id="{i + 1}_{anim["name"]}"]')
    lines.append("")
    for i, anim in enumerate(animations):
        for f in range(anim["count"]):
            lines += [
                f'[sub_resource type="AtlasTexture" id="{anim["name"]}_{f}"]',
                f'atlas = ExtResource("{i + 1}_{anim["name"]}")',
                f'region = Rect2({f * anim["w"]}, 0, {anim["w"]}, {anim["h"]})',
                "",
            ]
    entries = []
    for anim in animations:
        frames = ", ".join(
            f'{{"duration": 1.0, "texture": SubResource("{anim["name"]}_{f}")}}'
            for f in range(anim["count"]))
        entries.append(
            "{\n"
            f'"frames": [{frames}],\n'
            f'"loop": {"true" if anim["loop"] else "false"},\n'
            f'"name": &"{anim["name"]}",\n'
            f'"speed": {anim["fps"]:.1f}\n'
            "}")
    lines += ["[resource]", "animations = [" + ", ".join(entries) + "]", ""]
    with open(path, "w") as f:
        f.write("\n".join(lines))


def export(enemy, animations):
    """animations: list of (name, fps, loop, [Canvas frames])."""
    meta = []
    for name, fps, loop, frames in animations:
        file = f"{enemy.lower()}_{name}.png"
        write_strip(os.path.join(OUT_DIR, enemy, file), frames)
        meta.append({"name": name, "file": file, "fps": fps, "loop": loop,
                     "count": len(frames), "w": frames[0].width, "h": frames[0].height})
    write_sprite_frames(os.path.join(OUT_DIR, enemy, f"{enemy.lower()}_frames.tres"), enemy, meta)


# ---------------------------------------------------------------- shared FX

SMOKE = color("#f4eee6")
SMOKE_SHADE = color("#b9aebf")
STAR = color("#ffd84a")
STAR_CORE = color("#fffbe0")


def draw_poof(c, cx, cy, t, scale):
    """A puff of smoke at time t (0..1) for the death animation."""
    puffs = [(-1.0, 0.2), (1.0, 0.2), (0.0, -0.7), (-0.6, 0.8), (0.6, 0.8), (0.0, 0.1)]
    spread = scale * (0.35 + 0.8 * t)
    radius = scale * (0.3 + 0.35 * math.sin(math.pi * min(1.0, t * 1.6))) * (1.0 - 0.75 * t)
    if radius < 0.8:
        return
    for px, py in puffs:
        c.ellipse(cx + px * spread, cy + py * spread * 0.7, radius, radius,
                  lambda nx, ny: SMOKE_SHADE if ny > 0.35 else SMOKE)
    c.outline(SMOKE_SHADE)


def draw_star(c, x, y):
    for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
        c.set(x + dx, y + dy, STAR)
    c.set(x, y, STAR_CORE)


# ------------------------------------------------------------------ Crawler
# A red-shelled beetle. Canvas 40x24, feet on the bottom row.

CR_W, CR_H = 40, 24
CR_SHELL = color("#c8403e")
CR_SHELL_LIGHT = color("#f07560")
CR_SHELL_DARK = color("#8c2638")
CR_SPOT = color("#5c1630")
CR_BELLY = color("#3d2a4a")
CR_HEAD = color("#4b3560")
CR_HEAD_LIGHT = color("#6d5285")
CR_EYE = color("#ffd84a")
CR_PINCER = color("#f2dcb3")


def crawler_shell(nx, ny):
    if ny > 0.62:
        return CR_SHELL_DARK
    if nx < -0.1 and ny < -0.3 and nx * nx + ny * ny < 0.7:
        return CR_SHELL_LIGHT
    return CR_SHELL


def crawler_head(nx, ny):
    return CR_HEAD_LIGHT if nx < 0.1 and ny < -0.2 else CR_HEAD


def draw_crawler(bob=0, legs=(0, 0, 0), antenna=0, x_eyes=False, squash=0):
    c = Canvas(CR_W, CR_H)
    top = 18 + bob + squash

    # Three legs; each value shifts that foot left/right.
    for lx, shift in zip((10, 17, 24), legs):
        c.line(lx, top + 1, lx + shift, 22, OUTLINE)
        c.set(lx + shift, 23, OUTLINE)
        c.set(lx + shift + 1, 23, OUTLINE)

    c.ellipse(17, top + 1, 12, 2.5, CR_BELLY)
    c.ellipse(17, top, 13, 11 - squash, crawler_shell, max_y=top)
    for sx, sy in ((11, -6), (18, -9), (23, -4), (14, -2)):
        c.rect(sx, top + sy + squash // 2, sx + 1, top + sy + 1 + squash // 2, CR_SPOT)
    c.set(9, top - 7 + squash, color("#ffd6c8"))  # shine

    c.ellipse(31, top - 2, 4.6, 4, crawler_head)
    c.line(31, top - 6, 34, top - 9 + antenna, OUTLINE)
    c.outline()
    c.set(35, top - 10 + antenna, CR_EYE)  # glowing antenna tip

    if x_eyes:
        for dx, dy in ((0, 0), (2, 0), (1, 1), (0, 2), (2, 2)):
            c.set(32 + dx, top - 4 + dy, OUTLINE)
    else:
        c.rect(32, top - 4, 33, top - 3, CR_EYE)
        c.set(33, top - 3, OUTLINE)
    for px, py in ((36, -1), (37, -1), (37, 0), (36, 1), (37, 1)):
        c.set(px, top + py, CR_PINCER)
    c.set(38, top - 1, OUTLINE)
    c.set(38, top + 1, OUTLINE)
    return c


def crawler_animations():
    walk = [
        draw_crawler(bob=0, legs=(-1, 1, -1), antenna=0),
        draw_crawler(bob=-1, legs=(0, 0, 0), antenna=-1),
        draw_crawler(bob=0, legs=(1, -1, 1), antenna=0),
        draw_crawler(bob=-1, legs=(0, 0, 0), antenna=1),
    ]
    death = [draw_crawler(squash=2, x_eyes=True)]
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):
        f = Canvas(CR_W, CR_H)
        draw_poof(f, 20, 15, t, 9)
        death.append(f)
    return [("walk", 8, True, walk), ("death", 12, False, death)]


# ------------------------------------------------------------------ Charger
# A tusked boar. Canvas 56x36, hooves on the bottom row.

CH_W, CH_H = 56, 36
CH_FUR = color("#b9582f")
CH_FUR_LIGHT = color("#df7f45")
CH_FUR_DARK = color("#7c3521")
CH_MANE = color("#4a2219")
CH_SNOUT = color("#eda07e")
CH_SNOUT_DARK = color("#b86a52")
CH_HOOF = color("#2b1a1a")
CH_TUSK = color("#fff1d2")
CH_EYE = color("#ff3b30")
CH_SPEED = color("#f6e8cc", 190)


def charger_fur(nx, ny):
    if ny > 0.55:
        return CH_FUR_DARK
    if ny < -0.45 and nx < 0.4:
        return CH_FUR_LIGHT
    return CH_FUR


def charger_head(nx, ny):
    if nx < -0.72 or ny > 0.6:
        return CH_FUR_DARK
    if ny < -0.4 and nx < 0.3:
        return CH_FUR_LIGHT
    return CH_FUR


def charger_snout(nx, ny):
    return CH_SNOUT_DARK if ny > 0.45 else CH_SNOUT


def draw_charger(dx=0, dy=0, head_dy=0, legs=(0, 0, 0, 0), lifts=(0, 0, 0, 0),
                 eyes="angry", steam=0, stars=None, speed=None):
    c = Canvas(CH_W, CH_H)
    bx, by = 24 + dx, 22 + dy        # body center
    hx, hy = 41 + dx, 21 + dy + head_dy  # head center

    # Legs: back pair then front pair. legs shifts each hoof, lifts raises it.
    for i, (lx, shift, lift) in enumerate(zip((11 + dx, 17 + dx, 30 + dx, 35 + dx), legs, lifts)):
        hoof_y = 35 - lift
        leg = CH_FUR_DARK if i % 2 == 0 else CH_FUR  # far legs darker
        c.rect(lx, 29 + dy, lx + 2, 31 + dy, leg)
        c.rect(lx + shift, 32 + dy - lift, lx + shift + 2, hoof_y - 1, leg)
        c.rect(lx + shift, hoof_y, lx + shift + 2, hoof_y, CH_HOOF)

    c.ellipse(bx, by, 17, 10, charger_fur)

    # Spiky mane along the back.
    for mx in range(bx - 11, bx + 12, 4):
        n = (mx - bx) / 17
        top = by - 10 * math.sqrt(max(0.0, 1 - n * n))
        c.line(mx, top + 2, mx + 1, top - 3, CH_MANE)
        c.line(mx + 1, top - 3, mx + 3, top + 1, CH_MANE)
        c.set(mx + 1, top - 1, CH_MANE)
        c.set(mx + 2, top, CH_MANE)

    # Tail tuft.
    c.line(bx - 17, by - 3, bx - 19, by - 6, CH_MANE)
    c.set(bx - 20, by - 6, CH_MANE)

    # Head, ear, snout.
    c.ellipse(hx, hy, 7.5, 7, charger_head)
    c.line(hx - 4, hy - 6, hx - 3, hy - 10, CH_FUR_DARK)
    c.line(hx - 3, hy - 10, hx, hy - 6, CH_FUR_DARK)
    c.set(hx - 3, hy - 8, CH_SNOUT_DARK)
    c.set(hx - 3, hy - 7, CH_FUR_DARK)
    c.ellipse(hx + 7, hy + 3, 4.2, 3.4, charger_snout)

    # Tusk curling up from the jaw.
    for tx, ty in ((4, 7), (5, 8), (6, 8), (7, 7), (8, 6), (8, 5)):
        c.set(hx + tx, hy + ty, CH_TUSK)

    c.outline()

    c.set(hx + 10, hy + 2, OUTLINE)  # nostril
    c.set(hx + 10, hy + 4, OUTLINE)
    if eyes == "x":
        for ex, ey in ((0, 0), (2, 0), (1, 1), (0, 2), (2, 2)):
            c.set(hx + 2 + ex, hy - 3 + ey, OUTLINE)
    else:
        c.rect(hx + 3, hy - 2, hx + 4, hy - 2, CH_EYE)
        c.set(hx + 4, hy - 1, CH_EYE)
        # Angry brow slanting toward the snout.
        for ex, ey in ((1, -4), (2, -4), (3, -3), (4, -3), (5, -3)):
            c.set(hx + ex, hy + ey, OUTLINE)

    # Snorting steam (wind-up).
    if steam:
        puffs = [(54, hy + 1), (55, hy)] if steam == 1 else [(55, hy - 1), (54, hy - 2), (53, hy - 1)]
        for sx, sy in puffs:
            c.set(sx, sy, SMOKE)

    if stars:
        for sx, sy in stars:
            draw_star(c, sx + dx, sy)

    if speed is not None:
        for row, length in ((by - 5, 6), (by + 1, 8), (by + 6, 5)):
            start = (speed * 2 + row) % 4
            c.line(start, row, start + length, row, CH_SPEED)
    return c


def charger_animations():
    walk = [
        draw_charger(legs=(1, -1, -1, 1)),
        draw_charger(dy=-1, legs=(0, 0, 0, 0), lifts=(0, 1, 1, 0)),
        draw_charger(legs=(-1, 1, 1, -1)),
        draw_charger(dy=-1, legs=(0, 0, 0, 0), lifts=(1, 0, 0, 1)),
    ]
    idle = [
        draw_charger(),
        draw_charger(dy=1, head_dy=-1),
    ]
    windup = [
        draw_charger(dx=-1, head_dy=1, lifts=(0, 0, 0, 2), legs=(0, 0, 0, 1), steam=1),
        draw_charger(dx=-1, head_dy=1, steam=2),
    ]
    charge = [
        draw_charger(dy=1, head_dy=2, legs=(-2, -2, 2, 2), speed=0),
        draw_charger(dy=0, head_dy=2, legs=(0, 0, 0, 0), lifts=(1, 1, 1, 1), speed=1),
        draw_charger(dy=1, head_dy=2, legs=(2, 2, -2, -2), speed=2),
        draw_charger(dy=0, head_dy=2, legs=(0, 0, 0, 0), lifts=(1, 1, 1, 1), speed=3),
    ]
    stunned = [
        draw_charger(dy=1, head_dy=1, eyes="x", stars=[(36, 9), (44, 6), (51, 9)]),
        draw_charger(dy=1, head_dy=1, eyes="x", stars=[(39, 6), (48, 9), (53, 5)]),
    ]
    death = [draw_charger(dy=2, head_dy=1, eyes="x")]
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):
        f = Canvas(CH_W, CH_H)
        draw_poof(f, 28, 22, t, 13)
        death.append(f)
    return [
        ("walk", 8, True, walk),
        ("idle", 3, True, idle),
        ("windup", 8, True, windup),
        ("charge", 14, True, charge),
        ("stunned", 5, True, stunned),
        ("death", 12, False, death),
    ]


if __name__ == "__main__":
    export("Crawler", crawler_animations())
    export("Charger", charger_animations())
    print(f"Wrote enemy sprites to {OUT_DIR}")
