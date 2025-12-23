# Project Structure

Roller coaster editor using Force Vector Design (FVD) with portable, high-performance track computation.

## Architecture

### Design Philosophy

Mixed **Onion/Hexagonal-Lite** architecture optimized for:
- **Backend portability**: Swappable between Unity/Burst and Rust backends
- **Future migration**: Path to Rust backend + TypeScript frontend
- **Simplicity over ceremony**: Data contracts instead of formal adapters
- **Rust-idiomatic patterns**: Favor simple data over OOP/ECS boilerplate

### Core Principles

1. **Multiple Hex Cores**: Domain-agnostic, portable logic (Sim, Graph, Spline)
2. **Expanding Layers**: Each core has layers of increasingly KexEdit-aware logic
3. **Simple Data Contracts**: Structs as ports, no interface-based adapters
4. **Lightweight Application**: Thin orchestration connecting cores
5. **Infrastructure at Edge**: Unity/ECS is outermost, easily replaceable

### Dependency Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        INFRASTRUCTURE                               │
│  KexEdit.Legacy (ECS systems, rendering, input)                     │
│  KexEdit.UI (editors, viewport)                                     │
├─────────────────────────────────────────────────────────────────────┤
│                        APPLICATION                                  │
│  KexEdit.App.Coaster (aggregate root)                               │
│  KexEdit.App.Persistence (save/load)                                │
├─────────────────────────────────────────────────────────────────────┤
│                        HEX LAYERS                                   │
│  KexEdit.Sim.Schema (node types, ports)                             │
│  KexEdit.Sim.Nodes.* (node implementations)                         │
│  KexEdit.Graph.Typed (type-safe graph operations)                   │
│  KexEdit.Spline.Resampling (Point → SplinePoint)                    │
├─────────────────────────────────────────────────────────────────────┤
│                        HEX CORES (Portable)                         │
│  KexEdit.Sim (FVD physics, math primitives)                         │
│  KexEdit.Graph (generic directed graph)                             │
│  KexEdit.Spline (arc-length spline math)                            │
└─────────────────────────────────────────────────────────────────────┘
```

### Hex Cores

Each hex core is:
- **Domain-agnostic**: No KexEdit-specific knowledge
- **Portable**: Burst-compatible C# or pure Rust
- **Testable**: No Unity dependencies in core logic
- **Reusable**: Could power other applications

| Core | Domain | Purpose |
|------|--------|---------|
| `Sim` | Physics | FVD simulation, forces, frames, keyframes |
| `Graph` | Structure | Generic directed graph with ports |
| `Spline` | Geometry | Arc-length parameterized spline math |

### Hex Layers

Layers extend cores with domain-specific logic:

| Layer | Extends | Purpose |
|-------|---------|---------|
| `Sim.Schema` | Sim | Node types, port specs, property IDs |
| `Sim.Nodes.*` | Sim + Schema | Concrete node implementations (Force, Geometric, etc.) |
| `Graph.Typed` | Graph + Sim.Schema | Type-safe operations using node schema |
| `Spline.Resampling` | Sim + Spline | Bridges Sim points to Spline format |

### Application Layer

Lightweight orchestration that:
- Composes multiple hex cores
- Defines aggregate roots (`Coaster`)
- Implements use cases (`CoasterEvaluator`)
- Handles serialization

### Infrastructure Layer

Platform-specific code (Unity):
- ECS systems and components
- Rendering and mesh generation
- Input handling and UI
- File I/O adapters

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
│   │   │   └── Resampling/         # (KexEdit.Spline.Resampling) Adapter
│   │   ├── App/                    # Application Layer
│   │   │   ├── Coaster/            # (KexEdit.App.Coaster) Aggregate
│   │   │   └── Persistence/        # (KexEdit.App.Persistence) Save/load
│   │   ├── Legacy/                 # Infrastructure Layer
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
| Track | `Assets/Runtime/Legacy/Track/Track.cs` | Track data structure |
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

### Application
| What | Where |
|------|-------|
| Coaster aggregate | `Runtime/App/Coaster/` |
| Serialization format | `Runtime/App/Persistence/` |

### Infrastructure (Legacy)
| What | Where |
|------|-------|
| ECS systems | `Runtime/Legacy/<Domain>/Systems/` |
| ECS components | `Runtime/Legacy/<Domain>/Components/` |
| File I/O adapters | `Runtime/Legacy/Persistence/` |
| Debug visualization | `Runtime/Legacy/Debug/` |
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
- [x] Application layer (App.Coaster, App.Persistence)
- [x] Infrastructure renamed (Unity → Legacy)
- [x] Namespace/assembly rename complete

### In Progress
- [ ] Extracting logic from Legacy → hex cores
- [ ] Reducing Unity dependencies in application layer
- [ ] Rust backend parity with C# cores

### Remaining in Legacy Layer

The following are still in `KexEdit.Legacy` and should eventually be:
- Extracted to hex cores (portable logic)
- Or kept as infrastructure (Unity-specific)

| Folder | Status | Target |
|--------|--------|--------|
| `Trains/` | ECS components + systems | Extract physics to Sim, keep ECS in Legacy |
| `Track/` | ECS components | Keep in Legacy (infrastructure) |
| `Physics/` | ECS integration | Keep in Legacy (infrastructure) |
| `State/` | ECS singletons | Refactor to App layer |
| `Persistence/` | Adapters | Keep in Legacy (file I/O infrastructure) |
| `Visualization/` | Being replaced | Delete when Spline.Rendering complete |

## Migration Goals

1. **Hex cores contain all portable logic** - testable without Unity
2. **Legacy layer is thin** - only ECS wiring and infrastructure
3. **Rust backend matches C# cores** - swappable via FFI
4. **Future: TypeScript frontend** - reuse Rust backend via WASM

## Rendering Migration

See `PLAN.md` for detailed Articulation.Rendering implementation plan.
