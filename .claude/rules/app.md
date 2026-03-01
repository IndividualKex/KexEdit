# App Rules

Applies to `app/**`

## Core as WASM

App imports core as WASM module. Never reimplement physics or graph logic in TypeScript.

## Graph ownership

Core's Graph is authoritative for evaluation and persistence. Shallot ECS owns visual representation (entities for nodes, edges, track mesh). Bridge system syncs: user edits → core graph → evaluation → ECS components.

## Shallot plugin

App is a Shallot Plugin with components (TrackNode, TrackEdge, TrackMesh) and systems. Follows Shallot conventions: dependencies, initialize/warm hooks.

## Edit mode

Runs in Shallot edit mode. Node manipulation uses document-level operations (undo-aware). Track visualization systems use `mode: "always"`.

## UI

`config.editorUI` pattern. Framework-agnostic: `(container, state) => cleanup`.
