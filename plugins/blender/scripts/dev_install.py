"""Development installation helper for Blender.

Symlinks the addon source into Blender's user extensions directory so edits
are picked up live (after F3 -> "Reload Scripts" or a restart).

Usage:
    Inside Blender:   open this file in the Scripting workspace and run.
    From CLI:         blender --python scripts/dev_install.py
                      python scripts/dev_install.py    # symlink only
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

ADDON_ID = "kexedit"
MIN_VERSION = (4, 2)

try:
    import bpy
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False


def get_addon_source_path() -> Path:
    return Path(__file__).resolve().parent.parent / ADDON_ID


def get_extensions_target() -> Path:
    """Per-user extensions directory for the active Blender version."""
    if IN_BLENDER:
        return Path(bpy.utils.user_resource("EXTENSIONS")) / "user_default"

    base = _user_blender_root()
    version = _pick_blender_version(base)
    if version is None:
        raise RuntimeError(
            f"No Blender {MIN_VERSION[0]}.{MIN_VERSION[1]}+ directory found under {base}. "
            "Run from inside Blender, or install Blender 4.2+."
        )
    return base / version / "extensions" / "user_default"


def _user_blender_root() -> Path:
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", "")
        return Path(appdata) / "Blender Foundation" / "Blender"
    if sys.platform == "darwin":
        return Path.home() / "Library" / "Application Support" / "Blender"
    return Path.home() / ".config" / "blender"


def _pick_blender_version(base: Path) -> str | None:
    if not base.exists():
        return None
    candidates: list[tuple[tuple[int, int], str]] = []
    for entry in base.iterdir():
        if not entry.is_dir():
            continue
        parts = entry.name.split(".")
        try:
            major, minor = int(parts[0]), int(parts[1])
        except (ValueError, IndexError):
            continue
        if (major, minor) >= MIN_VERSION:
            candidates.append(((major, minor), entry.name))
    if not candidates:
        return None
    candidates.sort()
    return candidates[-1][1]


def create_symlink() -> bool:
    source = get_addon_source_path()
    target_dir = get_extensions_target()
    target = target_dir / ADDON_ID

    print(f"Source: {source}")
    print(f"Target: {target}")

    if not source.exists():
        print(f"ERROR: source does not exist: {source}")
        return False

    target_dir.mkdir(parents=True, exist_ok=True)

    if target.is_symlink():
        print("Removing existing symlink...")
        target.unlink()
    elif target.exists():
        print(f"ERROR: {target} exists and is not a symlink. Remove it manually.")
        return False

    try:
        if sys.platform == "win32":
            import subprocess
            result = subprocess.run(
                ["cmd", "/c", "mklink", "/D", str(target), str(source)],
                capture_output=True,
                text=True,
            )
            if result.returncode != 0:
                print(f"mklink failed: {result.stderr.strip()}")
                print("Run an Administrator shell, or enable Windows Developer Mode.")
                return False
        else:
            target.symlink_to(source, target_is_directory=True)
        print(f"Linked {target} -> {source}")
        return True
    except OSError as e:
        print(f"Failed to create symlink: {e}")
        return False


def main() -> None:
    print(f"=== {ADDON_ID} dev install ===\n")
    if create_symlink():
        print("\nDone.")
        print("\nNext:")
        print("  1. Open Blender 4.2+")
        print("  2. Edit > Preferences > Add-ons")
        print(f"  3. Enable '{ADDON_ID}' (User repository)")
        print("  4. View3D > N-panel > kexedit")
        print("\nReload after edits: F3 -> 'Reload Scripts' (or restart Blender).")
    else:
        print("\nFailed. See errors above.")


if __name__ == "__main__":
    main()
