"""Development installation helper for Blender.

Run this script from Blender's Python console or as a script to set up
development symlinks. This allows live-editing of addon code.

Usage in Blender:
    1. Open Blender
    2. Go to Scripting workspace
    3. Open this file and run it

Or from command line:
    blender --python scripts/dev_install.py
"""

import os
import sys
from pathlib import Path

# Detect if running in Blender
try:
    import bpy
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False


def get_addon_source_path() -> Path:
    """Get the path to the kexengine addon source."""
    # This script is in scripts/, addon is in kexengine/
    return Path(__file__).parent.parent / "kexengine"


def get_blender_addons_path() -> Path:
    """Get Blender's user addons path."""
    if IN_BLENDER:
        return Path(bpy.utils.user_resource('SCRIPTS')) / "addons"

    # Fallback for non-Blender environments
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", "")
        return Path(appdata) / "Blender" / "5.0" / "scripts" / "addons"
    elif sys.platform == "darwin":
        return Path.home() / "Library" / "Application Support" / "Blender" / "5.0" / "scripts" / "addons"
    else:
        return Path.home() / ".config" / "blender" / "5.0" / "scripts" / "addons"


def create_symlink():
    """Create symlink from Blender addons to source."""
    source = get_addon_source_path()
    target = get_blender_addons_path() / "kexengine"

    print(f"Source: {source}")
    print(f"Target: {target}")

    if not source.exists():
        print(f"ERROR: Source path does not exist: {source}")
        return False

    # Create addons directory if needed
    target.parent.mkdir(parents=True, exist_ok=True)

    if target.exists():
        if target.is_symlink():
            print(f"Symlink already exists, removing...")
            target.unlink()
        else:
            print(f"ERROR: {target} exists and is not a symlink")
            print("Please remove it manually first")
            return False

    # Create symlink
    try:
        if sys.platform == "win32":
            # Windows requires special handling
            import subprocess
            result = subprocess.run(
                ["cmd", "/c", "mklink", "/D", str(target), str(source)],
                capture_output=True,
                text=True
            )
            if result.returncode != 0:
                print(f"mklink failed: {result.stderr}")
                print("Try running as Administrator")
                return False
        else:
            target.symlink_to(source)

        print(f"Created symlink: {target} -> {source}")
        return True

    except OSError as e:
        print(f"Failed to create symlink: {e}")
        if sys.platform == "win32":
            print("On Windows, try running as Administrator")
        return False


def main():
    print("=== kexengine Development Install ===\n")

    if create_symlink():
        print("\nInstallation complete!")
        print("\nNext steps:")
        print("1. Open Blender")
        print("2. Edit > Preferences > Add-ons")
        print("3. Search for 'kexengine' and enable it")
        print("4. Find the panel in View3D > Sidebar > kexengine")
        print("\nTo reload after code changes:")
        print("  - Press F3 and search 'Reload Scripts'")
        print("  - Or restart Blender")
    else:
        print("\nInstallation failed. See errors above.")


if __name__ == "__main__":
    main()
