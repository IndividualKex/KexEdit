# Coaster Context

Application layer aggregate root and evaluator.

## Purpose

- `Coaster` - Aggregate: graph + node data (keyframes, durations, scalars, vectors, anchors)
- `CoasterEvaluator` - Use case: topologically evaluate graph → paths and output anchors

## Layout

```
Coaster/
├── context.md
├── KexEdit.Coaster.asmdef
├── Coaster.cs
└── CoasterEvaluator.cs
```

## Dependencies

- KexGraph, KexEdit.Core, KexEdit.Nodes, KexEdit.NodeGraph
