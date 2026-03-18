---
paths:
    - "app/**/*"
---

# App Rules

## Core as WASM

Never reimplement physics or graph logic in TypeScript. Core is the single source of truth for evaluation and persistence.

## Bridge pattern

User edits → core graph → evaluation → ECS components. Shallot ECS owns visual representation only.

## Shallot integration

App is a Shallot Plugin. Follows Shallot conventions: dependencies, initialize/warm hooks. Runs in edit mode — node manipulation uses document-level operations (undo-aware). Track visualization systems use `mode: "always"`.

## UI

`config.editorUI` pattern. Framework-agnostic: `(container, channel) => cleanup`.
