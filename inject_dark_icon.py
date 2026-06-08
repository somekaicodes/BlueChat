#!/usr/bin/env python3
"""
Injects the dark mode icon entry into MAUI's generated iOS icon Contents.json.
Called as a custom MSBuild target after _ResizeTizerRun.
Usage: python3 inject_dark_icon.py <path-to-Contents.json> <dark-icon-source.png>
"""
import json, sys, shutil, os

contents_path = sys.argv[1]
dark_src = sys.argv[2]
icon_dir = os.path.dirname(contents_path)
dark_dest_name = "appicon_dark_1024.png"
dark_dest = os.path.join(icon_dir, dark_dest_name)

# Copy dark icon into the appiconset folder
shutil.copy2(dark_src, dark_dest)
print(f"Copied dark icon to {dark_dest}")

with open(contents_path) as f:
    data = json.load(f)

# Remove any existing dark entries to avoid duplicates
data["images"] = [img for img in data.get("images", []) if "appearances" not in img]

# Add dark 1024x1024 entry (ios-marketing = App Store / home screen icon)
data["images"].append({
    "appearances": [{"appearance": "luminosity", "value": "dark"}],
    "filename": dark_dest_name,
    "idiom": "ios-marketing",
    "size": "1024x1024",
    "scale": "1x"
})

with open(contents_path, "w") as f:
    json.dump(data, f, indent=2)

print(f"Dark mode icon entry injected into {contents_path}")
