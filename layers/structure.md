# Project Structure

Advanced Unity-based roller coaster editor using Force Vector Design (FVD) with high-performance track computation

## Stack

- Runtime: Unity 6000.2.0f1
- Language: C# (.NET Standard 2.1)
- Framework: Unity DOTS (ECS, Burst, Jobs System)
- Build: Unity Build System
- Rendering: Universal Render Pipeline (URP)
- UI: UI Toolkit (Unity's modern UI system)
- Testing: Unity Test Framework

## Commands

- Dev: Open project in Unity Editor
- Build: File → Build Settings → Build (Windows/Mac/Linux)
- Test: Window → General → Test Runner
- Play: Unity Editor Play Mode (Ctrl/Cmd+P)

**Do not try to run code from the command line. Wait for the Unity Editor.**

## Layout

```
KexEdit/
├── CLAUDE.md  # Global context (Tier 0)
├── Assets/  # Unity assets root
│   ├── Runtime/  # Core runtime package
│   │   ├── Scripts/  # Main codebase (organized by feature)
│   │   │   ├── Core/  # Foundation & utilities
│   │   │   ├── Track/  # Track construction & graph
│   │   │   ├── Trains/  # Vehicle systems
│   │   │   ├── Physics/  # Simulation & dynamics
│   │   │   ├── Visualization/  # Rendering & visuals
│   │   │   ├── Persistence/  # Save/load & import
│   │   │   ├── State/  # Application state
│   │   │   └── Editor/  # Unity editor integration
│   │   ├── Shaders/  # Compute and rendering shaders
│   │   └── Resources/  # Runtime resources
│   ├── Scripts/  # Editor-only scripts
│   │   └── UI/  # UI layer
│   │       ├── NodeGraph/  # Node-based editor
│   │       ├── Timeline/  # Timeline editor
│   │       ├── Components/  # UI components
│   │       └── Systems/  # UI systems
│   ├── Tests/  # Test suites
│   ├── Scenes/  # Unity scenes
│   ├── Prefabs/  # Prefab assets
│   └── Materials/  # Materials and textures
├── layers/
│   ├── structure.md  # Project-level context (Tier 1)
│   └── context-template.md  # Template for context files
├── ProjectSettings/  # Unity project settings
├── Packages/  # Unity package manifest
└── README.md
```

## Architecture

**Pattern**: Entity Component System (ECS) with Model-View-Controller (MVC) for UI

- **ECS Layer**: High-performance track computation using Unity DOTS
  - Entities: Track nodes, sections, trains
  - Components: Position, rotation, forces, geometry
  - Systems: Physics simulation, mesh generation, rendering

- **UI Layer**: Node graph and timeline editors using UI Toolkit
  - Model: Track data, project state
  - View: Node graph, timeline, 3D viewport
  - Controller: User input handling, commands

- **Flow**: User Input → UI Controller → ECS Authoring → ECS Systems → Rendering

## Entry points

- Main entry: `Assets/Scenes/Main.unity` (Main editor scene)
- Runtime entry: `Assets/Runtime/Scripts/Core/KexEditManager.cs` (Main runtime manager)
- UI entry: `Assets/Scripts/UI/UIManager.cs` (UI initialization)
- Track system: `Assets/Runtime/Scripts/Track/Track.cs` (Track data structure)
- File loading: `Assets/Runtime/Scripts/Persistence/CoasterLoader.cs` (Track file loader)

## Naming Conventions

- Files: PascalCase for classes (e.g., `NodeAspect.cs`, `TrackSystem.cs`)
- Directories: PascalCase for code, lowercase for assets (e.g., `Systems/`, `materials/`)
- Classes/Components: PascalCase (e.g., `TrackNode`, `ForceComponent`)
- Systems: PascalCase with "System" suffix (e.g., `MeshGenerationSystem`)
- UI Classes: PascalCase with context suffix (e.g., `NodeGraphView`, `TimelineController`)

## Configuration

- Project Settings: `ProjectSettings/` (Unity project configuration)
- Package Manifest: `Packages/manifest.json` (Unity package dependencies)
- Editor Settings: `UserSettings/` (Per-user editor preferences)
- Runtime Package: `Assets/Runtime/package.json` (Core package definition)

## Where to add code

- Core utilities → `Assets/Runtime/Scripts/Core/`
- Track building → `Assets/Runtime/Scripts/Track/`
- Train logic → `Assets/Runtime/Scripts/Trains/`
- Physics/simulation → `Assets/Runtime/Scripts/Physics/`
- Rendering/visuals → `Assets/Runtime/Scripts/Visualization/`
- Save/load → `Assets/Runtime/Scripts/Persistence/`
- Global state → `Assets/Runtime/Scripts/State/`
- UI features → `Assets/Scripts/UI/`
- Node graph → `Assets/Scripts/UI/NodeGraph/`
- Timeline → `Assets/Scripts/UI/Timeline/`
- Tests → `Assets/Tests/`
- New feature → Create folder in appropriate feature area with `context.md`
