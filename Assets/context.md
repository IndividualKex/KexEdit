# Assets Context

Unity project assets root containing all game resources, scripts, and configurations

## Purpose

- Houses all Unity assets for the KexEdit roller coaster editor
- Contains both runtime package and editor-specific resources
- Manages prefabs, materials, scenes, and UI assets

## Layout

```
Assets/
├── context.md  # This file, folder context (Tier 2)
├── Runtime/  # Core runtime package
│   ├── context.md  # Runtime context
│   ├── Scripts/  # Main codebase
│   ├── Shaders/  # Compute shaders
│   └── Resources/  # Runtime resources
├── Scripts/  # Editor-only scripts
│   └── UI/  # UI layer implementation
├── Tests/  # Test suites
├── Scenes/  # Unity scenes
├── Prefabs/  # Reusable game objects
├── Materials/  # Materials and shaders
├── Textures/  # Texture assets
├── Tracks/  # Track style definitions
└── StandaloneFileBrowser/  # File browser library
```

## Scope

- In-scope: All Unity assets, scripts, resources needed for the editor
- Out-of-scope: Build outputs, user settings, temporary files

## Entrypoints

- Main scene: `Scenes/Main.unity` - Loaded on application start
- Runtime package: `Runtime/package.json` - Core package definition
- UI initialization: `Scripts/UI/UIManager.cs` - UI system bootstrap

## Dependencies

- Unity 6000.0 engine
- Unity DOTS packages (Entities, Physics, etc.)
- Universal Render Pipeline
- TextMeshPro for UI text
- StandaloneFileBrowser for file operations