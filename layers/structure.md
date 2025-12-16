# Project Structure

Advanced Unity-based roller coaster editor using Force Vector Design (FVD) with high-performance track computation

## Stack

- Runtime: Unity 6000.3.1f1
- Language: C# (.NET Standard 2.1), Rust (native core)
- Framework: Unity DOTS (ECS, Burst, Jobs System)
- Build: Unity Build System, Cargo (Rust)
- Rendering: Universal Render Pipeline (URP)
- UI: UI Toolkit (Unity's modern UI system)
- Testing: Unity Test Framework, Cargo test

## Commands

- Dev: Open project in Unity Editor
- Build: File → Build Settings → Build (Windows/Mac/Linux)
- Test (Unity): Window → General → Test Runner
- Test (Headless): `./run-tests.sh [TestName]` (Burst) or `./run-tests.sh --rust-backend [TestName]` (Rust FFI) - cross-platform (Windows/macOS)
- Test (Rust): `cd rust-backend && cargo test`
- Build (Rust): `./build-rust.sh` - cross-platform (auto-detects .dll/.dylib/.so)
- Play: Unity Editor Play Mode (Ctrl/Cmd+P)

## Layout

```
KexEdit/
├── CLAUDE.md  # Global context (Tier 0)
├── run-tests.sh  # Headless test runner
├── Assets/  # Unity assets root
│   ├── Plugins/  # Reusable libraries
│   │   └── KexGraph/  # Domain-agnostic graph library (KexGraph)
│   ├── Runtime/  # Application layer (hexagonal architecture)
│   │   ├── Core/  # Domain layer - pure physics/math (KexEdit.Core)
│   │   ├── Nodes/  # Node types (KexEdit.Nodes.*)
│   │   ├── NodeGraph/  # KexEdit-aware graph extensions (KexEdit.NodeGraph)
│   │   ├── Native/  # Rust FFI bindings (KexEdit.Native.RustCore)
│   │   ├── Plugins/  # Native DLLs (kexedit_core.dll)
│   │   ├── Legacy/  # Monolithic runtime being hollowed out (KexEdit)
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
├── rust-backend/  # Git submodule → kexedit-backend (standalone Rust workspace)
│   ├── Cargo.toml  # Workspace manifest
│   ├── kexedit-core/  # Pure domain layer (Rust)
│   ├── kexedit-nodes/ # Node schema layer (Rust)
│   └── kexedit-ffi/   # FFI adapter (Rust → C)
├── build-rust.sh  # Rust build script (builds submodule)
├── layers/
│   ├── structure.md  # Project-level context (Tier 1)
│   ├── migration-plan.md  # Runtime migration tracking
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
- Runtime entry: `Assets/Runtime/Legacy/Core/KexEditManager.cs` (Main runtime manager)
- UI entry: `Assets/Scripts/UI/UIManager.cs` (UI initialization)
- Track system: `Assets/Runtime/Legacy/Track/Track.cs` (Track data structure)
- File loading: `Assets/Runtime/Legacy/Persistence/CoasterLoader.cs` (Track file loader)

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

- Generic graph library → `Assets/Plugins/KexGraph/`
- KexEdit-aware graph extensions → `Assets/Runtime/NodeGraph/`
- Portable physics/math (C#) → `Assets/Runtime/Core/`
- Spline articulation (C#) → `Assets/Runtime/Core/Articulation/`
- Portable physics/math (Rust) → `rust-backend/kexedit-core/`
- Rust FFI layer → `rust-backend/kexedit-ffi/`
- Rust FFI bindings (C#) → `Assets/Runtime/Native/RustCore/`
- Node types → `Assets/Runtime/Nodes/`
- Legacy ECS systems → `Assets/Runtime/Legacy/Track/`
- Train logic → `Assets/Runtime/Legacy/Trains/`
- Physics/simulation → `Assets/Runtime/Legacy/Physics/`
- Rendering/visuals → `Assets/Runtime/Legacy/Visualization/`
- Save/load → `Assets/Runtime/Legacy/Persistence/`
- Global state → `Assets/Runtime/Legacy/State/`
- UI features → `Assets/Scripts/UI/`
- Node graph → `Assets/Scripts/UI/NodeGraph/`
- Timeline → `Assets/Scripts/UI/Timeline/`
- Tests → `Assets/Tests/`
