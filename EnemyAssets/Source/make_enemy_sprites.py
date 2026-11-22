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

# Each art pixel is written as a 2x2 block to match the player, whose
# scene is scaled 2x. Sizes in this file are in art pixels.
PIXEL_SCALE = 2


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

    def scaled(self, k):
        """A copy with every pixel blown up to a k x k block."""
        big = Canvas(self.width * k, self.height * k)
        big.pixels = [[p for p in row for _ in range(k)] for row in self.pixels for _ in range(k)]
        return big

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


def export(enemy, animations, prefix=None):
    """animations: list of (name, fps, loop, [Canvas frames]).
    prefix names the files (default: the enemy), so one enemy folder
    can hold more than one SpriteFrames, e.g. a boss and its effects."""
    prefix = prefix or enemy.lower()
    meta = []
    for name, fps, loop, frames in animations:
        frames = [f.scaled(PIXEL_SCALE) for f in frames]
        file = f"{prefix}_{name}.png"
        write_strip(os.path.join(OUT_DIR, enemy, file), frames)
        meta.append({"name": name, "file": file, "fps": fps, "loop": loop,
                     "count": len(frames), "w": frames[0].width, "h": frames[0].height})
    write_sprite_frames(os.path.join(OUT_DIR, enemy, f"{prefix}_frames.tres"), enemy, meta)


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
CR_SHELL_SHADE = color("#a6313f")
CR_SHELL_DARK = color("#7e2236")
CR_SHINE = color("#ffe2d6")
CR_SPOT = color("#4a1229")
CR_BELLY = color("#3d2a4a")
CR_PLATE = color("#35244a")
CR_PLATE_LIGHT = color("#55407a")
CR_HEAD = color("#4b3560")
CR_HEAD_LIGHT = color("#6d5285")
CR_LEG = color("#2e1d3a")
CR_LEG_LIGHT = color("#5d4678")
CR_EYE = color("#ffd84a")
CR_PINCER = color("#f2dcb3")


def crawler_shell(nx, ny):
    if ny > 0.7:
        return CR_SHELL_DARK
    if ny > 0.45 or nx > 0.82:
        return CR_SHELL_SHADE
    if (nx + 0.35) ** 2 + 2 * (ny + 0.45) ** 2 < 0.13:
        return CR_SHELL_LIGHT
    return CR_SHELL


def crawler_head(nx, ny):
    return CR_HEAD_LIGHT if (nx + 0.2) ** 2 + (ny + 0.4) ** 2 < 0.25 else CR_HEAD


def draw_crawler(bob=0, legs=(0, 0, 0), antenna=0, x_eyes=False, squash=0, alert=False):
    c = Canvas(CR_W, CR_H)
    top = 18 + bob + squash

    c.ellipse(17, top + 1, 12, 2.5, CR_BELLY)
    c.ellipse(17, top, 13, 11 - squash, crawler_shell, max_y=top)

    # The dark plate behind the head, like a ladybug's.
    for y in range(top - 9 + squash, top + 1):
        for x in range(25, 31):
            if c.get(x, y)[3] != 0:
                c.set(x, y, CR_PLATE_LIGHT if y < top - 5 and x < 28 else CR_PLATE)
    c.set(27, top - 4, CR_SHINE)

    for sx, sy in ((11, -6), (18, -9), (21, -4), (14, -2), (7, -3)):
        c.rect(sx, top + sy + squash // 2, sx + 1, top + sy + 1 + squash // 2, CR_SPOT)
    c.set(8, top - 7 + squash, CR_SHINE)  # shine along the top of the shell
    c.set(9, top - 8 + squash, CR_SHINE)
    c.set(11, top - 9 + squash, CR_SHINE)

    c.ellipse(32, top - 2, 4.6, 4, crawler_head)
    c.line(32, top - 6, 33, top - 8 + antenna, OUTLINE)
    c.line(33, top - 8 + antenna, 35, top - 9 + antenna, OUTLINE)
    c.outline()
    c.set(36, top - 9 + antenna, CR_EYE)  # glowing antenna tip

    # Jointed legs: up to a knee, then down to the foot.
    for lx, shift in zip((10, 17, 24), legs):
        knee_x = lx + shift - 1
        c.line(lx, top + 1, knee_x, top + 2, CR_LEG)
        c.line(knee_x, top + 2, lx + shift, 22, CR_LEG)
        c.set(knee_x, top + 2, CR_LEG_LIGHT)
        c.set(lx + shift, 23, CR_LEG)
        c.set(lx + shift + 1, 23, CR_LEG)

    if x_eyes:
        for dx, dy in ((0, 0), (2, 0), (1, 1), (0, 2), (2, 2)):
            c.set(33 + dx, top - 5 + dy, OUTLINE)
    else:
        c.rect(33, top - 5, 34, top - 3, CR_EYE)
        c.set(34, top - 4, OUTLINE)  # pupil
        c.set(33, top - 5, CR_SHINE)
    for px, py in ((37, -1), (38, -1), (38, 0), (37, 1), (38, 1)):
        c.set(px, top + py, CR_PINCER)
    c.set(39, top - 1, OUTLINE)
    c.set(39, top + 1, OUTLINE)

    if alert:
        # A "!" above the head when it spots the player.
        c.rect(28, 0, 29, 3, CR_EYE)
        c.rect(28, 5, 29, 6, CR_EYE)
        for x, y in ((27, 0), (27, 1), (27, 2), (27, 3), (30, 0), (30, 1), (30, 2), (30, 3),
                     (28, 4), (29, 4), (27, 5), (27, 6), (30, 5), (30, 6), (28, 7), (29, 7)):
            c.set(x, y, OUTLINE)
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
    # Spotting the player: hops up with a "!".
    alert = [draw_crawler(bob=-3, legs=(0, 0, 0), antenna=-2, alert=True),
             draw_crawler(bob=-2, legs=(0, 0, 0), antenna=-1, alert=True)]
    # Crouches low before pouncing.
    windup = [draw_crawler(bob=1, legs=(-2, 0, 2), antenna=1),
              draw_crawler(bob=2, legs=(-2, 0, 2), antenna=2)]
    # Mid-air with the legs tucked back.
    pounce = [draw_crawler(bob=-3, legs=(-2, -2, -1), antenna=2),
              draw_crawler(bob=-3, legs=(-1, -2, -2), antenna=1)]
    return [("walk", 8, True, walk), ("run", 16, True, walk), ("alert", 8, True, alert),
            ("windup", 10, True, windup), ("pounce", 10, True, pounce),
            ("death", 12, False, death)]


# ------------------------------------------------------------------ Charger
# A tusked boar. Canvas 56x36, hooves on the bottom row.

CH_W, CH_H = 56, 36
CH_FUR = color("#b9582f")
CH_FUR_LIGHT = color("#df7f45")
CH_FUR_SHADE = color("#9a4527")
CH_FUR_DARK = color("#7c3521")
CH_MANE = color("#4a2219")
CH_SNOUT = color("#eda07e")
CH_SNOUT_DARK = color("#b86a52")
CH_HOOF = color("#2b1a1a")
CH_TUSK = color("#fff1d2")
CH_EYE = color("#ff3b30")
CH_GLINT = color("#fff4e8")
CH_EAR_INNER = color("#d98a7a")
CH_SPEED = color("#f6e8cc", 190)


def charger_fur(nx, ny):
    if ny > 0.62:
        return CH_FUR_DARK
    if ny > 0.35:
        return CH_FUR_SHADE
    if (nx + 0.15) ** 2 + 2.5 * (ny + 0.5) ** 2 < 0.2:
        return CH_FUR_LIGHT
    return CH_FUR


def charger_head(nx, ny):
    if nx < -0.72 or ny > 0.6:
        return CH_FUR_DARK
    if ny > 0.3:
        return CH_FUR_SHADE
    if (nx + 0.1) ** 2 + 2 * (ny + 0.45) ** 2 < 0.25:
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
        far = i % 2 == 0  # far legs darker
        c.rect(lx, 29 + dy, lx + 2, 31 + dy, CH_FUR_DARK if far else CH_FUR_SHADE)
        c.rect(lx + shift, 32 + dy - lift, lx + shift + 2, hoof_y - 1, CH_FUR_DARK)
        c.rect(lx + shift, hoof_y, lx + shift + 2, hoof_y, CH_HOOF)
        c.set(lx + shift + 1, hoof_y, CH_MANE)  # split hoof

    c.ellipse(bx, by, 17, 10, charger_fur)

    # Fur strokes and a shaggy belly fringe.
    for fx, fy in ((-9, -2), (-4, 1), (1, -3), (6, 0), (-12, 3), (3, 4), (10, -4)):
        c.set(bx + fx, by + fy, CH_FUR_SHADE)
        c.set(bx + fx + 1, by + fy - 1, CH_FUR_SHADE)
    for fx in range(-12, 10, 3):
        c.set(bx + fx, by + 10, CH_FUR_DARK)

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
    for row in range(5):  # ear: a triangle with a pink inside
        c.rect(hx - 4 + row // 2, hy - 10 + row, hx - 2 + row // 2 + (1 if row > 1 else 0), hy - 10 + row, CH_FUR_DARK)
    c.set(hx - 3, hy - 8, CH_EAR_INNER)
    c.set(hx - 2, hy - 7, CH_EAR_INNER)
    c.set(hx - 2, hy - 8, CH_EAR_INNER)
    c.ellipse(hx + 7, hy + 3, 4.2, 3.4, charger_snout)

    # Tusk curling up from the jaw.
    for tx, ty in ((4, 7), (5, 8), (6, 8), (7, 7), (8, 6), (8, 5), (9, 4), (5, 7), (6, 7)):
        c.set(hx + tx, hy + ty, CH_TUSK)
    c.set(hx + 8, hy + 2, CH_GLINT)  # shine on the snout

    c.outline()

    c.set(hx + 10, hy + 2, OUTLINE)  # nostril
    c.set(hx + 10, hy + 4, OUTLINE)
    if eyes == "x":
        for ex, ey in ((0, 0), (2, 0), (1, 1), (0, 2), (2, 2)):
            c.set(hx + 2 + ex, hy - 3 + ey, OUTLINE)
    else:
        c.rect(hx + 2, hy - 3, hx + 5, hy, OUTLINE)  # eye socket
        c.rect(hx + 3, hy - 2, hx + 4, hy - 1, CH_EYE)
        c.set(hx + 3, hy - 2, CH_GLINT)
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


# --------------------------------------------------------- Giant Slime King
# A boss slime about three times the player's height, wearing a crown.
# Canvas 136x104, the slime's base on the bottom row. Phase 1 is purple;
# the enraged phase 2 uses the red palette.

SK_W, SK_H = 136, 104
SK_BASE = SK_H - 1
SK_NORMAL = {
    "body": color("#7c5ce0"), "light": color("#a98cff"), "shade": color("#5d42bf"),
    "dark": color("#432c8f"), "gloss": color("#ebe3ff"), "bubble": color("#9d84f5"),
}
SK_RAGE = {
    "body": color("#dd4459"), "light": color("#ff8b8f"), "shade": color("#b42f49"),
    "dark": color("#7d1834"), "gloss": color("#ffe3e3"), "bubble": color("#f2707f"),
}
SK_GOLD = color("#ffcf3f")
SK_GOLD_DARK = color("#c98a1a")
SK_GEM = color("#ff3b6b")
SK_EYE = color("#fbf7ff")
SK_MOUTH = color("#2a1430")
SK_TONGUE = color("#ff7aa8")
SK_STEAM = color("#f4eee6", 220)


def draw_crown(c, cx, base_y, tilt):
    """A 36px gold crown whose band sits at base_y; tilt skews the points."""
    left, right = cx - 18, cx + 18
    c.rect(left, base_y - 5, right, base_y, SK_GOLD)
    c.rect(left, base_y, right, base_y, SK_GOLD_DARK)
    for px, tall in ((left, 11), (cx - 9, 7), (cx - 1, 13), (cx + 8, 7), (right - 2, 11)):
        for row in range(tall):
            shift = round(tilt * row / 10)
            width = 3 if row < tall - 2 else 1
            c.rect(px + shift, base_y - 5 - row, px + shift + width - 1, base_y - 5 - row, SK_GOLD)
    c.rect(cx - 1, base_y - 4, cx + 1, base_y - 2, SK_GEM)
    c.set(left + 6, base_y - 3, SK_GEM)
    c.set(right - 6, base_y - 3, SK_GEM)


def draw_slime_king(w=96, h=64, lift=0, dx=0, pal=SK_NORMAL, eyes="normal", mouth="smile",
                    look=(1, 0), crown_tilt=0, stars=None, steam=False):
    c = Canvas(SK_W, SK_H)
    cx = SK_W // 2 + dx
    base = SK_BASE - lift
    top = base - h
    dome = h * 0.62           # the rounded top part of the body
    cy = top + dome
    half_w = w / 2

    # Body: a rounded dome that flares into a gooey base.
    for y in range(int(top), base + 1):
        if y <= cy:
            t = (cy - y) / dome
            half = half_w * math.sqrt(max(0.0, 1 - t * t))
        else:
            t = (y - cy) / max(1, base - cy)
            half = half_w * (1 + 0.06 * t * t)
        if y >= base - 1:
            half += 2
        for x in range(int(cx - half), int(cx + half) + 1):
            u = (x - cx) / max(half, 1)  # -1..1 across this row
            v = (y - top) / h            # 0 at the top, 1 at the base
            if y >= base - 2:
                shade = pal["dark"]
            elif u > 0.78 or v > 0.86 or (u < -0.9 and v > 0.4):
                shade = pal["shade"]
            elif (u + 0.38) ** 2 + 2.2 * (v - 0.3) ** 2 < 0.11:
                shade = pal["light"]
            else:
                shade = pal["body"]
            c.set(x, y, shade)

    # Bubbles and a glossy shine.
    for bx, by, r in ((0.24, 0.32, 2.2), (-0.12, 0.2, 1.5), (0.05, 0.48, 1.2), (-0.32, 0.38, 1.0)):
        c.ellipse(cx + bx * w, base - by * h, r, r, pal["bubble"])
    c.ellipse(cx - w * 0.22, top + h * 0.2, 5, 2.6, pal["gloss"])
    c.set(cx - w * 0.31, top + h * 0.3, pal["gloss"])

    draw_crown(c, cx - 2, int(top) + 4, crown_tilt)
    c.outline()

    # Face, toward the right (the facing side).
    ey = int(base - h * 0.55)
    eye_ry = 3.5 if eyes == "squint" else 6
    for ex in (int(cx + w * 0.03), int(cx + w * 0.27)):
        if eyes in ("x", "dazed"):
            if eyes == "x":
                for k in range(-2, 3):
                    c.set(ex + k, ey + k, SK_MOUTH)
                    c.set(ex + k, ey - k, SK_MOUTH)
            else:
                c.ellipse(ex, ey, 3.4, 3.4, SK_MOUTH)
                c.ellipse(ex, ey, 2.2, 2.2, SK_EYE)
                c.set(ex, ey, SK_MOUTH)
                c.set(ex + 1, ey - 1, SK_MOUTH)
            continue
        c.ellipse(ex, ey, 5.4, eye_ry + 0.8, SK_MOUTH)
        c.ellipse(ex, ey, 4.3, eye_ry - 0.3, SK_EYE)
        c.rect(ex + look[0], ey - 1 + look[1], ex + 2 + look[0], ey + 2 + look[1], SK_MOUTH)
        c.set(ex + look[0], ey - 1 + look[1], SK_EYE)  # glint
        if eyes in ("angry", "squint"):
            inner = 1 if ex > cx + w * 0.15 else -1  # brows slope down toward the middle
            for k in range(-5, 6):
                yy = int(ey - eye_ry - 3 + round(k * inner * -0.35))
                c.set(ex + k, yy, SK_MOUTH)
                c.set(ex + k, yy - 1, SK_MOUTH)

    mx0, mx1, my = int(cx - w * 0.01), int(cx + w * 0.33), int(ey + eye_ry + 5)
    if mouth == "smile":
        c.line(mx0, my, mx0 + 3, my + 2, SK_MOUTH)
        c.line(mx0 + 3, my + 2, mx1 - 3, my + 2, SK_MOUTH)
        c.line(mx1 - 3, my + 2, mx1, my, SK_MOUTH)
    elif mouth == "grit":
        c.rect(mx0 + 1, my, mx1 - 1, my + 4, SK_MOUTH)
        c.rect(mx0 + 2, my + 1, mx1 - 2, my + 1, SK_EYE)
        for tx in range(mx0 + 4, mx1 - 1, 4):
            c.set(tx, my + 2, SK_EYE)
    elif mouth in ("open", "pant"):
        c.ellipse((mx0 + mx1) / 2, my + 3, (mx1 - mx0) / 2, 4.5 if mouth == "open" else 3.2, SK_MOUTH)
        c.rect(mx0 + 3, my, mx1 - 3, my, SK_EYE)
        tongue_y = my + (5 if mouth == "open" else 4)
        c.rect((mx0 + mx1) // 2 - 2, tongue_y - 1, (mx0 + mx1) // 2 + 2, tongue_y + (2 if mouth == "pant" else 0), SK_TONGUE)

    if stars:
        for sx, sy in stars:
            x, y = cx + sx, int(top) + sy - 10
            for k in (1, 2):  # a bigger star than the small enemies use
                for dx, dy in ((0, -k), (-k, 0), (k, 0), (0, k)):
                    c.set(x + dx, y + dy, STAR)
            c.set(x, y, STAR_CORE)
    if steam:
        for sx, sy in ((-26, 2), (-22, -3), (24, 0), (28, -4), (-24, -7), (26, -9)):
            if (sx + sy + steam) % 2 == 0:
                c.set(cx + sx, int(top) + 10 + sy, SK_STEAM)
                c.set(cx + sx + 1, int(top) + 9 + sy, SK_STEAM)
    return c


def slime_king_animations():
    def phase(pal, face, grin):
        """The animations shared by both phases, in one palette."""
        idle = [draw_slime_king(w, h, pal=pal, eyes=face, mouth=grin, crown_tilt=t)
                for w, h, t in ((96, 64, 0), (99, 61, 1), (96, 64, 0), (93, 67, -1))]
        walk = [draw_slime_king(w, h, lift, pal=pal, eyes=face, mouth=grin, crown_tilt=t)
                for w, h, lift, t in ((106, 54, 0, 0), (96, 64, 0, -1), (86, 74, 4, -2),
                                      (92, 68, 9, 0), (88, 72, 5, 2), (104, 56, 0, 1))]
        windup = [draw_slime_king(w, h, pal=pal, eyes="squint", mouth="grit", crown_tilt=t)
                  for w, h, t in ((104, 56, 1), (112, 50, 2), (118, 45, 3))]
        jump = [draw_slime_king(w, h, pal=pal, eyes="angry", mouth="open", crown_tilt=t)
                for w, h, t in ((80, 78, -4), (84, 75, -3))]
        hang = [draw_slime_king(w, h, pal=pal, eyes="angry", mouth="grit", look=(0, 2), crown_tilt=t)
                for w, h, t in ((100, 62, 0), (102, 60, 1), (100, 62, 0), (98, 64, -1))]
        fall = [draw_slime_king(w, h, pal=pal, eyes="angry", mouth="open", look=(0, 2), crown_tilt=t)
                for w, h, t in ((80, 80, 3), (78, 82, 4))]
        land = [draw_slime_king(w, h, pal=pal, eyes=e, mouth=m, crown_tilt=t)
                for w, h, e, m, t in ((130, 34, "squint", "open", 4), (118, 44, "squint", "grit", 2),
                                      (100, 60, face, grin, -1), (96, 64, face, grin, 0))]
        recover = [draw_slime_king(w, h, pal=pal, eyes="dazed", mouth="pant", crown_tilt=t, stars=s)
                   for w, h, t, s in ((100, 58, 3, [(-18, -6), (2, -12), (20, -5)]),
                                      (102, 56, 4, [(-12, -11), (10, -9), (22, -12)]),
                                      (100, 58, 3, [(-20, -10), (-2, -6), (16, -11)]),
                                      (98, 60, 2, [(-14, -5), (6, -12), (24, -7)]))]
        # Sucks in a breath, then leans forward and spits.
        inhale = [draw_slime_king(w, h, dx=-2, pal=pal, eyes="squint", mouth="grit", look=(2, 0), crown_tilt=t)
                  for w, h, t in ((90, 70, -2), (86, 74, -3))]
        spit = [draw_slime_king(w, h, dx=dx, pal=pal, eyes="angry", mouth="open", look=(2, 0), crown_tilt=t)
                for w, h, dx, t in ((110, 56, 3, 3), (104, 60, 2, 2), (98, 63, 1, 1))]
        return idle, walk, windup, jump, hang, fall, land, recover, (inhale, spit)

    idle, walk, windup, jump, hang, fall, land, recover, spit = phase(SK_NORMAL, "normal", "smile")
    r_idle, r_walk, r_windup, r_jump, r_hang, r_fall, r_land, r_recover, r_spit = phase(SK_RAGE, "angry", "grit")

    enrage = [draw_slime_king(w, h, dx=dx, pal=SK_RAGE if i > 1 else SK_NORMAL, eyes="angry",
                              mouth="open", crown_tilt=dx, steam=i + 1)
              for i, (w, h, dx) in enumerate(((100, 64, -2), (106, 60, 2), (100, 68, -2),
                                              (108, 60, 2), (100, 68, -2), (106, 62, 2)))]

    death = [draw_slime_king(w, h, pal=SK_NORMAL, eyes="x", mouth="pant", crown_tilt=t)
             for w, h, t in ((104, 54, 3), (118, 40, 5), (130, 26, 6))]
    for t in (0.0, 0.25, 0.5, 0.75, 1.0):
        f = Canvas(SK_W, SK_H)
        draw_poof(f, SK_W // 2, SK_BASE - 18, t, 30)
        death.append(f)

    return [
        ("idle", 6, True, idle), ("walk", 10, True, walk), ("windup", 8, False, windup),
        ("jump", 8, True, jump), ("hang", 10, True, hang), ("fall", 10, True, fall),
        ("land", 14, False, land), ("recover", 5, True, recover),
        ("idle_rage", 8, True, r_idle), ("walk_rage", 14, True, r_walk),
        ("windup_rage", 12, False, r_windup), ("jump_rage", 8, True, r_jump),
        ("hang_rage", 12, True, r_hang), ("fall_rage", 10, True, r_fall),
        ("land_rage", 16, False, r_land), ("recover_rage", 7, True, r_recover),
        ("inhale", 6, True, spit[0]), ("spit", 8, False, spit[1]),
        ("inhale_rage", 9, True, r_spit[0]), ("spit_rage", 10, False, r_spit[1]),
        ("enrage", 10, True, enrage), ("death", 10, False, death),
    ]


# The slime globs the Slime King spits. Ball canvas 12x12, splat 20x10.
SB_W, SB_H = 12, 12
SP_W, SP_H = 20, 10


def draw_slime_ball(pal, w, h):
    c = Canvas(SB_W, SB_H)
    cx, cy = SB_W / 2 - 0.5, SB_H / 2 - 0.5

    def fill(nx, ny):
        if (nx + 0.35) ** 2 + (ny + 0.4) ** 2 < 0.12:
            return pal["gloss"]
        if ny > 0.45 or nx > 0.6:
            return pal["shade"]
        return pal["body"]

    c.ellipse(cx, cy, w, h, fill)
    c.outline()
    return c


def draw_slime_splat(pal, t):
    c = Canvas(SP_W, SP_H)
    spread = 4 + 5 * t
    height = 3.5 * (1 - t) + 1
    c.ellipse(SP_W / 2 - 0.5, SP_H - 2, spread, height,
              lambda nx, ny: pal["shade"] if ny > 0.2 else pal["body"])
    for side in (-1, 1):
        # Droplets flying off to the sides.
        x = SP_W / 2 + side * (spread + 1 + 2 * t)
        y = SP_H - 4 - 3 * (1 - t) + 4 * t * t
        if t < 0.9:
            c.rect(int(x), int(y), int(x), int(y), pal["light"])
    c.outline()
    return c


def slime_ball_animations():
    anims = []
    for suffix, pal in (("", SK_NORMAL), ("_rage", SK_RAGE)):
        fly = [draw_slime_ball(pal, w, h) for w, h in ((4.6, 4.6), (5.2, 4.0), (4.6, 4.6), (4.0, 5.2))]
        splat = [draw_slime_splat(pal, t) for t in (0.0, 0.3, 0.6, 0.9)]
        anims += [("fly" + suffix, 10, True, fly), ("splat" + suffix, 14, False, splat)]
    return anims


# The ground shockwave the Slime King makes when it lands.
SW_W, SW_H = 260, 30
SW_DUST = color("#ece2cf")
SW_DUST_SHADE = color("#b9a98f")
SW_CRACK = color("#2a1a2e")


def shockwave_animations():
    frames = []
    for i, t in enumerate((0.0, 0.2, 0.4, 0.6, 0.8, 1.0)):
        c = Canvas(SW_W, SW_H)
        reach = 22 + 104 * t
        size = 11 * (1 - 0.65 * t) + 2
        for side in (-1, 1):
            x = SW_W / 2 + side * reach
            c.ellipse(x, SW_H - 1 - size * 0.6, size * 1.6, size,
                      lambda nx, ny: SW_DUST_SHADE if ny > 0.3 else SW_DUST)
            c.ellipse(x - side * size * 1.4, SW_H - 1 - size * 0.4, size, size * 0.6, SW_DUST)
        c.outline(SW_DUST_SHADE)
        if i < 4:
            # Cracks in the ground under the impact.
            for side in (-1, 1):
                x, y = SW_W // 2, SW_H - 1
                for step in range(5 - i):
                    nx = x + side * 6
                    ny = SW_H - 1 - (step % 2)
                    c.line(x, y, nx, ny, SW_CRACK)
                    x, y = nx, ny
        frames.append(c)
    return [("burst", 16, False, frames)]


if __name__ == "__main__":
    export("Crawler", crawler_animations())
    export("Charger", charger_animations())
    export("SlimeKing", slime_king_animations())
    export("SlimeKing", shockwave_animations(), prefix="shockwave")
    export("SlimeKing", slime_ball_animations(), prefix="slimeball")
    print(f"Wrote enemy sprites to {OUT_DIR}")
