# Core Rules

Applies to `packages/core/**`

## Layer discipline

sim → graph → nodes → track → persistence → ffi. Never add upward dependencies. sim has zero deps. ffi and wasm are feature-gated.

## SoA data layout

Graph uses parallel Vecs (positions, node types, properties stored as separate vectors indexed by node ID). New fields follow this pattern — no per-node structs.

## Pure evaluation

`evaluate_graph` is pure: graph + properties in, Points out. No mutation during evaluation. Side effects only at ffi/persistence boundaries.

## Node dispatch

Each node type: file in `nodes/`, entry in `dispatch.rs`, port metadata via `*_ports()`. New nodes follow exactly this pattern.

## Testing

Build minimal graphs programmatically, assert on output Points. No mocks. Cover: empty graph, single node, cycle detection, disconnected subgraphs. Property-based tests for sim math (roundtrips, invariants).

`tests/trajectory_snapshot.rs` pins `evaluate_graph` + sections + splines per fixture as `.snap.json` siblings — the regression net for refactors that claim to preserve behavior. Regen with `UPDATE_SNAPSHOTS=1 cargo test --test trajectory_snapshot` only when the output change is intentional.
