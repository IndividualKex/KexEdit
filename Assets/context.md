# Assets Context

Unity project assets root containing all game resources, scripts, and configurations.

## Layout

```
Assets/
├── Runtime/    # Hexagonal architecture (Core, Nodes, Adapters, Legacy)
├── Scripts/    # Editor-only scripts (UI layer)
├── Tests/      # Test suites
├── Scenes/     # Unity scenes
├── Prefabs/    # Reusable game objects
└── Materials/  # Materials and shaders
```

## Entrypoints

- Main scene: `Scenes/Main.unity`
- Runtime package: `Runtime/package.json`
- UI initialization: `Scripts/UI/UIManager.cs`

## Dependencies

- Unity 6000.3 engine
- Unity DOTS packages (Entities, Physics, etc.)
- Universal Render Pipeline
