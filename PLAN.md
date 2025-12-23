# Migration Plan: Portable Core Architecture

## Target Architecture

```
Sim/                      Hex core: portable physics/math (Rust-swappable)
Sim.Schema/               Node schema and property storage
Sim.Nodes/                Node implementations
Graph/                    Hex core: generic graph structure
Graph.Typed/              Type-safe graph operations
Spline/                   Hex core: pure spline math (no KexEdit deps)
Spline.Resampling/        Adapter: Point[] → SplinePoint[]
Spline.Rendering/         Track mesh rendering (spline → GPU) [planned]
App.Coaster/              Application layer aggregate
App.Persistence/          Serialization (KEXD format)
Unity/                    Infrastructure layer (ECS, rendering)
Unity.Debug/              Spline gizmo visualization
```

**Principle:** Hex cores have no Unity dependencies. Unity depends on cores (not vice versa).

## Spline Hex Architecture

```
┌─────────────────┐                           ┌─────────────────┐
│  KexEdit.Sim    │                           │  KexEdit.Spline │
│   (Point)       │ ─────────────────────────→│  (SplinePoint)  │
└─────────────────┘         │                 └─────────────────┘
                            │                         │
                            ▼                         ▼
                 ┌─────────────────────┐    ┌─────────────────────┐
                 │ Spline.Resampling   │    │ Spline.Rendering    │
                 │ (Point→SplinePoint) │    │ (SplinePoint→Mesh)  │
                 └─────────────────────┘    └─────────────────────┘
                                                      │
                                                      ▼
                                            ┌─────────────────────┐
                                            │ Unity.Debug         │
                                            │ (gizmos)            │
                                            └─────────────────────┘
```

## Remaining in Unity Layer

### Trains (`Unity/Trains/`)

14 files + 9 systems. Self-contained train simulation module.

- **Keep in Unity:** ECS components and systems
- **Extract to Sim:** Train physics (distance following, car spacing)

### Other Unity Folders

| Folder | Status |
|--------|--------|
| `Physics/` | ECS integration - stays in Unity |
| `Track/` | ECS components - stays in Unity |
| `State/` | ECS singletons - refactor to App |
| `Persistence/` | Adapters (KexdAdapter, LegacyImporter) - stays in Unity |
| `Visualization/` | Being replaced by Spline.Rendering |

## Data Flow

```
.kex file  → LegacyImporter.Import() → Coaster aggregate
.kexd file → CoasterSerializer.Read() → Coaster aggregate
clipboard  → CoasterSerializer        → Coaster aggregate
```

---

# Spline.Rendering: Phased Implementation

## Design Principles

1. **Layered rendering** - Rails (extrusion), Ties (instancing), Structure (mesh bending)
2. **Arc-length parameterized** - All placement based on arc distance, not mesh segments
3. **Portable core** - Pure math in Burst-compatible structs, reproducible in Rust/WGPU
4. **Testable phases** - Each phase has validation via Python scripts or headless tests

## Target Rendering Layers

| Layer | Technique | Description |
|-------|-----------|-------------|
| Rails | Continuous extrusion | Cross-section extruded per spline point |
| Ties | Arc-length instancing | Meshes placed at fixed arc intervals |
| Spine | Mesh bending | Segment mesh deformed along spline |
| Supports | Point instancing | At specific arc positions (future) |

---

## Phase 1: Investigation & Data Inspection

**Goal:** Understand existing data formats, validate assumptions about spline/mesh data.

### 1.1 SplinePoint Data Inspection
- [ ] Create `tools/inspect_spline.py` - dump SplineBuffer from test scene
- [ ] Validate: Arc values are monotonic, Direction/Normal are normalized
- [ ] Output: CSV with Arc, Position, Direction, Normal, Lateral

### 1.2 Cross-Section Mesh Analysis
- [ ] Create `tools/analyze_cross_section.py` - analyze existing track style meshes
- [ ] Extract: vertex positions, edge loops, winding order
- [ ] Validate: 2-layer structure, vertex alignment between layers
- [ ] Output: visualization of cross-section profile

### 1.3 Existing Render Analysis
- [ ] Document current `TrackMeshCompute.compute` data flow
- [ ] Capture: buffer sizes, dispatch counts, render call patterns
- [ ] Identify: what data is actually used vs passed through

**Validation:** Python scripts run successfully, output matches expectations.

---

## Phase 2: Core Math Types (Portable)

**Goal:** Define Burst-compatible, Rust-idiomatic math primitives.

### 2.1 Assembly Setup
- [ ] Create `Assets/Runtime/Spline/Rendering/`
- [ ] Create `KexEdit.Spline.Rendering.asmdef`
  - Dependencies: Unity.Mathematics, Unity.Collections, Unity.Burst, KexEdit.Spline
- [ ] Create `context.md`

### 2.2 CrossSection Type
```csharp
// Portable: no Unity Mesh dependency in core type
public struct CrossSection {
    public NativeArray<float2> Vertices;    // 2D profile (Y=up, X=lateral)
    public NativeArray<float2> Normals;     // Per-vertex 2D normals
    public NativeArray<int> EdgeIndices;    // Ordered edge loop indices
    public int VertexCount;
}
```
- [ ] Implement `CrossSection` struct
- [ ] Implement `CrossSectionBuilder` - constructs from raw vertex data
- [ ] Unit test: round-trip serialization, edge loop validation

### 2.3 ExtrusionMath (Pure Functions)
```csharp
public static class ExtrusionMath {
    // Transform 2D profile point to 3D world position
    public static float3 TransformVertex(in SplinePoint frame, float2 localPos);

    // Transform 2D normal to 3D world normal
    public static float3 TransformNormal(in SplinePoint frame, float2 localNormal);

    // Build rotation matrix from spline frame
    public static float3x3 BuildFrame(float3 direction, float3 normal);
}
```
- [ ] Implement pure math functions (Burst-compatible)
- [ ] Unit test: known inputs → expected outputs
- [ ] Headless test: `./run-tests.sh ExtrusionMath`

**Validation:** `./run-tests.sh` passes, math matches hand-calculated values.

---

## Phase 3: CPU Extrusion Pipeline

**Goal:** Generate extruded mesh vertices on CPU (for validation before GPU).

### 3.1 ExtrusionJob (Burst)
```csharp
[BurstCompile]
public struct ExtrusionJob : IJobParallelFor {
    [ReadOnly] public NativeArray<SplinePoint> Spline;
    [ReadOnly] public NativeArray<float2> CrossSectionVerts;
    [ReadOnly] public NativeArray<float2> CrossSectionNormals;

    [WriteOnly] public NativeArray<float3> OutputVertices;
    [WriteOnly] public NativeArray<float3> OutputNormals;

    public void Execute(int splineIndex) { ... }
}
```
- [ ] Implement `ExtrusionJob`
- [ ] Implement index buffer generation (connecting adjacent slices)
- [ ] Unit test: simple spline (straight line) → expected vertex positions

### 3.2 CPU Mesh Builder
- [ ] Create `ExtrudedMeshBuilder` - orchestrates job, builds Unity Mesh
- [ ] Test: generate mesh from SplinePoint[], assign to MeshFilter
- [ ] Headless test: vertex count matches expected (splineCount × crossSectionCount)

### 3.3 Python Validation
- [ ] Create `tools/validate_extrusion.py`
- [ ] Export CPU-generated vertices to CSV
- [ ] Compare against expected positions (Python reference implementation)
- [ ] Visualize: plot 3D vertices in matplotlib

**Validation:** Visual inspection in editor, Python comparison passes.

---

## Phase 4: GPU Compute Pipeline

**Goal:** Port extrusion to compute shader for performance.

### 4.1 Compute Shader
- [ ] Create `Resources/TrackExtrusion.compute`
- [ ] Single kernel: `ExtrusionKernel`
- [ ] Input: SplinePoint buffer, CrossSection buffers
- [ ] Output: Vertex, Normal, Index buffers

### 4.2 GPU Buffer Management
```csharp
public class RenderBuffers : IDisposable {
    public ComputeBuffer SplineBuffer;
    public ComputeBuffer VertexBuffer;
    public ComputeBuffer NormalBuffer;
    public ComputeBuffer IndexBuffer;

    public void Resize(int splineCount, int crossSectionVertCount);
    public void Dispose();
}
```
- [ ] Implement `RenderBuffers` with proper lifecycle
- [ ] Implement buffer resizing (handle spline length changes)

### 4.3 TrackRenderer API
```csharp
public static class TrackRenderer {
    public static void UploadSpline(NativeArray<SplinePoint> spline, RenderBuffers buffers);
    public static void DispatchExtrusion(CrossSection crossSection, RenderBuffers buffers);
    public static void Draw(RenderBuffers buffers, Material material);
}
```
- [ ] Implement compute dispatch
- [ ] Implement `Graphics.RenderPrimitives` call

### 4.4 Validation
- [ ] Create `tools/compare_cpu_gpu.py` - readback GPU buffers, compare to CPU
- [ ] Headless test: GPU output matches CPU output (within float tolerance)
- [ ] Visual test: render with debug material showing vertex colors

**Validation:** CPU/GPU outputs match, visual render correct in editor.

---

## Phase 5: Arc-Length Instancing (Ties)

**Goal:** Place tie meshes at fixed arc intervals along spline.

### 5.1 InstancePlacement (Pure Math)
```csharp
public static class InstancePlacement {
    // Calculate instance transforms at fixed arc intervals
    public static int CalculateInstanceCount(float totalArc, float spacing);
    public static float GetArcAtInstance(int index, float spacing, float offset);
}
```
- [ ] Implement placement math
- [ ] Unit test: 10m spline, 0.5m spacing → 20 instances at correct arcs

### 5.2 Instance Buffer Generation
- [ ] Create `InstanceJob` - generates TRS matrices at arc positions
- [ ] Use `SplineInterpolation.Interpolate()` for sub-sample positions
- [ ] Output: NativeArray<float4x4> matrices

### 5.3 GPU Instanced Rendering
- [ ] Upload matrices to `ComputeBuffer`
- [ ] Use `Graphics.RenderMeshInstanced` or `RenderMeshIndirect`
- [ ] Test with simple cube mesh as placeholder

### 5.4 Validation
- [ ] Visual: ties appear at regular intervals
- [ ] Python: export instance positions, verify arc spacing
- [ ] Headless: instance count matches expected

**Validation:** Ties render at correct positions, spacing is uniform.

---

## Phase 6: Mesh Bending (Spine Structure)

**Goal:** Deform pre-modeled track segment mesh along spline.

### 6.1 Deformation Texture Approach
```csharp
public class SplineDeformationTexture : IDisposable {
    public RenderTexture Texture;  // 4 rows per matrix, width = resolution

    public void Bake(NativeArray<SplinePoint> spline, int resolution);
}
```
- [ ] Implement texture baking (4 pixels per transformation matrix)
- [ ] Use EXR format or RGBA32Float for precision

### 6.2 Deformation Shader
- [ ] Create `Shaders/SplineDeform.shader`
- [ ] Vertex shader: sample deformation texture, transform vertex
- [ ] Input: mesh Z-coordinate → spline progress (0-1)

### 6.3 Segment Instancing
```csharp
public static class SegmentRenderer {
    // Calculate segment count with uniform scaling
    public static int CalculateSegmentCount(float totalArc, float nominalLength);
    public static float CalculateActualLength(float totalArc, int segmentCount);
}
```
- [ ] Implement uniform scale distribution for circuit closure
- [ ] Generate per-segment material property blocks (arc offset)

### 6.4 Validation
- [ ] Visual: bent mesh follows spline smoothly
- [ ] Test: circuit closure - no gap at loop point
- [ ] Python: verify deformation texture values

**Validation:** Mesh bends correctly, circuits close seamlessly.

---

## Phase 7: Integration & Unity Replacement

**Goal:** Wire new rendering to existing ECS, deprecate old visualization.

### 7.1 ECS Bridge System
- [ ] Create `TrackRenderSystem` in Unity
- [ ] Query `SplineBuffer` from track entities
- [ ] Call `TrackRenderer` APIs

### 7.2 Style Configuration
- [ ] Create `TrackStyleAsset` ScriptableObject
- [ ] Define: rail cross-section, tie mesh, tie spacing, spine segment mesh
- [ ] Load styles at runtime

### 7.3 Legacy Deprecation
- [ ] Add `[Obsolete]` to old visualization systems
- [ ] Feature flag to switch between old/new rendering
- [ ] Performance comparison

### 7.4 Validation
- [ ] A/B comparison: old vs new rendering
- [ ] Performance: frame time, draw calls, GPU memory
- [ ] Visual: no regressions

**Validation:** New system renders correctly, performance equal or better.

---

## File Structure

```
Assets/Runtime/Spline/Rendering/
├── KexEdit.Spline.Rendering.asmdef
├── context.md
├── Core/
│   ├── CrossSection.cs
│   ├── ExtrusionMath.cs
│   └── InstancePlacement.cs
├── Jobs/
│   ├── ExtrusionJob.cs
│   └── InstanceJob.cs
├── Rendering/
│   ├── RenderBuffers.cs
│   ├── TrackRenderer.cs
│   ├── SplineDeformationTexture.cs
│   └── SegmentRenderer.cs
├── Resources/
│   └── TrackExtrusion.compute
└── Shaders/
    └── SplineDeform.shader

tools/
├── inspect_spline.py
├── analyze_cross_section.py
├── validate_extrusion.py
└── compare_cpu_gpu.py
```

---

## Test Commands

```bash
# Run all rendering tests
./run-tests.sh Spline.Rendering

# Run specific test
./run-tests.sh ExtrusionMath
./run-tests.sh ExtrusionJob
./run-tests.sh InstancePlacement

# Python validation
python tools/inspect_spline.py --scene TestTrack
python tools/validate_extrusion.py --compare cpu gpu
```
