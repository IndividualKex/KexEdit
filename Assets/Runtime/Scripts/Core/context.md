# Core Context

Foundation and shared infrastructure for the KexEdit runtime

## Purpose

- System initialization and lifecycle management
- Core data structures and utilities
- Global settings and constants
- Shared extension methods

## Key Components

- `Uuid` - Unique identifier for entities
- `Dirty` - Marks entities for update
- `GlobalSettings` - Application-wide configuration
- `InitializeEvent` - System initialization events

## Key Systems

- `InitializationSystem` - Bootstraps the application
- `PauseSystem` - Manages simulation pause state
- `CleanupSystemGroup` - Coordinates entity cleanup

## Entry Points

- `KexEditManager.cs` - Main runtime manager and entry point

## Dependencies

- Unity.Entities (ECS framework)
- Unity.Mathematics (math operations)

## Scope

- In: Core infrastructure, utilities, global state
- Out: Feature-specific logic, UI, rendering