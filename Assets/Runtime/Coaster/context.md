# Coaster Context

Application layer aggregate root and use cases for coaster evaluation.

## Purpose

- `Coaster` - Aggregate root combining graph topology with node data (keyframes, durations, scalars, vectors)
- `CoasterEvaluator` - Use case: evaluate coaster → track points (pending)

## Layout

```
Coaster/
├── context.md
├── KexEdit.Coaster.asmdef
├── Coaster.cs           # Aggregate root struct
└── CoasterEvaluator.cs  # Use case (pending)
```

## Dependencies

- KexGraph (graph topology)
- KexEdit.Core (Point, Keyframe)
- KexEdit.Nodes (node schemas, Build methods)
- KexEdit.NodeGraph (typed graph extensions)
