# Installation Guide

Get KexEdit installed and running on your system.

## Download

Download the latest version:

-   **[itch.io](https://individualkex.itch.io/kexedit)** - Primary download, optional [itch.io app](https://itch.io/app) for automatic updates
-   **[GitHub Releases](https://github.com/IndividualKex/KexEdit/releases)** - Alternative with version history

## Installation

### Windows

1. Download `kexedit-windows.zip`
2. Extract to a folder
3. Run `KexEdit.exe`

### Linux

1. Download `kexedit-linux.zip`
2. Extract to a folder
3. Make executable: `chmod +x KexEdit.x86_64`
4. Run `./KexEdit.x86_64`

### macOS (Apple Silicon only)

1. Download `kexedit-macos.zip`
2. Extract the archive
3. Remove quarantine attribute:
    ```bash
    xattr -d com.apple.quarantine /path/to/KexEdit.app
    ```
    _Tip: Drag the .app file into Terminal to auto-fill the path_
4. Run `KexEdit.app`

**Note**: Apple Silicon only. Intel Macs not supported.

## System Requirements

-   **Graphics**: Modern GPU with compute shader support
-   **Storage**: 100MB available space
-   **OS**: Windows 11, macOS 12+ (Apple Silicon), or Ubuntu 22.04+

## First Launch

Three main areas:

1. **Node Graph** (left) - Build track layout
2. **Game View** (center) - 3D preview
3. **Timeline** (bottom) - Animate properties

## Next Steps

-   **[Basic Workflow](user-guide/basic-workflow.md)** - Essential building process
-   **[Video Introduction](https://youtu.be/RRIkHtnoP18)** - Feature overview
-   **[Join Discord](https://discord.gg/eEY75Nqk3C)** - Community help

## Troubleshooting

**App won't start on macOS**: Remove quarantine attribute (step 3 above)

For help, visit [Discord](https://discord.gg/eEY75Nqk3C) or [GitHub Issues](https://github.com/IndividualKex/KexEdit/issues).

---

**Next**: [Basic Workflow](user-guide/basic-workflow.md)

---

[← Back to Documentation](../)
