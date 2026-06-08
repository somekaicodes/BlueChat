#!/usr/bin/env python3
"""
Injects dark mode icon variants for ALL icon sizes into the actool-cloned
asset catalog. iOS home screen dark icons require dark variants at each
display size (not just the 1024x1024 marketing size).
Uses macOS `sips` to resize the dark PNG.
"""
import json, sys, os, subprocess

def resize(src, dest, size):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    subprocess.run(
        ["sips", "-z", str(size), str(size), src, "--out", dest],
        capture_output=True)

def inject(cloned_dir, dark_src):
    icon_dir = os.path.join(cloned_dir, "Assets.xcassets", "appicon.appiconset")
    contents_path = os.path.join(icon_dir, "Contents.json")

    if not os.path.exists(contents_path):
        print(f"Skipping (not found): {contents_path}")
        return

    with open(contents_path) as f:
        data = json.load(f)

    # Remove any existing dark entries
    data["images"] = [img for img in data["images"] if "appearances" not in img]

    # All sizes we need dark variants for
    sizes = [
        ("iphone", "20x20", "2x", 40),
        ("iphone", "20x20", "3x", 60),
        ("iphone", "29x29", "2x", 58),
        ("iphone", "29x29", "3x", 87),
        ("iphone", "40x40", "2x", 80),
        ("iphone", "40x40", "3x", 120),
        ("iphone", "60x60", "2x", 120),
        ("iphone", "60x60", "3x", 180),
        ("ipad",   "20x20", "2x", 40),
        ("ipad",   "29x29", "2x", 58),
        ("ipad",   "40x40", "2x", 80),
        ("ipad",   "76x76", "2x", 152),
        ("ipad",   "83.5x83.5", "2x", 167),
        ("ios-marketing", "1024x1024", "1x", 1024),
    ]

    appearance = [{"appearance": "luminosity", "value": "dark"}]

    for idiom, size_str, scale, px in sizes:
        filename = f"appicon_dark_{px}x{px}@{scale}.png"
        dest = os.path.join(icon_dir, filename)
        resize(dark_src, dest, px)

        data["images"].append({
            "appearances": appearance,
            "filename": filename,
            "idiom": idiom,
            "size": size_str,
            "scale": scale
        })

    with open(contents_path, "w") as f:
        json.dump(data, f, indent=2)

    print(f"Injected {len(sizes)} dark icon sizes into {contents_path}")

obj_base = sys.argv[1]
dark_src  = sys.argv[2]

inject(os.path.join(obj_base, "actool/cloned-assets"), dark_src)
inject(os.path.join(obj_base, "resizetizer/r"), dark_src)
