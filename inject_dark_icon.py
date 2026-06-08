#!/usr/bin/env python3
"""
Injects the dark mode icon into BOTH the resizetizer and actool-cloned
asset catalog paths that MAUI generates for iOS builds.
"""
import json, sys, shutil, os

def inject(contents_path, dark_src):
    if not os.path.exists(contents_path):
        print(f"Skipping (not found): {contents_path}")
        return

    icon_dir = os.path.dirname(contents_path)
    dark_dest_name = "appicon_dark_1024.png"
    dark_dest = os.path.join(icon_dir, dark_dest_name)

    shutil.copy2(dark_src, dark_dest)
    print(f"Copied dark icon to {dark_dest}")

    with open(contents_path) as f:
        data = json.load(f)

    # Remove stale dark entries
    data["images"] = [img for img in data.get("images", []) if "appearances" not in img]

    # Inject dark 1024x1024
    data["images"].append({
        "appearances": [{"appearance": "luminosity", "value": "dark"}],
        "filename": dark_dest_name,
        "idiom": "ios-marketing",
        "size": "1024x1024",
        "scale": "1x"
    })

    with open(contents_path, "w") as f:
        json.dump(data, f, indent=2)

    print(f"Dark entry injected into {contents_path}")

dark_src    = sys.argv[2]
obj_base    = sys.argv[1]

# Inject into both the resizetizer source and the actool-cloned copy
inject(os.path.join(obj_base, "resizetizer/r/Assets.xcassets/appicon.appiconset/Contents.json"), dark_src)
inject(os.path.join(obj_base, "actool/cloned-assets/Assets.xcassets/appicon.appiconset/Contents.json"), dark_src)
