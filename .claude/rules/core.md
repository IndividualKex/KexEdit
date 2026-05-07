---
paths:
    - "packages/core/**/*"
---

# Core Rules

## Layer discipline

sim → graph → nodes → track → persistence → ffi. Never add upward dependencies. sim has zero deps. ffi is feature-gated (the only feature).

## SoA data layout

Graph uses parallel Vecs (positions, node types, properties stored as separate vectors indexed by node ID). New fields follow this pattern — no per-node structs.

## Pure evaluation

`evaluate_graph` is pure: graph + properties in, Points out. No mutation during evaluation. Side effects only at ffi/persistence boundaries.

## Node dispatch

Each node type: file in `nodes/`, port indices in a `pub mod ports` block at the top of that file (`pub const POSITION: u8 = 0`), pure `build` fn for the math, evaluator entry in `track/dispatch.rs::evaluate_node`, per-node-type metadata via the `NodeMeta` enum. New nodes follow this pattern.

## Testing

Build minimal graphs programmatically, assert on output Points. No mocks. Cover: empty graph, single node, cycle detection, disconnected subgraphs. Property-based tests for sim math (roundtrips, invariants). For multi-node fixtures, prefer `graph::GraphBuilder` (cfg(test)) over hand-wired SoA arrays.

`tests/trajectory_snapshot.rs` pins `evaluate_graph` + sections + splines per fixture as `.snap.json` siblings — the regression net for refactors that claim to preserve behavior. Regen with `UPDATE_SNAPSHOTS=1 cargo test --test trajectory_snapshot` only when the output change is intentional.
