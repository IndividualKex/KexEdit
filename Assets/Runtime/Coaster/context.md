# Coaster Context

Application layer aggregate root and evaluator.

## Purpose

- `Coaster` - Aggregate: graph + node data (keyframes, scalars, vectors, flags)
- `CoasterEvaluator` - Use case: topologically evaluate graph → paths and output anchors
- `NodeMeta` - Reserved property indices (240-254) for node-level metadata

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
