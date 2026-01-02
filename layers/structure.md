# Project Structure

Roller coaster editor using Force Vector Design (FVD) with portable, high-performance track computation.

## Architecture

**Nested Onion/Hexagonal**: Inner hex cores compose into Track Backend, wrapped by domain layers (Trains), then Application (Unity ECS) and UI at the edge.

```
┌─────────────────────────────────────────────────────────────────────┐
│  UI — editor state, undo/redo, viewport, input                      │
├─────────────────────────────────────────────────────────────────────┤
│  APPLICATION — Legacy (ECS), Rendering (GPU shell)                  │
├─────────────────────────────────────────────────────────────────────┤
│  DOMAIN LAYERS — Trains                                             │
├─────────────────────────────────────────────────────────────────────┤
│  TRACK BACKEND ─────────────────────────────────────────────────    │
│  │ Document, Track, Persistence                                     │
│  │ Sim.Schema, Sim.Nodes.*, Graph.Typed, Spline.Resampling/Rendering│
│  │ Sim (FVD) · Graph (DAG) · Spline (arc-length)  ← Hex Cores       │
└─────────────────────────────────────────────────────────────────────┘
```

### Track Backend

Portable coaster track computation. Composes three hex cores:

| Core | Purpose |
|------|---------|
| `Sim` | FVD physics, frames, keyframes |
| `Graph` | Generic directed graph with ports |
| `Spline` | Arc-length parameterized spline |

Hex cores are domain-agnostic. Hex layers extend them with coaster-specific logic (Schema, Nodes, Typed, Resampling). Document/Track/Persistence provide the document model, built track output, and serialization.

### Domain Layers

Wrap Track Backend with additional coaster features:

| Layer | Purpose |
|-------|---------|
| `Trains` | Train traversal (via Sim nodes), car positioning (via Spline) |

Domain layers depend inward on Track Backend, never vice versa.

### Application & UI

- **Legacy**: Unity ECS coordination, file I/O, system lifecycle
- **Rendering**: GPU buffer management, compute dispatch (wraps Spline.Rendering)
- **UI**: Editor state, undo/redo, viewport, input handling

---

## Stack

- **Runtime**: Unity 6000.3.1f1 (current), Rust (parallel backend)
- **Language**: C# (.NET Standard 2.1), Rust (native core)
- **Framework**: Unity DOTS (ECS, Burst, Jobs)
- **Build**: Unity Build System, Cargo (Rust)
- **Rendering**: Universal Render Pipeline (URP)
- **UI**: UI Toolkit
- **Testing**: Unity Test Framework, Cargo test

## Commands

```bash
# Development
./run-tests.sh [TestName]              # Headless tests (Burst backend)
./run-tests.sh --rust-backend [TestName]  # Headless tests (Rust FFI)

# Rust backend
cd rust-backend && cargo test          # Run all Rust tests
cd rust-backend && cargo test -p physics-verifier  # Physics verification
./build-rust.sh                        # Build FFI DLL

# Unity
# Dev: Open project in Unity Editor
# Build: File → Build Settings → Build
# Test: Window → General → Test Runner
# Play: Ctrl/Cmd+P
```

## Layout

```
KexEdit/
├── Assets/
│   ├── Runtime/                    # Core runtime package
│   │   ├── Sim/                    # Hex 1: Coaster Simulation
│   │   │   ├── Core/               # (KexEdit.Sim) Pure physics/math
│   │   │   ├── Schema/             # (KexEdit.Sim.Schema) Node vocabulary
│   │   │   └── Nodes/              # (KexEdit.Sim.Nodes.*) Implementations
│   │   │       ├── Anchor/
│   │   │       ├── Bridge/
│   │   │       ├── Force/
│   │   │       ├── Geometric/
│   │   │       ├── Curved/
│   │   │       ├── CopyPath/
│   │   │       ├── Reverse/
│   │   │       └── ReversePath/
│   │   ├── Graph/                  # Hex 2: Graph System
│   │   │   ├── Core/               # (KexEdit.Graph) Generic graph
│   │   │   └── Typed/              # (KexEdit.Graph.Typed) Type-safe ops
│   │   ├── Spline/                 # Hex 3: Spline System
│   │   │   ├── Core/               # (KexEdit.Spline) Spline math
│   │   │   ├── Resampling/         # (KexEdit.Spline.Resampling) Adapter
│   │   │   └── Rendering/          # (KexEdit.Spline.Rendering) Segmentation
│   │   ├── Document/               # (KexEdit.Document) Editable document
│   │   ├── Track/                  # (KexEdit.Track) Built track output
│   │   ├── Persistence/            # (KexEdit.Persistence) Save/load
│   │   ├── Trains/                 # Domain Layer
│   │   │   └── Sim/                # (KexEdit.Trains.Sim) CoM traversal
│   │   ├── Rendering/              # (KexEdit.Rendering) GPU pipeline
│   │   ├── Legacy/                 # Application (Unity ECS)
│   │   │   ├── Core/               # Foundation & utilities
│   │   │   ├── Track/              # Track ECS systems
│   │   │   ├── Trains/             # Vehicle systems
│   │   │   ├── Physics/            # ECS physics integration
│   │   │   ├── Persistence/        # File I/O adapters
│   │   │   ├── State/              # Application state
│   │   │   ├── Debug/              # (KexEdit.Legacy.Debug) Gizmos
│   │   │   └── Editor/             # (KexEdit.Legacy.Editor) Tools
│   │   ├── Native/                 # Rust FFI bindings
│   │   │   └── RustCore/           # (KexEdit.Native.RustCore)
│   │   ├── Shaders/                # Compute and rendering shaders
│   │   └── Resources/              # Runtime resources
│   ├── Scripts/
│   │   └── UI/                     # UI Layer (KexEdit.UI)
│   │       ├── NodeGraph/          # Node-based editor
│   │       ├── Timeline/           # Timeline editor
│   │       ├── Components/         # UI components
│   │       └── Systems/            # UI systems
│   ├── Tests/                      # Test suites
│   ├── Scenes/                     # Unity scenes
│   ├── Prefabs/                    # Prefab assets
│   └── Materials/                  # Materials and textures
├── rust-backend/                   # Git submodule → kexedit-backend
│   ├── kexedit-sim/                # Pure domain (mirrors KexEdit.Sim)
│   ├── kexedit-sim-nodes/          # Node schema (mirrors KexEdit.Sim.Schema)
│   ├── kexedit-ffi/                # FFI adapter (Rust → C)
│   └── physics-verifier/           # Standalone verification tool
├── tools/                          # Development utilities (Python)
├── layers/                         # Documentation
│   ├── structure.md                # This file
│   └── context-template.md         # Template for context files
└── CLAUDE.md                       # AI context (Tier 0)
```

## Entry Points

| Entry | Path | Purpose |
|-------|------|---------|
| Scene | `Assets/Scenes/Main.unity` | Main editor scene |
| Runtime | `Assets/Runtime/Legacy/Core/KexEditManager.cs` | Runtime initialization |
| UI | `Assets/Scripts/UI/UIManager.cs` | UI initialization |
| Document | `Assets/Runtime/Document/Document.cs` | Editable document structure |
| Track | `Assets/Runtime/Track/Track.cs` | Built track data structure |
| File I/O | `Assets/Runtime/Legacy/Persistence/CoasterLoader.cs` | File loading |

## Where to Add Code

### Hex Cores (Portable)
| What | Where |
|------|-------|
| Physics/math primitives | `Runtime/Sim/Core/` |
| Graph structure | `Runtime/Graph/Core/` |
| Spline math | `Runtime/Spline/Core/` |

### Hex Layers
| What | Where |
|------|-------|
| Node schema (types, ports) | `Runtime/Sim/Schema/` |
| Node implementations | `Runtime/Sim/Nodes/<NodeType>/` |
| Type-safe graph ops | `Runtime/Graph/Typed/` |
| Point → Spline conversion | `Runtime/Spline/Resampling/` |
| Track segment boundaries | `Runtime/Spline/Rendering/` |

### Track Backend (Application)
| What | Where |
|------|-------|
| Editable document | `Runtime/Document/` |
| Built track (Track, Section) | `Runtime/Track/` |
| Serialization format | `Runtime/Persistence/` |

### Domain Layers
| What | Where |
|------|-------|
| Train simulation | `Runtime/Trains/` |

### Rendering (GPU Shell)
| What | Where |
|------|-------|
| GPU buffer management | `Runtime/Rendering/` |

### Application (Legacy)
| What | Where |
|------|-------|
| ECS systems | `Runtime/Legacy/<Domain>/Systems/` |
| ECS components | `Runtime/Legacy/<Domain>/Components/` |
| File I/O | `Runtime/Legacy/Persistence/` |
| Debug gizmos | `Runtime/Legacy/Debug/` |
| Editor tools | `Runtime/Legacy/Editor/` |

### Rust Backend
| What | Where |
|------|-------|
| Physics/math (Rust) | `rust-backend/kexedit-sim/` |
| Node schema (Rust) | `rust-backend/kexedit-sim-nodes/` |
| FFI layer | `rust-backend/kexedit-ffi/` |
| C# FFI bindings | `Runtime/Native/RustCore/` |

### UI
| What | Where |
|------|-------|
| Node graph editor | `Scripts/UI/NodeGraph/` |
| Timeline editor | `Scripts/UI/Timeline/` |
| UI components | `Scripts/UI/Components/` |

### Tests
| What | Where |
|------|-------|
| All tests | `Assets/Tests/` |
| Physics verification | `rust-backend/physics-verifier/` |

## Naming Conventions

- **Files**: PascalCase for classes (`NodeAspect.cs`, `TrackSystem.cs`)
- **Directories**: PascalCase for code, lowercase for assets
- **Assemblies**: `KexEdit.<Hex>.<Layer>` (e.g., `KexEdit.Sim.Schema`)
- **Namespaces**: Match assembly names
- **Systems**: PascalCase with "System" suffix (`MeshGenerationSystem`)

---

# Migration Status (Transitory)

> This section documents temporary migration state. Remove when migration complete.

## Current State

The codebase is being refactored from a monolithic Unity ECS application to the hexagonal-lite architecture described above.

### Completed
- [x] Hex core structure (Sim, Graph, Spline)
- [x] Layer structure (Schema, Nodes, Typed, Resampling)
- [x] Application layer (Coaster, Persistence)
- [x] Infrastructure renamed (Unity → Legacy)
- [x] Namespace/assembly rename complete

### In Progress
- [ ] Extracting logic from Legacy → hex cores
- [ ] Reducing Unity dependencies in application layer
- [ ] Rust backend parity with C# cores

### Remaining in Legacy

| Folder | Target |
|--------|--------|
| `Trains/` | Replace with lightweight ECS in new Application layer |
| `Track/` | Replace with lightweight ECS in new Application layer |
| `Physics/` | Replace with lightweight ECS in new Application layer |
| `State/` | Replace with lightweight ECS in new Application layer |
| `Persistence/` | Replace with lightweight ECS in new Application layer |
| `Visualization/` | Replace with lightweight ECS in new Application layer |

Goal: Delete Legacy entirely. New Application layer will be minimal ECS coordination only.
