# KexEdit.App.Coaster

Application layer aggregate root and evaluator.

## Purpose

- `Coaster` - Aggregate: graph + node data (keyframes, scalars, vectors, flags)
- `CoasterEvaluator` - Use case: topologically evaluate graph → paths and output anchors
- `NodeMeta` - Reserved property indices (240-254) for node-level metadata

## Layout

```
App/Coaster/
├── context.md
├── KexEdit.App.Coaster.asmdef
├── Coaster.cs
└── CoasterEvaluator.cs
```

## Dependencies

- KexEdit.Graph, KexEdit.Sim, KexEdit.Sim.Schema, KexEdit.Graph.Typed
