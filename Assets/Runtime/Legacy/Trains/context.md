# Trains Context

Domain layer wrapping Track Backend.

Migration target: `Runtime/Trains/`

## Purpose

- Train center-of-mass traversal (via Sim nodes)
- Car positioning (via Spline)
- Wheel assemblies
- Visual styles

## Dependencies

- Track Backend (Sim, Graph, Spline)
- Application/Legacy (ECS, rendering)