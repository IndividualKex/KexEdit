# Testing Plan - Web Port

Testing strategy for migrating KexEdit to WebAssembly + WebGPU.

## Architecture

```
                    ┌─────────────┐
                    │   Visual    │  Few, expensive
                    │  Snapshots  │
                    └──────┬──────┘
                           │
                ┌──────────┴──────────┐
                │   Golden Fixtures   │  Medium count, high value
                │   (Real Examples)   │
                └──────────┬──────────┘
                           │
          ┌────────────────┴────────────────┐
          │         Property / Fuzz         │  Many, catch edge cases
          └────────────────┬────────────────┘
                           │
    ┌──────────────────────┴──────────────────────┐
    │                 Unit Tests                   │  Many, fast
    └─────────────────────────────────────────────┘
```

## Boundary Contract

The Wasm boundary is the critical integration point. Tests validate that Rust Wasm output matches Unity output exactly.

```
fixtures/
├── inputs/                      # Serialized track graphs (from Unity)
│   ├── straight_10m.bin
│   ├── simple_circle.bin
│   └── ...
├── outputs/                     # Expected track points (from Unity)
│   ├── straight_10m.points.bin
│   ├── straight_10m.meta.json
│   └── ...
└── visual/                      # Golden render images
    ├── simple_circle.png
    └── ...
```

### Fixture Data Format

**Input**: Serialized graph binary (existing Unity format)

**Output**: Flat float array matching boundary contract:
```
[pos.x, pos.y, pos.z, dir.x, dir.y, dir.z, norm.x, norm.y, norm.z, dist, heart, time, viz, ...] × N points
```

**Metadata** (`*.meta.json`):
```json
{
  "pointCount": 1000,
  "segmentCount": 3,
  "totalLength": 150.5,
  "checksum": "abc123"
}
```

## Golden Fixtures (Real Examples)

Curated set of representative tracks exported from Unity.

| Fixture | Purpose |
|---------|---------|
| `straight_10m` | Simplest case, baseline validation |
| `simple_circle` | Constant curvature, closed loop |
| `figure_eight` | Direction reversal, crossing paths |
| `vertical_loop` | Full inversion, high normal forces |
| `zero_g_parabola` | Near-zero normal force edge case |
| `steep_drop` | High velocity, energy conservation |
| `tight_helix` | High curvature, roll accumulation |
| `friction_test` | Energy loss accuracy over distance |
| `multi_section` | Multiple section types in graph |
| `real_coaster_1` | Representative real-world track |
| `real_coaster_2` | Different characteristics |
| `long_track` | Performance test, 10k+ points |

### Tolerance Thresholds

```rust
PointTolerance {
    position: 1e-4,   // 0.1mm
    direction: 1e-5,  // Angular precision
    velocity: 1e-3,   // 1mm/s
    force: 1e-3,      // 0.001g
}
```

## Property Tests

Invariants that must hold for any valid input. High value per test.

### Physics Invariants

| Property | Description |
|----------|-------------|
| Energy conservation | Energy never increases without boost |
| Direction normalization | `direction.length() == 1.0` always |
| Continuity | Adjacent points within reasonable distance |
| Force bounds | Forces within physically plausible range |
| Velocity positivity | Velocity stays positive (or simulation stops) |

### Example Properties

```rust
// Energy never increases (friction only removes energy)
fn energy_never_increases(initial_velocity, friction, steps) {
    // ... energy at end <= energy at start
}

// Direction vector stays normalized
fn direction_stays_normalized(forces, steps) {
    // ... |direction| == 1.0 ± epsilon
}

// Adjacent points are continuous (no teleportation)
fn adjacent_points_continuous(anchor, duration) {
    // ... distance between points < velocity / HZ * tolerance
}

// Roll accumulation is consistent
fn roll_accumulation_matches_roll_speed(roll_speed, duration) {
    // ... total roll ≈ integral of roll_speed
}
```

## Fuzz Tests

Random/malformed inputs to find crashes and panics.

### Targets

| Target | Input | Goal |
|--------|-------|------|
| `load_file` | Arbitrary bytes | No panic, returns Result |
| `build_track` | Arbitrary bytes | No panic, returns Result |
| `deserialize_graph` | Arbitrary bytes | No panic, returns Result |

### Implementation

```rust
// Using cargo-fuzz or proptest
fuzz_target!(|data: &[u8]| {
    let _ = build_track(data);  // Must not panic
});
```

## Unit Tests

Fast, focused tests for pure functions.

### Rust Wasm

| Area | Examples |
|------|----------|
| Math | Quaternion operations, vector math, interpolation |
| PointData | Energy computation, setters, lerp |
| Keyframes | Evaluation, edge cases |
| Serialization | Round-trip, format validation |
| Graph | Traversal, topological sort |

### TypeScript

| Area | Examples |
|------|----------|
| Buffer packing | Stride, alignment, byte order |
| Math utilities | Matrix operations, camera math |
| Wasm bridge | Type conversions, error handling |

## Visual Regression Tests

Rendered output comparison for catching rendering bugs.

### Setup

- Deterministic camera positions per fixture
- Fixed viewport size (e.g., 1920x1080)
- Disabled anti-aliasing for determinism
- Threshold: 1% pixel difference allowed

### Fixtures

| Fixture | Camera | Tests |
|---------|--------|-------|
| `simple_circle` | Top-down | Basic rendering |
| `vertical_loop` | Side view | Inversion rendering |
| `multi_section` | Perspective | Style transitions |

## Test Counts (Target)

```
Rust Wasm:
├── Unit tests:       50-100
├── Property tests:   20-30
├── Golden fixtures:  10-15
└── Fuzz targets:     3-5

TypeScript:
├── Unit tests:       30-50
├── Property tests:   10-15
└── Visual snapshots: 5-10
```

## Bug Detection Matrix

| Bug Type | Unit | Property | Golden | Visual |
|----------|------|----------|--------|--------|
| Off-by-one | ++ | + | + | - |
| Math errors | ++ | ++ | ++ | - |
| Edge case crashes | - | ++ | - | - |
| Accumulation drift | - | + | ++ | + |
| Integration bugs | - | - | ++ | + |
| Boundary mismatch | - | - | ++ | - |
| Rendering bugs | - | - | - | ++ |

## CI Pipeline

```yaml
jobs:
  # Generate fixtures from Unity (or use committed fixtures)
  fixtures:
    steps:
      - unity -executeMethod FixtureExporter.ExportFixtures
      - upload-artifact: fixtures/

  # Rust tests
  rust:
    needs: fixtures
    steps:
      - cargo test
      - cargo test --release  # Catch release-only bugs
      - wasm-pack test --headless --chrome

  # TypeScript tests
  typescript:
    needs: [fixtures, rust]
    steps:
      - npm test
      - npm run test:visual

  # Fuzz (nightly/scheduled)
  fuzz:
    schedule: "0 0 * * *"
    steps:
      - cargo fuzz run load_file -- -max_total_time=300
      - cargo fuzz run build_track -- -max_total_time=300
```

## Development Workflow

### Frontend Development (No Wasm)

```typescript
// Mock Wasm with fixtures
export async function buildTrack(input: ArrayBuffer): Promise<TrackOutput> {
  if (process.env.USE_FIXTURES) {
    return loadFixture('simple_circle.points.bin');
  }
  return wasmModule.build_track(input);
}
```

### Backend Development (No Renderer)

```bash
cargo test                    # Fast iteration
cargo test --release          # Release validation
cargo bench                   # Performance regression
```

### Integration

```bash
npm run test:integration      # Full pipeline
npm run test:visual           # Visual regression
```

## Fixture Generation (Unity)

```csharp
[MenuItem("KexEdit/Export Test Fixtures")]
public static void ExportFixtures() {
    var testCases = FindTestTracks();

    foreach (var track in testCases) {
        // Export input
        File.WriteAllBytes($"fixtures/inputs/{track.name}.bin", track.Data);

        // Build and export output
        var points = BuildTrackPoints(track);
        File.WriteAllBytes($"fixtures/outputs/{track.name}.points.bin", PackPoints(points));

        // Export metadata
        File.WriteAllText($"fixtures/outputs/{track.name}.meta.json", JsonUtility.ToJson(new {
            pointCount = points.Length,
            segmentCount = GetSegmentCount(track),
            totalLength = GetTotalLength(points),
            checksum = ComputeChecksum(points)
        }));
    }
}
```

## Priority Order

1. **Golden fixtures** - Export from Unity first. Migration safety net.
2. **Property tests** - Physics invariants. Catch subtle math bugs.
3. **Unit tests** - Pure functions. Fast feedback.
4. **Fuzz tests** - Deserialization. Security and robustness.
5. **Visual snapshots** - Last, once renderer is stable.
